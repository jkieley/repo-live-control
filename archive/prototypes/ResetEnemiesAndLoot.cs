using System;
using System.Collections;
using BepInEx;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RepoResetEnemiesAndLoot
{
    public static class Loader
    {
        internal const string HarmonyId = "Codex.REPO.ResetEnemiesAndLoot.Once";

        public static void Load()
        {
            Harmony.UnpatchID(HarmonyId);
            ThreadingHelper.Instance.StartSyncInvoke(RunOnMainThread.Execute);
        }
    }

    internal static class RunOnMainThread
    {
        private static bool started;

        internal static void Execute()
        {
            if (started)
                return;

            started = true;

            try
            {
                if (!PhotonNetwork.IsMasterClient)
                    throw new InvalidOperationException("This client is not the lobby host.");

                EnemyDirector enemyDirector = EnemyDirector.instance;
                ValuableDirector valuableDirector = ValuableDirector.instance;
                if (enemyDirector == null || valuableDirector == null)
                    throw new InvalidOperationException("The active level directors are not available.");

                EnemyParent[] enemies = enemyDirector.enemiesSpawned.ToArray();
                int despawned = 0;
                foreach (EnemyParent enemy in enemies)
                {
                    if (enemy == null)
                        continue;

                    enemy.Despawn();
                    despawned++;
                }

                valuableDirector.StartCoroutine(RespawnLoot(valuableDirector, despawned));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Reset] " + exception);
            }
        }

        private static IEnumerator RespawnLoot(ValuableDirector director, int despawnedEnemies)
        {
            IEnumerator setup = null;

            try
            {
                SetField(director, "valuableSpawnAmount", 0);
                SetField(director, "valuableTargetAmount", 0);
                SetField(director, "valuableSpawnPlayerReady", 0);
                SetField(director, "totalCurrentValue", 0f);
                SetField(director, "totalMaxAmount", 0);
                SetField(director, "valuablesSpawned", false);

                setup = (IEnumerator)AccessTools.Method(typeof(ValuableDirector), "SetupHost")
                    .Invoke(director, null);

                int frames = 0;
                while (setup.MoveNext())
                {
                    if (GetIntField(director, "valuableTargetAmount") > 0 &&
                        GetIntField(director, "valuableSpawnPlayerReady") >= 1)
                        break;

                    if (++frames > 5000)
                        throw new TimeoutException("Loot spawning did not complete within 5000 frames.");

                    yield return setup.Current;
                }

                Debug.Log(string.Format(
                    "[Codex Reset] Despawned {0} enemies and respawned {1} loot objects.",
                    despawnedEnemies,
                    GetIntField(director, "valuableTargetAmount")));
            }
            finally
            {
                IDisposable disposable = setup as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }
        }

        private static void SetField(object instance, string name, object value)
        {
            var field = AccessTools.Field(instance.GetType(), name);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, name);
            field.SetValue(instance, value);
        }

        private static int GetIntField(object instance, string name)
        {
            var field = AccessTools.Field(instance.GetType(), name);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, name);
            return (int)field.GetValue(instance);
        }
    }
}
