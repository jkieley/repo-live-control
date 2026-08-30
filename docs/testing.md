# Testing

## Automated checks

```powershell
.\scripts\Test-All.ps1
```

`Test-All.ps1` includes a disposable Windows PowerShell 5.1 integration test for absolute-path validation, the build command, direct profile installation, and nested legacy-plugin quarantine. It uses a temporary fake game/profile plus a stub `dotnet` command and removes only its validated GUID-named temporary directory. If the current terminal blocks local scripts, use `Set-ExecutionPolicy -Scope Process Bypass -Force`; the setting lasts only for that terminal process.

The dependency-free command/network harness verifies:

- spawn and despawn defaults, including location without an explicit count;
- every numeric count from `1` through `500`, exact execution translation, and rejection of malformed or out-of-range counts instead of clamping;
- quoted/escaped multiword targets and production-shaped player selectors, token spans, whitespace boundaries, and unterminated quotes;
- fuzzy precedence and normalization plus contextual command, target, every-count, location, and player matching;
- active-token replacement, cursor/suffix preservation, quote insertion/repair, invalid-command recovery, arity, and argument validation;
- role-aware catalogs that omit grant/revoke and player-management suggestions for non-host clients;
- Photon envelope/version/kind and request-ID validation, remote verb/permission/length policy, and per-actor rolling rate limits;
- pending-request completion, 30-second timeout, room exit/change, and host-migration failures;
- room/session grant creation, pruning, revocation, and reset behavior;
- Photon callback registration/disposal retries and the menu-versus-session activation boundary;
- queued-request cancellation/session mismatch rejection and grouped-enemy safe-placement acceptance policy.

The same script builds the full BepInEx plugin in Release configuration against the configured game/profile assemblies.

## Local Thunderstore host acceptance

1. Build the package with `scripts/Build-Package.ps1`.
2. Fully exit R.E.P.O., then import the ZIP through **Settings → Import local mod**, or use `scripts/Install-Local.ps1` for the direct-DLL development loop. The installer rejects an active game process because Windows cannot replace a loaded plugin DLL.
3. Quarantine older local bridge DLLs so only one command console owns the active Harmony patch.
4. Click **Start Modded**, create a private multiplayer game, and confirm `REPO Command Console 2.0.0` in `BepInEx/LogOutput.log`.
5. Capture the normal game view.
6. Press `F2` and capture the open console.
7. Type fuzzy input for each position, capturing the highlighted match:
   - `/spwan`
   - `/spawn strg`
   - `/spawn "item:Strength Upgrade" 205x`
   - `/spawn "item:Strength Upgrade" 2 rncl`
   - `/spawn "item:Strength Upgrade" rncl` (location in the count-or-location position)
8. Confirm the host is offered `/grant` and `/revoke` command suggestions. Use `Tab` to accept canonical values.
9. Submit `/spawn "item:Strength Upgrade" random-non-collision-location`; confirm the visible result reports exactly one spawned object, proving the omitted count default.
10. Submit a small explicit count and confirm the completed count matches it. Submit `0` and `501` and confirm both fail instead of spawning a clamped amount.
11. Spawn one enemy at `random-non-collision-location`; capture that the final spawned enemy is not intersecting visible world geometry, along with the `OK` result and matching log lines.
12. Run the matching `/despawn` command and confirm the reported observed count.
13. Press `Escape` and capture the closed state. Reopen with `F2`, press `F2` again, and capture that closed state too.

Correlate screenshots with `BepInEx/LogOutput.log`; screenshots alone cannot establish which DLL handled the command.

## Two-client acceptance

A genuine non-host result requires two Steam sessions/clients. On both machines install the same package and configure the same Photon event code.

1. Host and client join the same private room.
2. Client opens and closes the console with `F2`/`Escape`.
3. Client verifies fuzzy command, target, count, and location suggestions locally, including a location directly after the target.
4. Client types `/gr` and confirms `/grant` and `/revoke` are not offered; those host-only commands and player candidates remain available on the host.
5. Before a grant, the client submits a spawn and receives `ERROR The host has not granted you...`.
6. Host completes and runs `/grant "ClientName#ActorNumber"`.
7. Client receives the grant notice and repeats the same spawn.
8. Both players observe the networked object at the client's location, and the client receives an `OK` result.
9. While one client request is still pending, submit another and confirm it is rejected with `Wait for the previous host response...`; after completion, confirm another request can be sent.
10. Host runs `/revoke ...`; after the revoke completes, the client's next mutation is rejected. Separately, leaving the room or changing host during a batched mutation should stop remaining work with an explicit partial-progress error.
11. Repeat with an enemy at `random-non-collision-location` and a despawn command.
12. Capture open, autocomplete, role-filtered suggestions, denied, granted, successful, revoked, and closed states from the relevant client.

A synthetic event or a second window without an independent Photon/Steam actor is useful for diagnostics but is not evidence of the non-host acceptance case. Genuine two-client acceptance still requires two independently controlled Photon/Steam actors.
