using System;
using System.Collections.Generic;
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
        internal static List<RuntimeCommandTarget> GetTargets()
        {
            var targets = new List<RuntimeCommandTarget>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (Item item in Items.AllItems)
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
                foreach (PrefabRef prefab in Valuables.AllValuables)
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
            foreach (RuntimeCommandTarget target in GetTargets())
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
