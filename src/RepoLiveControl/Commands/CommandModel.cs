using System;

namespace RepoLiveControl.Commands
{
    public enum SlashCommandKind
    {
        Spawn,
        Despawn,
        Grant,
        Revoke,
        Permissions,
        Help
    }

    public enum CommandTargetKind
    {
        Unspecified,
        Item,
        Valuable,
        Enemy
    }

    public enum CommandParseErrorCode
    {
        None,
        EmptyInput,
        UnterminatedQuote,
        MissingSlash,
        UnknownCommand,
        MissingArgument,
        TooManyArguments,
        InvalidCount,
        CountOutOfRange,
        InvalidLocation
    }

    public static class CommandLocations
    {
        public const string PlayerLocation = "player-location";
        public const string RandomNonCollisionLocation = "random-non-collision-location";
    }

    /// <summary>
    /// A validated slash command. Spawn always has a Count and Location. Despawn
    /// has a null Count when the user requested "all" (including the default).
    /// </summary>
    public sealed class ParsedSlashCommand
    {
        internal ParsedSlashCommand(
            SlashCommandKind kind,
            string target,
            int? count,
            string location,
            string player)
        {
            Kind = kind;
            Target = target;
            Count = count;
            Location = location;
            Player = player;

            CommandTargetKind targetKind;
            string targetName;
            SplitTarget(target, out targetKind, out targetName);
            TargetKind = targetKind;
            TargetName = targetName;
        }

        public SlashCommandKind Kind { get; private set; }

        public string Target { get; private set; }

        public CommandTargetKind TargetKind { get; private set; }

        public string TargetName { get; private set; }

        public int? Count { get; private set; }

        public bool IsAllCount
        {
            get { return Kind == SlashCommandKind.Despawn && !Count.HasValue; }
        }

        public string Location { get; private set; }

        public string Player { get; private set; }

        private static void SplitTarget(
            string target,
            out CommandTargetKind targetKind,
            out string targetName)
        {
            targetKind = CommandTargetKind.Unspecified;
            targetName = target;
            if (string.IsNullOrEmpty(target))
                return;

            int separator = target.IndexOf(':');
            if (separator <= 0)
                return;

            string prefix = target.Substring(0, separator);
            if (prefix.Equals("item", StringComparison.OrdinalIgnoreCase))
                targetKind = CommandTargetKind.Item;
            else if (prefix.Equals("valuable", StringComparison.OrdinalIgnoreCase))
                targetKind = CommandTargetKind.Valuable;
            else if (prefix.Equals("enemy", StringComparison.OrdinalIgnoreCase))
                targetKind = CommandTargetKind.Enemy;
            else
                return;

            targetName = target.Substring(separator + 1);
        }
    }

    public sealed class CommandParseResult
    {
        internal CommandParseResult(
            ParsedSlashCommand command,
            CommandParseErrorCode errorCode,
            string errorMessage)
        {
            Command = command;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success
        {
            get { return Command != null; }
        }

        public ParsedSlashCommand Command { get; private set; }

        public CommandParseErrorCode ErrorCode { get; private set; }

        public string ErrorMessage { get; private set; }
    }
}
