# REPO Live Control

Host-only runtime control for a modded R.E.P.O. game. The bridge attaches to a running Unity/Mono process, executes Unity and Photon work on `RunManager.Update`, and exposes a local named-pipe command interface.

## Capabilities

- Spawn exact counts of named, high-level, or random enemies.
- Spawn random, named, or curated expensive valuables.
- Spawn named shop/items, including large frame-batched requests.
- Place objects safely, near the host, or directly at the host.
- Despawn all or filtered enemies while optionally keeping a requested number.
- Enable or disable automatic enemy spawning.
- Detect and relocate loot stuck in level geometry.
- Query current enemy, loot, and auto-spawn state.

## Build, install, and attach

```powershell
.\scripts\Attach-RepoLiveControl.ps1
```

The script discovers the active Thunderstore profile from the running `REPO.exe`, builds against the exact installed game and mod assemblies, installs the BepInEx plugin, and attaches it live. Use `-InstallOnly` when preparing for the next launch.

## Examples

```powershell
.\scripts\RepoControl.ps1 status
.\scripts\RepoControl.ps1 enemy apex 5 near-player
.\scripts\RepoControl.ps1 enemy gnome 20 near-player
.\scripts\RepoControl.ps1 despawn gnome 1
.\scripts\RepoControl.ps1 loot expensive 20 safe
.\scripts\RepoControl.ps1 item strength 100 at-player
.\scripts\RepoControl.ps1 auto off
.\scripts\RepoControl.ps1 unstick
```

See [docs/protocol.md](docs/protocol.md) before adding commands. Earlier exploratory payloads are preserved in `archive/prototypes`.
