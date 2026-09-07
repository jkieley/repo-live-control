# Spawn catalog coverage audit — 2.2.0

The catalog now includes ten networked game resources that REPOLib's normal valuable presets omit: four cosmetic cases, three enemy souls, and three surplus money bags. Guide names also work as aliases for existing items; the browser and execution still use one canonical target for each object.

## Evidence and scope

Compared the [Steam item guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3773432391), the installed game's `Assembly-CSharp.dll`, REPOLib 4.2, and a read-only runtime inventory captured on September 7, 2026 at 18:04 UTC. The running clean demo profile contained BepInEx, REPOLib, and RepoCommandConsole 2.1.0. The temporary diagnostic enumerated registered objects and Unity resources; it did not spawn objects or change the scene. Its local output and assembly are excluded from the release package.

The guide is a naming and coverage reference, not proof that an object is available in this game build. It also displays a Steam removal/incompatibility banner. The installed prefabs and APIs decide what the console can offer.

| Catalog | Registered by REPOLib | Available game resources | Expected console catalog with this change |
| --- | ---: | ---: | ---: |
| Items | 59 | 67 item definitions | 59 |
| Valuables and cosmetic cases | 157 | 169 prefabs | 167 |

These counts describe the audited clean profile, not every mod collection or future game version. Registered mod content remains included. The 157 preset valuables consist of 39 Manor, 41 Arctic, 38 Wizard, and 39 Museum entries.

## Newly covered resources

All ten additions were present in the installed game with a root `PhotonView`. Each has either `ValuableObject` or `CosmeticWorldObject`. The latter is a physical cosmetic case, not an ordinary saleable valuable, but lives under the `valuable:` command category.

| Accepted example selector | Actual resource path |
| --- | --- |
| `valuable:Small Soul` | `Valuables/Enemy Valuable - Small` |
| `valuable:Medium Soul` | `Valuables/Enemy Valuable - Medium` |
| `valuable:Large Soul` | `Valuables/Enemy Valuable - Big` |
| `valuable:Small Money Bag` | `Valuables/Surplus Valuable - Small` |
| `valuable:Medium Money Bag` | `Valuables/Surplus Valuable - Medium` |
| `valuable:Large Money Bag` | `Valuables/Surplus Valuable - Big` |
| `valuable:Common Cosmetic Case` | `Valuables/Cosmetic World Object - Common` |
| `valuable:Uncommon Cosmetic Case` | `Valuables/Cosmetic World Object - Uncommon` |
| `valuable:Rare Cosmetic Case` | `Valuables/Cosmetic World Object - Rare` |
| `valuable:Ultra Rare Cosmetic Case` | `Valuables/Cosmetic World Object - Ultra Rare` |

The original prefab names remain valid, for example `/spawn "valuable:Cosmetic World Object - Common" 1 player-location`. The guide-friendly equivalent is `/spawn "valuable:Common Cosmetic Case" 1 player-location`. Search for `cosmetic`, `soul`, or `money bag` and use Tab to accept the canonical browser entry. The normal tracked `/despawn` path also receives the canonical name.

`RuntimeTargetCatalog` validates each extra resource before exposing it, creates a `PrefabRef` for its actual resource path, and supplies the same catalog to browsing, preview lookup, and bridge spawning. Spawning still calls REPOLib's `Valuables.SpawnValuable` or `Items.SpawnItem`; multiplayer uses the existing network prefab route. No replacement local-only spawn path was added.

### Spawn and value initialization

The installed REPOLib `Valuables.SpawnValuable` method does not require a `ValuableObject` component. Its IL validates the reference and host, calls `NetworkPrefabs.SpawnNetworkPrefab`, checks the returned `GameObject`, and logs the result. The multiplayer branch calls `PhotonNetwork.InstantiateRoomObject` with the actual resource path. The game's own cosmetic-case spawner uses this same resource-instantiation approach, without a separate caller-side initialization step. Case rarity is serialized in the prefab; its native components initialize health and register it with the round.

Souls and bags have `ValuableObject`. Its `Start` method runs the native `DollarValueSet` coroutine, which waits for level generation and a valid Photon view, initializes the value on the host, and sends `DollarValueSetRPC` to peers. All six audited resource prefabs have a non-null value preset:

| Resource family | Small preset | Medium preset | Big preset |
| --- | --- | --- | --- |
| Enemy soul | 2,000–3,000 | 3,500–4,500 | 5,500–7,500 |
| Surplus money bag | 850–1,100 | 850–1,100 | 850–1,100 |

These are serialized preset ranges before the game's rounding to hundreds. The game's extraction routine overrides a money bag's value with the actual extraction surplus. Console-spawned bags use their prefab defaults: choosing a larger bag changes its physical variant, not its payout tier. The console does not copy the current extraction surplus or invent a replacement amount. A live Small Money Bag initialized with a displayed value of $1,000, confirming initialization for that observed spawn; this is not an extraction payout or a guarantee for later spawns. The acceptance scope is recorded below.

## Name mismatches resolved

Twenty existing item targets now have explicit aliases. This changes lookup, not the number of registered items. Examples include `Pistol` → `Gun`, `Tranquilizer Gun` → `Tranq Gun`, `Taser` → `Prodzap`, `Defib` → `Defibro`, `Light Bridge` → `Phase Bridge`, and `Walkie Talkie Set` → `Semibot Walkies`. Health-pack names also work without their numeric healing suffix. The exact mapping is maintained in [CatalogAliases.cs](../src/RepoLiveControl/Runtime/CatalogAliases.cs).

Ordinary valuables accept their name without the `Valuable Manor`, `Valuable Arctic`, `Valuable Wizard`, or `Valuable Museum` resource prefix. For example, `valuable:Diamond Display` resolves to `valuable:Valuable Manor Diamond Display` in the clean profile. This also makes the README's short valuable examples executable.

Aliases are attached only to targets actually present. Canonical names win over aliases, so a modded item genuinely named `Pistol` remains addressable. If two canonical targets share a short alias, execution asks for an explicit canonical selector. Fuzzy suggestions help search; a misspelled execution command does not silently choose an object.

Some guide labels remain uncertain: the guide's Crossbow, Shock Mine, and Beamer Trap do not exactly match the installed names `Boltzap`, `Shockwave Mine`, and `Trapzap`. The latter three already exist in the canonical item list. This audit does not add speculative aliases or claim the guide's descriptions accurately identify them. Other translated or descriptive guide names can still require selecting their canonical search result.

## Deliberate exclusions

The eight extra item definitions all live under `Items/Removed Items/`: Healer Drone, Recharge Orb, Feather Orb, Healer Orb, Indestructible Orb, Magnet Orb, Roll Orb, and the old singular Semibot Walkie. Seven are disabled; the last is still in the removed-items folder. The released Semibot Walkies box is already registered. Resource fallbacks reject disabled, removed, nonphysical, unnamed, missing-prefab, and non-networked entries. Explicitly registered mod items retain precedence.

Two further resource valuables, `Valuable Manor Marble Table` and `Valuable RACING Barrel`, are outside REPOLib's presets and outside the guide additions identified here. Their intended availability and special-stage behavior have not been verified, so this change does not expose them merely because a prefab exists.

## Validation

The isolated [catalog test project](../tests/RepoLiveControl.CatalogTests/RepoLiveControl.CatalogTests.csproj) compiles the production catalog and alias source against small game fakes. Its 26 checks cover the ten additional resources, exact alias execution, canonical precedence, alias ambiguity, missing/network-invalid assets, registered-mod precedence, broken third-party entries, removed-item filtering, shared preview identity, and session cache invalidation. The production Release build compiles against the installed Unity, Photon, game, and REPOLib assemblies.

The read-only runtime audit establishes resource availability and component identity. Subsequent 2.2.0 singleplayer checks confirmed normal spawning, native grab/hold interaction, and targeted despawn for a Valuable Tracker and Manor Goblet. Small Money Bag alias lookup, preview, spawning, and value initialization were observed. A representative cosmetic case and enemy soul were also spawned in an earlier 2.2.0 run. These checks sample the three new resource families; they do not establish live behavior for all ten variants, cosmetic unlocks, extraction payouts, or peer visibility.

After the at-player enemy placement correction, the final 2.2.0 build spawned Bowtie approximately 0.5 metres from the player during a live singleplayer check. Its normal attack and tumble behavior occurred, and targeted console despawn was verified. The corrected route samples the player's nearby NavMesh instead of choosing a distant roaming point; this observation verifies local placement for that run, without establishing collision-free placement or two-client behavior. The catalog test project also includes nine placement-policy checks, for 35 checks in total, and the separate [enemy placement contract suite](../tests/RepoLiveControl.EnemyPlacementContractTests/RepoLiveControl.EnemyPlacementContractTests.csproj) verifies nine compiled route and installed-game API contracts.

The actual preview provider also completed a read-only 255-target sweep with 254 upright previews: all 59 items, all 167 valuables, and 28 enemies. Hidden has no visible model mesh and keeps its category placeholder. A real two-client acceptance run remains outstanding; test object visibility and cleanup from an independent peer before claiming multiplayer acceptance for these additions.
