# Testing

## Automated checks

```powershell
.\scripts\Test-All.ps1
```

The command harness verifies:

- spawn and despawn defaults;
- quoted multiword targets and players;
- invalid counts, locations, excess arguments, and unterminated quotes;
- fuzzy command, target, count, location, and player matching;
- active-token replacement and quote insertion.

The same script builds the full BepInEx plugin in Release configuration against the configured game/profile assemblies.

## Local Thunderstore host acceptance

1. Build the package with `scripts/Build-Package.ps1`.
2. Import the ZIP through **Settings → Import local mod**, or use `scripts/Install-Local.ps1` for the direct-DLL development loop.
3. Quarantine older local bridge DLLs so only one command console owns the active Harmony patch.
4. Click **Start Modded**, create a private multiplayer game, and confirm `REPO Command Console 2.0.0` in `BepInEx/LogOutput.log`.
5. Capture the normal game view.
6. Press `F2` and capture the open console.
7. Type fuzzy input for each position, capturing the highlighted match:
   - `/spwan`
   - `/spawn strg`
   - `/spawn "item:Strength Upgrade" 205x`
   - `/spawn "item:Strength Upgrade" 2 rncl`
8. Use `Tab` to accept canonical values and `Enter` to spawn.
9. Confirm the visible result says `OK Spawned ...` and capture the spawned object in the world.
10. Run the matching `/despawn` command and confirm the reported observed count.
11. Press `Escape` and capture the closed view.

Correlate screenshots with `BepInEx/LogOutput.log`; screenshots alone cannot establish which DLL handled the command.

## Two-client acceptance

A genuine non-host result requires two Steam sessions/clients. On both machines install the same package and configure the same Photon event code.

1. Host and client join the same private room.
2. Client opens and closes the console with `F2`/`Escape`.
3. Client verifies fuzzy target, count, and location suggestions locally.
4. Before a grant, the client submits a spawn and receives `ERROR The host has not granted you...`.
5. Host completes and runs `/grant "ClientName#ActorNumber"`.
6. Client receives the grant notice and repeats the same spawn.
7. Both players observe the networked object at the client's location, and the client receives an `OK` result.
8. Host runs `/revoke ...`; the next client mutation is rejected.
9. Repeat with an enemy and a despawn command.
10. Capture open, autocomplete, denied, granted, successful, revoked, and closed states from the relevant client.

A synthetic event or a second window without an independent Photon/Steam actor is useful for diagnostics but is not evidence of the non-host acceptance case.
