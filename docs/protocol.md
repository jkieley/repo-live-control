# Bridge protocol

The local named pipe is `CodexRepoLiveControlV8`. Commands and responses are UTF-8, newline-delimited text. A response begins with `OK` or `ERROR`.

## Commands

| Command | Meaning |
|---|---|
| `enemy|<selector>|<count>|near-player` | Spawn an exact number of enemy objects. `high` prefers Reaper; `randomhigh` selects random Difficulty3 setups. |
| `loot|<selector>|<count>|<placement>` | Spawn valuables. Selectors may be a prefab-name substring, `random`, or `expensive`. |
| `item|<selector>|<count>|<placement>` | Spawn shop/items by `Item.itemName` substring. Use `weapon` for a random runtime weapon. |
| `itemeach|<selector>|<count-per-type>|<placement>` | Spawn the requested count of every distinct item type whose name contains the selector. |
| `despawn|<selector>|<keep>` | Destroy matching enemies while preserving the first `keep`. Use `all` for every enemy. |
| `despawnitem|<selector>` | Destroy matching items previously spawned by this bridge. Use `weapon` for bridge-spawned runtime-classified weapons. |
| `auto|on` / `auto|off` | Enable or disable the active level's automatic enemy director. |
| `unstick|loot` | Detect loot penetrating environment colliders and teleport it to clear positions. |
| `duplicate|loot` | Duplicate every tracked valuable once into distinct random, collision-free locations. |
| `inspect|loot` | Report unique tracked loot names and the registered valuable prefab catalog. |
| `status` | Return enemy count, loot count, and automatic-spawn state. |

Placements are `safe`, `near-player`, and `at-player`. Safe placement reserves separated collision-free locations. At-player placement intentionally allows piles.

## Extension points

Add a new action in `Bridge.Dispatch`, then keep Unity/Photon access on `RunManager.Update`. Long or high-volume operations should use a frame-batched job like `SpawnJob`. Network-created objects should use REPOLib or Photon host APIs; local `Instantiate`/`Destroy` calls will desynchronize clients.

The pipe thread must only enqueue requests and wait for responses. It must not access Unity objects.
