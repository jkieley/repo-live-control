using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using RepoLiveControl.Commands;
using UnityEngine;

namespace RepoLiveControl.Runtime
{
    internal static class RuntimePlayerCatalog
    {
        internal static List<PlayerAvatar> Players()
        {
            var players = new List<PlayerAvatar>();
            var actors = new HashSet<int>();
            // Include inactive death avatars, but never prefab assets or actors from another room.
            var found = new List<PlayerAvatar>(SemiFunc.PlayerGetList() ?? new List<PlayerAvatar>());
            found.AddRange(Resources.FindObjectsOfTypeAll<PlayerAvatar>());
            foreach (PlayerAvatar avatar in found)
            {
                if (avatar == null || !avatar.gameObject.scene.IsValid() || !avatar.gameObject.scene.isLoaded)
                    continue;
                int actor = Actor(avatar);
                if (PhotonNetwork.InRoom)
                {
                    if (actor <= 0 || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.CurrentRoom.Players.ContainsKey(actor)) continue;
                }
                else if (avatar != SemiFunc.PlayerAvatarLocal()) continue;
                if (actors.Add(actor)) players.Add(avatar);
            }
            return players;
        }

        internal static int Actor(PlayerAvatar player)
        {
            return player.photonView != null && player.photonView.Owner != null ? player.photonView.Owner.ActorNumber : -1;
        }

        internal static PlayerChoice Choice(PlayerAvatar player)
        {
            string name = player.photonView != null && player.photonView.Owner != null
                ? player.photonView.Owner.NickName : "Local Player";
            return new PlayerChoice(name, PhotonNetwork.InRoom ? Actor(player) : -1);
        }

        internal static List<string> Selectors() { return Players().Select(player => Choice(player).Selector).OrderBy(name => name).ToList(); }

        internal static List<PlayerAvatar> Resolve(string selector)
        {
            List<PlayerAvatar> players = Players();
            var selected = PlayerSelection.Resolve(selector, players.Select(Choice));
            return players.Where(player => selected.Any(choice => choice.Selector == Choice(player).Selector)).ToList();
        }
    }
}
