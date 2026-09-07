using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RepoLiveControl.Commands;
using RepoLiveControl.Runtime;
using UnityEngine;

namespace RepoLiveControl.Networking
{
    /// <summary>Host-approved owner-only effects; no general command execution on clients.</summary>
    internal sealed class PlayerEffectRelay
    {
        internal const string EffectKind = "player-effect";
        internal const string ResultKind = "player-effect-result";
        internal const string Capability = "rcc.player-effects";
        private readonly byte eventCode;
        private readonly Queue<Incoming> incoming = new Queue<Incoming>();
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly Queue<string> seenOrder = new Queue<string>();
        private object room;
        private int master;

        private sealed class Incoming
        {
            internal int Sender;
            internal string Kind, Id, Payload;
            internal object Room;
            internal int Master;
        }
        private sealed class Pending
        {
            internal int Actor;
            internal float Deadline;
            internal Func<bool> Authorized;
            internal Action<string> Complete;
        }

        internal PlayerEffectRelay(byte eventCode) { this.eventCode = eventCode; }

        internal void Receive(int sender, string kind, string id, string payload)
        {
            if (!PhotonNetwork.InRoom || !CommandNetworkPolicy.IsValidRequestId(id) || incoming.Count >= 64 ||
                payload.Length > CommandNetworkPolicy.MaximumCommandLength) return;
            int currentMaster = PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber;
            if (kind == EffectKind && sender != currentMaster) return;
            Pending expected;
            if (kind == ResultKind && (!PhotonNetwork.IsMasterClient || !pending.TryGetValue(id, out expected) || expected.Actor != sender)) return;
            incoming.Enqueue(new Incoming { Sender = sender, Kind = kind, Id = id, Payload = payload,
                Room = PhotonNetwork.CurrentRoom, Master = currentMaster });
        }

        // Called exclusively from the RunManager.Update bridge, on both hosts and clients.
        internal void ProcessFrame()
        {
            int currentMaster = PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber;
            object currentRoom = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null;
            if (!ReferenceEquals(room, currentRoom) || master != currentMaster)
            {
                foreach (Pending work in pending.Values.ToArray()) work.Complete("ERROR The effect's room or host changed.");
                pending.Clear(); seen.Clear(); seenOrder.Clear();
                room = currentRoom; master = currentMaster;
                if (currentRoom != null && PhotonNetwork.LocalPlayer != null)
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { Capability, 1 } });
            }
            foreach (var entry in pending.ToArray())
            {
                if (Time.realtimeSinceStartup >= entry.Value.Deadline || !entry.Value.Authorized())
                {
                    pending.Remove(entry.Key);
                    entry.Value.Complete("ERROR Owner effect timed out or its authorization expired.");
                }
            }
            for (int count = 0; count < 8 && incoming.Count > 0; count++)
            {
                Incoming message = incoming.Dequeue();
                if (currentRoom == null || !ReferenceEquals(currentRoom, message.Room) || currentMaster != message.Master) continue;
                if (message.Kind == ResultKind)
                {
                    Pending work;
                    if (pending.TryGetValue(message.Id, out work) && work.Actor == message.Sender)
                    {
                        pending.Remove(message.Id);
                        work.Complete(work.Authorized() ? message.Payload : "ERROR The requester is no longer authorized.");
                    }
                    continue;
                }
                if (message.Sender != currentMaster || !seen.Add(message.Id)) continue;
                seenOrder.Enqueue(message.Id);
                while (seenOrder.Count > 512) seen.Remove(seenOrder.Dequeue());
                string result;
                try
                {
                    PlayerAvatar local = SemiFunc.PlayerAvatarLocal();
                    PlayerActionCommand action = local == null ? null : OwnerActionPolicy.Validate(
                        message.Payload, message.Sender, currentMaster, RuntimePlayerCatalog.Choice(local).Selector,
                        local.photonView == null ? -1 : local.photonView.ViewID, PhotonNetwork.ServerTimestamp);
                    if (action == null) throw new InvalidOperationException("Invalid host-approved player effect.");
                    PlayerActionRuntime.ApplyOwner(action, local);
                    result = "OK Owner applied /" + action.Name + ".";
                }
                catch (Exception exception) { result = "ERROR " + exception.Message; }
                Send(ResultKind, message.Id, result, message.Sender);
            }
        }

        internal void SendEffect(PlayerAvatar player, PlayerActionCommand command, Func<bool> authorized, Action<string> complete)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !authorized())
                throw new InvalidOperationException("Host authority is required for player effects.");
            Player owner = player.photonView == null ? null : player.photonView.Owner;
            if (owner == null || !(owner.CustomProperties[Capability] is int) || (int)owner.CustomProperties[Capability] != 1)
                throw new InvalidOperationException("This effect requires RepoCommandConsole 2.1.0+ on " + RuntimePlayerCatalog.Choice(player).Selector + ".");
            string id = Guid.NewGuid().ToString("N");
            if (pending.Count >= 32) throw new InvalidOperationException("Too many pending player effects.");
            string payload = player.photonView.ViewID + "|" + PhotonNetwork.ServerTimestamp + "|" +
                command.ForPlayer(RuntimePlayerCatalog.Choice(player).Selector);
            if (payload.Length > CommandNetworkPolicy.MaximumCommandLength)
                throw new InvalidOperationException("Player effect exceeds the network command length limit.");
            pending.Add(id, new Pending { Actor = owner.ActorNumber, Authorized = authorized,
                Complete = complete, Deadline = Time.realtimeSinceStartup + 5f });
            if (!Send(EffectKind, id, payload, owner.ActorNumber))
            {
                pending.Remove(id);
                throw new InvalidOperationException("Photon did not accept the player effect.");
            }
        }

        private bool Send(string kind, string id, string payload, int actor)
        {
            return PhotonNetwork.RaiseEvent(eventCode, CommandNetworkPolicy.Envelope(kind, id, payload),
                new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
        }

        internal void Reset()
        {
            foreach (Pending work in pending.Values.ToArray()) work.Complete("ERROR Player effect relay closed.");
            pending.Clear(); incoming.Clear(); seen.Clear(); seenOrder.Clear();
            room = null; master = -1;
        }

        internal void Dispose()
        {
            Reset();
            if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { Capability, null } });
        }
    }
}
