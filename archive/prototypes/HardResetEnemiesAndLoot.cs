using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BepInEx;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RepoHardResetEnemiesAndLoot
{
    public static class Loader
    {
        private const string HarmonyId = "Codex.REPO.HardResetEnemiesAndLoot.V2";
        private static int fired;

        public static void Load()
        {
            ThreadingHelper helper = ThreadingHelper.Instance;
            object invokeLock = AccessTools.Field(typeof(ThreadingHelper), "_invokeLock").GetValue(helper);
            lock (invokeLock)
            {
                AccessTools.Field(typeof(ThreadingHelper), "_invokeList").SetValue(helper, null);
            }

            var harmony = new Harmony(HarmonyId);
            harmony.Patch(
                AccessTools.Method(typeof(RunManager), "Update"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Loader), "RunOnce")));
        }

        private static void RunOnce()
        {
            if (Interlocked.Exchange(ref fired, 1) == 0)
                Execute();
        }

        private static void Execute()
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient)
                    throw new InvalidOperationException("This client is not the lobby host.");

                EnemyDirector enemyDirector = EnemyDirector.instance;
                ValuableDirector valuableDirector = ValuableDirector.instance;
                if (enemyDirector == null || valuableDirector == null)
                    throw new InvalidOperationException("The active level directors are not available.");

                enemyDirector.enabled = false;
                EnemyParent[] enemies = enemyDirector.enemiesSpawned.ToArray();
                int destroyedEnemies = 0;

                foreach (EnemyParent enemy in enemies)
                {
                    if (enemy == null)
                        continue;

                    PhotonNetwork.Destroy(enemy.gameObject);
                    destroyedEnemies++;
                }

                enemyDirector.enemiesSpawned.Clear();

                int originalLootCount = GetListCount(valuableDirector, "valuableList");
                int targetLootCount = Math.Max(1, originalLootCount);
                SetField(valuableDirector, "valuableSpawnAmount", 0);
                SetField(valuableDirector, "valuableTargetAmount", 0);
                SetField(valuableDirector, "totalCurrentValue", 0f);
                SetField(valuableDirector, "totalMaxAmount", targetLootCount);

                int spawnedLoot = SpawnFreshLoot(valuableDirector, targetLootCount);
                SetField(valuableDirector, "valuablesSpawned", true);

                Debug.Log(string.Format(
                    "[Codex Hard Reset] Destroyed {0} enemies and respawned {1} loot objects.",
                    destroyedEnemies,
                    spawnedLoot));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Hard Reset] " + exception);
            }
        }

        private static int SpawnFreshLoot(ValuableDirector director, int targetCount)
        {
            ValuableVolume[] foundVolumes = UnityEngine.Object.FindObjectsOfType<ValuableVolume>(false);
            var volumes = new List<ValuableVolume>(foundVolumes);
            for (int index = volumes.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                ValuableVolume temporary = volumes[index];
                volumes[index] = volumes[swapIndex];
                volumes[swapIndex] = temporary;
            }

            var spawnMethod = AccessTools.Method(typeof(ValuableDirector), "SpawnValuable");
            if (spawnMethod == null)
                throw new MissingMethodException(typeof(ValuableDirector).FullName, "SpawnValuable");

            int spawned = 0;
            foreach (ValuableVolume volume in volumes)
            {
                if (spawned >= targetCount)
                    break;

                object volumeType = GetField(volume, "VolumeType");
                string typeName = volumeType.ToString();
                string prefix = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
                var prefabs = GetField(director, prefix + "Valuables") as IList;
                string path = GetField(director, prefix + "Path") as string;
                if (prefabs == null || prefabs.Count == 0 || string.IsNullOrEmpty(path))
                    continue;

                object prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
                spawnMethod.Invoke(director, new[] { prefab, (object)volume, path });
                spawned++;
            }

            return spawned;
        }

        private static object GetField(object instance, string name)
        {
            var field = AccessTools.Field(instance.GetType(), name);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, name);
            return field.GetValue(instance);
        }

        private static int GetListCount(object instance, string name)
        {
            var collection = GetField(instance, name) as ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static void SetField(object instance, string name, object value)
        {
            var field = AccessTools.Field(instance.GetType(), name);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, name);
            field.SetValue(instance, value);
        }
    }
}
