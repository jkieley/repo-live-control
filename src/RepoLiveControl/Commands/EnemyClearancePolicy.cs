using System;
using System.Collections.Generic;

namespace RepoLiveControl.Commands
{
    /// <summary>
    /// Keeps collision probes above the navigation floor while preserving the
    /// enemy envelope above it. The floor itself is an expected contact, not
    /// an obstruction to spawning.
    /// </summary>
    public static class EnemyClearancePolicy
    {
        public const float MinimumProbeBottomOffset = 0.15f;

        public static readonly IReadOnlyList<string> GameplaySolidLayerNames =
            Array.AsReadOnly(new[]
            {
                "Default",
                "StaticGrabObject",
                "Enemy",
                "Player",
                "PhysGrabObject",
                "PhysGrabObjectCart",
                "PhysGrabObjectHinge"
            });

        public static float ClampProbeBottomOffset(float bottomOffset)
        {
            if (float.IsNaN(bottomOffset) || float.IsInfinity(bottomOffset))
                throw new ArgumentOutOfRangeException("bottomOffset");

            return Math.Max(bottomOffset, MinimumProbeBottomOffset);
        }

        public static int BuildGameplaySolidMask(Func<string, int> layerLookup)
        {
            if (layerLookup == null)
                throw new ArgumentNullException("layerLookup");

            int mask = 0;
            foreach (string layerName in GameplaySolidLayerNames)
            {
                int layer = layerLookup(layerName);
                if (layer >= 0 && layer < 32)
                    mask |= 1 << layer;
            }
            return mask;
        }

        /// <summary>
        /// Only active solid geometry or geometry owned by a Rigidbody
        /// contributes to the fallback clearance envelope. Enemy prefabs can
        /// contain inactive helper meshes and colliders (for example,
        /// editor/debug planes) whose bounds dwarf the physical body.
        /// </summary>
        public static bool IsBodyGeometryEligible(
            bool componentEnabled,
            bool isTrigger,
            bool activeInPrefabHierarchy,
            bool attachedToRigidbody)
        {
            return componentEnabled && !isTrigger &&
                (activeInPrefabHierarchy || attachedToRigidbody);
        }

        /// <summary>
        /// Validates serialized NavMeshAgent dimensions before they are used
        /// as the enemy's collision-clearance envelope.
        /// </summary>
        public static bool IsNavigationEnvelopeUsable(
            float radius,
            float height,
            float baseOffset,
            float horizontalScale,
            float verticalScale)
        {
            return IsFinite(radius) && radius > 0f &&
                IsFinite(height) && height > 0f &&
                IsFinite(baseOffset) &&
                IsFinite(horizontalScale) && horizontalScale > 0f &&
                IsFinite(verticalScale) && verticalScale > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
