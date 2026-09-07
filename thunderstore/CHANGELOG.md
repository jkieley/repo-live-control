# Changelog

All notable changes to RepoCommandConsole are documented here.

## 2.2.0 - 2026-09-07

- Added a full scrollable spawn catalog: enter `/spawn` without a search query, browse with the mouse wheel, scrollbar, or arrow keys, and keep fuzzy search for finding a particular target.
- Removed the total autocomplete-result cap while retaining eight compact visible rows. Keyboard selection stays in view, and catalog refreshes preserve mouse scrolling.
- Added small previews of actual game items, valuables, and enemies beside target names. Previews reuse native icons or render asset meshes without instantiating gameplay objects, use a bounded cache, and fall back to text when unavailable.
- Expanded the supported catalog with ten verified network resources: four cosmetic cases, three enemy souls, and three surplus money bags. Disabled and removed prototype items remain excluded.
- Added familiar guide names such as Pistol, Defib, and Light Bridge, plus shorter valuable names. Alias searches insert canonical targets without duplicating browser rows.
- Protected the console's BepInEx manager through game transitions. Verified that the console survives menu-to-singleplayer level loading, opens with F2, and executes a spawn through Run.
- Fixed keyboard handling inside the focused console window; F2 and Escape closing were verified while the text field had focus.
- Fixed enemy `player-location` placement choosing distant roaming points. It now samples navigation near the player within 3 metres, extends to 5 metres if needed, and returns a clear error before spawning if no nearby walkable point is available.
- Recorded the corrected nearby placement with two Apex Predator spawns, normal enemy movement, and successful targeted cleanup in singleplayer.
- Verified upright actual-asset previews for 254 of 255 audited targets; Hidden has no visible model mesh and retains its category placeholder.
- Added complete-catalog, fuzzy-alias, scrolling, keyboard, and catalog regression tests, plus local enemy placement, compiled command-routing, thumbnail isolation, and installed-game API checks in the standard test command.

## 2.1.0 - 2026-09-07

- Added team recovery commands: revive, heal, summon, and truck return for one player or everyone, plus ordered command chains.
- Added full-word player commands for kill, revive, healing, maximum health, summon, truck return, knockback, damage, expression, speech, wings, tumble, flicker, animation speed, pupils, falling, and push reset.
- Added player-or-all fuzzy completion, including the host and inactive dead characters; duplicate names use explicit actor selectors. All suggestions are accessible with Up/Down, including larger lobbies.
- Added `/chain <player|all> kill revive heal summon truck` with up to eight ordered actions.
- Routed granted clients through the existing host executor, with authorization rechecks during batched work and sustained effects.
- Added a restricted host-to-owner relay for expression, animation, pupils, and falling, with owner acknowledgement, character-view binding, expiry, and sender validation. Those target clients need version 2.1.0 or newer.
- Used the current four-argument health RPC and retained vanilla RPC security checks.
- Added parser, completion, permission, owner-relay security, and installed-game API contract tests. AllPlayerCommands is not required.
- Introduced the selected Console Companion mascot icon and refreshed the package description and README with the new player features, genuine gameplay screenshots, and a getting-started tutorial.

## 2.0.1 - 2026-09-07

- Added real in-game screenshots of fuzzy autocomplete, a successful spawn command, and despawn cleanup to the package page.
- Documentation-only package update; the plugin DLL, dependencies, and icon are unchanged from 2.0.0.

## 2.0.0 - 2026-08-26

- Added a dedicated in-game command console that remains available when normal game chat is disabled.
- Added context-aware fuzzy autocomplete for commands, spawn targets, counts, locations, and player names.
- Added host-authoritative spawning and despawning for items, valuables, and enemies.
- Added host-managed grant and revoke permissions so approved non-host players can submit the same commands.
- Allowed spawn location to follow the target directly while count defaults to one.
- Made all accepted slash-command counts from 1 through 500 execute unchanged or return an explicit error instead of silently clamping per entity kind.
- Bound remote authorization and active jobs to their original room, host, session, and live grant; added one-pending-request client behavior and explicit timeout/session failures.
- Deferred Photon callback registration and network polling until an actual lobby/gameplay session, so menu and private-game region-selection scenes do not touch Photon through this mod.
- Made autocomplete role-aware so non-host clients are not offered host-only grant/revoke or player-management entries.
- Collision-checked the exact final enemy roam point used for random placement.
- Expanded pure command and network coverage for grammar, counts, translation, completion, protocol policy, request lifecycle, and session grants.
- Added safe local build, Thunderstore package, profile-install, legacy-plugin quarantine, and command-test scripts.
- Made the build, test, package, and local-install scripts compatible with Windows PowerShell 5.1, added disposable compatibility coverage for the quarantine path, and made the installer reject a running game with a clear restart instruction.
