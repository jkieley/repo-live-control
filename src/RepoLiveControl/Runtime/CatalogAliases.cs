using System;
using System.Collections.Generic;

namespace RepoLiveControl.Runtime
{
    // Aliases describe existing targets. They never manufacture or register a prefab.
    internal static class CatalogAliases
    {
        private static readonly Dictionary<string, string[]> Known =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "item:Small Health Pack (25)", new[] { "Small Health Pack" } },
                { "item:Medium Health Pack (50)", new[] { "Medium Health Pack" } },
                { "item:Large Health Pack (100)", new[] { "Large Health Pack" } },
                { "item:Gun", new[] { "Pistol" } },
                { "item:Tranq Gun", new[] { "Tranquilizer Gun" } },
                { "item:Prodzap", new[] { "Taser", "Stun Baton" } },
                { "item:Sledge Hammer", new[] { "Sledgehammer" } },
                { "item:Duct Taped Grenades", new[] { "Grenade Bundle" } },
                { "item:Shockwave Grenade", new[] { "Impact Grenade" } },
                { "item:Explosive Mine", new[] { "Landmine" } },
                { "item:Indestructible Drone", new[] { "Durability Drone" } },
                { "item:Energy Crystal", new[] { "Power Crystal" } },
                { "item:Zero Gravity Orb", new[] { "Zero Gravity Sphere" } },
                { "item:Extraction Tracker", new[] { "Dropoff Point Radar" } },
                { "item:Valuable Tracker", new[] { "Value Tracker" } },
                { "item:Phase Bridge", new[] { "Light Bridge" } },
                { "item:Semibot Walkies", new[] { "Walkie Talkie Set" } },
                { "item:Defibro", new[] { "Defib", "Defibrillator" } },
                { "item:Rubber Duck", new[] { "Rubber Ducky" } },
                { "item:Tumble Climb Upgrade", new[] { "Wall Climber", "Appearance (Wall Climber)" } },
                { "valuable:Enemy Valuable - Small", new[] { "Small Soul", "Small Enemy Orb" } },
                { "valuable:Enemy Valuable - Medium", new[] { "Medium Soul", "Medium Enemy Orb" } },
                { "valuable:Enemy Valuable - Big", new[] { "Large Soul", "Large Enemy Orb" } },
                { "valuable:Surplus Valuable - Small", new[] { "Small Money Bag", "Small Surplus Bag" } },
                { "valuable:Surplus Valuable - Medium", new[] { "Medium Money Bag", "Medium Surplus Bag" } },
                { "valuable:Surplus Valuable - Big", new[] { "Large Money Bag", "Large Surplus Bag" } },
                { "valuable:Cosmetic World Object - Common", new[] { "Common Cosmetic Case", "Common Cosmetic Box" } },
                { "valuable:Cosmetic World Object - Uncommon", new[] { "Uncommon Cosmetic Case", "Uncommon Cosmetic Box" } },
                { "valuable:Cosmetic World Object - Rare", new[] { "Rare Cosmetic Case", "Rare Cosmetic Box" } },
                { "valuable:Cosmetic World Object - Ultra Rare", new[] { "Ultra Rare Cosmetic Case", "Ultra Rare Cosmetic Box" } }
            };

        internal static string[] ForTarget(CommandEntityKind kind, string name)
        {
            string prefix = kind == CommandEntityKind.Item ? "item:" :
                kind == CommandEntityKind.Valuable ? "valuable:" : "enemy:";
            var values = new List<string>();
            string[] known;
            if (Known.TryGetValue(prefix + name, out known))
                values.AddRange(known);
            if (kind == CommandEntityKind.Valuable)
            {
                foreach (string region in new[] { "Manor", "Arctic", "Wizard", "Museum" })
                {
                    string resourcePrefix = "Valuable " + region + " ";
                    if (name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(name.Substring(resourcePrefix.Length));
                        break;
                    }
                }
            }
            return values.ToArray();
        }
    }
}
