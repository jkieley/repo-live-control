---
name: repo-live-control
description: Control a running modded R.E.P.O. host through the local RepoLiveControl bridge. Use for spawning or despawning enemies, loot, or specific items; toggling automatic enemy spawning; unsticking loot; checking live status; or extending the bridge with new host-side runtime actions.
---

# R.E.P.O. Live Control

Operate the local bridge repository at `C:\Users\James Kieley\Documents\Codex\2026-08-18\search-the-web-are-there-any\repo-live-control`.

## Execute requests

Translate the user's request using [references/commands.md](references/commands.md), then run `scripts/invoke.ps1` with the action, selector, count, and placement. Do not run a preliminary status request for an ordinary explicit action.

Report the returned `OK` or `ERROR` result. The bridge returns actual spawned/despawned object counts and handles multi-object enemy setups such as Gnomes.

If the pipe is unavailable, run `scripts/ensure-bridge.ps1` once and retry the requested command once. Stop and report the failure if the game is not running, the modded profile cannot be found, build/attachment fails, or the retry fails.

## Operational boundaries

- Treat each requested game mutation as authorization only for that mutation.
- Use `safe` placement for loot and items unless the user explicitly asks for their location, a pile, or near-player placement.
- Use `at-player` only when explicitly requested; it intentionally permits collisions and piles.
- Despawning enemies does not change automatic spawning. Send `auto off` or `auto on` only when requested or when the user explicitly wants enemies to remain cleared.
- For vague terms such as "lots" or "max," use 50 objects unless the user provides another count. Exact counts take precedence.
- The bridge is host-only and uses REPOLib/Photon network operations. Do not substitute local Unity `Instantiate` or `Destroy` calls.

## Extend the bridge

When the user asks for a new capability, read [references/development.md](references/development.md). Update the repository bridge and protocol, build it, attach the new uniquely versioned assembly/pipe when replacing a loaded version, and validate with a non-mutating status command before using the new mutation.
