using HarmonyLib;

namespace RepoLiveFix
{
    public static class Loader
    {
        public static void Load()
        {
            Harmony.UnpatchID("Nacho.Repo.EnemySpawning");
        }
    }
}
