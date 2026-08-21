using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RepoKeepOneGnome
{
    public static class Loader
    {
        private static int fired;

        public static void Load()
        {
            new Harmony("Codex.REPO.KeepOneGnome.Once").Patch(
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

                EnemyDirector director = EnemyDirector.instance;
                if (director == null)
                    throw new InvalidOperationException("The active enemy director is not available.");

                var gnomes = new List<EnemyParent>();
                foreach (EnemyParent enemy in director.enemiesSpawned.ToArray())
                {
                    if (enemy != null && enemy.enemyName.IndexOf("Gnome", StringComparison.OrdinalIgnoreCase) >= 0)
                        gnomes.Add(enemy);
                }

                int destroyed = 0;
                for (int index = 1; index < gnomes.Count; index++)
                {
                    EnemyParent enemy = gnomes[index];
                    director.enemiesSpawned.Remove(enemy);
                    PhotonNetwork.Destroy(enemy.gameObject);
                    destroyed++;
                }

                Debug.Log(string.Format(
                    "[Codex Gnome Cleanup] Destroyed {0} Gnomes; {1} remains.",
                    destroyed,
                    gnomes.Count > 0 ? 1 : 0));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Gnome Cleanup] " + exception);
            }
        }
    }
}
