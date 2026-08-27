using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

namespace RepoLiveControl.Networking
{
    internal sealed class PermissionService
    {
        private readonly HashSet<int> grantedActors = new HashSet<int>();
        private string roomName = string.Empty;
        private int masterActorNumber = -1;

        internal void UpdateSession()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                Reset();
                return;
            }

            string currentRoomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
            int currentMaster = PhotonNetwork.MasterClient == null
                ? -1
                : PhotonNetwork.MasterClient.ActorNumber;
            if (!string.Equals(roomName, currentRoomName, StringComparison.Ordinal) ||
                masterActorNumber != currentMaster)
            {
                grantedActors.Clear();
                roomName = currentRoomName;
                masterActorNumber = currentMaster;
            }

            var departed = new List<int>();
            foreach (int actorNumber in grantedActors)
            {
                if (!PhotonNetwork.CurrentRoom.Players.ContainsKey(actorNumber))
                    departed.Add(actorNumber);
            }
            foreach (int actorNumber in departed)
                grantedActors.Remove(actorNumber);
        }

        internal void Reset()
        {
            grantedActors.Clear();
            roomName = string.Empty;
            masterActorNumber = -1;
        }

        internal bool IsAllowed(int actorNumber)
        {
            if (!PhotonNetwork.InRoom)
                return true;
            if (actorNumber <= 0 || PhotonNetwork.CurrentRoom == null)
                return false;

            Player player;
            if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out player) || player == null)
                return false;
            return player.IsMasterClient || grantedActors.Contains(actorNumber);
        }

        internal bool IsGranted(int actorNumber)
        {
            return grantedActors.Contains(actorNumber);
        }

        internal bool TryGrant(string selector, out int actorNumber, out string message)
        {
            Player player;
            if (!TryResolveOtherPlayer(selector, false, out player, out message))
            {
                actorNumber = -1;
                return false;
            }

            actorNumber = player.ActorNumber;
            if (grantedActors.Add(actorNumber))
                message = "OK Granted command permission to " + PlayerLabel(player) + ".";
            else
                message = "OK " + PlayerLabel(player) + " already has command permission.";
            return true;
        }

        internal bool TryRevoke(string selector, out int actorNumber, out string message)
        {
            Player player;
            if (!TryResolveOtherPlayer(selector, true, out player, out message))
            {
                actorNumber = -1;
                return false;
            }

            actorNumber = player.ActorNumber;
            if (grantedActors.Remove(actorNumber))
                message = "OK Revoked command permission from " + PlayerLabel(player) + ".";
            else
                message = "OK " + PlayerLabel(player) + " did not have command permission.";
            return true;
        }

        internal string Describe()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return "OK Permissions: single player/local host; no grants are required.";

            var labels = new List<string>();
            foreach (int actorNumber in grantedActors)
            {
                Player player;
                if (PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out player) && player != null)
                    labels.Add(PlayerLabel(player));
            }
            labels.Sort(StringComparer.OrdinalIgnoreCase);
            return labels.Count == 0
                ? "OK Permissions: host only; no non-host players are granted."
                : "OK Granted players: " + string.Join(", ", labels.ToArray()) + ".";
        }

        internal List<string> GetGrantCandidates()
        {
            return GetPlayerCandidates(false);
        }

        internal List<string> GetRevokeCandidates()
        {
            return GetPlayerCandidates(true);
        }

        private List<string> GetPlayerCandidates(bool grantedOnly)
        {
            var values = new List<string>();
            if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerListOthers == null)
                return values;

            foreach (Player player in PhotonNetwork.PlayerListOthers)
            {
                if (player == null || (grantedOnly && !grantedActors.Contains(player.ActorNumber)))
                    continue;
                values.Add(PlayerSelector(player));
            }
            values.Sort(StringComparer.OrdinalIgnoreCase);
            return values;
        }

        private bool TryResolveOtherPlayer(
            string selector,
            bool grantedOnly,
            out Player selected,
            out string error)
        {
            selected = null;
            error = string.Empty;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                error = "ERROR Player grants are only available in a multiplayer room.";
                return false;
            }

            string query = (selector ?? string.Empty).Trim();
            int requestedActor;
            if (TryParseActorSuffix(query, out requestedActor))
            {
                Player byActor;
                if (PhotonNetwork.CurrentRoom.Players.TryGetValue(requestedActor, out byActor) &&
                    byActor != null && !byActor.IsMasterClient &&
                    (!grantedOnly || grantedActors.Contains(requestedActor)))
                {
                    selected = byActor;
                    return true;
                }
                error = "ERROR No eligible non-host player has actor number " + requestedActor + ".";
                return false;
            }

            var exact = new List<Player>();
            var partial = new List<Player>();
            foreach (Player player in PhotonNetwork.PlayerListOthers)
            {
                if (player == null || (grantedOnly && !grantedActors.Contains(player.ActorNumber)))
                    continue;
                string nickname = player.NickName ?? string.Empty;
                if (nickname.Equals(query, StringComparison.OrdinalIgnoreCase))
                    exact.Add(player);
                else if (query.Length > 0 &&
                         nickname.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add(player);
            }

            List<Player> matches = exact.Count > 0 ? exact : partial;
            if (matches.Count == 1)
            {
                selected = matches[0];
                return true;
            }
            if (matches.Count > 1)
            {
                var options = new List<string>();
                foreach (Player player in matches)
                    options.Add(PlayerSelector(player));
                error = "ERROR Player name is ambiguous. Use one of: " +
                        string.Join(", ", options.ToArray()) + ".";
                return false;
            }

            error = "ERROR No eligible non-host player matches '" + query + "'.";
            return false;
        }

        private static bool TryParseActorSuffix(string selector, out int actorNumber)
        {
            actorNumber = -1;
            int hash = selector.LastIndexOf('#');
            return hash >= 0 && hash + 1 < selector.Length &&
                   int.TryParse(selector.Substring(hash + 1), out actorNumber);
        }

        private static string PlayerSelector(Player player)
        {
            return (player.NickName ?? "Player") + "#" + player.ActorNumber;
        }

        private static string PlayerLabel(Player player)
        {
            return (player.NickName ?? "Player") + " (actor " + player.ActorNumber + ")";
        }
    }
}
