using System;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace RepoEnableAutoEnemies
{
    public static class Loader
    {
        private static int fired;

        public static void Load()
        {
            new Harmony("Codex.REPO.EnableAutoEnemies.Once").Patch(
                AccessTools.Method(typeof(RunManager), "Update"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Loader), "RunOnce")));
        }

        private static void RunOnce()
        {
            if (Interlocked.Exchange(ref fired, 1) != 0)
                return;

            try
            {
                EnemyDirector director = EnemyDirector.instance;
                if (director == null)
                    throw new InvalidOperationException("The active enemy director is not available.");

                director.enabled = true;
                Debug.Log("[Codex Enemy Director] Automatic enemy spawning enabled.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Enemy Director] " + exception);
            }
        }
    }
}
