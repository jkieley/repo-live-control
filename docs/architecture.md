# Architecture

## UI and parsing

The BepInEx plugin owns a small Unity IMGUI window and does not patch `ChatManager`, register with the vanilla `DebugCommandHandler`, or require developer mode. While open it refreshes `MenuManager.TextInputActive`, disables movement and aiming, and unlocks the cursor.

The command core is dependency-free and split into tokenizer, parser, fuzzy matcher, and completion engine components. The game layer supplies live target and player catalogs. This separation allows the grammar and token replacement behavior to run in a normal .NET test harness.

## Host executor

Both the local named pipe and the in-game console enqueue `ControlRequest` objects. Harmony patches `RunManager.Update`, and all Unity, REPOLib, and Photon operations execute there on the game thread. High-volume jobs process at most ten operations per frame.

Each request carries its source and authoritative requester actor number. `player-location` resolves the matching `PlayerAvatar.photonView.Owner.ActorNumber`, so a granted client spawns at that client's avatar rather than at the host.

The host creates network objects through `Items.SpawnItem`, `Valuables.SpawnValuable`, and `Enemies.SpawnEnemy`. Network removal uses `PhotonNetwork.Destroy` and updates the corresponding director/manager tracking list. Slash-command despawn is limited to objects recorded by this mod.

## Client-to-host requests

Every installed client registers a defensive Photon event callback on configurable event code `198`. A magic string and protocol version distinguish this protocol from other mods using the same event code.

```text
client console
  -> reliable request event to Master Client
  -> sender identity taken from EventData.Sender
  -> grant, length, verb, version, and rate-limit checks
  -> host game-thread queue
  -> REPOLib/Photon mutation
  -> reliable response targeted to the requesting actor
```

The payload never supplies a trusted actor identity. Responses and permission notices are accepted only from the current Master Client. `/grant` and `/revoke` are blocked before a remote command reaches the host queue and checked again by the runtime.

## Permission lifetime

The host keeps an in-memory set of granted Photon actor numbers. It prunes players who leave and clears the set when the room name or Master Client actor changes. This avoids persisting a grant to a different lobby or implicitly trusting players after host migration.

## Legacy bridge

The named pipe remains UTF-8, newline-delimited and host-local. Its listener thread only parses text, enqueues a request, waits, and writes the result. See [protocol.md](protocol.md) for exact messages.
