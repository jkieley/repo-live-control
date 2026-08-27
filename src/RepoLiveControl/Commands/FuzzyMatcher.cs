using System;
using System.Collections.Generic;

namespace RepoLiveControl.Commands
{
    public sealed class FuzzyMatch
    {
        internal FuzzyMatch(string value, int score, int originalIndex)
        {
            Value = value;
            Score = score;
            OriginalIndex = originalIndex;
        }

        public string Value { get; private set; }

        public int Score { get; private set; }

        internal int OriginalIndex { get; private set; }
    }

    /// <summary>
    /// Ranks exact, prefix, substring, subsequence, and small typo matches.
    /// Larger scores are better; NoMatch means the candidate should be omitted.
    /// </summary>
    public static class FuzzyMatcher
    {
        public const int NoMatch = int.MinValue;

        public static int Score(string query, string candidate)
        {
            query = Normalize(query);
            candidate = Normalize(candidate);

            if (candidate.Length == 0)
                return NoMatch;
            if (query.Length == 0)
                return 1;

            List<SearchVariant> queryVariants = BuildVariants(query, false);
            List<SearchVariant> candidateVariants = BuildVariants(candidate, true);
            int best = NoMatch;

            foreach (SearchVariant queryVariant in queryVariants)
            {
                foreach (SearchVariant candidateVariant in candidateVariants)
                {
                    int score = ScoreVariant(queryVariant.Text, candidateVariant.Text);
                    if (score == NoMatch)
                        continue;

                    score -= queryVariant.Penalty + candidateVariant.Penalty;
                    if (score > best)
                        best = score;
                }
            }

            return best;
        }

        public static IReadOnlyList<FuzzyMatch> Rank(
            string query,
            IEnumerable<string> candidates,
            int maxResults)
        {
            if (candidates == null)
                throw new ArgumentNullException("candidates");
            if (maxResults <= 0)
                return Array.AsReadOnly(new FuzzyMatch[0]);

            var matches = new List<FuzzyMatch>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int originalIndex = 0;
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    int score = Score(query, candidate);
                    if (score != NoMatch)
                        matches.Add(new FuzzyMatch(candidate, score, originalIndex));
                }
                originalIndex++;
            }

            matches.Sort(CompareMatches);
            if (matches.Count > maxResults)
                matches.RemoveRange(maxResults, matches.Count - maxResults);
            return matches.AsReadOnly();
        }

        private static int CompareMatches(FuzzyMatch left, FuzzyMatch right)
        {
            int byScore = right.Score.CompareTo(left.Score);
            if (byScore != 0)
                return byScore;
            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static int ScoreVariant(string query, string candidate)
        {
            if (query.Equals(candidate, StringComparison.Ordinal))
                return 100000;

            if (candidate.StartsWith(query, StringComparison.Ordinal))
                return 90000 - (candidate.Length - query.Length) * 5;

            int substringIndex = candidate.IndexOf(query, StringComparison.Ordinal);
            if (substringIndex >= 0)
            {
                return 80000 - substringIndex * 50 -
                    (candidate.Length - query.Length) * 3;
            }

            int subsequenceScore = ScoreSubsequence(query, candidate);
            if (subsequenceScore != NoMatch)
                return subsequenceScore;

            if (query.Length < 3)
                return NoMatch;

            int threshold = query.Length <= 4 ? 1 : query.Length <= 8 ? 2 : 3;
            if (Math.Abs(query.Length - candidate.Length) > threshold)
                return NoMatch;

            int distance = DamerauLevenshteinDistance(query, candidate);
            if (distance > threshold)
                return NoMatch;

            return 60000 - distance * 1000 -
                Math.Abs(candidate.Length - query.Length) * 20;
        }

        private static int ScoreSubsequence(string query, string candidate)
        {
            if (query.Length > candidate.Length)
                return NoMatch;

            int queryIndex = 0;
            int firstMatch = -1;
            int lastMatch = -1;
            int gaps = 0;

            for (int candidateIndex = 0;
                candidateIndex < candidate.Length && queryIndex < query.Length;
                candidateIndex++)
            {
                if (candidate[candidateIndex] != query[queryIndex])
                    continue;

                if (firstMatch < 0)
                    firstMatch = candidateIndex;
                if (lastMatch >= 0)
                    gaps += candidateIndex - lastMatch - 1;
                lastMatch = candidateIndex;
                queryIndex++;
            }

            if (queryIndex != query.Length)
                return NoMatch;

            return 70000 - firstMatch * 30 - gaps * 40 -
                (candidate.Length - query.Length) * 2;
        }

        private static int DamerauLevenshteinDistance(string left, string right)
        {
            var distances = new int[left.Length + 1, right.Length + 1];
            for (int leftIndex = 0; leftIndex <= left.Length; leftIndex++)
                distances[leftIndex, 0] = leftIndex;
            for (int rightIndex = 0; rightIndex <= right.Length; rightIndex++)
                distances[0, rightIndex] = rightIndex;

            for (int leftIndex = 1; leftIndex <= left.Length; leftIndex++)
            {
                for (int rightIndex = 1; rightIndex <= right.Length; rightIndex++)
                {
                    int substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                    int deletion = distances[leftIndex - 1, rightIndex] + 1;
                    int insertion = distances[leftIndex, rightIndex - 1] + 1;
                    int substitution = distances[leftIndex - 1, rightIndex - 1] + substitutionCost;
                    int best = Math.Min(Math.Min(deletion, insertion), substitution);

                    if (leftIndex > 1 && rightIndex > 1 &&
                        left[leftIndex - 1] == right[rightIndex - 2] &&
                        left[leftIndex - 2] == right[rightIndex - 1])
                    {
                        best = Math.Min(best, distances[leftIndex - 2, rightIndex - 2] + 1);
                    }

                    distances[leftIndex, rightIndex] = best;
                }
            }

            return distances[left.Length, right.Length];
        }

        private static List<SearchVariant> BuildVariants(string value, bool splitSegments)
        {
            var variants = new List<SearchVariant>();
            AddVariant(variants, value, 0);

            if (value.Length > 0 && value[0] == '/')
                AddVariant(variants, value.Substring(1), 10);

            if (splitSegments)
            {
                int colon = value.IndexOf(':');
                if (colon >= 0 && colon + 1 < value.Length)
                    AddVariant(variants, value.Substring(colon + 1), 20);

                string[] segments = value.Split(new[]
                {
                    ':', '/', '-', '_', '.', ' ', '\t'
                }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string segment in segments)
                    AddVariant(variants, segment, 40);

                var compact = new System.Text.StringBuilder(value.Length);
                foreach (char current in value)
                {
                    if (char.IsLetterOrDigit(current))
                        compact.Append(current);
                }
                AddVariant(variants, compact.ToString(), 60);
            }

            return variants;
        }

        private static void AddVariant(List<SearchVariant> variants, string value, int penalty)
        {
            if (string.IsNullOrEmpty(value))
                return;

            foreach (SearchVariant existing in variants)
            {
                if (existing.Text.Equals(value, StringComparison.Ordinal))
                    return;
            }
            variants.Add(new SearchVariant(value, penalty));
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private sealed class SearchVariant
        {
            internal SearchVariant(string text, int penalty)
            {
                Text = text;
                Penalty = penalty;
            }

            internal string Text { get; private set; }

            internal int Penalty { get; private set; }
        }
    }
}
