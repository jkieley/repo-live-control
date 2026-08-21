using System;
using System.IO;
using System.Reflection;

namespace RepoRuntimeStatus
{
    public static class Loader
    {
        public static void Load()
        {
            const string output = @"C:\Users\James Kieley\Documents\Codex\2026-08-18\search-the-web-are-there-any\repo-runtime-status.txt";

            try
            {
                EnemyDirector enemies = EnemyDirector.instance;
                ValuableDirector loot = ValuableDirector.instance;
                int enemyTotal = 0;
                int enemySpawned = 0;

                if ((object)enemies != null)
                {
                    EnemyParent[] snapshot = enemies.enemiesSpawned.ToArray();
                    enemyTotal = snapshot.Length;
                    foreach (EnemyParent enemy in snapshot)
                    {
                        if ((object)enemy != null && object.Equals(GetField(enemy, "Spawned"), true))
                            enemySpawned++;
                    }
                }

                string status = string.Format(
                    "enemyTotal={0}\nenemySpawned={1}\nlootTarget={2}\nlootSpawnAmount={3}\nlootReadyPlayers={4}\nlootCurrentValue={5}\nlootListCount={6}\nlootSpawnedFlag={7}\n{8}",
                    enemyTotal,
                    enemySpawned,
                    GetField(loot, "valuableTargetAmount"),
                    GetField(loot, "valuableSpawnAmount"),
                    GetField(loot, "valuableSpawnPlayerReady"),
                    GetField(loot, "totalCurrentValue"),
                    GetListCount(loot, "valuableList"),
                    GetField(loot, "valuablesSpawned"),
                    GetLootPools(loot));

                File.WriteAllText(output, status);
            }
            catch (Exception exception)
            {
                File.WriteAllText(output, exception.ToString());
            }
        }

        private static object GetField(object instance, string name)
        {
            if (instance == null)
                return "null-director";
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? "missing-field" : field.GetValue(instance);
        }

        private static object GetListCount(object instance, string name)
        {
            object value = GetField(instance, name);
            var collection = value as System.Collections.ICollection;
            return collection == null ? value : (object)collection.Count;
        }

        private static string GetLootPools(object director)
        {
            string result = string.Empty;
            foreach (string prefix in new[] { "tiny", "small", "medium", "big", "wide", "tall", "veryTall" })
            {
                result += string.Format(
                    "{0}: volumes={1}, prefabs={2}, path={3}\n",
                    prefix,
                    GetListCount(director, prefix + "Volumes"),
                    GetListCount(director, prefix + "Valuables"),
                    GetField(director, prefix + "Path"));
            }
            return result;
        }
    }
}
