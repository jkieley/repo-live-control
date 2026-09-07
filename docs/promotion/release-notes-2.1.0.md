# RepoCommandConsole 2.1.0

RepoCommandConsole adds player controls to the R.E.P.O. console. Press **F2**, choose a command and player with fuzzy autocomplete, then apply it to one character or `all`.

- **Manage a run:** kill, revive, heal, set maximum health, summon players, return them to the truck, apply damage or knockback, and reset push forces.
- **Create visual effects:** expressions, speech, wings, tumble, flashlight flicker, animation speed, pupil size, and falling overrides.
- **Combine actions:** `/chain all revive heal truck` runs steps in order. Chains accept up to eight actions from kill, revive, heal, summon, and truck.
- **Find every target:** player autocomplete includes the host and dead characters, disambiguates duplicate names, and lets you browse beyond the first eight suggestions. Long command results now scroll.

The host controls permissions. Approved clients submit player commands through the host; `/grant` and `/revoke` remain local host commands. Expression, animation speed, pupils, and falling require RepoCommandConsole 2.1.0 or newer on the target's client. Maximum health changes apply at runtime; they are not permanent upgrade purchases. AllPlayerCommands is not required.

Validation passed: command/completion/network tests, five simulated production player-runtime and owner-relay scenarios, 24 installed-game API signature/authority checks, Windows PowerShell 5.1 compatibility checks, and a Release build with zero warnings or errors. A real two-client 2.1.0 acceptance session has not yet been completed; simulated tests do not establish that every visual effect renders correctly on both peers.

[Install RepoCommandConsole on Thunderstore](https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/) · [Command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md) · [Report a reproducible problem](https://github.com/jkieley/repo-live-control/issues)
