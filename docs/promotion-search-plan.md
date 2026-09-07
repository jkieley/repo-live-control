# RepoCommandConsole discovery and promotion plan

Audited September 7, 2026; updated for the authorized 2.1.0 promotion work. The original observations below describe the page before these improvements.

## Implementation status

- Applied to the working tree: readable README heading, player-focused introduction and short description, implemented 2.1.0 player features, direct installation/tutorial links, and the existing three-image gameplay gallery. The screenshots retain their 2.0.0 capture label and immutable URLs from the published 2.0.1 update.
- The selected **Console Companion** mascot has replaced the package icon locally.
- Applied on GitHub: About description, canonical Thunderstore homepage link, and seven relevant topics. The empty metadata finding below is now resolved.
- Created a [complete tutorial](promotion/getting-started.md), [Steam-ready BBCode](promotion/steam-guide.bbcode), and [campaign kit](promotion/campaign-kit.md) containing a short announcement, creator outreach draft, three researched promotion targets, and a 45-second recording script.
- The campaign kit distinguishes prepared materials from sent messages or recorded footage. Publication and outreach should be recorded with their actual destination URLs when completed; prepared copy is not itself a published post or a video.
- Keep live two-player acceptance separate from automated validation of the new commands. The current automated suites pass, but no cross-client gameplay verification claim is made here.

## What the audit found

- The [Thunderstore listing](https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/) appeared in web searches for `repo command console`, `"repo command console"`, and its exact package name. The page is discoverable already. These are search-tool observations, not a verified Google rank or search-volume measurement.
- The current summary leads with networking terminology rather than the useful actions players search for. The README has helpful installation instructions, commands, and multiplayer answers, but its opening section sends people to an external Steam guide before showing the mod in action.
- The public [GitHub repository](https://github.com/jkieley/repo-live-control) has no About description, homepage, or topics; the public GitHub API confirmed these fields are empty. The repository name also differs from the published mod name, making a clear About description especially useful.
- Related spawning searches surface established tools such as [EnemySpawning](https://thunderstore.io/c/repo/p/NachitoSMO/EnemySpawning/) and [EnemySpawn](https://thunderstore.io/c/repo/p/XiaohaiMod/EnemySpawn/). Useful differentiation is the searchable F2 console, spawning across multiple catalogs, and host-granted friend access. This is an opportunity hypothesis, not evidence of keyword demand or competitor conversion rates.

## Priorities

| Priority | Proposal | Purpose |
| --- | --- | --- |
| 1 | Use the readable phrase **REPO Command Console** in the README heading and identify **RepoCommandConsole** immediately below it. | Connect the search phrase to the exact installable package. |
| 1 | Replace the short description with the player-focused draft below. | Make search snippets and mod-manager list entries explain what the mod does. |
| 1 | Place 2–3 genuine gameplay screenshots near the opening explanation, with captions describing the visible action. | Let visitors evaluate the actual interface and results quickly. |
| 1 | Fill GitHub About, set its website to the canonical Thunderstore package URL, and add relevant topics. | Connect GitHub visitors to installation and improve GitHub topic discovery. |
| 2 | Reorder the page: introduction, screenshots, install/first spawn, features, command reference, multiplayer, troubleshooting, external references. | Reduce the distance from discovering the mod to using it. |
| 2 | Use descriptive headings such as “How to spawn items and enemies in R.E.P.O.” and “How to open the console: press F2.” | Answer useful searches in visible content without repeating keyword lists. |
| 2 | Create a short demonstration video and one complete tutorial, then share where mod promotion is welcome. | Reach people searching for the task rather than the mod's existing name. |
| 3 | Consider a small official documentation site with a real command tutorial and links to Thunderstore. | Gain control over page titles, metadata, indexing tools, and traffic measurement if ongoing promotion warrants the maintenance. |

Google can use prominent headings when generating title links, and uses page content as the main source of snippets. Clear descriptive copy helps it understand the page; it does not guarantee a particular title, snippet, or ranking. [Title guidance](https://developers.google.com/search/docs/appearance/title-link), [snippet guidance](https://developers.google.com/search/docs/appearance/snippet).

## Current copy and search intent

README heading:

> REPO Command Console

Opening paragraph:

> RepoCommandConsole is a free in-game command console mod for R.E.P.O. Spawn enemies, weapons, upgrades, items, and valuables from your live game catalog. Version 2.1.0 adds player commands for healing, reviving, summoning, returning to the truck, and more. Press F2, find a command or target with fuzzy autocomplete, and run it. The host controls multiplayer access and can grant friends permission to use the commands.

Thunderstore short description, 219 characters, within its 250-character limit:

> R.E.P.O. command console: spawn enemies, items, weapons, upgrades and valuables; revive, heal, summon or return players to the truck. F2 fuzzy autocomplete, ordered command chains and host-controlled multiplayer access.

GitHub About, now applied:

> REPO Command Console: spawn enemies, items, upgrades and valuables; heal, revive, teleport and customize players. F2 autocomplete and host-controlled multiplayer permissions.

Applied GitHub topics: `repo-game`, `repo-mod`, `bepinex`, `thunderstore`, `command-console`, `multiplayer`, `csharp`. Topics aid discovery inside GitHub; they are not a promised Google ranking boost. [GitHub topic documentation](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/classifying-your-repository-with-topics).

Possible tutorial/video titles:

- **How to Spawn Items and Enemies in R.E.P.O. with RepoCommandConsole**
- **REPO Command Console: Install, Press F2, Spawn Your First Item**
- **Give Friends Spawn Commands in R.E.P.O.: Host Permissions Explained**

Capture filenames can describe the actual content, such as `repo-command-console-autocomplete.png`. Use truthful alt text and captions, for example “RepoCommandConsole autocomplete listing matching item targets in R.E.P.O.” when that is what the screenshot shows. Google recommends relevant surrounding text, descriptive alt text, and sharp images; filenames provide only light subject clues. Screenshots primarily improve understanding and confidence. [Google image guidance](https://developers.google.com/search/docs/appearance/google-images).

## Promotion ideas

Create a 30–60 second demonstration covering F2, autocomplete, one spawn, and cleanup. A longer tutorial can cover installation and granting a friend access. Use the exact mod name and a direct install link in each description. A Steam guide, release announcement in an appropriate modding channel, and outreach to creators who already demonstrate R.E.P.O. mods are sensible experiments. Use the real screenshots and a small press kit with the chosen logo, short description, and compatibility details. No outreach or messages have been sent as part of this audit. Community engagement and useful content promotion are consistent with [Google's starter guidance](https://developers.google.com/search/docs/fundamentals/seo-starter-guide).

## Platform limits and guardrails

- Keep `Coollectors` and manifest name `RepoCommandConsole`. Thunderstore states that changing the team or name creates a different package. Even README/image changes require a new version; existing versions are immutable. A presentation-only patch release is appropriate if changes are honestly described. [Updating a package](https://wiki.thunderstore.io/mods/updating-a-package).
- Thunderstore exposes package description, README, icon, website link, and categories. Its documented manifest does not expose custom HTML titles, robots directives, canonicals, structured data, or sitemap settings. Use its Markdown preview before publishing; retain an exactly 256×256 PNG package icon. [Creating a package](https://wiki.thunderstore.io/mods/creating-a-package).
- Keep categories and feature claims accurate. In particular, describe host-granted access, distinguish the F2 console from vanilla chat, and explain that despawn affects only objects created by this mod. Do not imply universal mod compatibility or advertise drop-rate controls that this console does not provide.
- An owned website can be verified in Search Console; Thunderstore publisher access alone does not give access to Thunderstore's Search Console property. Google requires appropriate property access to request indexing, and a crawl request does not guarantee indexing. [Recrawl documentation](https://developers.google.com/search/docs/crawling-indexing/ask-google-to-recrawl).

## Measure what changes

Record the publication date, package download count, and weekly download deltas. Search a consistent small set of phrases: `repo command console`, `R.E.P.O. console commands mod`, `repo spawn items mod`, and `repo spawn enemies mod`. Treat manual positions as noisy observations, not precise ranking data. If an owned documentation site is added, use Search Console impressions/clicks and install-link clicks there; these do not measure all Thunderstore visitors or unique installs. Review after roughly four weeks, allowing time for recrawling, and improve whichever step has evidence of a problem.
