using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;

namespace RepoRespawnLoot
{
    public static class Loader
    {
        private const string HarmonyId = "Codex.REPO.RespawnLoot.Once";
        private static int fired;

        public static void Load()
        {
            new Harmony(HarmonyId).Patch(
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

                ValuableDirector director = ValuableDirector.instance;
                if (director == null)
                    throw new InvalidOperationException("The active loot director is not available.");

                var positions = new List<Vector3>();
                var existingLoot = GetField(director, "valuableList") as IList;
                int targetCount = existingLoot == null ? 0 : existingLoot.Count;

                if (existingLoot != null)
                {
                    foreach (object entry in existingLoot)
                    {
                        ValuableObject valuable = entry as ValuableObject;
                        if (valuable != null)
                            positions.Add(valuable.transform.position);
                    }
                }

                PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
                Vector3 fallback = player == null ? Vector3.zero : player.transform.position;
                var prefabs = Valuables.AllValuables;
                if (prefabs == null || prefabs.Count == 0)
                    throw new InvalidOperationException("No valuable prefabs are registered.");

                targetCount = Math.Max(1, targetCount);
                int spawned = 0;

                for (int index = 0; index < targetCount; index++)
                {
                    Vector3 position = positions.Count == 0
                        ? fallback
                        : positions[index % positions.Count];
                    Vector3 offset = UnityEngine.Random.insideUnitSphere * 0.75f;
                    offset.y = Math.Abs(offset.y) + 0.5f;
                    position += offset;

                    PrefabRef prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
                    GameObject spawnedObject = Valuables.SpawnValuable(prefab, position, Quaternion.identity);
                    if (spawnedObject != null)
                        spawned++;
                }

                SetField(director, "valuableTargetAmount", spawned);
                SetField(director, "valuablesSpawned", true);
                Debug.Log(string.Format("[Codex Loot Respawn] Respawned {0} networked loot objects.", spawned));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Loot Respawn] " + exception);
            }
        }

        private static object GetField(object instance, string name)
        {
            var field = AccessTools.Field(instance.GetType(), name);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, name);
            return field.GetValue(instance);
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
