using System;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;

namespace RepoEnemySpawn
{
    public static class Loader
    {
        internal const string HarmonyId = "Codex.RepoEnemySpawn.Once";

        public static void Load()
        {
            new Harmony(HarmonyId).PatchAll(typeof(Loader).Assembly);
        }
    }

    [HarmonyPatch(typeof(ChatManager), "Update")]
    internal static class SpawnOnMainThread
    {
        private static bool completed;

        private static void Prefix()
        {
            if (completed)
                return;

            PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
            if (player == null)
                return;

            completed = true;

            try
            {
                EnemySetup selected = null;

                foreach (EnemySetup candidate in Enemies.AllEnemies)
                {
                    EnemyParent parent = GetEnemyParent(candidate);
                    if (parent == null || parent.difficulty != EnemyParent.Difficulty.Difficulty3)
                        continue;

                    selected = candidate;
                    if (parent.enemyName.IndexOf("Reaper", StringComparison.OrdinalIgnoreCase) >= 0)
                        break;
                }

                if (selected == null)
                    throw new InvalidOperationException("No Difficulty3 enemy is currently registered.");

                Vector3 position = SemiFunc.EnemyRoamFindPoint(player.transform.position);
                EnemyParent selectedParent = GetEnemyParent(selected);
                var spawned = Enemies.SpawnEnemy(selected, position, Quaternion.identity, false);

                Debug.Log(string.Format(
                    "[Codex Live Spawn] Spawned {0} high-level enemy object(s) from {1} at {2}.",
                    spawned == null ? 0 : spawned.Count,
                    selectedParent == null ? "unknown" : selectedParent.enemyName,
                    position));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Live Spawn] " + exception);
            }
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
}
