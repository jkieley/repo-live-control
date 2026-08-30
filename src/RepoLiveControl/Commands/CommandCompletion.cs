using System;
using System.Collections.Generic;

namespace RepoLiveControl.Commands
{
    /// <summary>
    /// Dynamic names supplied by the game layer. Target strings may include a
    /// category prefix such as item:, valuable:, or enemy:.
    /// </summary>
    public sealed class CompletionCatalog
    {
        private static readonly CompletionCatalog EmptyCatalog =
            new CompletionCatalog(new string[0], new string[0]);

        public CompletionCatalog(IEnumerable<string> targets, IEnumerable<string> players)
            : this(targets, players, true)
        {
        }

        public CompletionCatalog(
            IEnumerable<string> targets,
            IEnumerable<string> players,
            bool includeHostManagementCommands)
            : this(
                targets,
                players,
                players,
                includeHostManagementCommands)
        {
        }

        public CompletionCatalog(
            IEnumerable<string> targets,
            IEnumerable<string> grantPlayers,
            IEnumerable<string> revokePlayers,
            bool includeHostManagementCommands)
        {
            Targets = CopyDistinct(targets);
            GrantPlayers = CopyDistinct(grantPlayers);
            RevokePlayers = CopyDistinct(revokePlayers);
            IncludeHostManagementCommands = includeHostManagementCommands;
        }

        public static CompletionCatalog Empty
        {
            get { return EmptyCatalog; }
        }

        public IReadOnlyList<string> Targets { get; private set; }

        public IReadOnlyList<string> GrantPlayers { get; private set; }

        public IReadOnlyList<string> RevokePlayers { get; private set; }

        public bool IncludeHostManagementCommands { get; private set; }

        private static IReadOnlyList<string> CopyDistinct(IEnumerable<string> values)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                        result.Add(value);
                }
            }
            return result.AsReadOnly();
        }
    }

    public sealed class CompletionItem
    {
        internal CompletionItem(
            string value,
            int score,
            int argumentIndex,
            int replacementStart,
            int replacementLength)
        {
            Value = value;
            Score = score;
            ArgumentIndex = argumentIndex;
            ReplacementStart = replacementStart;
            ReplacementLength = replacementLength;
        }

        public string Value { get; private set; }

        public int Score { get; private set; }

        /// <summary>
        /// Zero is the command name; one and above are command arguments.
        /// </summary>
        public int ArgumentIndex { get; private set; }

        public int ReplacementStart { get; private set; }

        public int ReplacementLength { get; private set; }
    }

    public sealed class CompletionApplication
    {
        internal CompletionApplication(string text, int caretPosition)
        {
            Text = text;
            CaretPosition = caretPosition;
        }

        public string Text { get; private set; }

        public int CaretPosition { get; private set; }
    }

    /// <summary>
    /// Produces command-, target-, count-, location-, and player-aware suggestions.
    /// </summary>
    public static class CommandCompletionEngine
    {
        private static readonly IReadOnlyList<string> SpawnCounts = BuildSpawnCounts();
        private static readonly IReadOnlyList<string> DespawnCounts = BuildDespawnCounts();
        private static readonly IReadOnlyList<string> Locations = Array.AsReadOnly(new[]
        {
            CommandLocations.PlayerLocation,
            CommandLocations.RandomNonCollisionLocation
        });
        private static readonly IReadOnlyList<string> SpawnCountOrLocations =
            BuildSpawnCountOrLocations();

        public static IReadOnlyList<CompletionItem> GetCompletions(
            string input,
            int caretPosition,
            CompletionCatalog catalog,
            int maxResults)
        {
            input = input ?? string.Empty;
            catalog = catalog ?? CompletionCatalog.Empty;
            if (caretPosition < 0 || caretPosition > input.Length)
                throw new ArgumentOutOfRangeException("caretPosition");
            if (maxResults <= 0)
                return Array.AsReadOnly(new CompletionItem[0]);

            CommandTokenization tokenization = CommandTokenizer.Tokenize(input);
            CompletionPosition position = FindCompletionPosition(
                input,
                caretPosition,
                tokenization.Tokens);

            IEnumerable<string> source;
            if (position.ArgumentIndex == 0)
            {
                source = GetCommandNames(catalog);
            }
            else
            {
                SlashCommandKind kind;
                if (!TryGetCommandKind(tokenization.Tokens, out kind))
                {
                    if (tokenization.Tokens.Count == 0)
                        return Array.AsReadOnly(new CompletionItem[0]);

                    position = new CompletionPosition(
                        0,
                        tokenization.Tokens[0].Start,
                        tokenization.Tokens[0].Length,
                        tokenization.Tokens[0].Value);
                    source = GetCommandNames(catalog);
                }
                else
                {
                    source = GetArgumentSource(
                        kind,
                        position.ArgumentIndex,
                        tokenization.Tokens,
                        catalog);
                }
            }

            IReadOnlyList<FuzzyMatch> matches = FuzzyMatcher.Rank(
                position.Query,
                source,
                maxResults);
            var completions = new List<CompletionItem>(matches.Count);
            foreach (FuzzyMatch match in matches)
            {
                completions.Add(new CompletionItem(
                    match.Value,
                    match.Score,
                    position.ArgumentIndex,
                    position.ReplacementStart,
                    position.ReplacementLength));
            }
            return completions.AsReadOnly();
        }

        public static CompletionApplication ApplyCompletion(
            string input,
            CompletionItem completion)
        {
            return ApplyCompletion(input, completion, false);
        }

        public static CompletionApplication ApplyCompletion(
            string input,
            CompletionItem completion,
            bool appendSpace)
        {
            input = input ?? string.Empty;
            if (completion == null)
                throw new ArgumentNullException("completion");
            if (completion.ReplacementStart < 0 ||
                completion.ReplacementLength < 0 ||
                completion.ReplacementStart + completion.ReplacementLength > input.Length)
            {
                throw new ArgumentException("The completion replacement span is outside the input.");
            }

            string replacement = CommandTokenizer.QuoteArgument(completion.Value);
            string result = input.Substring(0, completion.ReplacementStart) +
                replacement +
                input.Substring(completion.ReplacementStart + completion.ReplacementLength);
            int caret = completion.ReplacementStart + replacement.Length;

            if (appendSpace)
            {
                if (caret >= result.Length || !char.IsWhiteSpace(result[caret]))
                {
                    result = result.Insert(caret, " ");
                    caret++;
                }
                else
                {
                    caret++;
                }
            }

            return new CompletionApplication(result, caret);
        }

        private static CompletionPosition FindCompletionPosition(
            string input,
            int caretPosition,
            IReadOnlyList<CommandToken> tokens)
        {
            for (int index = 0; index < tokens.Count; index++)
            {
                CommandToken token = tokens[index];
                if (caretPosition >= token.Start && caretPosition <= token.End)
                {
                    return new CompletionPosition(
                        index,
                        token.Start,
                        token.Length,
                        token.Value);
                }
            }

            int argumentIndex = 0;
            foreach (CommandToken token in tokens)
            {
                if (token.End <= caretPosition)
                    argumentIndex++;
            }

            return new CompletionPosition(argumentIndex, caretPosition, 0, string.Empty);
        }

        private static bool TryGetCommandKind(
            IReadOnlyList<CommandToken> tokens,
            out SlashCommandKind kind)
        {
            kind = SlashCommandKind.Help;
            if (tokens.Count == 0)
                return false;

            switch (tokens[0].Value.ToLowerInvariant())
            {
                case "/spawn":
                    kind = SlashCommandKind.Spawn;
                    return true;
                case "/despawn":
                    kind = SlashCommandKind.Despawn;
                    return true;
                case "/grant":
                    kind = SlashCommandKind.Grant;
                    return true;
                case "/revoke":
                    kind = SlashCommandKind.Revoke;
                    return true;
                case "/permissions":
                    kind = SlashCommandKind.Permissions;
                    return true;
                case "/help":
                    kind = SlashCommandKind.Help;
                    return true;
                default:
                    return false;
            }
        }

        private static IEnumerable<string> GetArgumentSource(
            SlashCommandKind kind,
            int argumentIndex,
            IReadOnlyList<CommandToken> tokens,
            CompletionCatalog catalog)
        {
            switch (kind)
            {
                case SlashCommandKind.Spawn:
                    if (argumentIndex == 1)
                        return GetSpawnTargets(catalog.Targets);
                    if (argumentIndex == 2)
                        return SpawnCountOrLocations;
                    if (argumentIndex == 3)
                    {
                        if (tokens.Count >= 3 && IsLocation(tokens[2].Value))
                            return new string[0];
                        return Locations;
                    }
                    break;
                case SlashCommandKind.Despawn:
                    if (argumentIndex == 1)
                        return catalog.Targets;
                    if (argumentIndex == 2)
                        return DespawnCounts;
                    break;
                case SlashCommandKind.Grant:
                    if (argumentIndex == 1 && catalog.IncludeHostManagementCommands)
                        return catalog.GrantPlayers;
                    break;
                case SlashCommandKind.Revoke:
                    if (argumentIndex == 1 && catalog.IncludeHostManagementCommands)
                        return catalog.RevokePlayers;
                    break;
            }
            return new string[0];
        }

        private static bool IsLocation(string value)
        {
            foreach (string location in Locations)
            {
                if (location.Equals(value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> GetSpawnTargets(IEnumerable<string> targets)
        {
            foreach (string target in targets)
            {
                if (!target.EndsWith(":all", StringComparison.OrdinalIgnoreCase))
                    yield return target;
            }
        }

        private static IReadOnlyList<string> BuildSpawnCounts()
        {
            var counts = new List<string>();
            int[] common = { 1, 5, 10, 25, 50, 100, 250, 500 };
            foreach (int count in common)
                counts.Add(count.ToString(System.Globalization.CultureInfo.InvariantCulture));

            for (int count = SlashCommandParser.MinimumCount;
                count <= SlashCommandParser.MaximumCount;
                count++)
            {
                string value = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!counts.Contains(value))
                    counts.Add(value);
            }
            return counts.AsReadOnly();
        }

        private static IReadOnlyList<string> BuildDespawnCounts()
        {
            var counts = new List<string> { "all" };
            foreach (string count in SpawnCounts)
                counts.Add(count);
            return counts.AsReadOnly();
        }

        private static IEnumerable<string> GetCommandNames(CompletionCatalog catalog)
        {
            foreach (string commandName in SlashCommandParser.CommandNames)
            {
                if (!catalog.IncludeHostManagementCommands &&
                    (commandName.Equals("/grant", StringComparison.OrdinalIgnoreCase) ||
                     commandName.Equals("/revoke", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                yield return commandName;
            }
        }

        private static IReadOnlyList<string> BuildSpawnCountOrLocations()
        {
            var values = new List<string>(SpawnCounts);
            foreach (string location in Locations)
                values.Add(location);
            return values.AsReadOnly();
        }

        private sealed class CompletionPosition
        {
            internal CompletionPosition(
                int argumentIndex,
                int replacementStart,
                int replacementLength,
                string query)
            {
                ArgumentIndex = argumentIndex;
                ReplacementStart = replacementStart;
                ReplacementLength = replacementLength;
                Query = query;
            }

            internal int ArgumentIndex { get; private set; }

            internal int ReplacementStart { get; private set; }

            internal int ReplacementLength { get; private set; }

            internal string Query { get; private set; }
        }
    }
}
