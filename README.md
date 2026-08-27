# REPO Command Console

An independent in-game slash-command console for R.E.P.O. It works without the game's chat UI, provides contextual fuzzy autocomplete, and keeps every world mutation host-authoritative.

## Features

- Press `F2` to open or close the console; `Escape` also closes it.
- Fuzzy autocomplete for command, entity, count, location, and player arguments.
- `Up`/`Down` selects a suggestion, `Tab` accepts it, and `Enter` runs the command.
- Spawn and despawn items, valuables, and enemies.
- Spawn at the requesting player's location by default or at a random collision-free level location.
- Non-host players can use the same console after installing the mod and receiving an in-room grant from the host.
- Client requests use reliable Photon events, but only the host calls REPOLib spawn APIs or `PhotonNetwork.Destroy`.
- The existing named-pipe bridge remains available for local host automation.

The mod does not register or patch the game's existing `/spawn` command. Its private parser is attached only to this console, so it does not depend on `ChatManager` and does not collide with the vanilla debug command.

## Requirements

- R.E.P.O. `v0.4.4.3` or compatible
- BepInExPack `5.4.2305`
- REPOLib `4.2.0`

For multiplayer delegation, install the mod on the host and on every player who will use the console. Players without the mod can still join normally, but they cannot open this console or submit its commands.

## Commands

```text
/spawn <target> [count=1] [location=player-location]
/despawn <target> [count=all]
/grant <player>       # host-local only
/revoke <player>      # host-local only
/permissions
/help
```

Targets are qualified as `item:`, `valuable:`, or `enemy:`. Autocomplete inserts quotes around names containing spaces.

```text
/spawn "item:Strength Upgrade" 2 player-location
/spawn "valuable:Diamond Display" 5 random-non-collision-location
/spawn "enemy:Headman" 1 random-non-collision-location
/despawn "enemy:Headman" all
/grant "Player Name#2"
```

`/despawn` intentionally affects matching objects created by this mod, not normal level content. Counts are limited to `1..500`. See [docs/commands.md](docs/commands.md) for the complete behavior.

## Permission and networking model

- The lobby host is always authorized.
- A non-host request is rejected until the host runs `/grant <player>` locally.
- Grants are stored by Photon actor number for the current room only.
- Grants are removed when a player leaves and all grants are cleared on room or Master Client changes.
- `/grant` and `/revoke` are rejected over the network even if a client is otherwise authorized.
- The host re-parses and validates every target, count, placement, sender, protocol version, payload length, and rate limit.
- `player-location` resolves to the requesting player's avatar, not the host's avatar.

See [docs/architecture.md](docs/architecture.md) and [docs/protocol.md](docs/protocol.md) for implementation details.

## Build and local install

Build, test, and create a Thunderstore-compatible ZIP:

```powershell
.\scripts\Test-All.ps1
.\scripts\Build-Package.ps1
```

The ZIP is written to `dist/JamesKieley-RepoCommandConsole-2.0.0.zip`. It contains the required root-level `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png`, and plugin DLL.

For a tight development loop, copy the current build directly into a Thunderstore profile:

```powershell
.\scripts\Install-Local.ps1 `
  -RepoProfilePath "$env:APPDATA\Thunderstore Mod Manager\DataFolder\REPO\profiles\modded" `
  -QuarantineLegacyPlugins
```

`-QuarantineLegacyPlugins` moves older local `RepoLiveControl*` and `RepoSpawnBridge*` DLLs into a timestamped backup inside the profile; it does not delete them. Restart R.E.P.O. after rebuilding.

To use the mod manager's local-package path instead, import the generated ZIP with **Settings → Import local mod**, then click **Start Modded**.

## Testing

The pure parser/completion harness covers defaults, quoted names, invalid input, fuzzy completion for every `/spawn` argument, and token replacement. Runtime acceptance additionally requires a modded game session. See [docs/testing.md](docs/testing.md) for the host and two-client checklists and evidence expectations.

Earlier local bridge actions and exploratory payloads are retained for compatibility in `scripts`, `skill`, and `archive/prototypes`.
