using System;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;

namespace RepoSpawnStrengthPacks
{
    public static class Loader
    {
        private static bool initialized;
        private static bool completed;
        private static Item strengthPack;
        private static Vector3 spawnPosition;
        private static int remaining = 100;
        private static int spawned;

        public static void Load()
        {
            new Harmony("Codex.REPO.SpawnStrengthPacks.Once").Patch(
                AccessTools.Method(typeof(RunManager), "Update"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Loader), "Tick")));
        }

        private static void Tick()
        {
            if (completed)
                return;

            try
            {
                if (!initialized)
                {
                    if (!PhotonNetwork.IsMasterClient)
                        throw new InvalidOperationException("This client is not the lobby host.");

                    PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
                    if (player == null)
                        return;

                    foreach (Item item in Items.AllItems)
                    {
                        if (item != null &&
                            item.itemName.IndexOf("strength", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            strengthPack = item;
                            break;
                        }
                    }
                    if (strengthPack == null)
                        throw new InvalidOperationException("No registered item contains 'strength'.");

                    spawnPosition = player.transform.position + Vector3.up * 1.5f;
                    initialized = true;
                }

                int batch = Math.Min(10, remaining);
                for (int index = 0; index < batch; index++)
                {
                    if (Items.SpawnItem(strengthPack, spawnPosition, Quaternion.identity) != null)
                        spawned++;
                    remaining--;
                }

                if (remaining <= 0)
                {
                    completed = true;
                    Debug.Log("[Codex Strength Packs] Spawned " + spawned + " Strength Packs at the host location.");
                }
            }
            catch (Exception exception)
            {
                completed = true;
                Debug.LogError("[Codex Strength Packs] " + exception);
            }
        }
    }
}
