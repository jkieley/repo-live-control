# Bridge development

Repository: `C:\Users\James Kieley\Documents\Codex\2026-08-18\search-the-web-are-there-any\repo-live-control`

Read the repository's `docs/protocol.md` and `src/RepoLiveControl/RepoLiveControl.cs` before extending it.

## Invariants

- The pipe thread only parses text, enqueues `ControlRequest`, waits, and writes the response.
- Unity, Photon, and REPOLib APIs run from the `RunManager.Update` Harmony patch.
- High-volume work is frame-batched and completes its original request only when finished or failed.
- Enemy requests count actual `EnemyParent` objects, not setup invocations. Destroy setup overages so Gnomes and similar grouped setups honor exact counts.
- Network creation uses `Enemies.SpawnEnemy`, `Valuables.SpawnValuable`, or `Items.SpawnItem`. Network removal uses host `PhotonNetwork.Destroy` and updates director tracking lists.
- Safe placement reserves separated points and rejects occupied physics volumes.
- Every action returns an `OK` or `ERROR` response with observed counts.

## Updating a running process

Unity/Mono keeps loaded assemblies resident. When replacing the running bridge, use a new assembly identity, plugin/Harmony ID, and pipe name; have the new bridge unpatch the prior Harmony owner. Update `scripts/RepoControl.ps1` and `docs/protocol.md` to the same pipe name. The installed DLL can overwrite the prior on-disk plugin for future launches.

Run `scripts/Attach-RepoLiveControl.ps1`, then validate with `scripts/RepoControl.ps1 status`. Do not test a new mutation unless the user authorized that mutation.
