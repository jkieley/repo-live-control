# Unattended game launch and screenshot capture

Status: design proposal; not implemented.

## Top-level goal

From a machine with Steam, a modded R.E.P.O. profile, and a usable graphics device, run one command that launches R.E.P.O. without operator input, waits until it is capture-ready, writes a requested rendered frame to a known PNG path, and shuts down only the game process that command started. The workflow must not use the operator's keyboard or mouse or require the game window on the primary display.

The target is unattended, visually hidden rendering. It is not graphics-headless execution.

## Why `-nographics` is not the target

Unity's true headless modes, such as `-batchmode -nographics`, do not create the rendered player output needed for a gameplay screenshot. They may be useful for simulation or server work, but a screenshot taken without a graphics device is either unavailable or blank.

The supported model must therefore keep Unity's graphics device and render loop active while isolating the game window from the operator. On Windows, that normally means a windowed game on a virtual or secondary display. A minimized window is not sufficient: Unity or the graphics driver may throttle or stop presenting frames while minimized.

| Mode | Rendering | Screenshot support | Intended use |
|---|---:|---:|---|
| `-batchmode -nographics` | No player graphics device | No | Out of scope |
| Windowed on a virtual/secondary display | Yes | Yes | Primary target |
| Normal foreground window | Yes | Yes | Diagnostic fallback |

## Current gap

RepoLiveControl currently attaches to an already-running modded host. The named pipe can inspect or mutate the live game, but it cannot:

- launch the Thunderstore profile;
- navigate from startup into a capture scenario;
- request an in-process frame capture;
- report the completed image path; or
- close a game process that an automation session launched.

The manual acceptance flow in [testing.md](testing.md) still relies on operator-created screenshots.

## Proposed workflow

A future `scripts/Capture-RepoFrame.ps1` entry point should own the complete session:

1. Validate the profile, game, Steam, output directory, and rendering environment.
2. Refuse to take ownership of an existing R.E.P.O. process unless an explicit attach-only mode was requested.
3. Launch the same modded configuration used by **Start Modded**, with a fixed windowed resolution and GPU rendering enabled.
4. Record the launched process identity and start time. Steam may return before creating the actual game process, so the launcher PID alone is not proof of ownership.
5. Wait for the `CodexRepoCommandConsoleV2` pipe and a successful `status` response.
6. Wait for the requested capture state. The first implementation may capture the startup/menu state; gameplay capture additionally requires a scenario driver.
7. Send a local capture request and wait until the PNG has been completely written.
8. Validate the PNG signature, nonzero dimensions, and expected resolution, then return its absolute path.
9. If stop-after-capture was requested, ask the owned game process to exit cleanly. Never stop a process that was already running or whose identity no longer matches the recorded process.

The script should return a nonzero exit code for launch, readiness, capture, validation, or shutdown failures and preserve the relevant BepInEx log path in its error output.

## In-process capture

Capture belongs inside the plugin rather than in desktop screenshot automation. In-process capture:

- captures only game output rather than unrelated desktop content;
- does not depend on window focus, keyboard shortcuts, or screen coordinates;
- can synchronize with Unity's end-of-frame boundary; and
- can report completion only after the image exists.

A proposed local-pipe message is:

```text
capture|frame|<basename>
```

`frame` means the rendered player frame, including screen-space UI such as the RepoLiveControl IMGUI console. `<basename>` must be a short filename component, not a path; the plugin should reject separators and traversal sequences and add the `.png` extension itself. Captures should be written below one fixed profile-owned directory, for example:

```text
BepInEx/captures/RepoLiveControl/<basename>.png
```

A successful response should be machine-readable and include the final path and dimensions:

```text
OK capture|<absolute-path>|<width>|<height>
```

The request must be enqueued through the existing game-thread executor. A coroutine should wait for the end of a rendered frame, capture the player output, encode one PNG, dispose of temporary Unity objects, atomically publish the final file, and only then complete the pipe request. Only one capture may be active at a time. The existing 30-second pipe timeout remains authoritative: a timed-out request must not write a late screenshot.

A camera-only `RenderTexture` mode may be useful later for scene imagery, but it is not an equivalent fallback for acceptance screenshots because it can omit screen-space UI and post-processing. The first implementation should prove full-frame capture on the actual game build.

## Rendering environment

The launch harness should request a deterministic windowed resolution, initially `1280x720`, and leave the render loop active. The implementation must be tested rather than assuming that Unity command-line flags are honored by the installed R.E.P.O. build.

For a machine without a physical monitor, use a GPU-backed virtual display or an equivalent persistent display device. Disconnecting an RDP session can remove or replace the active graphics context, so RDP-only rendering is not an acceptance environment unless capture has been demonstrated after disconnect.

Moving a window off the primary display is acceptable only after proving that captures remain current and include UI. Minimizing the window or using `-nographics` is not an acceptable substitute.

## Scenario-control boundary

Frame capture records the current frame; it does not make the current state meaningful. Starting from no process and producing a reproducible gameplay image also requires automation for:

- startup and menu readiness;
- save or lobby creation/selection;
- level-load completion;
- local player and camera readiness; and
- any requested world setup before capture.

Those lifecycle actions should be explicit, local-only bridge operations rather than simulated keyboard input. They must not bypass Steam, Photon ownership, REPOLib spawning, or normal host authority. Until that scenario driver exists, the useful intermediate capabilities are unattended startup/menu capture and capture from an already-running gameplay host.

## Safety and invariants

- Keep capture and lifecycle commands on the host-local named pipe; do not expose them through Photon events or the slash-command console.
- Write only beneath the fixed capture root. The caller chooses a sanitized basename, never an arbitrary plugin-side path.
- Refuse launch ownership when R.E.P.O. is already running. Attach-only capture must never imply permission to close that process.
- Stop only the exact process created by the current session, validated by PID and start time.
- Preserve the current rule that Unity APIs run on the game thread.
- Complete a capture response only after the final PNG is readable.
- Reject a second capture while one is pending instead of queueing unbounded image work.
- Correlate every capture with a BepInEx log entry containing the request identifier, active scene, dimensions, and final path. As noted in [testing.md](testing.md), an image alone does not prove which plugin build handled the request.

## Acceptance criteria

The feature is complete when the following behavior is demonstrated on the supported R.E.P.O. build:

1. With no R.E.P.O. process running, one command launches the configured modded profile without keyboard or mouse input and reaches bridge readiness.
2. The game renders at the requested resolution on a virtual or secondary display without occupying the primary display.
3. A capture request returns only after a valid, nonempty `1280x720` PNG exists at the reported path.
4. A full-frame capture contains both a known game scene element and a known screen-space UI element; a black, stale, camera-only, or desktop image fails acceptance.
5. Repeated captures reflect a deliberately changed visible game state rather than returning the same stale frame.
6. Capture timeout, invalid basename, unavailable graphics device, and file-write failure return explicit `ERROR` responses and leave no final partial file.
7. Stop-after-capture exits the process launched by that session. Attach-only mode leaves a pre-existing game untouched.
8. The screenshot is correlated with the matching plugin log entry and bridge response.

A later gameplay milestone must additionally start from no process, enter a reproducible gameplay scene through explicit lifecycle controls, perform a small RepoLiveControl setup action, capture the resulting frame, and exit cleanly.

## Delivery order

1. Prove full-frame PNG capture manually on the installed game build while unfocused and while hosted on the intended virtual/secondary display.
2. Add the bounded local-pipe capture request and its error paths.
3. Add the process-owning PowerShell launch/capture/stop harness.
4. Add explicit startup and gameplay scenario controls.
5. Run the end-to-end acceptance flow and inspect the actual images alongside `BepInEx/LogOutput.log`.
