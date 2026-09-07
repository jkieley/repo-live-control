using UnityEngine;
using UnityEngine.AI;

namespace RepoLiveControl.Runtime
{
    internal static class PlayerEnemyPlacement
    {
        private static readonly float[] SearchRadii = { 3f, 5f };

        internal static bool TryFind(Vector3 playerPosition, out Vector3 position)
        {
            // EnemyRoamFindPoint intentionally chooses a distant random destination.
            // Player-location instead samples the nearest local navigation surface.
            foreach (float radius in SearchRadii)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(playerPosition, out hit, radius, NavMesh.AllAreas) &&
                    (hit.position - playerPosition).sqrMagnitude <= radius * radius)
                {
                    position = hit.position;
                    return true;
                }
            }
            position = Vector3.zero;
            return false;
        }
    }
}
