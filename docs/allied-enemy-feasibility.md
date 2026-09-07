# Allied enemy mod feasibility

## Conclusion

A R.E.P.O. mod can spawn an allied creature that follows a player, finds hostile enemies, and damages them. The current community modding stack exposes the required spawn, navigation, health, and Photon networking primitives.

The missing layer is allied combat behavior. R.E.P.O. and REPOLib do not expose a general faction system, an ally flag, or a common `Attack(target)` interface. The mod must implement target selection, pursuit, attack timing, team filtering, and network authority.

The practical boundary is:

- Reuse the game's enemy lifecycle, NavMesh, health, stun, death, and damage synchronization.
- Reuse REPOLib for prefab registration and host-authoritative spawning.
- Write the allied state machine and affiliation rules.
- Write per-enemy adapters if the goal is to preserve multiple vanilla enemies' original movesets.

See [allied-enemy-architecture-options.md](allied-enemy-architecture-options.md) for implementation choices and [allied-enemy-research-sources.md](allied-enemy-research-sources.md) for source evidence.

## Supported capabilities

The researched stack consists of R.E.P.O. 0.4.4, REPOLib 4.2.0, Unity 2022.3.62, BepInEx 5.4.21, and the publicized `R.E.P.O.GameLibs.Steam` 0.4.4 reference package.

| Requirement | Existing support | Work owned by the mod |
|---|---|---|
| Register a custom enemy | REPOLib `Enemies.RegisterEnemy` | Configure a valid prefab and avoid unwanted natural spawning |
| Spawn an enemy | REPOLib `Enemies.SpawnEnemy` | Select location, owner, limits, and summon trigger |
| Spawn a network object | REPOLib `NetworkPrefabs.SpawnNetworkPrefab` | Route non-host requests to the host |
| Multiplayer messages | Photon RPCs, `IPunObservable`, and REPOLib `NetworkedEvent` | Define request and state payloads |
| Movement | `EnemyNavMeshAgent` and Unity NavMesh | Select destinations, repath, leash, and recover from stalls |
| Health and death | `EnemyHealth`, `EnemyStateStunned`, and `EnemyParent` | Decide which targets can be damaged |
| Enemy damage | `EnemyHealth.Hurt(int, Vector3)` and `HurtCollider.enemyDamage` | Validate range, line of sight, cooldown, and faction |
| Enemy catalog | REPOLib enemy setup catalog | Maintain or query live enemy instances separately |
| Teams and factions | No general facility found | Add allied metadata and filtering |
| Enemy-to-enemy aggro | No general facility found | Patch or replace hostile AI if retaliation is required |

`REPOLib.Enemies.AllEnemies` is a catalog of `EnemySetup` definitions, not a list of active actors. A combat brain needs a live target registry or a low-frequency, non-allocating nearby query.

## Evidence from existing mods

### Complete custom AI is possible

The current Swordsmachine implementation builds a custom `EnemyParent`/`Enemy` hierarchy, adds health and NavMesh components, runs decisions only on the host, and synchronizes state and target identity with Photon. It implements patrol, detection, windup, dash, impact, recovery, stun, death, and remote-client visuals.

Its targets are `PlayerAvatar` instances. An allied creature would replace that player-specific target domain with live `Enemy`/`EnemyHealth` instances and use the native enemy damage path.

### Vanilla AI can be replaced

Enemy Overhaul intercepts `EnemyRobe.Awake`, attaches a replacement controller, removes the original Robe controller, and reuses the model, animator, sounds, health, hitbox, and `EnemyNavMeshAgent`. Its replacement controller implements follow, help, roam, chase, and attack states and synchronizes selected fields through Photon RPCs.

This proves that a spawned vanilla enemy can be converted into a companion. It does not make the conversion generic: each vanilla enemy has different controllers, animations, attacks, and persistent components.

### Native enemy damage is usable

Repo-Assess documents and patches the current enemy health flow. Damage from weapons, equipment, tumbles, collisions, falls, pits, and instant kills converges on `EnemyHealth.Hurt`. The host changes `healthCurrent`; hurt and death results are broadcast to clients.

An allied attack can therefore:

1. run its hit decision on the host,
2. revalidate the target, distance, and line of sight,
3. call `target.Health.Hurt(damage, direction)`, and
4. let the game distribute native hurt and death results.

This avoids inventing a parallel health or damage protocol.

## What must be coded

A minimum allied brain needs these states:

```text
Spawn
FollowOwner
AcquireTarget
ChaseTarget
AttackWindup
AttackImpact
AttackRecovery
ReturnToOwner
Stunned
DeadOrDespawned
```

It also needs:

- an owner reference, normally a player Photon ViewID;
- an affiliation marker distinguishing allied and hostile enemies;
- target validity and friendly-fire filters;
- target scoring, leash distance, and reacquisition behavior;
- NavMesh sampling, reachability checks, and stuck recovery;
- attack range, line-of-sight, windup, impact, and recovery rules;
- state and target synchronization for remote clients;
- owner disconnect, scene transition, death, and despawn handling;
- host migration recovery if multiplayer sessions may continue after a host change.

The game supplies the mechanical components but not these decisions.

## Damage implementation choices

### Direct `EnemyHealth.Hurt`

This is the preferred first implementation.

At the attack impact frame, the host validates exactly one target and calls its health component. This provides deterministic team filtering, prevents accidental player damage, and retains native health, hurt, death, and drop behavior.

Visual collision can remain separate from authoritative damage. An animation event or state timer can identify the single impact frame.

### `HurtCollider`

A physical attack hitbox can set `enemyDamage` above zero and `playerDamage` to zero, then enable the collider only during the attack window.

This produces collision-driven contact but complicates self-hits, allied friendly fire, multi-target hits, and cooldown control. It may require a Harmony filter that understands which allied actor owns the collider.

### Projectile or weapon prefab

A ranged ally can spawn or reuse a networked projectile whose `HurtCollider` damages enemies. This is viable after melee authority and filtering are proven, but adds projectile ownership, collision attribution, and network-effect work.

## The retaliation limitation

An ally intentionally attacking hostile enemies is straightforward. Hostile enemies intentionally attacking the ally is not.

Existing enemy controllers are player-centric:

- perception resolves a `PlayerAvatar`;
- target fields and RPCs carry player ViewIDs;
- chase states navigate to player transforms;
- many attacks call player-specific death/damage methods or use `playerDamage`.

No common player-or-enemy attack target interface was found. In the first implementation, hostile monsters would continue to target players. The ally could still receive incidental enemy damage from explosions, physics, hazards, and attacks that already damage enemies.

True bidirectional factions require one of these larger changes:

- patch each supported vanilla enemy to accept allied-enemy targets;
- replace a curated set of vanilla controllers with faction-aware versions;
- build a wider enemy AI overhaul around a new target abstraction.

Representing the ally as a fake `PlayerAvatar` is not recommended. Player lists, networking, death, extraction, UI, and spectating could all treat it as a real participant.

## Multiplayer constraints

The master client should exclusively perform:

- summon validation;
- network spawning and despawning;
- target acquisition and changes;
- NavMesh destination decisions;
- attack hit validation;
- enemy health mutation.

Remote clients should interpolate movement and render state, animation, audio, and effects.

REPOLib rejects network-prefab spawning when the caller is not the host and uses `PhotonNetwork.InstantiateRoomObject` in multiplayer. A non-host summon action therefore needs a request to the current master client, followed by host-side validation.

A custom prefab is resolved locally from the registered resource path. Consequently, a production custom-enemy package should require the same mod and prefab registration on every participant. A vanilla-prefab prototype may reduce asset requirements, but correct custom state and animation remain more reliable when every client has the behavior DLL.

## Compatibility and maintenance

This is a viable community modding stack, not a stable first-party gameplay SDK. Updates can change internal fields, prefab hierarchy, Photon observed-component order, state enums, animation names, health methods, and navigation behavior.

REPOLib itself has required version-specific fixes, including an update for R.E.P.O. 0.4.4 and an earlier fix for REPOLib-spawned enemies being unable to see players. A maintainable implementation should:

- compile against the versioned publicized game references;
- isolate reflection or Harmony access behind a small compatibility boundary;
- avoid depending on unrelated private fields;
- keep authoritative state independent of animations;
- test spawning, damage, death, and multiplayer after each game update.
