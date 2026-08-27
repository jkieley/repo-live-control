# In-game command reference

Open the independent console with `F2`. It remains available when R.E.P.O. chat is disabled because it does not read or send chat messages.

Use `Up` and `Down` to change the highlighted fuzzy match, `Tab` to replace only the active argument, `Enter` to execute, and `Escape` or `F2` to close. Autocomplete adds quotes when an entity or player name contains spaces.

## Spawn

```text
/spawn <target> [count=1] [location=player-location]
```

- `target` is a canonical autocomplete entry beginning with `item:`, `valuable:`, or `enemy:`.
- `count` is an optional whole number from `1` through `500`.
- `location` is either `player-location` or `random-non-collision-location`.
- An omitted location is `player-location` and resolves to the player who submitted the command.
- The random placement reserves separated level points and rejects occupied physics volumes.
- Multi-object enemy setups are trimmed so the reported count is the actual number of `EnemyParent` objects.

Examples:

```text
/spawn "item:Strength Upgrade"
/spawn "valuable:Diamond Display" 10 random-non-collision-location
/spawn "enemy:Headman" 2 player-location
```

## Despawn

```text
/despawn <target> [count=all]
```

Despawn removes the newest matching objects that this mod created. It deliberately does not scan and delete normal map content. Use `item:all`, `valuable:all`, or `enemy:all` to remove every mod-spawned object of one kind.

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

An ungranted client can open, close, and autocomplete in the console, and can use `/help` or `/permissions`, but spawn/despawn requests are rejected by the host.

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
| Count | `1..500`, plus `all` for despawn |
| Spawn location | `player-location`, `random-non-collision-location` |
| Grant/revoke player | Current Photon room players |

Ranking prefers exact, prefix, substring, subsequence, then bounded Damerau-Levenshtein typo matches. Execution never silently chooses a fuzzy target: accept a canonical suggestion first.
