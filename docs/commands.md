# In-game command reference

Open the independent console with `F2`. It remains available when R.E.P.O. chat is disabled because it does not read or send chat messages.

Use `Up` and `Down` to change the highlighted fuzzy match, `Tab` to replace only the active argument, `Enter` to execute, and `Escape` or `F2` to close. Autocomplete adds quotes when an entity or player name contains spaces.

## Spawn

```text
/spawn <target> [count=1] [location=player-location]
/spawn <target> <location>    # count defaults to 1
```

- `target` is a canonical autocomplete entry beginning with `item:`, `valuable:`, or `enemy:`.
- `count` is an optional whole number from `1` through `500`.
- `location` is either `player-location` or `random-non-collision-location`.
- `location` may follow `target` directly; when count is omitted it must be the final argument and count defaults to `1`.
- An omitted location is `player-location` and resolves to the player who submitted the command.
- Malformed counts and values outside `1..500` are rejected. A valid count reaches the host executor unchanged instead of being silently reduced to an older per-kind limit.
- Random placement reserves separated level points and rejects occupied physics volumes. For enemies, the collision test runs against the final point returned by `EnemyRoamFindPoint`, not merely the earlier seed point.
- Multi-object enemy setups are trimmed so the reported count is the actual number of `EnemyParent` objects.
- A completed spawn reports the requested object count. If spawning cannot finish, the result is an explicit error containing the completed/requested counts.

Examples:

```text
/spawn "item:Strength Upgrade"
/spawn "item:Strength Upgrade" random-non-collision-location
/spawn "valuable:Diamond Display" 10 random-non-collision-location
/spawn "enemy:Headman" 2 player-location
```

## Despawn

```text
/despawn <target> [count=all]
```

Despawn removes the newest matching objects that this mod created. It deliberately does not scan and delete normal map content. Use `item:all`, `valuable:all`, or `enemy:all` to remove every mod-spawned object of one kind.

A numeric count is a `1..500` maximum number of matching tracked objects to remove; it is validated strictly and is never silently clamped. Despawn may report fewer removals when fewer matching objects exist. Omitting count or using `all` removes every match.

Examples:

```text
/despawn "item:Strength Upgrade" 1
/despawn enemy:all all
/despawn "valuable:Diamond Display"
```

## Permissions

```text
/grant <player>
/revoke <player>
/permissions
```

`/grant` and `/revoke` must be entered in the host's local console. Player autocomplete uses `Nickname#ActorNumber`, which remains unambiguous when nicknames are duplicated. Grants last only for the current room and are cleared on host migration.

Autocomplete follows the local role. The host sees `/grant`, `/revoke`, and eligible player selectors; a non-host client does not see those host-management suggestions even if granted. All clients still receive command, target, count, and location completion.

An ungranted client can open, close, and autocomplete in the console, and can use `/help` or `/permissions`, but spawn/despawn requests are rejected by the host. Authorization is rechecked while a remote request is queued and while batched work runs, so leaving the room or changing host stops remaining work with an error. A completed `/revoke` rejects the client's next queued or newly submitted mutation.

## Help

```text
/help
```

Displays the compact grammar in the result panel.

## Fuzzy behavior

Every semantic position has its own candidate set:

| Position | Candidates |
|---|---|
| Command | `/spawn`, `/despawn`, `/grant`, `/revoke`, `/permissions`, `/help` |
| Spawn/despawn target | Live REPOLib item, valuable, and enemy catalogs |
| Spawn argument after target | `1..500` and both locations; choosing a location keeps count at `1` |
| Spawn location after a numeric count | `player-location`, `random-non-collision-location` |
| Despawn count | `1..500`, plus `all` |
| Grant/revoke player | Current Photon room players, host only |

For non-host clients, the command row omits `/grant` and `/revoke`, and the player row is unavailable. Ranking prefers exact, prefix, substring, subsequence, then bounded Damerau-Levenshtein typo matches. Execution never silently chooses a fuzzy target: accept a canonical suggestion first.
