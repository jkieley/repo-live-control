using RepoLiveControl.Runtime;
using REPOLib.Modules;
using UnityEngine;
using Photon.Pun;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
void Reset()
{
    Resources.Values.Clear(); Resources.ResourceItems = Array.Empty<Item>();
    Items.AllItems.Clear(); Valuables.AllValuables.Clear(); Enemies.AllEnemies.Clear();
    RunManager.instance = new RunManager();
}
PrefabRef AddPrefab(string path, string name, bool network = true, bool cosmetic = false)
{
    var prefab = new GameObject { name = name };
    if (network) prefab.With<PhotonView>();
    if (cosmetic) prefab.With<CosmeticWorldObject>(); else prefab.With<ValuableObject>();
    Resources.Values[path] = prefab;
    var reference = new PrefabRef(); reference.SetPrefab(prefab, path); return reference;
}
Item AddItem(string name, string path, bool registered = true, bool disabled = false)
{
    var item = new Item { name = name, itemName = name, disabled = disabled, prefab = AddPrefab(path, name) };
    if (registered) Items.AllItems.Add(item);
    return item;
}
RuntimeCommandTarget Resolve(string selector)
{
    Check(RuntimeTargetCatalog.TryResolve(selector, false, out var target, out var error), error);
    return target;
}

// The three special families live outside REPOLib's preset list. Every exact
// network resource is exposed once and the preview points at that same prefab.
Reset();
foreach (var size in new[] { "Small", "Medium", "Big" })
{
    AddPrefab("Valuables/Enemy Valuable - " + size, "Enemy Valuable - " + size);
    AddPrefab("Valuables/Surplus Valuable - " + size, "Surplus Valuable - " + size);
}
foreach (var rarity in new[] { "Common", "Uncommon", "Rare", "Ultra Rare" })
    AddPrefab("Valuables/Cosmetic World Object - " + rarity, "Cosmetic World Object - " + rarity, cosmetic: true);
Check(RuntimeTargetCatalog.GetValuables().Count == 10, "All ten non-preset network resources must be included.");
Check(Resolve("valuable:Common Cosmetic Case").Name == "Cosmetic World Object - Common", "Cosmetic guide alias must resolve to the real prefab name.");
Check(Resolve("valuable:Small Soul").Name == "Enemy Valuable - Small", "Soul alias must preserve the spawn resource identity.");
Check(Resolve("valuable:Large Money Bag").Name == "Surplus Valuable - Big", "Money bag alias must preserve the spawn resource identity.");
Check(RuntimeTargetCatalog.TryGetPreviewSource("valuable:Common Cosmetic Box", out var preview, out _) && ReferenceEquals(preview, Resources.Values["Valuables/Cosmetic World Object - Common"]), "Preview must use the same network prefab as alias resolution.");
Check(!RuntimeTargetCatalog.TryGetPreviewSource("valuable:all", out _, out _), "Despawn wildcard must never acquire a spawn preview.");

// Merely finding a named asset does not prove it is a networked spawn target.
Reset();
AddPrefab("Valuables/Enemy Valuable - Small", "Enemy Valuable - Small", network: false);
Check(RuntimeTargetCatalog.GetValuables().Count == 0, "Reject special resources without a root PhotonView.");
Check(!RuntimeTargetCatalog.TryResolve("valuable:Small Soul", false, out _, out _), "An alias cannot manufacture a missing target.");

// Preserve registered mod precedence and make a broken entry local to that entry.
Reset();
var gun = AddItem("Gun", "Mod/Gun");
var duplicate = AddItem("Gun", "Items/Fallback Gun", registered: false);
var fallback = AddItem("Leaf Blower", "Items/Item Leaf Blower", registered: false);
var removed = AddItem("Semibot Walkie", "Items/Removed Items/Item WalkieTalkie", registered: false);
var disabled = AddItem("Healer Orb", "Items/Item Orb Heal", registered: false, disabled: true);
var nonphysical = AddItem("Not a physical item", "Items/NotPhysical", registered: false); nonphysical.physicalItem = false;
Resources.ResourceItems = new[] { duplicate, fallback, removed, disabled, nonphysical };
var brokenReference = AddPrefab("Mod/Broken", "Broken"); brokenReference.Broken = true;
Items.AllItems.Insert(0, new Item { itemName = "Broken", prefab = brokenReference });
Check(RuntimeTargetCatalog.GetItems().Count == 2, "Include missing valid items, preserve registered precedence, and exclude removed/disabled/nonphysical fallbacks.");
Check(ReferenceEquals(RuntimeTargetCatalog.GetItems().Single(item => item.itemName == "Gun"), gun), "Registered item must win over resource fallback.");
Check(Resolve("item:Pistol").Name == "Gun", "Guide item alias should execute the registered canonical item.");
Check(RuntimeTargetCatalog.GetSearchAliases()["item:Gun"].Contains("item:Pistol"), "Alias metadata must enable canonical completion search.");
Check(!RuntimeTargetCatalog.GetSelectors(false).Contains("item:Pistol"), "Aliases must not create duplicate browse rows.");
var namedPistol = AddItem("Pistol", "Mod/Pistol");
Check(Resolve("item:Pistol").Name == "Pistol", "A real canonical mod target must outrank another target's alias.");

// Short names aid existing README commands without guessing ambiguous matches.
Reset();
Valuables.AllValuables.Add(AddPrefab("Valuables/Manor/Display", "Valuable Manor Diamond Display"));
Check(Resolve("valuable:Diamond Display").Name == "Valuable Manor Diamond Display", "Short valuable aliases must map back to canonical tracking names.");
Valuables.AllValuables.Add(AddPrefab("Valuables/Museum/Display", "Valuable Museum Diamond Display"));
Check(!RuntimeTargetCatalog.TryResolve("valuable:Diamond Display", false, out _, out var ambiguous) && ambiguous.Contains("ambiguous"), "Ambiguous aliases must require a qualified canonical choice.");
Check(Resolve("valuable:Valuable Museum Diamond Display").Name == "Valuable Museum Diamond Display", "Original canonical names remain accepted.");
Check(!RuntimeTargetCatalog.TryResolve("valuable:Diamond Displa", false, out _, out _), "Execution must not silently select a fuzzy near-match.");
RunManager.instance = null;
Resources.Values.Clear();
Check(RuntimeTargetCatalog.GetValuables().Count == 0, "A stale resource cache must not survive the loss of its RunManager.");

Console.WriteLine($"PASS {checks} production catalog coverage, alias, validation, preview identity, and session-cache checks.");
