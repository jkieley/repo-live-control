using System;

namespace RepoLiveControl.Commands
{
    public static class OwnerActionPolicy
    {
        public static PlayerActionCommand Validate(string payload, int sender, int master, string localSelector, int localViewId, int serverTimestamp)
        {
            if (sender <= 0 || sender != master || payload == null || payload.Length > 512)
                return null;
            int separator = payload.IndexOf('|');
            int viewId;
            if (separator <= 0 || !int.TryParse(payload.Substring(0, separator), out viewId) ||
                viewId <= 0 || viewId != localViewId) return null;
            int second = payload.IndexOf('|', separator + 1);
            int issuedAt;
            if (second <= separator + 1 || !int.TryParse(payload.Substring(separator + 1, second - separator - 1), out issuedAt)) return null;
            int age = unchecked(serverTimestamp - issuedAt);
            if (age < 0 || age > 4000) return null;
            CommandParseResult parsed = SlashCommandParser.Parse(payload.Substring(second + 1));
            if (!parsed.Success || parsed.Command.PlayerAction == null) return null;
            PlayerActionCommand action = parsed.Command.PlayerAction;
            return PlayerActionCommands.RequiresOwner(action.Name) &&
                !action.Player.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                action.Player.Equals(localSelector, StringComparison.Ordinal)
                ? action : null;
        }
    }
}
