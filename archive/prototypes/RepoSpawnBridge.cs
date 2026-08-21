using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using BepInEx;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;

namespace RepoSpawnBridge
{
    [BepInPlugin("Codex.REPO.SpawnBridge.V2", "Codex REPO Spawn Bridge", "1.1.0")]
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
        private const string HarmonyId = "Codex.REPO.SpawnBridge.V2";
        private const string PipeName = "CodexRepoSpawnV2";
        private static readonly ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();
        private static int started;

        internal static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                return;

            new Harmony(HarmonyId).PatchAll(typeof(Bridge).Assembly);

            var serverThread = new Thread(ListenForRequests)
            {
                IsBackground = true,
                Name = "Codex REPO Spawn Bridge"
            };
            serverThread.Start();
            Debug.Log("[Codex Spawn Bridge] Ready on pipe " + PipeName + ".");
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
                    Debug.LogError("[Codex Spawn Bridge] Pipe error: " + exception.Message);
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

                PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
                if (player == null)
                {
                    Requests.Enqueue(request);
                    return;
                }

                string[] parts = request.Split('|');
                string selector = parts[0].Trim();
                int count = 1;
                if (parts.Length > 1)
                    int.TryParse(parts[1], out count);
                count = Mathf.Clamp(count, 1, 10);

                int totalSpawned = 0;
                string spawnedNames = string.Empty;

                for (int index = 0; index < count; index++)
                {
                    EnemySetup selected = FindEnemy(selector);
                    if (selected == null)
                        throw new InvalidOperationException("No matching enemy is registered for '" + selector + "'.");

                    EnemyParent selectedParent = GetEnemyParent(selected);
                    if (selectedParent != null)
                        spawnedNames += (spawnedNames.Length == 0 ? "" : ", ") + selectedParent.enemyName;

                    Vector3 offset = UnityEngine.Random.insideUnitSphere * 4f;
                    offset.y = 0f;
                    Vector3 position = SemiFunc.EnemyRoamFindPoint(player.transform.position + offset);
                    var spawned = Enemies.SpawnEnemy(selected, position, Quaternion.identity, false);
                    if (spawned != null)
                        totalSpawned += spawned.Count;
                }

                Debug.Log(string.Format(
                    "[Codex Spawn Bridge] Spawned {0} object(s) from {1} for request '{2}|{3}'.",
                    totalSpawned,
                    spawnedNames.Length == 0 ? "unknown" : spawnedNames,
                    selector,
                    count));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Spawn Bridge] " + exception);
            }
        }

        private static EnemySetup FindEnemy(string selector)
        {
            bool chooseRandomHigh =
                string.Equals(selector, "random", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(selector, "randomhigh", StringComparison.OrdinalIgnoreCase);
            bool chooseHigh =
                chooseRandomHigh ||
                string.Equals(selector, "high", StringComparison.OrdinalIgnoreCase);
            EnemySetup firstHigh = null;
            int highSeen = 0;

            foreach (EnemySetup candidate in Enemies.AllEnemies)
            {
                EnemyParent parent = GetEnemyParent(candidate);
                if (parent == null)
                    continue;

                if (!chooseHigh && parent.enemyName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;

                if (chooseHigh && parent.difficulty == EnemyParent.Difficulty.Difficulty3)
                {
                    if (chooseRandomHigh)
                    {
                        highSeen++;
                        if (UnityEngine.Random.Range(0, highSeen) == 0)
                            firstHigh = candidate;
                        continue;
                    }

                    if (firstHigh == null)
                        firstHigh = candidate;
                    if (parent.enemyName.IndexOf("Reaper", StringComparison.OrdinalIgnoreCase) >= 0)
                        return candidate;
                }
            }

            return firstHigh;
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
    }

    [HarmonyPatch(typeof(ChatManager), "Update")]
    internal static class MainThreadPatch
    {
        private static void Prefix()
        {
            Bridge.ProcessNextRequest();
        }
    }
}
