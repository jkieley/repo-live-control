using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RepoUnstickLoot
{
    public static class Loader
    {
        private static int fired;

        public static void Load()
        {
            new Harmony("Codex.REPO.UnstickLoot.V2.Once").Patch(
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
                PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
                if (director == null || player == null)
                    throw new InvalidOperationException("The loot director or local player is unavailable.");

                var field = AccessTools.Field(typeof(ValuableDirector), "valuableList");
                var trackedLoot = field == null ? null : field.GetValue(director) as IList;
                if (trackedLoot == null)
                    throw new InvalidOperationException("The tracked loot list is unavailable.");

                var stuckObjects = new List<PhysGrabObject>();
                foreach (object entry in trackedLoot)
                {
                    ValuableObject valuable = entry as ValuableObject;
                    if (valuable == null)
                        continue;

                    PhysGrabObject phys = valuable.GetComponent<PhysGrabObject>();
                    if (phys == null)
                        phys = valuable.GetComponentInParent<PhysGrabObject>();
                    if (phys != null && IsStuckInStaticGeometry(phys))
                        stuckObjects.Add(phys);
                }

                for (int index = 0; index < stuckObjects.Count; index++)
                {
                    float angle = index * 137.5f * Mathf.Deg2Rad;
                    float radius = 2.5f + (index % 5) * 1.25f;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 walkable = SemiFunc.EnemyRoamFindPoint(player.transform.position + direction * radius);
                    Vector3 destination = walkable + Vector3.up * (2f + (index % 3) * 1.25f);

                    PhysGrabObject phys = stuckObjects[index];
                    phys.Teleport(destination, Quaternion.identity);
                    if (phys.rb != null)
                    {
                        phys.rb.velocity = Vector3.zero;
                        phys.rb.angularVelocity = Vector3.zero;
                    }
                }

                Debug.Log("[Codex Loot Unstick] Moved " + stuckObjects.Count + " loot object(s) out of static geometry.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Codex Loot Unstick] " + exception);
            }
        }

        private static bool IsStuckInStaticGeometry(PhysGrabObject phys)
        {
            Collider[] ownColliders = phys.GetComponentsInChildren<Collider>();
            foreach (Collider own in ownColliders)
            {
                if (own == null || !own.enabled || own.isTrigger)
                    continue;

                Bounds bounds = own.bounds;
                Collider[] overlaps = Physics.OverlapBox(
                    bounds.center,
                    bounds.extents * 0.95f,
                    own.transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                foreach (Collider other in overlaps)
                {
                    if (other == null || other == own || other.attachedRigidbody != null)
                        continue;
                    if (other.transform.IsChildOf(phys.transform))
                        continue;

                    Vector3 direction;
                    float distance;
                    if (Physics.ComputePenetration(
                        own,
                        own.transform.position,
                        own.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance) &&
                        distance > 0.05f &&
                        (Mathf.Abs(direction.y) < 0.75f || distance > 0.5f))
                        return true;
                }
            }

            return false;
        }
    }
}
