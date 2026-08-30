# Thunderstore Publishing Guide

_Last verified: 2026-08-30_

This document records the requirements and release procedure for publishing RepoCommandConsole to the R.E.P.O. community on Thunderstore.

## Current package status

The package build is automated by `scripts/Build-Package.ps1`. It validates the manifest and icon, builds the release DLL, stages exactly the expected files, and verifies that every archive entry is at the ZIP root.

Run:

```powershell
.\scripts\Build-Package.ps1
```

Expected output:

```text
dist/Coollectors-RepoCommandConsole-2.0.0.zip
```

Expected archive structure:

```text
CHANGELOG.md
icon.png
manifest.json
README.md
RepoCommandConsole.dll
```

The package generated during the 2026-08-30 verification was 147,458 bytes and built with zero warnings and zero errors. Treat that as a historical verification result, not a substitute for rebuilding and testing the final release commit.

## Account and ownership requirements

Thunderstore publishing requires an authenticated account and a Team:

1. Sign in using Discord, GitHub, or Overwolf.
2. Create or select the Team that will permanently own the package.
3. Choose the Team name carefully. The R.E.P.O. publishing guide warns that a Team cannot be renamed or deleted after publishing.

Thunderstore identifies a package version as:

```text
TeamName-PackageName-Version
```

With the `Coollectors` Team selected, this release's dependency string will be:

```text
Coollectors-RepoCommandConsole-2.0.0
```

The ZIP filename does not determine package ownership. The Team selected during upload and the `name` and `version_number` fields in `manifest.json` determine the identity.

## Required package files

A valid Thunderstore ZIP must contain the following files at its root. File names are case-sensitive.

| File | Requirement | Project source |
|---|---|---|
| `manifest.json` | Valid UTF-8 JSON | `thunderstore/manifest.json` |
| `README.md` | UTF-8 Markdown rendered on the package page | `README.md` |
| `icon.png` | PNG, exactly 256 by 256 pixels | `thunderstore/icon.png` |
| Mod files | Correct for the game's loader | Release `RepoCommandConsole.dll` |
| `CHANGELOG.md` | Optional Markdown | `thunderstore/CHANGELOG.md` |

Do not compress a parent directory around these files. `manifest.json`, `README.md`, and `icon.png` must not be one directory below the ZIP root.

A root-level plugin DLL is valid for this R.E.P.O./BepInEx package. Thunderstore Mod Manager installs package contents according to the community's BepInEx install rules.

Thunderstore currently documents a soft maximum package size of 5,242,880,000 bytes, roughly 5 GB.

## Manifest requirements

The current manifest is:

```json
{
  "name": "RepoCommandConsole",
  "version_number": "2.0.0",
  "website_url": "https://github.com/jkieley/repo-live-control",
  "description": "Host-authoritative R.E.P.O. command console with fuzzy autocomplete and host-granted client permissions.",
  "dependencies": [
    "BepInEx-BepInExPack-5.4.2305",
    "Zehs-REPOLib-4.2.0"
  ]
}
```

Manifest constraints:

- `name`: maximum 128 characters; only `A-Z`, `a-z`, `0-9`, and `_`; no spaces or hyphens.
- `description`: maximum 250 characters.
- `version_number`: numeric `Major.Minor.Patch` semantic version without a prerelease suffix.
- `website_url`: required field, but it may be an empty string.
- `dependencies`: exact Thunderstore dependency strings in `Team-Package-Version` form.

As of the verification date, the current R.E.P.O. package pages list:

- `BepInEx-BepInExPack-5.4.2305`
- `Zehs-REPOLib-4.2.0`

Check these package pages again before a future release rather than assuming this snapshot remains current. RepoCommandConsole also declares REPOLib as a hard runtime dependency through `[BepInDependency]` in the plugin class.

## R.E.P.O. upload settings

Use the [Thunderstore upload page](https://thunderstore.io/package/create/) and select:

- **Team:** `Coollectors`
- **Community:** `R.E.P.O.`
- **Categories:**
  - `Mods`
  - `Tools`
  - `Client-side`
  - `Server-side`
- **Contains NSFW content:** No

The category combination reflects the actual architecture: an installed client-side console submits requests, while the host authoritatively validates and performs world mutations. Do not select `Quality Of Life`; this package is primarily an administrative and spawning tool.

## Content and moderation requirements

Thunderstore's global rules prohibit or restrict the following:

- Do not package game files such as `Assembly-CSharp.dll` without explicit permission from the game developer.
- Do not redistribute another author's package, code, or assets without permission or a compatible license.
- Do not include malware, token collection, deceptive disruption, or unauthorized personal-data collection.
- Avoid obfuscated code and code downloaded for execution at runtime because they prevent auditing and compatibility work.
- Use the mod manager for package updates rather than implementing a self-updater.
- Mark packages containing NSFW content. Sexually explicit material is still prohibited in the icon, README, changelog, and other content displayed by Thunderstore or a mod manager.
- Packages must be functional and accurately described. Empty packages, misleading metadata, spam, and untested or nonfunctional AI-generated packages may be removed.

The generated RepoCommandConsole archive contains its own DLL and metadata only; it does not include R.E.P.O. game assemblies.

## Pre-upload release checklist

Published package versions are immutable. Test the exact ZIP intended for upload.

### Package validation

- [ ] Run `scripts/Test-All.ps1` successfully.
- [ ] Run `scripts/Build-Package.ps1` successfully.
- [ ] Confirm the generated ZIP contains exactly the five expected root entries.
- [ ] Confirm the manifest version matches the assembly, file, package-script, and changelog versions.
- [ ] Confirm dependency strings against their current Thunderstore package pages.
- [ ] Preview `README.md` and `CHANGELOG.md` using Thunderstore's Markdown Preview tool.
- [ ] Optionally submit `manifest.json` to Thunderstore's Manifest Validator.

### Clean-profile runtime verification

1. Import the generated ZIP through the mod manager's **Settings → Import local mod** action.
2. Confirm BepInExPack and REPOLib install automatically.
3. Launch R.E.P.O. through **Start Modded**.
4. Confirm `F2` opens and closes the command console.
5. Exercise `/help` and `/permissions`.
6. Spawn one known entity and despawn one matching entity.
7. Run the host/client grant flow if multiplayer support is part of the release claim.
8. Review BepInEx logs for errors attributable to RepoCommandConsole.

## Upload procedure

1. Open [Upload package](https://thunderstore.io/package/create/).
2. Drag the exact tested ZIP into the upload form.
3. Select the permanent Team.
4. Select the R.E.P.O. community.
5. Select the recommended categories.
6. Confirm the NSFW disclosure is correct.
7. Submit the package.
8. Open the resulting package page and verify its README, changelog, icon, dependencies, categories, and install action.
9. Allow several hours for Thunderstore and mod-manager caches to update.

At the time of the 2026-08-30 verification, the intended package URL, `https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/`, returned 404, indicating no existing listing under the `Coollectors` namespace. A first public upload may start at `2.0.0`; Thunderstore does not require a new package to start at `1.0.0`.

## Updating an existing package

To publish an update:

1. Keep the same Team.
2. Keep the same `name` in `manifest.json`.
3. Increase `version_number` to a higher semantic version.
4. Update every project location that carries the version.
5. Rebuild, retest, and upload through the same upload page.

Changing the Team or manifest name creates a different package instead of updating the existing listing. An existing version cannot be edited; even a README-only correction requires a new version. Categories and deprecation status can be managed after upload.

## Sources

- [Thunderstore: Creating a Package](https://wiki.thunderstore.io/mods/creating-a-package)
- [Thunderstore: Packaging Your Mods](https://wiki.thunderstore.io/mods/packaging-your-mods)
- [Thunderstore: Updating a Package](https://wiki.thunderstore.io/mods/updating-a-package)
- [Thunderstore: Global Rules](https://wiki.thunderstore.io/moderation/global-rules)
- [R.E.P.O. Modding Wiki: Publishing to Thunderstore](https://repomods.com/thunderstore/publish.html)
- [R.E.P.O. Modding Wiki: Thunderstore Overview and Teams](https://repomods.com/thunderstore/overview.html)
- [R.E.P.O. Thunderstore community](https://thunderstore.io/c/repo/)
- [BepInExPack dependency page](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)
- [REPOLib dependency page](https://thunderstore.io/c/repo/p/Zehs/REPOLib/)
