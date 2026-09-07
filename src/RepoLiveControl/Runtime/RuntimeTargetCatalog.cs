using System;
using System.Collections.Generic;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;

namespace RepoLiveControl.Runtime
{
    internal enum CommandEntityKind
    {
        Item,
        Valuable,
        Enemy
    }

    internal sealed class RuntimeCommandTarget
    {
        internal readonly CommandEntityKind Kind;
        internal readonly string Name;

        internal RuntimeCommandTarget(CommandEntityKind kind, string name)
        {
            Kind = kind;
            Name = name;
        }

        internal string KindName
        {
            get
            {
                if (Kind == CommandEntityKind.Enemy)
                    return "enemy";
                if (Kind == CommandEntityKind.Valuable)
                    return "valuable";
                return "item";
            }
        }

        internal string Selector { get { return KindName + ":" + Name; } }
    }

    internal static class RuntimeTargetCatalog
    {
        private static RunManager resourceOwner;
        private static Item[] resourceItems = new Item[0];
        private static readonly List<PrefabRef> resourceValuables = new List<PrefabRef>();

        // These are networked game resources, not ordinary level-preset valuables.
        // In particular, REPOLib's AllValuables does not enumerate reward orbs or boxes.
        private static readonly string[] AdditionalValuablePaths =
        {
            "Valuables/Enemy Valuable - Small", "Valuables/Enemy Valuable - Medium",
            "Valuables/Enemy Valuable - Big", "Valuables/Surplus Valuable - Small",
            "Valuables/Surplus Valuable - Medium", "Valuables/Surplus Valuable - Big",
            "Valuables/Cosmetic World Object - Common", "Valuables/Cosmetic World Object - Uncommon",
            "Valuables/Cosmetic World Object - Rare", "Valuables/Cosmetic World Object - Ultra Rare"
        };

        private static void LoadResourceCatalog()
        {
            RunManager owner = RunManager.instance;
            if (ReferenceEquals(resourceOwner, owner))
                return;
            resourceOwner = owner;
            resourceItems = new Item[0];
            resourceValuables.Clear();
            if (owner == null)
                return;

            try { resourceItems = Resources.LoadAll<Item>("Items"); }
            catch { }
            foreach (string path in AdditionalValuablePaths)
            {
                try
                {
                    GameObject prefab = Resources.Load<GameObject>(path);
                    if (!IsNetworkPrefab(prefab))
                        continue;
                    if (prefab.GetComponentInChildren<ValuableObject>(true) == null &&
                        prefab.GetComponentInChildren<CosmeticWorldObject>(true) == null)
                        continue;
                    var reference = new PrefabRef();
                    reference.SetPrefab(prefab, path);
                    resourceValuables.Add(reference);
                }
                catch { }
            }
        }

        internal static List<Item> GetItems()
        {
            LoadResourceCatalog();
            var items = new List<Item>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Registered mod items win over resource fallbacks with the same name.
            try
            {
                foreach (Item item in Items.AllItems)
                    AddItem(items, seen, item, false);
            }
            catch { }
            foreach (Item item in resourceItems)
                AddItem(items, seen, item, true);
            return items;
        }

        private static void AddItem(List<Item> items, HashSet<string> seen, Item item, bool fallback)
        {
            try
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemName) || item.prefab == null)
                    return;
                string path = item.prefab.ResourcePath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path) || !IsNetworkPrefab(item.prefab.Prefab))
                    return;
                if (fallback && (item.disabled || !item.physicalItem ||
                    path.IndexOf("/Removed Items/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.itemName.Equals("N/A", StringComparison.OrdinalIgnoreCase)))
                    return;
                if (seen.Add(item.itemName.Trim()))
                    items.Add(item);
            }
            catch { } // One broken third-party entry must not hide the rest of the catalog.
        }

        internal static List<PrefabRef> GetValuables()
        {
            LoadResourceCatalog();
            var prefabs = new List<PrefabRef>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (PrefabRef prefab in Valuables.AllValuables)
                    AddValuable(prefabs, seen, prefab);
            }
            catch { }
            foreach (PrefabRef prefab in resourceValuables)
                AddValuable(prefabs, seen, prefab);
            return prefabs;
        }

        private static void AddValuable(List<PrefabRef> prefabs, HashSet<string> seen, PrefabRef reference)
        {
            try
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.ResourcePath))
                    return;
                GameObject prefab = reference.Prefab;
                if (IsNetworkPrefab(prefab) && seen.Add(NormalizeObjectName(prefab.name)))
                    prefabs.Add(reference);
            }
            catch { }
        }

        private static bool IsNetworkPrefab(GameObject prefab)
        {
            return prefab != null && prefab.GetComponent<PhotonView>() != null;
        }

        internal static bool TryGetPreviewSource(string selector, out GameObject prefab, out Sprite icon)
        {
            prefab = null;
            icon = null;
            RuntimeCommandTarget target;
            string error;
            if (!TryResolve(selector, false, out target, out error))
                return false;
            try
            {
                if (target.Kind == CommandEntityKind.Item)
                {
                    foreach (Item item in GetItems())
                        if (item.itemName.Trim().Equals(target.Name, StringComparison.OrdinalIgnoreCase))
                        { prefab = item.prefab.Prefab; break; }
                }
                else if (target.Kind == CommandEntityKind.Valuable)
                {
                    foreach (PrefabRef reference in GetValuables())
                        if (NormalizeObjectName(reference.Prefab.name).Equals(target.Name, StringComparison.OrdinalIgnoreCase))
                        { prefab = reference.Prefab; break; }
                }
                else
                {
                    foreach (EnemySetup setup in Enemies.AllEnemies)
                    {
                        EnemyParent parent = setup == null ? null : Bridge.GetEnemyParent(setup);
                        if (parent != null && (parent.enemyName ?? string.Empty).Trim().Equals(target.Name, StringComparison.OrdinalIgnoreCase))
                        { prefab = parent.gameObject; break; }
                    }
                }
            }
            catch { return false; }
            return prefab != null;
        }

        internal static List<RuntimeCommandTarget> GetTargets()
        {
            var targets = new List<RuntimeCommandTarget>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (Item item in GetItems())
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.itemName))
                        continue;
                    Add(targets, seen, CommandEntityKind.Item, item.itemName.Trim());
                }
            }
            catch
            {
            }

            try
            {
                foreach (PrefabRef prefab in GetValuables())
                {
                    if (prefab == null || prefab.Prefab == null)
                        continue;
                    Add(targets, seen, CommandEntityKind.Valuable,
                        NormalizeObjectName(prefab.Prefab.name));
                }
            }
            catch
            {
            }

            try
            {
                foreach (EnemySetup setup in Enemies.AllEnemies)
                {
                    EnemyParent parent = setup == null ? null : Bridge.GetEnemyParent(setup);
                    if (parent == null || string.IsNullOrWhiteSpace(parent.enemyName))
                        continue;
                    Add(targets, seen, CommandEntityKind.Enemy, parent.enemyName.Trim());
                }
            }
            catch
            {
            }

            targets.Sort((left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.Selector, right.Selector));
            return targets;
        }

        internal static List<string> GetSelectors(bool includeAll)
        {
            var values = new List<string>();
            if (includeAll)
            {
                values.Add("enemy:all");
                values.Add("item:all");
                values.Add("valuable:all");
            }
            foreach (RuntimeCommandTarget target in GetTargets())
                values.Add(target.Selector);
            return values;
        }

        internal static Dictionary<string, string[]> GetSearchAliases()
        {
            var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (RuntimeCommandTarget target in GetTargets())
            {
                string[] names = CatalogAliases.ForTarget(target.Kind, target.Name);
                if (names.Length == 0)
                    continue;
                var qualified = new string[names.Length];
                for (int index = 0; index < names.Length; index++)
                    qualified[index] = target.KindName + ":" + names[index];
                aliases[target.Selector] = qualified;
            }
            return aliases;
        }

        internal static bool TryResolve(
            string selector,
            bool allowAll,
            out RuntimeCommandTarget selected,
            out string error)
        {
            selected = null;
            error = string.Empty;
            string query = (selector ?? string.Empty).Trim();
            if (query.Length == 0)
            {
                error = "ERROR A target is required.";
                return false;
            }

            CommandEntityKind? kind = null;
            int colon = query.IndexOf(':');
            if (colon > 0)
            {
                string prefix = query.Substring(0, colon).Trim();
                query = query.Substring(colon + 1).Trim();
                if (prefix.Equals("enemy", StringComparison.OrdinalIgnoreCase))
                    kind = CommandEntityKind.Enemy;
                else if (prefix.Equals("valuable", StringComparison.OrdinalIgnoreCase) ||
                         prefix.Equals("loot", StringComparison.OrdinalIgnoreCase))
                    kind = CommandEntityKind.Valuable;
                else if (prefix.Equals("item", StringComparison.OrdinalIgnoreCase))
                    kind = CommandEntityKind.Item;
                else
                {
                    error = "ERROR Unknown target kind '" + prefix +
                            "'. Use item:, valuable:, or enemy:.";
                    return false;
                }
            }

            if (allowAll && query.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (!kind.HasValue)
                {
                    error = "ERROR Qualify all as item:all, valuable:all, or enemy:all.";
                    return false;
                }
                selected = new RuntimeCommandTarget(kind.Value, "all");
                return true;
            }

            var exact = new List<RuntimeCommandTarget>();
            List<RuntimeCommandTarget> available = GetTargets();
            foreach (RuntimeCommandTarget target in available)
            {
                if (kind.HasValue && target.Kind != kind.Value)
                    continue;
                if (target.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                    exact.Add(target);
            }

            if (exact.Count == 1)
            {
                selected = exact[0];
                return true;
            }
            if (exact.Count > 1)
            {
                var options = new List<string>();
                foreach (RuntimeCommandTarget target in exact)
                    options.Add(target.Selector);
                error = "ERROR Target is ambiguous. Use one of: " +
                        string.Join(", ", options.ToArray()) + ".";
                return false;
            }

            // Canonical names always win; an alias cannot shadow a modded item.
            foreach (RuntimeCommandTarget target in available)
            {
                if (kind.HasValue && target.Kind != kind.Value)
                    continue;
                foreach (string alias in CatalogAliases.ForTarget(target.Kind, target.Name))
                {
                    if (alias.Equals(query, StringComparison.OrdinalIgnoreCase))
                    { exact.Add(target); break; }
                }
            }
            if (exact.Count == 1)
            {
                selected = exact[0];
                return true;
            }
            if (exact.Count > 1)
            {
                var options = new List<string>();
                foreach (RuntimeCommandTarget target in exact)
                    options.Add(target.Selector);
                error = "ERROR Alias is ambiguous. Use one of: " + string.Join(", ", options.ToArray()) + ".";
                return false;
            }

            error = "ERROR No canonical target matches '" + selector +
                    "'. Choose a fuzzy autocomplete suggestion with Tab before executing.";
            return false;
        }

        private static void Add(
            List<RuntimeCommandTarget> targets,
            HashSet<string> seen,
            CommandEntityKind kind,
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            string key = kind + "\0" + name;
            if (seen.Add(key))
                targets.Add(new RuntimeCommandTarget(kind, name));
        }

        private static string NormalizeObjectName(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            while (normalized.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 7).Trim();
            return normalized;
        }
    }
}
