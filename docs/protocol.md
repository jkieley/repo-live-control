# Command protocols

## In-game slash grammar

The independent console sends the raw slash command to the host, which parses it again before execution.

| Command | Meaning |
|---|---|
| `/spawn <target> [count=1] [location=player-location]` | Spawn a canonical `item:`, `valuable:`, or `enemy:` target; location may directly follow target while count defaults to `1`. |
| `/despawn <target> [count=all]` | Remove matching objects previously spawned through this mod. |
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
