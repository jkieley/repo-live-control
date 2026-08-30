using System;
using System.Collections.Generic;

namespace RepoLiveControl.Commands
{
    /// <summary>
    /// Builds a bounded, counted summary for large spawn jobs. This keeps a
    /// valid high-count command from flooding network responses or the console.
    /// </summary>
    public sealed class SpawnNameSummary
    {
        public const int MaximumDisplayedNames = 8;

        private readonly Dictionary<string, int> counts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> orderedNames = new List<string>();

        public void Add(string name, int count)
        {
            if (string.IsNullOrWhiteSpace(name) || count <= 0)
                return;

            int existing;
            if (counts.TryGetValue(name, out existing))
            {
                counts[name] = existing + count;
                return;
            }

            counts.Add(name, count);
            orderedNames.Add(name);
        }

        public string Format()
        {
            var parts = new List<string>();
            int displayed = Math.Min(orderedNames.Count, MaximumDisplayedNames);
            for (int index = 0; index < displayed; index++)
            {
                string name = orderedNames[index];
                int count = counts[name];
                parts.Add(count == 1 ? name : name + " x" + count);
            }

            int omitted = orderedNames.Count - displayed;
            if (omitted > 0)
                parts.Add("+" + omitted + " more name(s)");

            return string.Join(", ", parts.ToArray());
        }
    }
}
