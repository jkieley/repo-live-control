using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using BepInEx;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;

namespace RepoControlBridge
{
    [BepInPlugin("Codex.REPO.ControlBridge", "Codex REPO Control Bridge", "1.0.0")]
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

    internal static class Bridge
    {
        private const string HarmonyId = "Codex.REPO.ControlBridge";
        private const string PipeName = "CodexRepoControlV1";
        private static readonly ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();
        private static int started;

        internal static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                return;

            Harmony.UnpatchID("Codex.REPO.SpawnBridge");
            Harmony.UnpatchID("Codex.REPO.SpawnBridge.V2");
            new Harmony(HarmonyId).PatchAll(typeof(Bridge).Assembly);

            var serverThread = new Thread(ListenForRequests)
            {
                IsBackground = true,
                Name = "Codex REPO Control Bridge"
            };
            serverThread.Start();
        }

        private static void ListenForRequests()
        {
            while (true)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1))
                    {
                        pipe.WaitForConnection();
                        using (var reader = new StreamReader(pipe))
                        {
                            string request = reader.ReadLine();
                            if (!string.IsNullOrWhiteSpace(request))
                                Requests.Enqueue(request);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Codex Control Bridge] Pipe error: " + exception.Message);
                    Thread.Sleep(250);
                }
            }
        }

        internal static void ProcessNextRequest()
        {
            string request;
            if (!Requests.TryDequeue(out request))
                return;

            try
            {
                if (!PhotonNetwork.IsMasterClient)
                    throw new InvalidOperationException("This client is not the lobby host.");

                string[] parts = request.Split('|');
                string action = parts[0].Trim().ToLowerInvariant();
                string selector = parts.Length > 1 ? parts[1].Trim() : "random";
                int count = ParseCount(parts, 2);

                switch (action)
                {
                    case "enemy":
                        SpawnEnemies(selector, Mathf.Clamp(count, 1, 10));
                        break;
                    case "loot":
                        SpawnLoot(selector, Mathf.Clamp(count, 1, 50));
                        break;
                    case "despawn":
                        DespawnEnemies();
                        break;
                    case "status":
                        LogStatus();
                        break;
                    default:
                        throw new InvalidOperationException("Unknown bridge action '" + action + "'.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Control Bridge] " + exception);
            }
        }

        private static void SpawnEnemies(string selector, int count)
        {
            PlayerAvatar player = RequireLocalPlayer();
            int totalSpawned = 0;
            string names = string.Empty;

            for (int index = 0; index < count; index++)
            {
                EnemySetup selected = FindEnemy(selector);
                if (selected == null)
                    throw new InvalidOperationException("No matching enemy is registered for '" + selector + "'.");

                EnemyParent parent = GetEnemyParent(selected);
                names += (names.Length == 0 ? "" : ", ") + (parent == null ? "unknown" : parent.enemyName);

                Vector3 offset = UnityEngine.Random.insideUnitSphere * 4f;
                offset.y = 0f;
                Vector3 position = SemiFunc.EnemyRoamFindPoint(player.transform.position + offset);
                var spawned = Enemies.SpawnEnemy(selected, position, Quaternion.identity, false);
                if (spawned != null)
                    totalSpawned += spawned.Count;
            }

            Debug.Log(string.Format(
                "[Codex Control Bridge] Spawned {0} enemy object(s): {1}.",
                totalSpawned,
                names));
        }

        private static void SpawnLoot(string selector, int count)
        {
            PlayerAvatar player = RequireLocalPlayer();
            int spawned = 0;
            string names = string.Empty;

            for (int index = 0; index < count; index++)
            {
                PrefabRef prefab = FindValuable(selector);
                if (prefab == null)
                    throw new InvalidOperationException("No matching loot is registered for '" + selector + "'.");

                GameObject prefabObject = prefab.Prefab;
                names += (names.Length == 0 ? "" : ", ") + prefabObject.name;

                Vector3 offset = UnityEngine.Random.insideUnitSphere * 3f;
                offset.y = Math.Abs(offset.y) + 1f;
                Vector3 position = player.transform.position + offset;
                if (Valuables.SpawnValuable(prefab, position, Quaternion.identity) != null)
                    spawned++;
            }

            Debug.Log(string.Format(
                "[Codex Control Bridge] Spawned {0} loot object(s): {1}.",
                spawned,
                names));
        }

        private static void DespawnEnemies()
        {
            EnemyDirector director = EnemyDirector.instance;
            if (director == null)
                throw new InvalidOperationException("The active enemy director is not available.");

            director.enabled = false;
            EnemyParent[] snapshot = director.enemiesSpawned.ToArray();
            int destroyed = 0;

            foreach (EnemyParent enemy in snapshot)
            {
                if (enemy == null)
                    continue;

                PhotonNetwork.Destroy(enemy.gameObject);
                destroyed++;
            }

            director.enemiesSpawned.Clear();
            Debug.Log("[Codex Control Bridge] Despawned " + destroyed + " enemy object(s).");
        }

        private static void LogStatus()
        {
            EnemyDirector enemies = EnemyDirector.instance;
            ValuableDirector loot = ValuableDirector.instance;
            int enemyCount = enemies == null ? 0 : enemies.enemiesSpawned.Count;
            int lootCount = loot == null ? 0 : GetListCount(loot, "valuableList");
            bool automaticEnemies = enemies != null && enemies.enabled;

            Debug.Log(string.Format(
                "[Codex Control Bridge] Status: enemies={0}, loot={1}, automaticEnemySpawning={2}.",
                enemyCount,
                lootCount,
                automaticEnemies));
        }

        private static EnemySetup FindEnemy(string selector)
        {
            bool randomHigh =
                string.Equals(selector, "random", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(selector, "randomhigh", StringComparison.OrdinalIgnoreCase);
            bool preferHigh =
                randomHigh ||
                string.Equals(selector, "high", StringComparison.OrdinalIgnoreCase);
            EnemySetup selected = null;
            int matches = 0;

            foreach (EnemySetup candidate in Enemies.AllEnemies)
            {
                EnemyParent parent = GetEnemyParent(candidate);
                if (parent == null)
                    continue;

                if (!preferHigh && parent.enemyName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;

                if (preferHigh && parent.difficulty == EnemyParent.Difficulty.Difficulty3)
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

        private static PrefabRef FindValuable(string selector)
        {
            var prefabs = Valuables.AllValuables;
            bool random = string.IsNullOrWhiteSpace(selector) ||
                string.Equals(selector, "random", StringComparison.OrdinalIgnoreCase);

            if (random)
                return prefabs.Count == 0 ? null : prefabs[UnityEngine.Random.Range(0, prefabs.Count)];

            foreach (PrefabRef prefab in prefabs)
            {
                GameObject prefabObject = prefab.Prefab;
                if (prefabObject != null &&
                    prefabObject.name.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    return prefab;
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
                throw new InvalidOperationException("The local player is not available.");
            return player;
        }

        private static int ParseCount(string[] parts, int index)
        {
            int count;
            return parts.Length > index && int.TryParse(parts[index], out count) ? count : 1;
        }

        private static int GetListCount(object instance, string name)
        {
            var field = AccessTools.Field(instance.GetType(), name);
            var collection = field == null ? null : field.GetValue(instance) as ICollection;
            return collection == null ? 0 : collection.Count;
        }
    }

    [HarmonyPatch(typeof(RunManager), "Update")]
    internal static class MainThreadPatch
    {
        private static void Prefix()
        {
            Bridge.ProcessNextRequest();
        }
    }
}
