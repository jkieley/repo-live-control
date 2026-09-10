# Command protocols

## In-game slash grammar

The independent console sends the raw slash command to the host, which parses it again before execution.

| Command | Meaning |
|---|---|
| `/spawn <target> [count=1] [location=player-location]` | Spawn a canonical `item:`, `valuable:`, or `enemy:` target; location may directly follow target while count defaults to `1`. |
| `/despawn <target> [count=all]` | Remove matching objects previously spawned through this mod. |
| `/<player-action> <player|all> [options...]` | Run one of the full-word player actions in `docs/commands.md` through the host. |
| `/resetupgrades <player|all>` | Unreleased: reset consumed vanilla upgrade levels to zero and restore their base values. No options. |
| `/chain <player|all> <actions...>` | Run 1–8 ordered kill/revive/heal/summon/truck actions. |
| `/grant <player>` | Locally grant a non-host actor for this room; host only. |
| `/revoke <player>` | Locally revoke a room grant; host only. |
| `/permissions` | Report the current room grant list. |
| `/help` | Report compact command help. |

Numeric spawn and despawn counts are whole numbers in `1..500`. The parser rejects malformed or out-of-range values, and the slash-command translation preserves every accepted count rather than relying on executor clamping.

## Photon event envelope

The default custom event code is `198`, configurable under `Networking.PhotonEventCode`. All clients in one room must use the same value.

```text
object[] {
  "com.jameskieley.repo.commandconsole",
  2,
  "request" | "response" | "notice",
  requestId,
  commandOrResult
}
```

- Requests are reliable events sent to `ReceiverGroup.MasterClient`.
- Responses and notices are reliable events targeted to one actor.
- The host trusts `EventData.Sender`, not payload identity data.
- Clients accept responses/notices only from the current Master Client.
- Commands are limited to 512 characters, responses to 2048 characters, and remote actors to five requests per rolling three seconds.
- Remote `/grant` and `/revoke` are rejected before enqueue and checked again during dispatch.
- Request IDs are 32 hexadecimal characters. Once the host accepts an ID, it cannot be reused within that observed room/host session.
- A client keeps at most one outgoing request pending. A second submission is rejected locally until the first completes or fails.
- Client pending state fails explicitly after 30 seconds, when the room closes or changes, or when the Master Client changes; a late or unsolicited response is ignored.
- The host accepts at most two queued/in-progress commands per remote actor and 32 globally.
- Accepted remote work is bound to the session revision that authorized it. The host rechecks the room, Master Client, and required grant before dispatch and during frame-batched work; invalidated work returns a partial-progress error.

## Local named pipe

The local named pipe is `CodexRepoCommandConsoleV2`. Commands and responses are UTF-8, newline-delimited text. Every response begins with `OK` or `ERROR`.

| Command | Meaning |
|---|---|
| `enemy|<selector>|<count>|<placement>` | Spawn an exact number of enemy objects. |
| `loot|<selector>|<count>|<placement>` | Spawn valuables by substring, `random`, `medium`, or `expensive`. |
| `item|<selector>|<count>|<placement>` | Spawn shop/items by item-name substring; `weapon` selects a runtime weapon. |
| `cart|<selector>|<count>|<placement>` | Spawn the medium or small networked cart. |
| `itemeach|<selector>|<count-per-type>|<placement>` | Spawn a count of every matching item type. |
| `itemspread|<selector>|<count>|<placement>` | Distribute an exact total across matching item types. |
| `despawn|<selector>|<keep>` | Destroy matching live enemies while preserving the first `keep`. |
| `despawnitem|<selector>` | Destroy matching items previously spawned through this bridge. |
| `auto|on` / `auto|off` | Toggle the active enemy director. |
| `unstick|loot` | Move loot penetrating environment colliders to clear points. |
| `duplicate|loot` | Duplicate tracked loot once into distinct clear points. |
| `topup3|loot` | Add one copy per original after one full duplication. |
| `inspect|loot` | Report tracked and registered valuable names. |
| `status` | Report enemy count, loot count, and automatic-spawn state. |

Pipe placements are `safe`, `near-player`, and `at-player`. The pipe thread never touches Unity objects; it enqueues and waits while the `RunManager.Update` Harmony patch performs game work. A 30-second pipe timeout cancels the queued or active request, so a caller cannot receive a timeout and then have that command execute later or in a different room.

## Runtime invariants

- Only the host creates/destroys network objects.
- Enemy results count actual `EnemyParent` objects, including grouped setups.
- High-volume operations are frame-batched.
- Every accepted slash-command count in `1..500` is translated unchanged; a spawn either completes that count or reports an explicit partial-progress error.
- Random collision-free placement reserves separated points and checks occupied volumes. Enemy placement checks the exact final `EnemyRoamFindPoint` result before spawn.
- Remote authorization cannot outlive its original room, Master Client, session revision, or grant.
- Request completion occurs only after the observed job finishes or fails.

## Player actions (2.1.0)

The existing request envelope and room grants authorize all player actions. The host parses every command, resolves an exact player selector or the explicit `all` token, and captures avatar references including inactive death avatars. Each player job processes up to four targets per frame and rechecks its bound room/host/grant before subsequent work. Player jobs advance alongside ordinary console dispatch, so `/revoke` can interrupt an active player batch. A chain waits for each group-wide step before starting the next. Changes with an observable health/death state are checked for up to three seconds; unobservable visual/physics RPC dispatch is reported as applied without claiming every peer rendered it. Jobs stop after 25 seconds with partial counts.

The unreleased `/resetupgrades <player|all>` action uses the existing player-action request path and authorization checks. It requires an explicit player selector or `all`, rejects extra arguments, and resets consumed vanilla upgrade levels to zero for the captured targets. It does not remove upgrade item objects or reset mod-defined upgrades. For example, `/resetupgrades "Bob Builder#2"` selects one character; `/resetupgrades all` selects every current character. `/resetpush` remains a separate physics-pusher action.

Upgrade reset waits for level generation and a short initialization delay, then preflights the selected player's identity, components, and all 13 registered upgrade dictionaries. Twelve non-health types use `TesterUpgradeCommandRPC` with `int.MinValue`: vanilla clamps each peer's count to zero and subtracts its effective live bonus. Health uses `UpdateStat` to clear its level and `UpdateHealthRPC` to set maximum HP to 100 and cap current HP, avoiding the damage caused by the negative health-upgrade path. A missing stripped dictionary entry counts as zero. Completion observes the host's cleared counts and maximum HP; remote RPCs are dispatched without per-peer acknowledgements.

Cleared counts participate in the game's normal save lifecycle; there is no forced mid-level save. Unknown third-party upgrade types are preserved. The host and command sender need the new build; targets use vanilla RPC handlers and do not require RepoCommandConsole or an owner-effect relay capability.

Expression, animation speed, pupils, and falling use the character owner's game API after host approval. This preserves vanilla owner-only security checks. Both ends advertise local Photon player property `rcc.player-effects=1` when their session becomes active. This is a capability signal, never a grant or authorization source.

The same version-2 envelope accepts two additional kinds:

```text
"player-effect"        payload = "<avatar-view-id>|<Photon-server-timestamp>|<canonical-player-command>"
"player-effect-result" payload = "OK ..." or "ERROR ..."
```

Only the current Master Client may send an effect. The owner accepts only the four supported verbs, an exact selector matching its own character, a matching Photon view ID, and a server timestamp no more than four seconds old. `all`, chains, grants, arbitrary commands, duplicate IDs, wrong targets, and stale work cannot pass the owner policy. Receipt queues work for `RunManager.Update`; it does not execute game actions in the event callback. Replies are bound to the request ID and expected actor and expire after five seconds. The host rechecks its original authorization before accepting a reply. Neither endpoint invokes a generic client command executor.

Sustained effects retain their original authorization and target reference. Wings and tumble renew on the host every half-second; falling renews a two-second lease on the owner. Death, departure, revoke, scene unload, or host/room changes stop renewal. Cleanup never issues RPCs under the old host's authority into a changed room. A completed one-shot action is not rolled back by revocation.
