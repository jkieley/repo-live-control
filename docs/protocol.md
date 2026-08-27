# Command protocols

## In-game slash grammar

The independent console sends the raw slash command to the host, which parses it again before execution.

| Command | Meaning |
|---|---|
| `/spawn <target> [count=1] [location=player-location]` | Spawn a canonical `item:`, `valuable:`, or `enemy:` target. |
| `/despawn <target> [count=all]` | Remove matching objects previously spawned through this mod. |
| `/grant <player>` | Locally grant a non-host actor for this room; host only. |
| `/revoke <player>` | Locally revoke a room grant; host only. |
| `/permissions` | Report the current room grant list. |
| `/help` | Report compact command help. |

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

Pipe placements are `safe`, `near-player`, and `at-player`. The pipe thread never touches Unity objects; it enqueues and waits while the `RunManager.Update` Harmony patch performs game work.

## Runtime invariants

- Only the host creates/destroys network objects.
- Enemy results count actual `EnemyParent` objects, including grouped setups.
- High-volume operations are frame-batched.
- Random collision-free placement reserves separated points and checks occupied volumes.
- Request completion occurs only after the observed job finishes or fails.
