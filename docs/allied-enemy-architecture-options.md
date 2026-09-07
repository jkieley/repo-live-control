# Allied enemy architecture options

## Scope tiers

The phrase "spawn enemies that fight for you" can describe materially different systems.

| Scope | Result | Complexity |
|---|---|---|
| Dedicated ally | One purpose-built creature with one moveset | Lowest production risk |
| One converted vanilla enemy | A selected vanilla enemy follows and attacks for its owner | Best feasibility proof |
| Curated vanilla roster | Several enemy types supported through adapters | Higher but controllable |
| Arbitrary enemy conversion | Any vanilla or modded enemy preserves its own attacks | Not supported by a common API |
| Full factions | Hostiles and allies deliberately target one another | Broad AI overhaul |

The dedicated ally and one-enemy conversion are both viable. Arbitrary conversion and full factions require substantially more than REPOLib integration.

See [allied-enemy-feasibility.md](allied-enemy-feasibility.md) for the API boundary and [allied-enemy-research-sources.md](allied-enemy-research-sources.md) for supporting sources.

## Option A: dedicated allied enemy prefab

Build a network prefab specifically for allied combat.

### Core hierarchy

A production prefab would normally contain:

```text
AlliedEnemyRoot
  EnemyParent
  PhotonView
  EnableObject
    AlliedEnemyController
      Enemy
      EnemyHealth
      EnemyNavMeshAgent
      EnemyStateSpawn
      EnemyStateStunned
      PhotonView
      AlliedEnemyBrain
      Animator and visual hierarchy
```

The exact hierarchy should follow a known-good current enemy prefab rather than being assembled from assumptions.

### Benefits

- clean owner and faction state;
- predictable attack timing and damage;
- no competition with a vanilla AI controller;
- explicit Photon state contract;
- independent balance and lifecycle;
- easier maintenance than patching many unrelated enemies.

### Costs

- prefab and animation work;
- custom state machine;
- network-state implementation;
- all participants should install the custom prefab package;
- care is required to prevent the ally from entering normal enemy spawn pools.

### Manual-only registration

`Enemies.RegisterEnemy` adds an `EnemySetup` to the appropriate `EnemyDirector` difficulty list. That is appropriate for naturally spawning monsters but may be wrong for a summon-only ally.

A manual-only design can:

1. register the prefab through `NetworkPrefabs.RegisterNetworkPrefab`;
2. keep a private `EnemySetup` whose `spawnObjects` refer to that prefab;
3. pass the unregistered setup to `Enemies.SpawnEnemy` when the host authorizes a summon.

`Enemies.SpawnEnemy` does not require the setup to be present in the director catalog. It still performs the normal setup, teleport, spawn tracking, and first-spawn-point integration.

This separation avoids random allied spawns and keeps summoning under mod control.

## Option B: convert one vanilla enemy

Use a vanilla `EnemySetup`, spawn the normal network prefab, and replace the behavior only on the summoned instance.

### Conversion sequence

1. Resolve an allowed vanilla `EnemySetup`.
2. Host-spawn it through `Enemies.SpawnEnemy`.
3. attach an allied identity containing owner and team;
4. disable or remove its original behavior controller;
5. remove original UnityEvent listeners that still issue hostile transitions;
6. attach the allied brain;
7. reuse its `EnemyParent`, health, NavMesh, stun, model, animator, sounds, and attack assets;
8. synchronize the new state and target contract.

### Benefits

- no new model or asset bundle for the first proof;
- existing health, stun, ragdoll, effects, and animations;
- exercises real R.E.P.O. enemy lifecycle immediately;
- suitable for testing single-player combat behavior before building production assets.

### Risks

- vanilla AI may be split across several scripts;
- persistent child components can continue acting after the obvious controller is removed;
- animator triggers and attack hitboxes are enemy-specific;
- updates can change prefab paths and component layout;
- another AI-overhaul mod may patch the same enemy;
- remote clients need a consistent view of replacement state and animation.

This option should begin with one enemy type and one attack. It should not present itself as a generic conversion framework.

## Option C: shared brain with per-enemy adapters

If several vanilla enemies must become summonable, keep common behavior in one brain and isolate presentation and attack differences in adapters.

```text
AlliedEnemyBrain
  ownership
  target selection
  navigation
  leash and return
  stuck recovery
  attack scheduling
  host authority
  Photon state

EnemyAdapter
  animation parameters
  attack range
  windup and impact timing
  movement tuning
  sound and effects
  optional projectile behavior
```

Example adapters could support Robe, Runner, and Reaper without pretending that their native controllers share one moveset contract.

The adapter boundary should be introduced only after the first enemy works. A speculative universal abstraction would hide the actual differences rather than remove them.

## Option D: full faction-aware enemy AI

A full faction system would assign every actor a team and permit hostile controllers to choose a player or enemy target.

That requires:

- a common target abstraction;
- distance, visibility, position, alive, and damage operations for both players and enemies;
- faction-aware perception;
- per-enemy translation from that target abstraction into existing state machines;
- per-attack translation from player damage to enemy damage;
- compatibility rules for custom enemies from other mods.

No current common API supplies those translations. This option is technically possible through Harmony and controller replacement but should be treated as an AI-overhaul project, not an extension of the summon feature.

## Recommended production architecture

Use a dedicated manually spawned network prefab with a host-authoritative brain.

### Runtime identities

Conceptually, each allied actor needs:

| Field | Purpose |
|---|---|
| Ally network ViewID | Stable identity for RPC/state references |
| Owner actor or player ViewID | Follow target and summon ownership |
| Team | Exclude self and allied actors from target selection |
| Current state | Remote animation and behavior display |
| Target enemy ViewID | Stable target reference across clients |
| Attack sequence | Prevent duplicate impact effects |
| Spawn session | Reject stale state after room or level changes |

The owner and target must be resolved from authoritative Photon identities, not untrusted IDs supplied by a client request.

### State machine

```text
Spawn
  -> FollowOwner

FollowOwner
  -> AcquireTarget when scan cooldown expires
  -> ReturnToOwner when outside follow band

AcquireTarget
  -> ChaseTarget when a valid reachable hostile exists
  -> FollowOwner when no target exists

ChaseTarget
  -> AttackWindup when in range and visible
  -> AcquireTarget when target dies, despawns, or becomes unreachable
  -> ReturnToOwner when leash is exceeded

AttackWindup
  -> AttackImpact at the authoritative impact time

AttackImpact
  -> AttackRecovery after exactly one damage decision

AttackRecovery
  -> ChaseTarget if target remains valid
  -> AcquireTarget otherwise

Any active state
  -> Stunned when the game stun state begins
  -> DeadOrDespawned on death, scene exit, or authorized dismissal
```

Only the host advances authoritative transitions. Clients may use received state timers for smooth visuals but do not choose the next state.

## Target acquisition

### Live registry

Maintain active enemy records from spawn, death, and despawn hooks. A record needs only stable references and cached target facts; it should not duplicate game health state.

A valid hostile target should satisfy all of these:

- non-null and spawned;
- not the attacker;
- no allied identity, or a different hostile team;
- health exists and is not dead or death-started;
- inside acquisition and owner leash ranges;
- center position can be projected onto the NavMesh;
- path is reachable within the selected tolerance;
- optional line of sight for acquisition or attack.

### Scheduling

Do not enumerate the scene or allocate collections every frame.

- Refresh target choice on a decision interval or when the current target becomes invalid.
- Recalculate a moving destination at a lower frequency than rendering.
- Use non-allocating physics queries where collision sensing is required.
- Reuse target buffers and path data.
- Cap allied units per player and per room.

### Scoring

A simple initial score should prefer:

1. a current valid target to avoid oscillation;
2. an enemy threatening or near the owner;
3. shortest reachable NavMesh distance;
4. deterministic ViewID order as a tie breaker.

Complex threat tables are unnecessary until observed gameplay requires them.

## Navigation and recovery

Enemy targets can be stunned, thrown, carried, or moved off NavMesh. The brain therefore needs more than `SetDestination(target.position)`.

For each repath:

1. take the target's current center position;
2. sample a nearby NavMesh point;
3. reject the target if no usable point exists;
4. keep an attack standoff distance instead of pathing through the target;
5. stop pathing during windup and impact;
6. reset or repath if movement remains below a threshold;
7. abandon the target after a bounded stall;
8. return to the owner when the combat leash is exceeded.

Teleport recovery should be reserved for severe owner separation or level geometry failures, not normal combat pathing.

## Attack authority

The initial melee attack should use direct, host-side `EnemyHealth.Hurt`.

At impact:

1. verify the brain is still in `AttackImpact`;
2. verify this attack sequence has not dealt damage;
3. resolve the target from its current Photon identity;
4. verify target team, alive state, range, and line of sight again;
5. calculate direction from attacker to target;
6. call `EnemyHealth.Hurt` once;
7. mark the attack sequence consumed;
8. broadcast or serialize visual impact state.

Never trust the target or damage supplied by a non-host client. A client should request a summon, not report combat hits.

A later physical-hitbox adapter can use `HurtCollider` when a particular enemy's animation genuinely benefits from collision-driven multi-target damage.

## Summon request flow

```text
local command or summon item
  -> request to current Master Client
  -> host validates sender, scene, limit, cooldown, and spawn point
  -> host spawns room object
  -> host assigns owner and initial state
  -> clients resolve prefab and render synchronized state
```

The host should reject:

- requests outside a gameplay level;
- unknown ally types;
- invalid or distant spawn points;
- requests above owner or room limits;
- duplicate request IDs;
- requests from a player who left or changed rooms;
- work that crossed a host migration or session revision.

The existing RepoLiveControl request/session model is a useful local pattern for sender identity, host migration, and stale-room rejection, though the allied brain should remain independent of the command bridge.

## Lifecycle rules

The implementation needs explicit behavior for:

- owner death and revive;
- owner disconnect;
- owner entering the truck;
- level completion and scene switch;
- ally death and valuable drops;
- ally stun and physics displacement;
- dismissal or replacement at the summon cap;
- host migration;
- target death during windup;
- target despawn during pursuit;
- multiple allies selecting the same target.

For a first version, the safest defaults are current-level lifetime, one ally per owner, normal finite health, no persistence, no normal enemy rewards, and despawn on owner disconnect or scene exit.

## Validation sequence

### Single-player behavior

- Spawn at a sampled NavMesh position near the player.
- Follow without colliding continuously with the owner.
- Acquire one live hostile enemy.
- Navigate through doors and around room geometry.
- Deliver exactly one damage event per attack.
- Never damage the owner or another ally.
- Recover when the target dies, despawns, is stunned, or leaves NavMesh.
- Resume after the ally is stunned.
- Despawn cleanly on level transition.

### Multiplayer behavior

- Host summon appears and animates on a client.
- Client summon request is authorized and executed only by the host.
- A client cannot choose a target or report damage.
- Host and client observe the same target, state, hurt, and death result.
- Duplicate and stale requests are rejected.
- A late joiner resolves room objects and current ally state if late join is supported.
- Host migration either transfers authority safely or despawns allies deterministically.

### Compatibility

- Run with no AI-overhaul mods.
- Run with supported enemy content mods.
- Detect incompatible prefab/controller changes explicitly instead of leaving two brains active.
- Recheck component layout and method signatures after each R.E.P.O. or REPOLib update.

## Open gameplay decisions

Before implementation, decide:

- dedicated creature or converted vanilla enemy;
- host-only summon input or client-to-host requests;
- command, item, shop upgrade, or another summon trigger;
- one ally per player or a room-wide cap;
- follow, guard, or hold-position behavior;
- combat leash and return distance;
- finite lifetime or until death;
- player and allied friendly-fire rules;
- whether hostile enemies must deliberately retaliate;
- normal, suppressed, or custom death rewards;
- whether allies persist across levels.

The retaliation decision changes the project boundary most. "Allies attack monsters" fits a contained custom brain. "All monsters participate in factions" requires broader enemy AI replacement.
