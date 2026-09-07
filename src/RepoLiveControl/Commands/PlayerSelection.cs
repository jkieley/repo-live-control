using System;
using System.Collections.Generic;
using System.Linq;

namespace RepoLiveControl.Commands
{
    public sealed class PlayerChoice
    {
        public PlayerChoice(string name, int actorNumber)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Player" : name;
            ActorNumber = actorNumber;
        }
        public string Name { get; private set; }
        public int ActorNumber { get; private set; }
        public string Selector { get { return Name + "#" + (ActorNumber > 0 ? ActorNumber.ToString() : "local"); } }
    }

    public static class PlayerSelection
    {
        public static IReadOnlyList<PlayerChoice> Resolve(string selector, IEnumerable<PlayerChoice> players)
        {
            var choices = players.ToList();
            if (string.Equals(selector, "all", StringComparison.OrdinalIgnoreCase)) return choices.AsReadOnly();
            var matches = choices.Where(player => player.Selector.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
                matches = choices.Where(player => player.Name.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException("Player name is ambiguous. Select a name#actor entry with Tab.");
            if (matches.Count == 0)
                throw new InvalidOperationException("No current player matches '" + selector + "'. Use fuzzy autocomplete and Tab to select a player.");
            return matches.AsReadOnly();
        }
    }
}
