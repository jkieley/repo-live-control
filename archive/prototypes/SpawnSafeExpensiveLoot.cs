using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;

namespace RepoSpawnSafeExpensiveLoot
{
    public static class Loader
    {
        private static int fired;

        public static void Load()
        {
            new Harmony("Codex.REPO.SpawnSafeExpensiveLoot.Once").Patch(
                AccessTools.Method(typeof(RunManager), "Update"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Loader), "RunOnce")));
        }

        private static void RunOnce()
        {
            if (Interlocked.Exchange(ref fired, 1) != 0)
                return;

            try
            {
                if (!PhotonNetwork.IsMasterClient)
                    throw new InvalidOperationException("This client is not the lobby host.");

                PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
                if (player == null)
                    throw new InvalidOperationException("The local player is unavailable.");

                string[] expensiveNames =
                {
                    "Diamond Display",
                    "Griffin Statue",
                    "Dragon Skull",
                    "GoldTooth",
                    "Server Rack"
                };

                var prefabs = new List<PrefabRef>();
                foreach (string name in expensiveNames)
                {
                    PrefabRef prefab = FindValuable(name);
                    if (prefab == null)
                        throw new InvalidOperationException("Could not find expensive loot prefab '" + name + "'.");
                    prefabs.Add(prefab);
                }

                List<Vector3> positions = FindClearPositions(player.transform.position, 20);
                if (positions.Count < 20)
                    throw new InvalidOperationException("Only " + positions.Count + " collision-free positions were available.");

                int spawned = 0;
                for (int index = 0; index < 20; index++)
                {
                    PrefabRef prefab = prefabs[index % prefabs.Count];
                    if (Valuables.SpawnValuable(prefab, positions[index], Quaternion.identity) != null)
                        spawned++;
                }

                Debug.Log("[Codex Safe Loot] Spawned " + spawned + " expensive loot objects at collision-free positions.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Safe Loot] " + exception);
            }
        }

        private static List<Vector3> FindClearPositions(Vector3 origin, int targetCount)
        {
            var positions = new List<Vector3>();
            var generator = LevelGenerator.Instance;
            var levelPoints = generator == null ? null : generator.LevelPathPoints;

            for (int attempt = 0; attempt < 3000 && positions.Count < targetCount; attempt++)
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
                foreach (Vector3 reserved in positions)
                {
                    if (Vector3.Distance(candidate, reserved) < 4f)
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
                    positions.Add(candidate);
            }

            return positions;
        }

        private static PrefabRef FindValuable(string selector)
        {
            foreach (PrefabRef prefab in Valuables.AllValuables)
            {
                GameObject prefabObject = prefab.Prefab;
                if (prefabObject != null &&
                    prefabObject.name.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    return prefab;
            }

            return null;
        }
    }
}
