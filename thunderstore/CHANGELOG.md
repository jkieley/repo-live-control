# Changelog

All notable changes to RepoCommandConsole are documented here.

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
