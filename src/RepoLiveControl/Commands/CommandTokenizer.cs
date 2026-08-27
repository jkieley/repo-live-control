using System;
using System.Collections.Generic;
using System.Text;

namespace RepoLiveControl.Commands
{
    /// <summary>
    /// A decoded command token and its raw, end-exclusive span in the input text.
    /// </summary>
    public sealed class CommandToken
    {
        internal CommandToken(string value, int start, int length, bool isQuoted)
        {
            Value = value;
            Start = start;
            Length = length;
            IsQuoted = isQuoted;
        }

        public string Value { get; private set; }

        public int Start { get; private set; }

        public int Length { get; private set; }

        public int End
        {
            get { return Start + Length; }
        }

        public bool IsQuoted { get; private set; }
    }

    /// <summary>
    /// The result of tokenizing command text. Tokens are still returned for an
    /// unterminated quote so the completion UI can repair partially typed input.
    /// </summary>
    public sealed class CommandTokenization
    {
        internal CommandTokenization(List<CommandToken> tokens, bool hasUnterminatedQuote)
        {
            Tokens = tokens.AsReadOnly();
            HasUnterminatedQuote = hasUnterminatedQuote;
        }

        public IReadOnlyList<CommandToken> Tokens { get; private set; }

        public bool HasUnterminatedQuote { get; private set; }
    }

    /// <summary>
    /// Splits slash-command text while decoding single- or double-quoted values.
    /// Within a quoted value, a backslash may escape the active quote or another
    /// backslash. Token spans always include any quote characters in the raw input.
    /// </summary>
    public static class CommandTokenizer
    {
        public static CommandTokenization Tokenize(string input)
        {
            input = input ?? string.Empty;
            var tokens = new List<CommandToken>();
            bool hasUnterminatedQuote = false;
            int index = 0;

            while (index < input.Length)
            {
                while (index < input.Length && char.IsWhiteSpace(input[index]))
                    index++;

                if (index >= input.Length)
                    break;

                int start = index;
                char activeQuote = '\0';
                bool isQuoted = false;
                var value = new StringBuilder();

                while (index < input.Length)
                {
                    char current = input[index];

                    if (activeQuote != '\0')
                    {
                        if (current == activeQuote)
                        {
                            activeQuote = '\0';
                            index++;
                            continue;
                        }

                        if (current == '\\' && index + 1 < input.Length)
                        {
                            char escaped = input[index + 1];
                            if (escaped == activeQuote || escaped == '\\')
                            {
                                value.Append(escaped);
                                index += 2;
                                continue;
                            }
                        }

                        value.Append(current);
                        index++;
                        continue;
                    }

                    if (char.IsWhiteSpace(current))
                        break;

                    if (current == '"' || current == '\'')
                    {
                        activeQuote = current;
                        isQuoted = true;
                        index++;
                        continue;
                    }

                    value.Append(current);
                    index++;
                }

                if (activeQuote != '\0')
                    hasUnterminatedQuote = true;

                tokens.Add(new CommandToken(value.ToString(), start, index - start, isQuoted));
            }

            return new CommandTokenization(tokens, hasUnterminatedQuote);
        }

        /// <summary>
        /// Formats one semantic argument for insertion into command text.
        /// </summary>
        public static string QuoteArgument(string value)
        {
            value = value ?? string.Empty;
            bool requiresQuotes = value.Length == 0;

            for (int index = 0; index < value.Length && !requiresQuotes; index++)
            {
                char current = value[index];
                requiresQuotes = char.IsWhiteSpace(current) || current == '"' || current == '\'';
            }

            if (!requiresQuotes)
                return value;

            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            foreach (char current in value)
            {
                if (current == '"' || current == '\\')
                    quoted.Append('\\');
                quoted.Append(current);
            }
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
