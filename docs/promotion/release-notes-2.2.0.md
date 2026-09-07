# RepoCommandConsole 2.2.0

September 7, 2026

RepoCommandConsole 2.2.0 makes the R.E.P.O. spawn catalog browsable without memorizing item names. Press **F2** and type `/spawn` to see every supported target, with compact previews and optional fuzzy search.

[Watch the gameplay demo](https://youtu.be/j_Qd60MG6VA) · [Visit the demo website](https://repo-command-console.jkieley543940.chatgpt.site)

The 2-minute demo shows genuine singleplayer gameplay at original speed with game audio: browse, spawn equipment and loot, pick up the results, find cosmetic cases, spawn a nearby enemy, and clean up matching mod-spawned objects.

- **Browse the whole list:** use the mouse wheel, scrollbar, or Up/Down. Eight compact rows stay visible, the counter shows your selection and total matches, and there is no total-result cap. Clicking a row or pressing Tab selects its canonical name; Enter executes.
- **Recognize targets visually:** small previews use actual game icons or asset meshes when available. Browsing does not instantiate gameplay objects. Missing previews leave targets selectable by name.
- **Find ten additional targets:** four cosmetic cases, three enemy souls, and three surplus money bags are included when their supported network resources are present. Cosmetic cases use the `valuable:` category but are physical cosmetic containers, not ordinary saleable loot. Disabled and removed prototype item fallbacks remain excluded.
- **Search familiar names:** Pistol finds Gun, Defib finds Defibro, and Light Bridge finds Phase Bridge. Short valuable names also work. Aliases return the existing canonical row; exact ambiguous aliases require an explicit target instead of choosing one silently.
- **Keep the console through level loading:** startup protection preserves its BepInEx manager through the menu-to-singleplayer transition. Focused-window keyboard handling also lets F2 and Escape close the console while its text field has focus; both were verified in game.
- **Spawn enemies near the player:** `player-location` now searches for a walkable navigation point within 3 metres, then up to 5 metres. If neither succeeds, the command reports an error before spawning. It no longer substitutes a distant roaming point; this mode does not promise collision clearance.

Console-spawned money bags use their prefab's default value. All three audited bag variants have the same **850–1,100** preset range before the game's rounding to hundreds. A live Small Money Bag initialized with a displayed value of **$1,000**; this is one observed result, not a guaranteed payout. A larger bag selects a physical variant, not a higher payout tier, and the console does not copy the current extraction surplus. See the [catalog audit](https://github.com/jkieley/repo-live-control/blob/main/docs/catalog-coverage-audit.md) for source evidence and scope.

The existing player commands and host-managed permissions remain available. Expression, animation speed, pupils, and falling still require **2.1.0 or newer on the target's client**. Required dependencies remain BepInExPack 5.4.2305 and REPOLib 4.2.0; this build targets R.E.P.O. v0.4.4.3.

The full test command passed: 76 command/network scenarios, five simulated production player-runtime and owner-relay scenarios, 35 catalog/local-placement checks, 24 installed-game API contracts, nine compiled enemy-placement route/API contracts, **25** compiled-plugin preview isolation/API checks, Windows PowerShell 5.1 compatibility, and a Release build with zero warnings or errors. The placement contracts also reject deliberate regressions that restore distant roaming or bypass a failed local search.

Live verification covered menu-to-singleplayer persistence, focused-text-field F2/Escape closing, normal spawning and native grab/hold interaction for a Valuable Tracker and Goblet, and their targeted despawn. Small Money Bag alias lookup, preview, spawning, and value initialization were checked; a representative case and soul were also spawned in an earlier 2.2.0 run. A read-only sweep produced actual upright previews for **254 of 255** audited targets. Hidden has no visible model mesh and retains its category placeholder. These checks do not establish live behavior for every new target variant or peer visibility; a real two-client acceptance run has not been completed.

The corrected enemy placement was also recorded in singleplayer: Apex Predator spawned near the player twice, walked normally, and was removed with targeted despawn. The final cleanup showed the console result and the cleared floor. This verifies the nearby-spawn route for the recorded enemy, not every enemy or collision-free placement.

[Thunderstore package page](https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/) · [Getting started](https://github.com/jkieley/repo-live-control/blob/main/docs/promotion/getting-started.md) · [Command reference](https://github.com/jkieley/repo-live-control/blob/main/docs/commands.md) · [Report a problem](https://github.com/jkieley/repo-live-control/issues)
