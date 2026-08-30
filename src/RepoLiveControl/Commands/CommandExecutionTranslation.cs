using System;
using System.Globalization;

namespace RepoLiveControl.Commands
{
    /// <summary>
    /// Converts validated, canonical slash commands into the host executor's
    /// internal pipe-shaped command. This layer is pure so default placement,
    /// target kind, and exact count behavior can be tested without Unity.
    /// </summary>
    public static class CommandExecutionTranslation
    {
        public static string TranslateSpawn(
            CommandTargetKind targetKind,
            string targetName,
            int count,
            string location)
        {
            string action = ActionFor(targetKind);
            ValidateTargetName(targetName);
            ValidateCount(count);

            string placement;
            if (string.Equals(
                location,
                CommandLocations.PlayerLocation,
                StringComparison.OrdinalIgnoreCase))
            {
                placement = "at-player";
            }
            else if (string.Equals(
                location,
                CommandLocations.RandomNonCollisionLocation,
                StringComparison.OrdinalIgnoreCase))
            {
                placement = "safe";
            }
            else
            {
                throw new ArgumentException("Unknown spawn location.", "location");
            }

            return action + "|" + targetName + "|" +
                count.ToString(CultureInfo.InvariantCulture) + "|" + placement;
        }

        public static string TranslateDespawn(
            CommandTargetKind targetKind,
            string targetName,
            int? count)
        {
            string kindName = KindNameFor(targetKind);
            ValidateTargetName(targetName);
            if (count.HasValue)
                ValidateCount(count.Value);

            return "despawnspawned|" + kindName + "|" + targetName + "|" +
                (count.HasValue
                    ? count.Value.ToString(CultureInfo.InvariantCulture)
                    : "-1");
        }

        public static int AcceptedEnemyCountForSetup(
            int needed,
            int liveSpawned,
            bool collisionFreePlacement)
        {
            if (needed <= 0)
                throw new ArgumentOutOfRangeException("needed");
            if (liveSpawned <= 0)
                throw new ArgumentOutOfRangeException("liveSpawned");
            return collisionFreePlacement
                ? 1
                : Math.Min(needed, liveSpawned);
        }

        private static string ActionFor(CommandTargetKind targetKind)
        {
            if (targetKind == CommandTargetKind.Item)
                return "item";
            if (targetKind == CommandTargetKind.Valuable)
                return "loot";
            if (targetKind == CommandTargetKind.Enemy)
                return "enemy";
            throw new ArgumentOutOfRangeException(
                "targetKind",
                "A canonical item, valuable, or enemy kind is required.");
        }

        private static string KindNameFor(CommandTargetKind targetKind)
        {
            if (targetKind == CommandTargetKind.Item)
                return "item";
            if (targetKind == CommandTargetKind.Valuable)
                return "valuable";
            if (targetKind == CommandTargetKind.Enemy)
                return "enemy";
            throw new ArgumentOutOfRangeException(
                "targetKind",
                "A canonical item, valuable, or enemy kind is required.");
        }

        private static void ValidateTargetName(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
                throw new ArgumentException("A canonical target name is required.", "targetName");
            if (targetName.IndexOf('|') >= 0 || targetName.IndexOf('\r') >= 0 ||
                targetName.IndexOf('\n') >= 0)
            {
                throw new ArgumentException(
                    "Target names cannot contain protocol delimiters.",
                    "targetName");
            }
        }

        private static void ValidateCount(int count)
        {
            if (count < SlashCommandParser.MinimumCount ||
                count > SlashCommandParser.MaximumCount)
            {
                throw new ArgumentOutOfRangeException(
                    "count",
                    "Count must be from 1 through 500.");
            }
        }
    }
}
