# Allied enemy research sources

## Purpose

This document records the sources behind the feasibility and architecture conclusions in:

- [allied-enemy-feasibility.md](allied-enemy-feasibility.md)
- [allied-enemy-architecture-options.md](allied-enemy-architecture-options.md)

It separates source-backed behavior from implementation inferences so later work can revalidate assumptions after R.E.P.O. updates.

## Researched versions

| Component | Version or target | Evidence |
|---|---|---|
| R.E.P.O. | 0.4.4 | REPOLib 4.1 changelog compatibility target |
| REPOLib | 4.2.0 | Thunderstore package and repository changelog |
| Unity | 2022.3.62 | current R.E.P.O. Project Patcher and REPOLib project references |
| BepInEx | 5.4.21 | REPOLib project reference |
| Game reference package | `R.E.P.O.GameLibs.Steam` 0.4.4-ngd.0 | NuGet package and REPOLib project reference |
| Runtime target | .NET Standard 2.1 | REPOLib and current custom-enemy projects |

These versions describe the researched API surface. They are not a promise of forward compatibility.

## Primary sources

### REPOLib

- Repository: <https://github.com/ZehsTeam/REPOLib>
- Enemy API: <https://github.com/ZehsTeam/REPOLib/blob/master/REPOLib/Modules/Enemies.cs>
- Network prefab API: <https://github.com/ZehsTeam/REPOLib/blob/master/REPOLib/Modules/NetworkPrefabs.cs>
- Network event API: <https://github.com/ZehsTeam/REPOLib/blob/master/REPOLib/Modules/NetworkedEvent.cs>
- Command API: <https://github.com/ZehsTeam/REPOLib/blob/master/REPOLib/Modules/Commands.cs>
- Changelog: <https://github.com/ZehsTeam/REPOLib/blob/master/CHANGELOG.md>
- Thunderstore: <https://thunderstore.io/c/repo/p/Zehs/REPOLib/>

Relevant findings:

- `Enemies.RegisterEnemy` registers the enemy's spawn objects as network prefabs and adds its `EnemySetup` to an `EnemyDirector` difficulty list.
- `Enemies.AllEnemies` returns `EnemySetup` catalog entries, not live enemy actors.
- `Enemies.SpawnEnemy` accepts an `EnemySetup`, spawns each `PrefabRef`, completes `EnemyParent` setup, teleports the controller, and registers its first spawn point.
- `Enemies.SpawnEnemy` does not require that the setup was previously added to the director catalog.
- `NetworkPrefabs.SpawnNetworkPrefab` rejects callers that are not the master client or single-player authority.
- Multiplayer spawning uses `PhotonNetwork.InstantiateRoomObject` and a prefab resource path.
- `NetworkedEvent` can target all clients, other clients, or the master client, but the file is explicitly marked as not fully documented.
- REPOLib 4.1 updated for R.E.P.O. 0.4.4; 4.2 fixed network-prefab registration behavior.
- REPOLib 4.0.1 fixed REPOLib-spawned enemies being unable to see players, demonstrating that enemy integration can be affected by library and game-version details.

### REPOLib SDK and custom scripts

- SDK repository: <https://github.com/ZehsTeam/REPOLib-Sdk>
- Enemy setup guide: <https://repomods.com/apis/repolib/sdk/enemies.html>
- Custom script guide: <https://repomods.com/apis/repolib/sdk/custom-scripts.html>

Relevant findings:

- The SDK supports custom enemies, automatic asset bundling, and Thunderstore packaging.
- An SDK enemy content asset references an `EnemySetup` plus one or more spawn-object prefabs.
- Custom scripts are compiled in a separate Harmony/BepInEx project, copied into the Unity project, attached to prefabs, and included in the exported package.
- The SDK handles content registration; it does not generate allied AI or faction behavior.

### R.E.P.O. Project Patcher

- Repository: <https://github.com/ZehsTeam/unity-repo-project-patcher>

Relevant findings:

- The patcher generates a Unity project from the developer's locally owned R.E.P.O. build.
- The current wrapper targets Unity 2022.3.62 and the built-in render pipeline.
- The generated project exposes game assets and scripts for personal mod development and in-editor testing.
- It is the appropriate environment for validating prefab hierarchy, animation events, colliders, shaders, and NavMesh behavior.

### Publicized game references

- NuGet package: <https://www.nuget.org/packages/R.E.P.O.GameLibs.Steam>
- Plugin build tooling: <https://github.com/linkoid/Repo.Sdks>

Relevant findings:

- `R.E.P.O.GameLibs.Steam` provides stripped and publicized game assemblies for mod compilation.
- Version 0.4.4-ngd.0 corresponds to the researched R.E.P.O. version.
- `Linkoid.Repo.Plugin.Build` locates the local game installation, supplies game and Unity references, and supports building and launching a plugin from a standard project.
- Publicized assemblies reduce reflection needs but do not create a stable game ABI.

### Puppet custom enemy example

- Repository: <https://github.com/vyrusgames/PuppetEnemy>
- Brain: <https://github.com/vyrusgames/PuppetEnemy/blob/main/AI/EnemyPuppet.cs>
- Game-field access helper: <https://github.com/vyrusgames/PuppetEnemy/blob/main/AI/EnemyUtil.cs>
- Thunderstore: <https://thunderstore.io/c/repo/p/VyrusGames/Puppet/>

Relevant findings:

- Puppet is explicitly published as a custom-enemy example and REPOLib reference.
- Its brain implements spawn, idle, roam, investigate, follow/curiosity, leave, stun, hurt, and death handling.
- The master client or single-player instance advances the AI.
- State changes are synchronized with Photon RPCs.
- It reuses `EnemyNavMeshAgent`, `EnemyRigidbody`, `EnemyVision`, and standard enemy state components.
- The older implementation uses reflection for internal game fields, illustrating the compatibility risk around non-public internals.

### Swordsmachine custom enemy

- Thunderstore package and decompiled source: <https://old.thunderstore.io/c/repo/p/Omniscye/Swordsmachine/source/>

Relevant findings:

- The implementation builds a complete custom enemy hierarchy with `EnemyParent`, nested `Enemy`, Photon views, health, state components, NavMesh, and custom visuals.
- The host runs patrol, player visibility, windup, dash, slam, recovery, and reposition decisions.
- Remote instances consume synchronized state, rotation, timers, and target ViewID.
- The brain implements `IPunObservable` and uses master-client validation for RPC effects.
- Target selection is explicitly based on `PlayerAvatar`, confirming that a custom enemy brain normally owns its target domain.
- Attack impact is separately authored from navigation and visuals, which is the pattern needed for host-authoritative enemy damage.
- The example demonstrates technical feasibility; its implementation quality and gameplay choices are not assumed to be a reusable library contract.

### Enemy Overhaul

- Repository: <https://github.com/Ardot66/REPO.EnemyOverhaul>
- Robe replacement: <https://github.com/Ardot66/REPO.EnemyOverhaul/blob/main/Source/Patches/Robe.cs>
- State machine: <https://github.com/Ardot66/REPO.EnemyOverhaul/blob/main/Source/Patches/StateMachine.cs>
- Aggro helper: <https://github.com/Ardot66/REPO.EnemyOverhaul/blob/main/Source/Patches/AggroHandler.cs>

Relevant findings:

- A Harmony prefix on `EnemyRobe.Awake` attaches `RobeOverride`, removes the original Robe behavior, and suppresses the original initialization.
- The replacement reuses the Robe's animator, sounds, effects, health, hitbox, and navigation.
- Its custom states include follow-player, help-player, chase, attack, and give-space behavior.
- It uses Photon RPCs for state and selected target data.
- It demonstrates that a vanilla enemy can be repurposed, but the code is specific to Robe components and animations.

### Enemy health and damage flow

- Repo-Assess repository: <https://github.com/headclef/Repo-Assess>
- Health patches: <https://github.com/headclef/Repo-Assess/blob/core/Patches/AssessPatch.cs>
- Health reconstruction analysis: <https://github.com/headclef/Repo-Assess/blob/core/EnemyTracker.cs>
- Additional decompiled `EnemyHealth.Hurt` patch signature: <https://old.thunderstore.io/c/repo/p/dev_duviz/REPOCAOS/source/>

Relevant findings:

- `EnemyHealth.Hurt` has the effective signature `Hurt(int damage, Vector3 hurtDirection)`.
- Health mutation is guarded by master-client or single-player authority.
- Non-lethal hurt and death outcomes are broadcast to clients through the game's RPCs.
- Weapons and equipment reach health through `HurtCollider.EnemyHurt`.
- Collision, fall, pit, and instant-kill paths also converge on `EnemyHealth.Hurt`.
- A host-side allied attack can use the normal health pipeline rather than implementing custom health synchronization.

### Existing friendly or companion mods

- FriendlyDuck: <https://thunderstore.io/c/repo/p/purplehaxttv/FriendlyDuck/>
- BotFriends: <https://thunderstore.io/c/repo/p/Omniscye/BotFriends/>
- Enemy Overhaul: <https://thunderstore.io/c/repo/p/Ardot66/Enemy_Overhaul/>
- REPOCompanions: <https://thunderstore.io/c/repo/p/BellaModsGames/REPOCompanions/>
- EnemyLove: <https://thunderstore.io/c/repo/p/sunryze/EnemyLove/>
- EnemyLove source: <https://github.com/sunryze-git/EnemyLove/blob/master/EnemyLove/Plugin.cs>

Relevant findings:

- FriendlyDuck follows and does not attack.
- BotFriends provides single-player player-avatar bots that collect valuables, not combat enemies.
- Enemy Overhaul makes Robe follow and help players but does not turn it into an enemy-fighting combat companion.
- REPOCompanions supplies followers without enemy combat interaction.
- EnemyLove substitutes a nearby enemy's name into Love Potion behavior; its source does not implement faction or combat AI changes.
- No researched package supplied a general ally that intentionally acquires and attacks enemy targets.

## Source-backed conclusions

The following conclusions follow directly from source behavior:

1. REPOLib can register and host-spawn enemy and network prefabs.
2. The game and current mods support custom enemy state machines and Photon synchronization.
3. A vanilla enemy controller can be replaced while retaining game components and assets.
4. `EnemyNavMeshAgent` can drive custom patrol, follow, chase, and attack positioning.
5. `EnemyHealth.Hurt` is the central host-authoritative enemy damage entry point.
6. Current example enemy controllers explicitly target players rather than a generic combat actor.
7. REPOLib registration and game integration are sensitive to R.E.P.O. versions.

## Implementation inferences to validate

The following are strong implementation inferences, not promises made by REPOLib documentation:

### Every client should install a custom allied prefab

REPOLib registers prefab resource paths locally and passes the path to `PhotonNetwork.InstantiateRoomObject`. A client missing that prefab mapping cannot reliably instantiate the room object. The production package should therefore require matching mod and prefab versions on all participants.

Validation: attempt host plus modded client, then host plus unmodded client, and confirm failure behavior is explicit rather than a loading hang or invisible object.

### An unregistered setup can provide manual-only spawning

`Enemies.SpawnEnemy` consumes the supplied setup and does not check director membership. A private setup with registered `PrefabRef` spawn objects should support manual summons without natural spawn-pool registration.

Validation: register only the network prefab, spawn through the private setup, and verify lifecycle, player detection, director tracking, despawn, and scene cleanup.

### Direct `EnemyHealth.Hurt` is the safest first attack

The native path synchronizes enemy health and avoids physical hitbox filtering. It should trigger normal hurt/death events, but specific reward, stun, impulse, and custom-enemy behavior must be checked against the current target types.

Validation: attack vanilla and REPOLib enemies, including normal death, overkill, stun, valuable drops, healing enemies, and targets that despawn during windup.

### Hostile enemies will not generally retaliate

The researched controllers and fields are player-oriented, and no faction interface was found. Some attacks may incidentally damage the ally through enemy damage or physics, but deliberate hostile targeting should not be assumed.

Validation: test every enemy claimed to retaliate and distinguish intentional target acquisition from incidental collision or area damage.

### Vanilla-prefab conversion may reduce client asset requirements

All clients already own vanilla prefabs, but custom AI state and animation synchronization still depend on the behavior DLL and protocol. A host-only prototype may be visible to unmodded clients through base enemy synchronization, but correctness is not guaranteed.

Validation: test unmodded remote clients for state, animation, damage, stun, death, despawn, and host migration before advertising host-only compatibility.

## Research gaps for implementation time

Before coding against a chosen enemy or prefab, inspect the exact current game assembly and Unity hierarchy for:

- live enemy collection and spawn/despawn events;
- the selected enemy's controller and persistent child scripts;
- `EnemyHealth` death, reward, heal, and stun interactions;
- `HurtCollider` self-hit, cooldown, and source-attribution behavior;
- `EnemyParent` director and level-transition tracking;
- Photon ViewIDs and observed-component order on the chosen prefab;
- master-client change callbacks and room-object ownership transfer;
- late-join behavior for current room objects;
- NavMesh behavior around doors, elevators, pits, and moving enemies.

These are implementation inspections, not feasibility blockers. The researched stack already demonstrates the required spawn, AI, navigation, health, and networking primitives.
