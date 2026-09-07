# REPO Command Console: spawn items, revive players, and run your first commands

This tutorial covers **RepoCommandConsole 2.2.0** for R.E.P.O. Browse equipment and loot with compact previews, spawn your first item, then try the player commands. The mod uses a separate `F2` console with host-controlled multiplayer permissions.

[Install RepoCommandConsole](https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/) · [Complete command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md)

## 1. Install with Thunderstore or r2modman

1. Open Thunderstore Mod Manager or r2modman and select **R.E.P.O.**
2. Create or choose a profile. Search for **RepoCommandConsole by Coollectors** and download it.
3. Confirm that **BepInExPack 5.4.2305** and **REPOLib 4.2.0** are installed as dependencies.
4. Choose **Start Modded**, then enter a lobby or run.
5. Press **F2** to open the console.

This version targets R.E.P.O. **v0.4.4.3**. For multiplayer, the host and everyone who wants to submit commands need the mod. Use **2.2.0** for the full scrollable catalog, previews, and aliases described here. AllPlayerCommands is not required.

## 2. Find an item without memorizing its internal name

Type `/spawn`. The full supported item, valuable, and enemy catalog opens immediately; a search query is optional. Move the pointer over the list and use the **mouse wheel**, drag the **scrollbar**, or press **Up/Down** to browse. Eight compact rows remain visible while the rest scroll into view. The number above the list shows the highlighted entry and total matches.

Small previews help identify targets as you browse. An entry without a preview still has its name and can be selected. Click a row or highlight it and press **Tab** to insert its canonical name; this selects the target without executing the command.

To find a particular item, type `/spawn strength`, select **item:Strength Upgrade**, and press **Tab**. Autocomplete supplies the catalog prefix and quotes. Press **Enter** to execute.

The accepted command typically looks like:

```text
/spawn "item:Strength Upgrade"
```

The default count is one, and the default location is the player who submitted the command. Read the result before submitting another command. Press **Escape** or **F2** to close the console and inspect the item.

[See an earlier in-game autocomplete screenshot](https://raw.githubusercontent.com/jkieley/repo-live-control/166dbec2d6298abb351ef05fc1f8dfb250fb2e12/docs/promotion/screenshots/repo-command-console-autocomplete.jpg). It was captured with version 2.0.0 and does not show the new scrolling or previews; other installed mods provide some surrounding HUD.

Fuzzy search also recognizes verified alternative names. For example, `/spawn pistol` finds **item:Gun**, `defib` finds **item:Defibro**, and `light bridge` finds **item:Phase Bridge**. Accepting a suggestion inserts the canonical name without adding a duplicate browser row. If an example is absent, select an available entry from your current game instead.

## 3. Spawn equipment, valuables, or an enemy

Targets come from three catalogs: `item:` for equipment, weapons and upgrades; `valuable:` for loot; and `enemy:` for enemies. Include a prefix such as `/spawn item:` in your search to find that category. Supported resource entries also include cosmetic cases, enemy souls, and surplus money bags when available in the installed game.

```text
/spawn "item:Strength Upgrade" 2 player-location
/spawn "valuable:Valuable Manor Diamond Display" 3 random-non-collision-location
/spawn "enemy:Headman" 1 random-non-collision-location
```

Use autocomplete to confirm the target before running each example. `random-non-collision-location` searches the level for a clear position, so the result may be outside your view. Counts must be whole numbers from **1 through 500**. If placement fails, the console reports the problem; use a smaller count or try another location.

## 4. Clean up what you spawned

```text
/despawn "item:Strength Upgrade" all
/despawn "valuable:Valuable Manor Diamond Display" all
/despawn "enemy:Headman" all
```

Despawn removes matching objects created by RepoCommandConsole. Normal level content is outside its scope. To remove every enemy created by this mod, use `/despawn enemy:all all`. A numeric count is a maximum, so fewer removals can be a valid result.

## 5. Revive, heal, gather, or return the party

Player commands always need a target: choose **all**, or type part of a player's name and accept the autocomplete entry. Entries include an actor number, such as `"Bob Builder#2"`, to distinguish duplicate names. The host and dead characters are included. The wheel, scrollbar, and Up/Down also reach player suggestions beyond the eight visible rows.

```text
/revive all
/heal all
/summon all
/truck all
```

`/revive` revives eligible dead characters at their death heads; it requires an available death head and skips living characters. `/heal` fully heals living targets by default. To add a specific amount, accept a player's live selector and append a number:

```text
/heal "Bob Builder#2" 50
```

`/summon` moves the selected characters or death heads to the command sender's position. `/truck` uses the truck safety spawn. To perform recovery in sequence:

```text
/chain all revive heal truck
```

Each step finishes for the selected group before the next begins. A failed step stops the chain, so read the result before assuming later actions ran. Chains accept one to eight actions from `kill`, `revive`, `heal`, `summon`, and `truck`.

## 6. Let a friend use commands

1. The host opens the console, types `/grant `, and accepts the friend's live name suggestion with **Tab**.
2. The host presses **Enter**. The granted friend can now submit spawn, despawn, and player commands from their own console.
3. Run `/permissions` to inspect access.
4. The host can use `/revoke ` with the same selection flow to remove the friend's command access.

Grants belong to the current room and clear when the player leaves or the room/host changes. Only the host's local console can grant or revoke access. A grant allows the full command set, including actions affecting other players.

Unmodded friends can join but cannot open this console. Targets of `/expression`, `/animationspeed`, `/pupils`, or `/falling` also need **2.1.0 or newer**, even when they do not submit commands. They do not need a grant merely to receive an effect.

## 7. Explore the remaining player commands

The player command set also includes `/kill`, `/damage`, `/maxhealth`, `/knockback`, `/expression`, `/speak`, `/wings`, `/tumble`, `/flicker`, `/animationspeed`, `/pupils`, `/falling`, and `/resetpush`.

For a visual example, select a player and try `/wings <player> pink`, then `/wings <player> off`. `/maxhealth` changes maximum health for the current runtime; it does not purchase a permanent upgrade, revive a character, or automatically fill the new maximum. Use the [full reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md#player-commands) for each command's defaults and limits.

## Troubleshooting

- **F2 does nothing:** confirm you used Start Modded, enabled the mod in the launched profile, installed both dependencies, and have not changed its toggle key in BepInEx configuration.
- **No target matches:** clear the target text back to `/spawn`, then browse or search again and accept a live entry. Verified aliases help with familiar names, but arbitrary external names may differ from the installed catalog.
- **A preview is blank:** select by its name; missing previews do not remove an otherwise supported target. A newly visible preview can take a moment to appear.
- **Permission denied:** the host must grant access after you join the current room.
- **An effect is unsupported:** confirm the host, command sender, and the target of an owner-applied effect are using 2.1.0 or newer.
- **A command partially completes:** read the applied/skipped/failed result. Unavailable characters or failed placements can leave only part of a group affected.

[Report a problem](https://github.com/jkieley/repo-live-control/issues) with the command, mod/game versions, multiplayer role, and relevant BepInEx log excerpt. [Download and screenshots](https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/) are on Thunderstore.
