namespace RepoLiveControl.Runtime
{
    internal static class NetworkSessionSceneActivationPolicy
    {
        internal static bool ShouldActivate(
            bool managerAvailable,
            bool currentLevelAvailable,
            bool isLobby,
            bool isGameplay,
            bool isShop,
            bool isArena)
        {
            return managerAvailable &&
                currentLevelAvailable &&
                (isLobby || isGameplay || isShop || isArena);
        }
    }
}
