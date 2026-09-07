# Changelog

All notable changes to RepoCommandConsole are documented here.

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
