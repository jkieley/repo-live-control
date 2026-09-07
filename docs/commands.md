# In-game command reference

Open the independent console with `F2`. It remains available when R.E.P.O. chat is disabled. Commands do not pass through the chat parser; `/speak` explicitly sends speech through the game's speech API.

Use `Up` and `Down` to change the highlighted fuzzy match, `Tab` or a row click to replace only the active argument, `Enter` to execute, and `Escape` or `F2` to close. Autocomplete adds quotes when an entity or player name contains spaces. Selecting a suggestion does not execute the command.

## Browse the catalog

In **2.2.0**, typing `/spawn` opens the full supported target list without requiring a search query or trailing space. Scroll over the list with the mouse wheel, drag its scrollbar, or use `Up`/`Down`. The window keeps eight compact rows visible, with no total-result cap; keyboard selection stays in view. The counter above the list shows the selected position and total matches. The same scrolling controls work for other completion lists, including player selectors and counts.

Target rows have small previews drawn from actual game icons or asset meshes when available. Previews load as entries come into view. Missing previews leave the target available by name; browsing does not spawn gameplay objects.

Type after the command to filter, for example `/spawn strength`. Use `/spawn item:`, `/spawn valuable:`, or `/spawn enemy:` to narrow the list by kind. Fuzzy search also matches verified aliases, then inserts the canonical target:

| Search name | Canonical target inserted |
|---|---|
| `Pistol` | `item:Gun` |
| `Defib` or `Defibrillator` | `item:Defibro` |
| `Light Bridge` | `item:Phase Bridge` |
| `Diamond Display` | `valuable:Valuable Manor Diamond Display` |

Aliases do not create duplicate rows. The catalog includes registered REPOLib targets and supported networked game resources, including cosmetic cases, enemy souls, and surplus money bags. Disabled, nonphysical, and removed prototype item fallbacks are excluded. Installed game content and other mods determine which targets are available.

## Spawn

```text
/spawn <target> [count=1] [location=player-location]
/spawn <target> <location>    # count defaults to 1
```

- `target` is a canonical autocomplete entry beginning with `item:`, `valuable:`, or `enemy:`. A recognized exact alias is also accepted when it identifies one target; accepting autocomplete is the easiest way to supply the canonical name.
- `count` is an optional whole number from `1` through `500`.
- `location` is either `player-location` or `random-non-collision-location`.
- `location` may follow `target` directly; when count is omitted it must be the final argument and count defaults to `1`.
- An omitted location is `player-location` and resolves to the player who submitted the command.
- For enemies, `player-location` selects the nearest NavMesh point within 3 metres, extending the search to at most 5 metres if necessary. If no nearby walkable floor exists, the command asks you to move or use `random-non-collision-location`; it does not redirect the enemy to a distant roaming point. This local search does not guarantee collision-free placement.
- Malformed counts and values outside `1..500` are rejected. A valid count reaches the host executor unchanged instead of being silently reduced to an older per-kind limit.
- Random placement reserves separated level points and rejects occupied physics volumes. For enemies, the collision test runs against the final point returned by `EnemyRoamFindPoint`, not merely the earlier seed point.
- Multi-object enemy setups are trimmed so the reported count is the actual number of `EnemyParent` objects.
- A completed spawn reports the requested object count. If spawning cannot finish, the result is an explicit error containing the completed/requested counts.

Examples:

```text
/spawn "item:Strength Upgrade"
/spawn "item:Strength Upgrade" random-non-collision-location
/spawn "valuable:Valuable Manor Diamond Display" 10 random-non-collision-location
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
/despawn "valuable:Valuable Manor Diamond Display"
```

## Player commands

Every command below requires `<player|all>` as its first argument. Type part of a name and press `Tab` to accept `"Nickname#ActorNumber"`, or choose `all`. The list includes the host, yourself, other characters, and dead characters. The wheel, scrollbar, and Up/Down reach all available suggestions. Names are resolved exactly at execution; ambiguous duplicate names and stale selectors fail rather than affecting a different character. Singleplayer offers `"Local Player#local"` and `all`.

| Command | Behavior / defaults |
|---|---|
| `/kill <player|all>` | Force death; already dead characters are skipped. |
| `/revive <player|all>` | Revive at the death head; living characters are skipped. A death head must exist. |
| `/heal <player|all> [full|amount]` | Full healing by default, or add a positive integer amount. Dead characters must be revived first. |
| `/maxhealth <player|all> [maximum=200]` | Change maximum health, reducing current health if necessary. Does not revive or fill the new maximum. |
| `/summon <player|all>` | Move characters/death heads to the command sender's position captured when the host starts the command. |
| `/truck <player|all>` | Move characters/death heads to the truck safety spawn. |
| `/knockback <player|all> [strength=5]` | Apply an impulse away from the sender, with an upward component. |
| `/damage <player|all> [amount=10]` | Apply damage; vanilla invincibility and game-state checks remain active. |
| `/expression <player|all> [index=4]` | Set facial expression; index is checked against the character's expression list. |
| `/speak <player|all> [message=Hello!!!]` | Speak text, quoted or unquoted; slashes in text cannot execute commands. |
| `/wings <player|all> [on|off|pink]` | Maintain regular or pink wings visuals until off; default on. |
| `/tumble <player|all> [on|off|seconds=3]` | Force a timed tumble, keep tumbling with on, or release with off. |
| `/flicker <player|all> [multiplier=2]` | Flicker flashlights. |
| `/animationspeed <player|all> [speed=0.5] [in=0.05] [out=0.2] [seconds=3]` | Temporarily override animation speed; `off` clears the override. Transition parameters are passed to the game's animation API. |
| `/pupils <player|all> [size=1.8] [priority=10] [springIn=25] [dampIn=0.8] [springOut=12] [dampOut=0.8] [seconds=3]` | Temporarily override pupils; `off` clears the override. |
| `/falling <player|all> [on|off]` | Maintain or clear the falling flag; default on. |
| `/resetpush <player|all>` | Reset the physics pusher state. |
| `/chain <player|all> <actions...>` | Run 1–8 full-word actions, in order: `kill`, `revive`, `heal`, `summon`, `truck`. Each step finishes for the selected group before the next starts. |

Examples:

```text
/revive all
/heal "Bob Builder#2" 50
/maxhealth all 200
/wings "Bob Builder#2" pink
/animationspeed all 2 0.05 0.2 10
/speak all Ready for extraction!
/chain all revive heal truck
```

HP, healing, and damage values accept integers `1..1000000`. Expression indices accept `0..1000` and must exist at runtime. Knockback accepts `0..10000`; flicker and animation/pupil multipliers accept `0..100`. Timed effects accept `0.1..3600` seconds. Animation transition values accept `0.001..100`. Pupil priority accepts integers `0..1000`, springs `0.001..1000`, and damping `0.001..100`. Non-finite numbers and surplus arguments are rejected.

Granted clients can execute every player command through the host. The host and anyone submitting these commands need this version. Target characters also need **RepoCommandConsole 2.1.0+** for expression, animation speed, pupils, and falling, because those effects run on the character owner after host authorization. No extra grant is required just to be a target. Other actions use vanilla host-compatible RPCs. AllPlayerCommands itself is not required.

Targets are captured when execution begins; later joiners are not added to an in-flight `all` command. Work is batched and reports applied, skipped, failed, and planned action counts. Health/death changes are observed before completion; owner effects require acknowledgement. RPC-driven visual/physics actions report dispatch, not proof that every peer rendered the effect. A chain stops after a failed step. Sustained wings/tumble/falling stop when revoked, the target dies/leaves, or the room/host changes; falling uses a short owner lease and may take up to two seconds to expire. Completed one-shot changes are not undone by revoke. Maximum HP is a runtime change, not a permanent upgrade purchase.

## Permissions

```text
/grant <player>
/revoke <player>
/permissions
```

`/grant` and `/revoke` must be entered in the host's local console. Player autocomplete uses `Nickname#ActorNumber`, which remains unambiguous when nicknames are duplicated. Grants last only for the current room and are cleared on host migration.

Autocomplete follows the local role. The host sees `/grant`, `/revoke`, and eligible player selectors; a non-host client does not see those host-management suggestions even if granted. All clients still receive command, target, count, and location completion.

An ungranted client can open, close, and autocomplete in the console, and can use `/help` or `/permissions`, but spawn/despawn and player-action requests are rejected by the host. Authorization is rechecked while a remote request is queued and while batched work runs, so leaving the room or changing host stops remaining work with an error. A completed `/revoke` rejects the client's next queued or newly submitted mutation.

## Help

```text
/help
```

Displays the compact grammar in the result panel.

## Fuzzy behavior

Every semantic position has its own candidate set:

| Position | Candidates |
|---|---|
| Command | Spawn/despawn, all full-word player commands above, grant/revoke, permissions, help |
| Spawn/despawn target | Registered item, valuable, and enemy catalogs plus supported resource fallbacks; canonical names and verified aliases are searchable |
| Spawn argument after target | `1..500` and both locations; choosing a location keeps count at `1` |
| Spawn location after a numeric count | `player-location`, `random-non-collision-location` |
| Despawn count | `1..500`, plus `all` |
| Grant/revoke player | Current Photon room players, host only |
| Player-action target | `all` and every current character, including host/dead characters; available on clients too |
| Player-action options | Appropriate modes, common numbers, or supported chain actions; speech is free text |

For non-host clients, the command row omits `/grant` and `/revoke`, and grant/revoke player suggestions are unavailable. Player-action targeting remains available. Ranking prefers exact, prefix, substring, subsequence, then bounded Damerau-Levenshtein typo matches. Alias matches return the existing canonical row. Execution accepts exact canonical names or unambiguous verified aliases; it never silently chooses a fuzzy target. Accept a suggestion before executing a partial name or typo.
