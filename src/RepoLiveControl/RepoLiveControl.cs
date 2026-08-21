using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using BepInEx;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;

namespace RepoLiveControl
{
    [BepInPlugin("Codex.REPO.LiveControl.V2", "Codex REPO Live Control", "1.0.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Bridge.Start();
        }
    }

    public static class Loader
    {
        public static void Load()
        {
            Bridge.Start();
        }
    }

    internal sealed class ControlRequest
    {
        internal readonly string Command;
        internal readonly ManualResetEventSlim Completed = new ManualResetEventSlim(false);
        internal string Result = "ERROR No result was produced.";

        internal ControlRequest(string command)
        {
            Command = command;
        }

        internal void Complete(string result)
        {
            Result = result;
            Completed.Set();
        }
    }

    internal enum SpawnKind
    {
        Enemy,
        Loot,
        Item
    }

    internal sealed class SpawnJob
    {
        internal readonly ControlRequest Request;
        internal readonly SpawnKind Kind;
        internal readonly string Selector;
        internal readonly string Placement;
        internal readonly int Requested;
        internal readonly Vector3 Anchor;
        internal readonly List<Vector3> ReservedPositions = new List<Vector3>();
        internal int Spawned;
        internal bool Finished;
        internal string Names = string.Empty;

        internal SpawnJob(
            ControlRequest request,
            SpawnKind kind,
            string selector,
            string placement,
            int requested,
            Vector3 anchor)
        {
            Request = request;
            Kind = kind;
            Selector = selector;
            Placement = placement;
            Requested = requested;
            Anchor = anchor;
        }
    }

    internal static class Bridge
    {
        internal const string PipeName = "CodexRepoLiveControlV3";
        private const string HarmonyId = "Codex.REPO.LiveControl.V2";
        private static readonly ConcurrentQueue<ControlRequest> Requests = new ConcurrentQueue<ControlRequest>();
        private static readonly string[] ExpensiveLootNames =
        {
            "Diamond Display",
            "Griffin Statue",
            "Dragon Skull",
            "GoldTooth",
            "Server Rack"
        };

        private static int started;
        private static SpawnJob activeJob;

        internal static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                return;

            Harmony.UnpatchID("Codex.REPO.SpawnBridge");
            Harmony.UnpatchID("Codex.REPO.SpawnBridge.V2");
            Harmony.UnpatchID("Codex.REPO.ControlBridge");
            Harmony.UnpatchID("Codex.REPO.LiveControl");
            new Harmony(HarmonyId).PatchAll(typeof(Bridge).Assembly);

            var serverThread = new Thread(ListenForRequests)
            {
                IsBackground = true,
                Name = "Codex REPO Live Control"
            };
            serverThread.Start();
        }

        private static void ListenForRequests()
        {
            while (true)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1))
                    {
                        pipe.WaitForConnection();
                        string command;
                        using (var reader = new StreamReader(
                            pipe,
                            new System.Text.UTF8Encoding(false),
                            false,
                            1024,
                            true))
                        {
                            command = reader.ReadLine();
                        }

                        string response;
                        if (string.IsNullOrWhiteSpace(command))
                        {
                            response = "ERROR Empty command.";
                        }
                        else
                        {
                            var request = new ControlRequest(command);
                            Requests.Enqueue(request);
                            response = request.Completed.Wait(TimeSpan.FromSeconds(30))
                                ? request.Result
                                : "ERROR Command timed out waiting for the game thread.";
                        }

                        using (var writer = new StreamWriter(
                            pipe,
                            new System.Text.UTF8Encoding(false),
                            1024,
                            true))
                        {
                            writer.AutoFlush = true;
                            writer.WriteLine(response);
                        }
                    }
                }
                catch (Exception exception)
                {
                    try
                    {
                        File.AppendAllText(
                            Path.Combine(Path.GetTempPath(), "RepoLiveControl-pipe.log"),
                            DateTime.UtcNow.ToString("O") + " " + exception + Environment.NewLine);
                    }
                    catch
                    {
                    }
                    Thread.Sleep(250);
                }
            }
        }

        internal static void ProcessFrame()
        {
            if (activeJob != null)
            {
                ProcessSpawnJob(activeJob);
                if (activeJob.Finished)
                    activeJob = null;
                return;
            }

            ControlRequest request;
            if (!Requests.TryDequeue(out request))
                return;

            try
            {
                if (!PhotonNetwork.IsMasterClient)
                    throw new InvalidOperationException("This client is not the lobby host.");

                Dispatch(request);
            }
            catch (Exception exception)
            {
                Complete(request, "ERROR " + exception.Message);
            }
        }

        private static void Dispatch(ControlRequest request)
        {
            string[] parts = request.Command.Split('|');
            string action = Part(parts, 0, string.Empty).ToLowerInvariant();

            switch (action)
            {
                case "enemy":
                    BeginSpawn(request, SpawnKind.Enemy, parts, 200, "near-player");
                    return;
                case "loot":
                    BeginSpawn(request, SpawnKind.Loot, parts, 200, "safe");
                    return;
                case "item":
                    BeginSpawn(request, SpawnKind.Item, parts, 500, "safe");
                    return;
                case "despawn":
                    DespawnEnemies(request, Part(parts, 1, "all"), ParseInt(parts, 2, 0));
                    return;
                case "auto":
                    SetAutomaticEnemies(request, Part(parts, 1, "on"));
                    return;
                case "unstick":
                    UnstickLoot(request);
                    return;
                case "status":
                    ReportStatus(request);
                    return;
                default:
                    throw new InvalidOperationException("Unknown action '" + action + "'.");
            }
        }

        private static void BeginSpawn(
            ControlRequest request,
            SpawnKind kind,
            string[] parts,
            int maximum,
            string defaultPlacement)
        {
            PlayerAvatar player = RequireLocalPlayer();
            string selector = Part(parts, 1, "random");
            int count = Mathf.Clamp(ParseInt(parts, 2, 1), 1, maximum);
            string placement = Part(parts, 3, defaultPlacement).ToLowerInvariant();
            activeJob = new SpawnJob(
                request,
                kind,
                selector,
                placement,
                count,
                player.transform.position);
            ProcessSpawnJob(activeJob);
        }

        private static void ProcessSpawnJob(SpawnJob job)
        {
            try
            {
                int operations = 0;
                while (!job.Finished && operations < 10)
                {
                    switch (job.Kind)
                    {
                        case SpawnKind.Enemy:
                            SpawnEnemyStep(job);
                            break;
                        case SpawnKind.Loot:
                            SpawnLootStep(job);
                            break;
                        case SpawnKind.Item:
                            SpawnItemStep(job);
                            break;
                    }

                    operations++;
                    if (job.Spawned >= job.Requested)
                    {
                        job.Finished = true;
                        string message = string.Format(
                            "OK Spawned {0} {1} object(s){2}.",
                            job.Spawned,
                            job.Kind.ToString().ToLowerInvariant(),
                            job.Names.Length == 0 ? string.Empty : ": " + job.Names);
                        Complete(job.Request, message);
                    }
                }
            }
            catch (Exception exception)
            {
                job.Finished = true;
                Complete(job.Request, string.Format(
                    "ERROR Spawn stopped after {0}/{1}: {2}",
                    job.Spawned,
                    job.Requested,
                    exception.Message));
            }
        }

        private static void SpawnEnemyStep(SpawnJob job)
        {
            EnemySetup setup = FindEnemy(job.Selector);
            if (setup == null)
                throw new InvalidOperationException("No enemy matches '" + job.Selector + "'.");

            Vector3 offset = UnityEngine.Random.insideUnitSphere * 4f;
            offset.y = 0f;
            Vector3 position = SemiFunc.EnemyRoamFindPoint(job.Anchor + offset);
            List<EnemyParent> spawned = Enemies.SpawnEnemy(setup, position, Quaternion.identity, false);
            if (spawned == null || spawned.Count == 0)
                throw new InvalidOperationException("The enemy setup spawned no objects.");

            int needed = job.Requested - job.Spawned;
            int accepted = Math.Min(needed, spawned.Count);
            EnemyDirector director = EnemyDirector.instance;

            for (int index = accepted; index < spawned.Count; index++)
            {
                EnemyParent extra = spawned[index];
                if (director != null)
                    director.enemiesSpawned.Remove(extra);
                if (extra != null)
                    PhotonNetwork.Destroy(extra.gameObject);
            }

            EnemyParent parent = GetEnemyParent(setup);
            AppendName(job, parent == null ? "unknown" : parent.enemyName, accepted);
            job.Spawned += accepted;
        }

        private static void SpawnLootStep(SpawnJob job)
        {
            PrefabRef prefab = FindValuable(job.Selector, job.Spawned);
            if (prefab == null)
                throw new InvalidOperationException("No loot matches '" + job.Selector + "'.");

            Vector3 position = GetPlacement(job);
            GameObject spawned = Valuables.SpawnValuable(prefab, position, Quaternion.identity);
            if (spawned == null)
                throw new InvalidOperationException("REPOLib returned no spawned loot object.");

            AppendName(job, prefab.Prefab.name, 1);
            job.Spawned++;
        }

        private static void SpawnItemStep(SpawnJob job)
        {
            Item item = FindItem(job.Selector);
            if (item == null)
                throw new InvalidOperationException("No item matches '" + job.Selector + "'.");

            Vector3 position = GetPlacement(job);
            GameObject spawned = Items.SpawnItem(item, position, Quaternion.identity);
            if (spawned == null)
                throw new InvalidOperationException("REPOLib returned no spawned item object.");

            AppendName(job, item.itemName, 1);
            job.Spawned++;
        }

        private static Vector3 GetPlacement(SpawnJob job)
        {
            if (job.Placement == "at-player")
                return job.Anchor + Vector3.up * 1.5f;

            if (job.Placement == "near-player")
            {
                Vector3 offset = UnityEngine.Random.insideUnitSphere * 3f;
                offset.y = Math.Abs(offset.y) + 1f;
                return job.Anchor + offset;
            }

            Vector3 safe;
            if (!TryFindClearPosition(job.Anchor, job.ReservedPositions, out safe))
                throw new InvalidOperationException("No additional collision-free placement was found.");
            job.ReservedPositions.Add(safe);
            return safe;
        }

        private static bool TryFindClearPosition(
            Vector3 origin,
            List<Vector3> reserved,
            out Vector3 result)
        {
            LevelGenerator generator = LevelGenerator.Instance;
            List<LevelPoint> levelPoints = generator == null ? null : generator.LevelPathPoints;

            for (int attempt = 0; attempt < 500; attempt++)
            {
                Vector3 seed;
                if (levelPoints != null && levelPoints.Count > 0 && attempt % 2 == 0)
                {
                    LevelPoint point = levelPoints[UnityEngine.Random.Range(0, levelPoints.Count)];
                    seed = point.transform.position;
                }
                else
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float radius = UnityEngine.Random.Range(4f, 30f);
                    seed = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                }

                Vector3 floor = SemiFunc.EnemyRoamFindPoint(seed);
                Vector3 candidate = floor + Vector3.up * 1.75f;
                bool tooClose = false;
                foreach (Vector3 existing in reserved)
                {
                    if (Vector3.Distance(candidate, existing) < 4f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                Collider[] overlaps = Physics.OverlapBox(
                    candidate,
                    new Vector3(1.35f, 1.25f, 1.35f),
                    Quaternion.identity,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                bool blocked = false;
                foreach (Collider overlap in overlaps)
                {
                    if (overlap != null && !overlap.isTrigger)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked)
                {
                    result = candidate;
                    return true;
                }
            }

            result = Vector3.zero;
            return false;
        }

        private static void DespawnEnemies(ControlRequest request, string selector, int keep)
        {
            EnemyDirector director = EnemyDirector.instance;
            if (director == null)
                throw new InvalidOperationException("The enemy director is unavailable.");

            keep = Math.Max(0, keep);
            var matches = new List<EnemyParent>();
            foreach (EnemyParent enemy in director.enemiesSpawned.ToArray())
            {
                if (enemy == null)
                    continue;
                if (selector.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                    enemy.enemyName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    matches.Add(enemy);
            }

            int destroyed = 0;
            for (int index = keep; index < matches.Count; index++)
            {
                EnemyParent enemy = matches[index];
                director.enemiesSpawned.Remove(enemy);
                PhotonNetwork.Destroy(enemy.gameObject);
                destroyed++;
            }

            Complete(request, string.Format(
                "OK Despawned {0} matching enemy object(s); kept {1}.",
                destroyed,
                Math.Min(keep, matches.Count)));
        }

        private static void SetAutomaticEnemies(ControlRequest request, string setting)
        {
            EnemyDirector director = EnemyDirector.instance;
            if (director == null)
                throw new InvalidOperationException("The enemy director is unavailable.");

            bool enabled;
            if (setting.Equals("on", StringComparison.OrdinalIgnoreCase) || setting == "1" ||
                setting.Equals("true", StringComparison.OrdinalIgnoreCase))
                enabled = true;
            else if (setting.Equals("off", StringComparison.OrdinalIgnoreCase) || setting == "0" ||
                setting.Equals("false", StringComparison.OrdinalIgnoreCase))
                enabled = false;
            else
                throw new InvalidOperationException("Auto setting must be on or off.");

            director.enabled = enabled;
            Complete(request, "OK Automatic enemy spawning is " + (enabled ? "enabled." : "disabled."));
        }

        private static void UnstickLoot(ControlRequest request)
        {
            ValuableDirector director = ValuableDirector.instance;
            PlayerAvatar player = RequireLocalPlayer();
            IList tracked = GetField(director, "valuableList") as IList;
            if (tracked == null)
                throw new InvalidOperationException("The tracked loot list is unavailable.");

            var stuck = new List<PhysGrabObject>();
            foreach (object entry in tracked)
            {
                ValuableObject valuable = entry as ValuableObject;
                if (valuable == null)
                    continue;
                PhysGrabObject phys = valuable.GetComponent<PhysGrabObject>() ??
                    valuable.GetComponentInParent<PhysGrabObject>();
                if (phys != null && IsStuck(phys))
                    stuck.Add(phys);
            }

            var reserved = new List<Vector3>();
            int moved = 0;
            foreach (PhysGrabObject phys in stuck)
            {
                Vector3 destination;
                if (!TryFindClearPosition(player.transform.position, reserved, out destination))
                    break;
                reserved.Add(destination);
                phys.Teleport(destination, Quaternion.identity);
                if (phys.rb != null)
                {
                    phys.rb.velocity = Vector3.zero;
                    phys.rb.angularVelocity = Vector3.zero;
                }
                moved++;
            }

            Complete(request, "OK Moved " + moved + " stuck loot object(s) to clear positions.");
        }

        private static bool IsStuck(PhysGrabObject phys)
        {
            foreach (Collider own in phys.GetComponentsInChildren<Collider>())
            {
                if (own == null || !own.enabled || own.isTrigger)
                    continue;

                Bounds bounds = own.bounds;
                Collider[] overlaps = Physics.OverlapBox(
                    bounds.center,
                    bounds.extents * 0.95f,
                    own.transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                foreach (Collider other in overlaps)
                {
                    if (other == null || other == own || other.attachedRigidbody != null ||
                        other.transform.IsChildOf(phys.transform))
                        continue;

                    Vector3 direction;
                    float distance;
                    if (Physics.ComputePenetration(
                        own,
                        own.transform.position,
                        own.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance) &&
                        distance > 0.05f &&
                        (Mathf.Abs(direction.y) < 0.75f || distance > 0.5f))
                        return true;
                }
            }
            return false;
        }

        private static void ReportStatus(ControlRequest request)
        {
            EnemyDirector enemies = EnemyDirector.instance;
            ValuableDirector loot = ValuableDirector.instance;
            int enemyCount = enemies == null ? 0 : enemies.enemiesSpawned.Count;
            int lootCount = loot == null ? 0 : GetListCount(loot, "valuableList");
            bool automatic = enemies != null && enemies.enabled;
            Complete(request, string.Format(
                "OK Status: enemies={0}, loot={1}, automaticEnemySpawning={2}.",
                enemyCount,
                lootCount,
                automatic));
        }

        private static EnemySetup FindEnemy(string selector)
        {
            bool randomHigh = selector.Equals("random", StringComparison.OrdinalIgnoreCase) ||
                selector.Equals("randomhigh", StringComparison.OrdinalIgnoreCase);
            bool high = randomHigh || selector.Equals("high", StringComparison.OrdinalIgnoreCase);
            EnemySetup selected = null;
            int matches = 0;

            foreach (EnemySetup candidate in Enemies.AllEnemies)
            {
                EnemyParent parent = GetEnemyParent(candidate);
                if (parent == null)
                    continue;
                if (!high && parent.enemyName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
                if (high && parent.difficulty == EnemyParent.Difficulty.Difficulty3)
                {
                    if (!randomHigh && parent.enemyName.IndexOf("Reaper", StringComparison.OrdinalIgnoreCase) >= 0)
                        return candidate;
                    matches++;
                    if (UnityEngine.Random.Range(0, matches) == 0)
                        selected = candidate;
                }
            }
            return selected;
        }

        private static PrefabRef FindValuable(string selector, int index)
        {
            if (selector.Equals("expensive", StringComparison.OrdinalIgnoreCase))
                selector = ExpensiveLootNames[index % ExpensiveLootNames.Length];

            var prefabs = Valuables.AllValuables;
            if (selector.Equals("random", StringComparison.OrdinalIgnoreCase))
                return prefabs.Count == 0 ? null : prefabs[UnityEngine.Random.Range(0, prefabs.Count)];

            foreach (PrefabRef prefab in prefabs)
            {
                GameObject gameObject = prefab.Prefab;
                if (gameObject != null && gameObject.name.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    return prefab;
            }
            return null;
        }

        private static Item FindItem(string selector)
        {
            var items = Items.AllItems;
            if (selector.Equals("random", StringComparison.OrdinalIgnoreCase))
                return items.Count == 0 ? null : items[UnityEngine.Random.Range(0, items.Count)];

            foreach (Item item in items)
            {
                if (item != null && item.itemName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    return item;
            }
            return null;
        }

        private static EnemyParent GetEnemyParent(EnemySetup setup)
        {
            foreach (PrefabRef spawnObject in setup.spawnObjects)
            {
                GameObject prefab = spawnObject.Prefab;
                if (prefab == null)
                    continue;
                EnemyParent parent = prefab.GetComponent<EnemyParent>();
                if (parent != null)
                    return parent;
            }
            return null;
        }

        private static PlayerAvatar RequireLocalPlayer()
        {
            PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
            if (player == null)
                throw new InvalidOperationException("The local player is unavailable.");
            return player;
        }

        private static object GetField(object instance, string name)
        {
            if (instance == null)
                return null;
            var field = AccessTools.Field(instance.GetType(), name);
            return field == null ? null : field.GetValue(instance);
        }

        private static int GetListCount(object instance, string name)
        {
            ICollection collection = GetField(instance, name) as ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static string Part(string[] parts, int index, string fallback)
        {
            return parts.Length > index && !string.IsNullOrWhiteSpace(parts[index])
                ? parts[index].Trim()
                : fallback;
        }

        private static int ParseInt(string[] parts, int index, int fallback)
        {
            int value;
            return int.TryParse(Part(parts, index, fallback.ToString()), out value) ? value : fallback;
        }

        private static void AppendName(SpawnJob job, string name, int count)
        {
            for (int index = 0; index < count; index++)
            {
                if (job.Names.Length > 0)
                    job.Names += ", ";
                job.Names += name;
            }
        }

        private static void Complete(ControlRequest request, string result)
        {
            Debug.Log("[Codex Live Control] " + result);
            request.Complete(result);
        }
    }

    [HarmonyPatch(typeof(RunManager), "Update")]
    internal static class MainThreadPatch
    {
        private static void Prefix()
        {
            Bridge.ProcessFrame();
        }
    }
}
