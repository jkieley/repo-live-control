# REPO Command Console

**Spawn enemies and items, revive your team, and control the next run from one searchable F2 console.**

RepoCommandConsole is a free in-game command console mod for **R.E.P.O.** Spawn enemies, weapons, upgrades, items, and valuables from your live game catalog. Version **2.2.0** adds a fully scrollable catalog with compact item previews, guide-friendly search names, and cosmetic cases, souls, and money bags. Press `F2`, browse or fuzzy-search a target, and run it. Heal, revive, summon players, or return them to the truck. The host controls multiplayer access and can grant friends permission to use the commands.

[Install RepoCommandConsole on Thunderstore](https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/) · [Watch gameplay](https://youtu.be/j_Qd60MG6VA) · [Demo website](https://repo-command-console.jkieley543940.chatgpt.site) · [Step-by-step tutorial](https://github.com/jkieley/repo-live-control/blob/main/docs/promotion/getting-started.md) · [All commands](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md)

> **Current compatibility:** R.E.P.O. `v0.4.4.3` or compatible  
> **Required dependencies:** BepInExPack `5.4.2305` and REPOLib `4.2.0`

## Watch the gameplay demo

[![Watch REPO Command Console 2.2.0 gameplay: browse, spawn, pick up items, and clean up enemies](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/01-full-spawn-browser.jpg)](https://youtu.be/j_Qd60MG6VA)

[Watch the 2-minute gameplay demo on YouTube](https://youtu.be/j_Qd60MG6VA). See the scrollable catalog, fuzzy search, equipment and loot pickup, money bags, cosmetic cases, nearby enemies, and targeted cleanup. Genuine singleplayer footage runs at its original speed with game audio. Visit the [demo website](https://repo-command-console.jkieley543940.chatgpt.site) for the video, screenshots, and installation links.

## See the console in game

These genuine **RepoCommandConsole 2.2.0** screenshots were captured in a clean gameplay profile. Click any image to open it at full resolution.

### Browse the full spawn catalog

[![R.E.P.O. Command Console 2.2.0 showing 255 spawn targets with compact previews and a scrollbar](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/01-full-spawn-browser.jpg)](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/01-full-spawn-browser.jpg)

Type `/spawn` to browse without a search query. This demo profile contains 255 targets; the list follows your installed game and mods.

### Spawn and hold real equipment

[![A Valuable Tracker spawned by REPO Command Console and held with the game's native grab control](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/03-holding-spawned-tracker.jpg)](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/03-holding-spawned-tracker.jpg)

The spawned Valuable Tracker can be held with the game's normal grab control.

### Clean up your spawned objects

[![REPO Command Console confirming targeted cleanup of one spawned Valuable Tracker](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/04-targeted-cleanup.jpg)](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/04-targeted-cleanup.jpg)

`/despawn "item:Valuable Tracker" 1` removes the tracked item, and the console confirms one matching object was removed.

### Search familiar names with previews

[![R.E.P.O. console searching case and showing cosmetic case targets with real previews](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/06-cosmetic-case-search.jpg)](https://raw.githubusercontent.com/jkieley/repo-live-control/a211d245125cd7ed6a48ded3a28e1230dce8f0af/docs/promotion/screenshots/2.2.0/06-cosmetic-case-search.jpg)

Search `case` to find cosmetic-case targets through their supported aliases. Accept a result to insert its canonical name.

### Spawn an enemy nearby

[![An Apex Predator spawned near the player with REPO Command Console 2.2.0](https://raw.githubusercontent.com/jkieley/repo-live-control/43e8731ae189c554e0444efc8cc7c1e3f685c108/docs/promotion/screenshots/2.2.0/07-apex-predator-in-game.jpg)](https://raw.githubusercontent.com/jkieley/repo-live-control/43e8731ae189c554e0444efc8cc7c1e3f685c108/docs/promotion/screenshots/2.2.0/07-apex-predator-in-game.jpg)

An Apex Predator spawned using `player-location` appears in the level. Version 2.2.0 finds a nearby walkable point within five metres or reports a placement error.

[See all eight gameplay captures, including enemy cleanup, in the tutorial](https://github.com/jkieley/repo-live-control/blob/main/docs/promotion/getting-started.md#220-gameplay-gallery).

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

## How to spawn items and enemies in R.E.P.O.

1. Press `F2` to open the console.
2. Type `/spawn` to open the complete spawn catalog.
3. Scroll with the mouse wheel or scrollbar, or use `Up` and `Down` to move through every result.
4. Optionally type part of a target name, such as `head`, `strength`, `pistol`, or `cosmetic`, to filter the list.
5. Highlight the target you want and press `Tab`. Autocomplete adds the canonical target and quotes when a name contains spaces.
6. Optionally choose a count and location with the same autocomplete flow.
7. Press `Enter` to run the command.

Accepting the autocomplete suggestion matters: fuzzy matching helps you find a target, while execution accepts canonical names or explicitly supported aliases. It does not guess from a misspelled command.

## What's new in 2.2.0: browse the whole catalog

- **Scroll every result.** `/spawn` opens the full catalog. Mouse wheel, scrollbar, and arrow keys reach the entire list, with no total-results cap.
- **Recognize items at a glance.** Compact 24-pixel previews use the game's native icons or actual prefab models while retaining eight visible rows. Custom shader effects may not appear in previews.
- **Find familiar names.** Search aliases include Pistol, Defib, Light Bridge, and short valuable names; accepting a result inserts its canonical selector.
- **Spawn more of the installed game.** The catalog includes four cosmetic cases, three enemy souls, and three surplus money bags missing from the standard valuable presets. Removed prototypes remain excluded.

The [catalog coverage audit](https://github.com/jkieley/repo-live-control/blob/main/docs/catalog-coverage-audit.md) explains the additions and aliases. The available list follows your installed game and mods.

## Player commands: recover and control your team

- **Help the team recover.** Use `/revive`, `/heal`, `/summon`, and `/truck` for one selected character or `all`.
- **Run actions in order.** `/chain all revive heal truck` revives eligible dead characters, heals the group, and returns them to the truck. A failed step stops the chain.
- **Choose players by name.** Fuzzy autocomplete includes the host and dead characters; actor numbers distinguish duplicate names.
- **Stage a scene or experiment.** Additional commands cover health, damage, knockback, speech, expressions, wings, tumble, flashlights, animation speed, pupils, and falling.

```text
/revive all
/heal all
/summon all
/truck all
/chain all revive heal truck
```

Every player command requires a target. Revive needs an available death head, and heal applies to living characters. `/maxhealth` changes the current session's maximum health; it is not a permanent upgrade purchase. Characters receiving expression, animation-speed, pupil, or falling effects also need **2.1.0 or newer**. See [multiplayer requirements](#multiplayer-host-control-with-friend-access) and the [full player command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md#player-commands).

The unreleased source build also adds `/resetupgrades <player|all>` to set consumed vanilla upgrade levels to zero and restore their base values. It takes no options. The host or a friend with a current room grant can run it for one selected character or everyone:

```text
/resetupgrades "Bob Builder#2"
/resetupgrades all
```

The reset restores maximum health to 100 and caps current health at 100 without healing or reviving. The game records the cleared upgrades through its normal save lifecycle; the command does not force an immediate save. The host and anyone submitting `/resetupgrades` need the new build; target players receive the game's normal RPCs and do not need this mod.

`/resetpush` resets the physics pusher state; `/resetupgrades` resets consumed upgrades. `/despawn` removes unused upgrade objects created by this mod.

## Built for custom runs

- **Find targets without memorizing internal names.** Search the live item, valuable, and enemy catalogs with fuzzy autocomplete.
- **Keep the host in control.** Grant or revoke a friend's command access for the current room.
- **Clean up your own spawns.** Despawn removes only matching objects previously created by this mod.
- **Use a dedicated console.** `F2` works independently of the chat interface and does not replace the game's chat commands.

## Console controls: press F2 to open

| Key | Action |
|---|---|
| `F2` | Open or close the console |
| `Escape` | Close the console |
| `Up` / `Down` | Change the highlighted suggestion |
| Mouse wheel / scrollbar | Browse the entire result list |
| `Tab` | Accept the highlighted suggestion for the active argument |
| `Enter` | Run the command |

`F2` is the default toggle key and can be changed in the mod's BepInEx configuration.

## Commands at a glance

```text
/spawn <target> [count=1] [location=player-location]
/spawn <target> <location>
/despawn <target> [count=all]
/revive <player|all>
/heal <player|all> [full|amount]
/summon <player|all>
/truck <player|all>
/chain <player|all> revive heal truck
/grant <player>
/revoke <player>
/permissions
/help
```

Targets use one of three catalog prefixes: `item:`, `valuable:`, or `enemy:`. Weapons, upgrades, and other usable equipment are found through the `item:` catalog.

Player commands use `all` or a character selected with fuzzy autocomplete, such as `"Bob Builder#2"`. Every character is available, including the host and dead players. `/revive` requires an explicit target; `/revive all` revives the party, while `/revive "Bob Builder#2"` revives only Bob. Use Up/Down to reach suggestions beyond the eight visible rows.

The player command set also includes `/kill`, `/damage`, `/knockback`, `/maxhealth`, `/expression`, `/speak`, `/wings`, `/tumble`, `/flicker`, `/animationspeed`, `/pupils`, `/falling`, and `/resetpush`. `/summon` gathers the selected characters at the command sender's position, including when a granted friend submits it. `/chain all revive heal truck` performs those three actions in order. See the [player command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md#player-commands) for parameters and defaults.

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

All spawn, despawn, and player commands are host-authoritative. A friend's console sends a request to the lobby host; the host validates the request and performs the world change. Player effects that the game restricts to a character's owner use an authenticated host-to-owner request after the host approves them.

| Player | Needs the mod? | Can use the console? |
|---|---:|---|
| Lobby host | Yes | Yes, always authorized |
| Friend granted by the host | Yes | Yes, for the current room |
| Friend not granted by the host | Yes | Can open and autocomplete, but mutation requests are rejected |
| Unmodded friend | No | Can join the lobby, but cannot open or use this console |

### Grant a friend access

1. The host opens their local console with `F2`.
2. The host enters `/grant ` and uses autocomplete to select the friend. Player selectors include the Photon actor number, such as `"Player Name#2"`, so duplicate nicknames remain unambiguous.
3. The friend can now run spawn, despawn, and all player commands from their own console.
4. The host can enter `/revoke ` and select that player to remove access.
5. Anyone with the mod can use `/permissions` to check the current permission state.

Only the host's local console can run `/grant` and `/revoke`; those commands are rejected when sent over the network. Grants belong to the current room, are removed when a player leaves, and are cleared when the room or lobby host changes.

Use **2.2.0** for the current catalog, previews, and command interface. Characters targeted by `/expression`, `/animationspeed`, `/pupils`, or `/falling` need **2.1.0 or newer** for the owner relay; they do not need a grant just to receive an effect. Other player actions use the game's host-compatible RPCs and can affect unmodded characters. Unavailable targets and unsupported clients produce explicit errors, including partial results for `all`. The AllPlayerCommands mod is not a dependency.

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

### Why was my friend's command rejected?

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

- [Install and first-command tutorial](https://github.com/jkieley/repo-live-control/blob/main/docs/promotion/getting-started.md)
- [Complete command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md)
- [Changelog](https://github.com/jkieley/repo-live-control/blob/main/thunderstore/CHANGELOG.md)
- [Report a problem](https://github.com/jkieley/repo-live-control/issues)

### Find your next spawn

The [visual item-and-valuables guide on Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3773432391) offers ideas for shop goods, weapons, upgrades, drones, carts, and valuables. Its availability and names may change. Use the console's live autocomplete for the canonical target strings available in your current game; the guide is not an enemy catalog or a guarantee that a target exists.

Developer architecture, networking protocol, testing, packaging, and local-install guidance remain in the repository's [`docs`](https://github.com/jkieley/repo-live-control/tree/main/docs) directory so this page can stay focused on players.
