# Architecture

## UI and parsing

The BepInEx plugin owns a small Unity IMGUI window and does not patch `ChatManager`, register with the vanilla `DebugCommandHandler`, or require developer mode. While open it refreshes `MenuManager.TextInputActive`, disables movement and aiming, and unlocks the cursor.

The command core is dependency-free and split into tokenizer, parser, fuzzy matcher, completion engine, and execution-translation components. The game layer supplies live target and player catalogs plus a role flag. Hosts receive grant/revoke and player-management candidates; non-host clients retain normal command/target/count/location completion without being offered host-only actions. This separation allows grammar, exact count/location translation, and token replacement behavior to run in a normal .NET test harness.

The spawn parser accepts either `[count] [location]` or a location directly after the target. In the latter form count defaults to `1`. It validates every numeric spawn/despawn count against `1..500` before translation, so a valid slash command never depends on a lower-level silent clamp.

## Host executor

Both the local named pipe and the in-game console enqueue `ControlRequest` objects. Harmony patches `RunManager.Update`, and all Unity, REPOLib, and Photon operations execute there on the game thread. High-volume jobs process at most ten operations per frame.

Each request carries its source and authoritative requester actor number. `player-location` resolves the matching `PlayerAvatar.photonView.Owner.ActorNumber`, so a granted client spawns at that client's avatar rather than at the host.

When execution begins, the request records whether it is in a room, the room object, Master Client actor, and permission-session revision. Remote requests also carry the revision and live authorization predicate captured when the host accepted them. These conditions are checked before dispatch and on each frame of an active batch. Room exit/change, host migration, or requester departure aborts remaining work and reports completed/requested progress instead of allowing an old authorization to cross sessions. A host `/revoke` is serialized through the same executor, so it governs commands that dispatch after the revoke completes.

The host creates network objects through `Items.SpawnItem`, `Valuables.SpawnValuable`, and `Enemies.SpawnEnemy`. Network removal uses `PhotonNetwork.Destroy` and updates the corresponding director/manager tracking list. Slash-command despawn is limited to objects recorded by this mod. Random enemy placement runs the game's roam-point resolution first, then checks separation and physics overlap at that exact final point before spawning.

## Client-to-host requests

Every installed client registers a defensive Photon event callback on configurable event code `198` only after R.E.P.O. enters a multiplayer lobby or gameplay scene, and removes it again on session exit. Menu, loading, and region-selection scenes do not initialize or poll Photon through this mod. A magic string and protocol version distinguish this protocol from other mods using the same event code.

```text
client console
  -> reliable request event to Master Client
  -> sender identity taken from EventData.Sender
  -> grant, length, verb, version, duplicate, rate-limit, and queue-cap checks
  -> host game-thread queue
  -> room/host/session/grant revalidation
  -> REPOLib/Photon mutation
  -> reliable response targeted to the requesting actor
```

The payload never supplies a trusted actor identity. Responses and permission notices are accepted only from the current Master Client. `/grant` and `/revoke` are blocked before a remote command reaches the host queue and checked again by the runtime. Each client permits one pending host request, expires it after 30 seconds, and fails it immediately on room/session or host changes. The host bounds accepted work to two outstanding requests per actor and 32 overall.

## Permission lifetime

The host keeps an in-memory set of granted Photon actor numbers. It prunes players who leave and increments the permission-session revision while clearing all grants on room entry/exit, room-name change, or Master Client change. Queued and active remote mutations require their original revision and, for non-public verbs, the actor's live grant. This avoids persisting a grant to a different lobby or implicitly trusting players after host migration.

## Legacy bridge

The named pipe remains UTF-8, newline-delimited and host-local. Its listener thread only parses text, enqueues a request, waits, and writes the result. See [protocol.md](protocol.md) for exact messages.
