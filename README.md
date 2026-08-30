# RepoCommandConsole

**Spawn any enemy, weapon, item, or valuable exposed by R.E.P.O.'s live modding catalog at will—for you and your friends.**

RepoCommandConsole is a free, host-authoritative in-game command console built for players who want more control over every run. Create challenge rooms, hand out upgrades, test loadouts, summon enemies, stage screenshots, or turn the next lobby into carefully managed chaos. Press `F2`, find what you want with fuzzy autocomplete, and spawn it without touching the game's chat.

> **Current compatibility:** R.E.P.O. `v0.4.4.3` or compatible  
> **Required dependencies:** BepInExPack `5.4.2305` and REPOLib `4.2.0`

## Find your next spawn

**Visual item-and-valuables reference and idea catalog:**  
https://steamcommunity.com/sharedfiles/filedetails/?id=3773432391

This Steam guide covers shop goods, weapons, upgrades, drones, carts, and valuables found across locations. Steam may display a removed or incompatible warning, but the guide content is currently still readable.

Use it for visual discovery and inspiration. It is not a list of enemies or guaranteed canonical console target strings. **The autocomplete shown in RepoCommandConsole is always the authoritative source for names you can spawn in your current game.**

## Why you and your friends will love playing with this mod

- **Build a custom run in seconds.** Spawn enemies, useful items, upgrades, and valuables exactly when the lobby needs them.
- **Create memorable multiplayer moments.** Set up challenge encounters, rescue a struggling team, or give everyone the tools for a ridiculous experiment.
- **Find targets without memorizing internal names.** Context-aware fuzzy autocomplete searches the live REPOLib item, valuable, and enemy catalogs.
- **Keep the host in control.** Friends can use the console only after the host grants permission for the current room.
- **Clean up without erasing the level.** Despawn commands affect only matching objects previously created by this mod.
- **Leave vanilla chat alone.** The console is independent of chat and does not register, patch, or collide with the game's existing `/spawn` command.

## Install with Thunderstore or r2modman

1. Open **Thunderstore Mod Manager** or **r2modman** and select **R.E.P.O.**
2. Create or choose the profile you want to use.
3. Find **RepoCommandConsole** in the online mod list and select **Download**. If you are already on the Thunderstore package page, choose **Install with Mod Manager**.
4. Confirm that the profile includes these exact dependencies:
   - `BepInEx-BepInExPack-5.4.2305`
   - `Zehs-REPOLib-4.2.0`
5. Launch the profile with **Start Modded**.
6. Enter a lobby or run, then press `F2`.

Every friend who wants to open the console or submit commands must install the mod in their own profile. Players without RepoCommandConsole can still join the lobby normally, but they cannot use its console.

## Your first spawn

1. Press `F2` to open the console.
2. Begin typing `/spawn`.
3. Use `Up` and `Down` to highlight the command, then press `Tab` to accept it.
4. Type part of a target name, such as `head` or `strength`.
5. Highlight the target you want and press `Tab` again. Autocomplete adds quotes when a name contains spaces.
6. Optionally choose a count and location with the same autocomplete flow.
7. Press `Enter` to run the command.

Accepting the autocomplete suggestion matters: fuzzy matching helps you find a target, but execution requires a canonical target from the live catalog rather than guessing what you meant.

### Controls

| Key | Action |
|---|---|
| `F2` | Open or close the console |
| `Escape` | Close the console |
| `Up` / `Down` | Change the highlighted suggestion |
| `Tab` | Accept the highlighted suggestion for the active argument |
| `Enter` | Run the command |

`F2` is the default toggle key and can be changed in the mod's BepInEx configuration.

## Commands at a glance

```text
/spawn <target> [count=1] [location=player-location]
/spawn <target> <location>
/despawn <target> [count=all]
/grant <player>
/revoke <player>
/permissions
/help
```

Targets use one of three catalog prefixes: `item:`, `valuable:`, or `enemy:`. Weapons, upgrades, and other usable equipment are found through the `item:` catalog.

Spawn and numeric despawn counts must be whole numbers from `1` through `500`. Invalid or out-of-range counts return an error instead of being silently changed.

### Ready-to-try examples

```text
/spawn "item:Strength Upgrade"
/spawn "item:Strength Upgrade" 2 player-location
/spawn "valuable:Diamond Display" 5 random-non-collision-location
/spawn "enemy:Headman" 1 random-non-collision-location
/despawn "enemy:Headman" all
```

- `player-location` spawns at the player who submitted the command, not automatically at the host.
- `random-non-collision-location` searches for a collision-free level location.
- If the location directly follows the target, the count defaults to `1`.
- Omitting both optional spawn arguments creates one object at `player-location`.
- Omitting a despawn count, or using `all`, removes all matching mod-spawned objects.

For the full grammar and edge cases, see the [complete command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md).

## Multiplayer: host control with friend access

All spawn and despawn mutations are host-authoritative. A friend's console sends a request to the lobby host; the host validates the request and performs the world change.

| Player | Needs the mod? | Can use the console? |
|---|---:|---|
| Lobby host | Yes | Yes, always authorized |
| Friend granted by the host | Yes | Yes, for the current room |
| Friend not granted by the host | Yes | Can open and autocomplete, but mutation requests are rejected |
| Unmodded friend | No | Can join the lobby, but cannot open or use this console |

### Grant a friend access

1. The host opens their local console with `F2`.
2. The host enters `/grant ` and uses autocomplete to select the friend. Player selectors include the Photon actor number, such as `"Player Name#2"`, so duplicate nicknames remain unambiguous.
3. The friend can now run spawn and despawn commands from their own console.
4. The host can enter `/revoke ` and select that player to remove access.
5. Anyone with the mod can use `/permissions` to check the current permission state.

Only the host's local console can run `/grant` and `/revoke`; those commands are rejected when sent over the network. Grants belong to the current room, are removed when a player leaves, and are cleared when the room or lobby host changes.

## Despawning is intentionally scoped

RepoCommandConsole tracks what it creates. `/despawn` removes the newest matching tracked objects and **never scans the level to delete normal map content**.

```text
/despawn "item:Strength Upgrade" 1
/despawn "valuable:Diamond Display" all
/despawn enemy:all all
```

`item:all`, `valuable:all`, and `enemy:all` target every mod-spawned object of that kind. A numeric count is a maximum, so the command can report fewer removals when fewer matching tracked objects exist.

## FAQ and troubleshooting

### Why does `F2` not open the console?

Make sure you launched the correct profile with **Start Modded**, RepoCommandConsole is enabled on this computer, and the profile contains BepInExPack `5.4.2305` plus REPOLib `4.2.0`. Also check whether the console toggle key was changed from its default in the BepInEx configuration.

### Why was my friend's spawn or despawn command rejected?

The lobby host must run `/grant <player>` from the host's own console after that friend joins the room. The friend also needs RepoCommandConsole installed. Grants do not carry into another room and are cleared if the lobby host changes.

### Why does a target that looks correct still return “No canonical target matches”?

Open the console, type part of the name, highlight the desired live autocomplete result, and press `Tab` before running the command. Names in screenshots, external guides, or older game versions may not match the current REPOLib catalog exactly.

### Why did despawn remove fewer objects than requested?

Only matching objects created and tracked by this mod are eligible. Normal level content and objects created by other systems are left alone. A numeric count is the maximum to remove, not a guarantee that many matching tracked objects exist.

### Can friends play without installing the mod?

Yes. Unmodded players can join the lobby, but they cannot open RepoCommandConsole or submit its commands. Any friend who wants console access must install the mod and receive host permission.

### Does this replace or interfere with R.E.P.O. chat commands?

No. RepoCommandConsole uses its own `F2` interface and private parser. It does not depend on the chat UI and does not register or patch the vanilla `/spawn` command.

### Why did random placement fail?

The mod searches for a collision-free level position and returns an explicit error if it cannot complete the requested spawn safely. Try a smaller count, use `player-location`, or move to another part of the level and try again.

## More information

- [Complete command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md)
- [Changelog](https://github.com/jkieley/repo-live-control/blob/main/thunderstore/CHANGELOG.md)
- [Report a problem](https://github.com/jkieley/repo-live-control/issues)

Developer architecture, networking protocol, testing, packaging, and local-install guidance remain in the repository's [`docs`](https://github.com/jkieley/repo-live-control/tree/main/docs) directory so this page can stay focused on players.
