using System;
using Photon.Pun;
using RepoLiveControl.Commands;

namespace RepoLiveControl.Runtime
{
    internal static class SlashCommandRuntime
    {
        internal static bool TryTranslateOrComplete(
            ControlRequest request,
            string rawCommand,
            out string translatedCommand)
        {
            translatedCommand = string.Empty;
            CommandParseResult parsed = SlashCommandParser.Parse(rawCommand);
            if (!parsed.Success)
            {
                Bridge.Complete(request, "ERROR " + parsed.ErrorMessage);
                return false;
            }

            ParsedSlashCommand command = parsed.Command;
            switch (command.Kind)
            {
                case SlashCommandKind.Help:
                    Bridge.Complete(request, HelpText());
                    return false;
                case SlashCommandKind.Permissions:
                    Bridge.Complete(request, RequireConsoleRuntime().Permissions.Describe());
                    return false;
                case SlashCommandKind.Grant:
                    Grant(request, command.Player);
                    return false;
                case SlashCommandKind.Revoke:
                    Revoke(request, command.Player);
                    return false;
                case SlashCommandKind.Spawn:
                    return TryTranslateSpawn(request, command, out translatedCommand);
                case SlashCommandKind.Despawn:
                    return TryTranslateDespawn(request, command, out translatedCommand);
                default:
                    Bridge.Complete(request, "ERROR Unsupported slash command.");
                    return false;
            }
        }

        private static bool TryTranslateSpawn(
            ControlRequest request,
            ParsedSlashCommand command,
            out string translated)
        {
            translated = string.Empty;
            RuntimeCommandTarget target;
            string error;
            if (!RuntimeTargetCatalog.TryResolve(command.Target, false, out target, out error))
            {
                Bridge.Complete(request, error);
                return false;
            }

            string action;
            if (target.Kind == CommandEntityKind.Enemy)
                action = "enemy";
            else if (target.Kind == CommandEntityKind.Valuable)
                action = "loot";
            else
                action = "item";

            string placement = command.Location == CommandLocations.RandomNonCollisionLocation
                ? "safe"
                : "at-player";
            translated = action + "|" + target.Name + "|" +
                         command.Count.Value + "|" + placement;
            return true;
        }

        private static bool TryTranslateDespawn(
            ControlRequest request,
            ParsedSlashCommand command,
            out string translated)
        {
            translated = string.Empty;
            RuntimeCommandTarget target;
            string error;
            if (!RuntimeTargetCatalog.TryResolve(command.Target, true, out target, out error))
            {
                Bridge.Complete(request, error);
                return false;
            }

            string count = command.Count.HasValue ? command.Count.Value.ToString() : "-1";
            translated = "despawnspawned|" + target.KindName + "|" + target.Name + "|" + count;
            return true;
        }

        private static void Grant(ControlRequest request, string player)
        {
            if (request.Source == CommandRequestSource.RemoteClient)
            {
                Bridge.Complete(request, "ERROR /grant can only be run locally by the host.");
                return;
            }
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                Bridge.Complete(request, "ERROR Only the lobby host can grant command permission.");
                return;
            }

            CommandConsoleRuntime runtime = RequireConsoleRuntime();
            int actorNumber;
            string result;
            runtime.Permissions.TryGrant(player, out actorNumber, out result);
            Bridge.Complete(request, result);
            if (result.StartsWith("OK", StringComparison.Ordinal) && actorNumber > 0)
                runtime.Network.SendNotice(actorNumber,
                    "OK The host granted you REPO Command Console permission.");
        }

        private static void Revoke(ControlRequest request, string player)
        {
            if (request.Source == CommandRequestSource.RemoteClient)
            {
                Bridge.Complete(request, "ERROR /revoke can only be run locally by the host.");
                return;
            }
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                Bridge.Complete(request, "ERROR Only the lobby host can revoke command permission.");
                return;
            }

            CommandConsoleRuntime runtime = RequireConsoleRuntime();
            int actorNumber;
            string result;
            runtime.Permissions.TryRevoke(player, out actorNumber, out result);
            Bridge.Complete(request, result);
            if (result.StartsWith("OK", StringComparison.Ordinal) && actorNumber > 0)
                runtime.Network.SendNotice(actorNumber,
                    "OK The host revoked your REPO Command Console permission.");
        }

        private static CommandConsoleRuntime RequireConsoleRuntime()
        {
            if (Plugin.Instance == null || Plugin.Instance.CommandConsole == null)
                throw new InvalidOperationException("The in-game command console runtime is unavailable.");
            return Plugin.Instance.CommandConsole;
        }

        private static string HelpText()
        {
            return "OK Commands: /spawn <item:|valuable:|enemy:name> [count=1] " +
                   "[player-location|random-non-collision-location]; " +
                   "/despawn <target> [count=all]; /grant <player>; /revoke <player>; " +
                   "/permissions; /help. Use Up/Down and Tab for fuzzy autocomplete.";
        }
    }
}
