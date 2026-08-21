# Command mapping

Invoke `scripts/invoke.ps1` with these parameters.

| User intent | Action | Target | Count | Placement |
|---|---|---:|---:|---|
| Spawn named enemy | `enemy` | enemy substring | exact requested objects | `near-player` |
| Spawn high-level enemy | `enemy` | `high` or `randomhigh` | requested | `near-player` |
| Spawn random loot | `loot` | `random` | requested | `safe` |
| Spawn expensive loot | `loot` | `expensive` | requested | `safe` |
| Spawn named loot | `loot` | prefab-name substring | requested | `safe` |
| Spawn specific item | `item` | item-name substring | requested | `safe` |
| Spawn at the user | `loot` or `item` | requested selector | requested | `at-player` |
| Despawn all enemies | `despawn` | `all` | `0` (keep count) | unused |
| Despawn type except N | `despawn` | enemy substring | N (keep count) | unused |
| Enable/disable automatic enemies | `auto` | `on` or `off` | unused | unused |
| Fix wall-stuck loot | `unstick` | `loot` | unused | unused |
| Inspect state | `status` | unused | unused | unused |

Examples:

```powershell
scripts/invoke.ps1 enemy apex 5 near-player
scripts/invoke.ps1 enemy gnome 20 near-player
scripts/invoke.ps1 despawn gnome 1
scripts/invoke.ps1 loot expensive 20 safe
scripts/invoke.ps1 item strength 100 at-player
scripts/invoke.ps1 auto off
scripts/invoke.ps1 unstick
scripts/invoke.ps1 status
```

Selectors are case-insensitive substrings. `high` prefers Reaper; `randomhigh` samples registered Difficulty3 enemies. `expensive` rotates through the curated high-end valuable set.
