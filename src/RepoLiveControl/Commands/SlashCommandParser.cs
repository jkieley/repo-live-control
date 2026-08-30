using System;
using System.Collections.Generic;
using System.Globalization;

namespace RepoLiveControl.Commands
{
    /// <summary>
    /// Validates the user-facing slash-command grammar without touching game APIs.
    /// </summary>
    public static class SlashCommandParser
    {
        public const int MinimumCount = 1;
        public const int MaximumCount = 500;

        private static readonly IReadOnlyList<string> KnownCommandNames =
            Array.AsReadOnly(new[]
            {
                "/spawn",
                "/despawn",
                "/grant",
                "/revoke",
                "/permissions",
                "/help"
            });

        public static IReadOnlyList<string> CommandNames
        {
            get { return KnownCommandNames; }
        }

        public static CommandParseResult Parse(string input)
        {
            CommandTokenization tokenization = CommandTokenizer.Tokenize(input);
            if (tokenization.Tokens.Count == 0)
            {
                return Failure(
                    CommandParseErrorCode.EmptyInput,
                    "Enter a slash command.");
            }

            if (tokenization.HasUnterminatedQuote)
            {
                return Failure(
                    CommandParseErrorCode.UnterminatedQuote,
                    "Close the quoted argument before running the command.");
            }

            string commandName = tokenization.Tokens[0].Value;
            if (!commandName.StartsWith("/", StringComparison.Ordinal))
            {
                return Failure(
                    CommandParseErrorCode.MissingSlash,
                    "Commands must begin with '/'.");
            }

            string normalized = commandName.ToLowerInvariant();
            switch (normalized)
            {
                case "/spawn":
                    return ParseSpawn(tokenization.Tokens);
                case "/despawn":
                    return ParseDespawn(tokenization.Tokens);
                case "/grant":
                    return ParsePlayerCommand(tokenization.Tokens, SlashCommandKind.Grant);
                case "/revoke":
                    return ParsePlayerCommand(tokenization.Tokens, SlashCommandKind.Revoke);
                case "/permissions":
                    return ParseNoArgumentCommand(tokenization.Tokens, SlashCommandKind.Permissions);
                case "/help":
                    return ParseNoArgumentCommand(tokenization.Tokens, SlashCommandKind.Help);
                default:
                    return Failure(
                        CommandParseErrorCode.UnknownCommand,
                        "Unknown command '" + commandName + "'.");
            }
        }

        private static CommandParseResult ParseSpawn(IReadOnlyList<CommandToken> tokens)
        {
            if (tokens.Count < 2 || string.IsNullOrWhiteSpace(tokens[1].Value))
                return Missing("Spawn requires a target.");
            if (tokens.Count > 4)
                return TooMany("Spawn accepts a target, optional count, and optional location.");

            int count = 1;
            string location = CommandLocations.PlayerLocation;
            if (tokens.Count >= 3)
            {
                string secondArgument = tokens[2].Value;
                string locationWithoutCount;
                if (TryNormalizeLocation(secondArgument, out locationWithoutCount))
                {
                    location = locationWithoutCount;
                    if (tokens.Count >= 4)
                    {
                        return TooMany(
                            "When count is omitted, spawn location must be the final argument.");
                    }
                }
                else
                {
                    CommandParseResult countError = TryParseCount(
                        secondArgument,
                        false,
                        out count);
                    if (countError != null)
                        return countError;
                }
            }

            if (tokens.Count >= 4)
            {
                string requestedLocation = tokens[3].Value;
                if (!TryNormalizeLocation(requestedLocation, out location))
                {
                    return Failure(
                        CommandParseErrorCode.InvalidLocation,
                        "Spawn location must be '" + CommandLocations.PlayerLocation +
                        "' or '" + CommandLocations.RandomNonCollisionLocation + "'.");
                }
            }

            return Success(new ParsedSlashCommand(
                SlashCommandKind.Spawn,
                tokens[1].Value,
                count,
                location,
                null));
        }

        private static bool TryNormalizeLocation(string value, out string location)
        {
            if (value.Equals(
                CommandLocations.PlayerLocation,
                StringComparison.OrdinalIgnoreCase))
            {
                location = CommandLocations.PlayerLocation;
                return true;
            }

            if (value.Equals(
                CommandLocations.RandomNonCollisionLocation,
                StringComparison.OrdinalIgnoreCase))
            {
                location = CommandLocations.RandomNonCollisionLocation;
                return true;
            }

            location = null;
            return false;
        }

        private static CommandParseResult ParseDespawn(IReadOnlyList<CommandToken> tokens)
        {
            if (tokens.Count < 2 || string.IsNullOrWhiteSpace(tokens[1].Value))
                return Missing("Despawn requires a target.");
            if (tokens.Count > 3)
                return TooMany("Despawn accepts a target and optional count.");

            int? count = null;
            if (tokens.Count >= 3)
            {
                int parsedCount;
                CommandParseResult countError = TryParseCount(
                    tokens[2].Value,
                    true,
                    out parsedCount);
                if (countError != null)
                    return countError;
                if (!tokens[2].Value.Equals("all", StringComparison.OrdinalIgnoreCase))
                    count = parsedCount;
            }

            return Success(new ParsedSlashCommand(
                SlashCommandKind.Despawn,
                tokens[1].Value,
                count,
                null,
                null));
        }

        private static CommandParseResult ParsePlayerCommand(
            IReadOnlyList<CommandToken> tokens,
            SlashCommandKind kind)
        {
            if (tokens.Count < 2 || string.IsNullOrWhiteSpace(tokens[1].Value))
                return Missing(kind + " requires a player.");
            if (tokens.Count > 2)
                return TooMany(kind + " accepts exactly one player.");

            return Success(new ParsedSlashCommand(kind, null, null, null, tokens[1].Value));
        }

        private static CommandParseResult ParseNoArgumentCommand(
            IReadOnlyList<CommandToken> tokens,
            SlashCommandKind kind)
        {
            if (tokens.Count > 1)
                return TooMany(kind + " does not accept arguments.");
            return Success(new ParsedSlashCommand(kind, null, null, null, null));
        }

        private static CommandParseResult TryParseCount(
            string value,
            bool allowAll,
            out int count)
        {
            count = 0;
            if (allowAll && value.Equals("all", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out count))
            {
                return Failure(
                    CommandParseErrorCode.InvalidCount,
                    allowAll
                        ? "Count must be 'all' or a whole number from 1 through 500."
                        : "Count must be a whole number from 1 through 500.");
            }

            if (count < MinimumCount || count > MaximumCount)
            {
                return Failure(
                    CommandParseErrorCode.CountOutOfRange,
                    "Count must be from 1 through 500.");
            }

            return null;
        }

        private static CommandParseResult Success(ParsedSlashCommand command)
        {
            return new CommandParseResult(command, CommandParseErrorCode.None, null);
        }

        private static CommandParseResult Failure(
            CommandParseErrorCode errorCode,
            string errorMessage)
        {
            return new CommandParseResult(null, errorCode, errorMessage);
        }

        private static CommandParseResult Missing(string message)
        {
            return Failure(CommandParseErrorCode.MissingArgument, message);
        }

        private static CommandParseResult TooMany(string message)
        {
            return Failure(CommandParseErrorCode.TooManyArguments, message);
        }
    }
}
