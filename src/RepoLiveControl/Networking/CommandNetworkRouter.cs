using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RepoLiveControl.Commands;
using UnityEngine;

namespace RepoLiveControl.Networking
{
    internal sealed class CommandNetworkRouter : IOnEventCallback, IDisposable
    {
        private const int MaximumOutstandingPerActor = 2;
        private const int MaximumOutstandingGlobal = 32;
        private const int MaximumRememberedRequestIds = 2048;

        private readonly byte eventCode;
        private readonly PermissionService permissions;
        private readonly Action<string> resultSink;
        private readonly PendingCommandRegistry pendingRequests =
            new PendingCommandRegistry(30f);
        private readonly SlidingWindowRateLimiter rateLimiter =
            new SlidingWindowRateLimiter(5, 3f);
        private readonly RateLimitNoticeGate rateLimitNoticeGate =
            new RateLimitNoticeGate(3f);
        private readonly PhotonCallbackRegistrationLifecycle callbackRegistration =
            new PhotonCallbackRegistrationLifecycle();
        private readonly HashSet<string> acceptedRemoteRequests =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> seenRemoteRequests =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> seenRemoteRequestOrder = new Queue<string>();
        private readonly Dictionary<int, int> outstandingByActor =
            new Dictionary<int, int>();
        private long observedSessionRevision = -1;
        private bool disposed;

        internal CommandNetworkRouter(
            byte eventCode,
            PermissionService permissions,
            Action<string> resultSink)
        {
            this.eventCode = eventCode;
            this.permissions = permissions;
            this.resultSink = resultSink;
        }

        internal string SendRequest(string command)
        {
            Update(true);
            if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null)
                throw new InvalidOperationException("No multiplayer host is available.");
            if (command == null || command.Length == 0 ||
                command.Length > CommandNetworkPolicy.MaximumCommandLength)
                throw new InvalidOperationException("Command length must be between 1 and 512 characters.");
            if (!CommandNetworkPolicy.IsSlashCommandPayload(command))
                throw new InvalidOperationException("Network commands must use the slash-command interface.");
            CommandParseResult parsed = SlashCommandParser.Parse(command);
            if (!parsed.Success)
                throw new InvalidOperationException(parsed.ErrorMessage);
            if (pendingRequests.Count > 0)
                throw new InvalidOperationException(
                    "Wait for the previous host response before sending another command.");

            string requestId = Guid.NewGuid().ToString("N");
            int masterActorNumber = PhotonNetwork.MasterClient.ActorNumber;
            if (!pendingRequests.TryAdd(
                requestId,
                masterActorNumber,
                permissions.SessionRevision,
                Time.realtimeSinceStartup))
            {
                throw new InvalidOperationException("Could not track the command request.");
            }
            bool sent = PhotonNetwork.RaiseEvent(
                eventCode,
                CommandNetworkPolicy.Envelope(
                    CommandNetworkPolicy.RequestKind,
                    requestId,
                    command),
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
            if (!sent)
            {
                pendingRequests.Remove(requestId);
                throw new InvalidOperationException("Photon did not accept the command request.");
            }
            return requestId;
        }

        internal void Update(bool networkSessionSceneActive)
        {
            if (disposed)
                return;

            if (!networkSessionSceneActive)
            {
                callbackRegistration.Synchronize(
                    false,
                    () => PhotonNetwork.AddCallbackTarget(this),
                    () => PhotonNetwork.RemoveCallbackTarget(this));
                permissions.Reset();
                return;
            }

            bool roomActive = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
            callbackRegistration.Synchronize(
                roomActive,
                () => PhotonNetwork.AddCallbackTarget(this),
                () => PhotonNetwork.RemoveCallbackTarget(this));

            permissions.UpdateSession();
            long currentRevision = permissions.SessionRevision;
            if (observedSessionRevision < 0)
            {
                observedSessionRevision = currentRevision;
            }
            else if (observedSessionRevision != currentRevision)
            {
                observedSessionRevision = currentRevision;
                rateLimiter.Clear();
                rateLimitNoticeGate.Clear();
                seenRemoteRequests.Clear();
                seenRemoteRequestOrder.Clear();
            }

            bool inRoom = roomActive;
            int currentMaster = inRoom && PhotonNetwork.MasterClient != null
                ? PhotonNetwork.MasterClient.ActorNumber
                : -1;
            IReadOnlyList<PendingCommandFailure> failures =
                pendingRequests.CollectFailures(
                    Time.realtimeSinceStartup,
                    inRoom,
                    currentMaster,
                    currentRevision);
            if (resultSink == null)
                return;
            foreach (PendingCommandFailure failure in failures)
                resultSink(failure.Error);
        }

        internal void SendNotice(int targetActorNumber, string message)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || targetActorNumber <= 0)
                return;
            SendToActor(
                CommandNetworkPolicy.NoticeKind,
                string.Empty,
                message,
                targetActorNumber);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (disposed || photonEvent == null || photonEvent.Code != eventCode)
                return;

            object[] envelope = photonEvent.CustomData as object[];
            string kind;
            string requestId;
            string payload;
            if (!CommandNetworkPolicy.TryReadEnvelope(
                envelope,
                out kind,
                out requestId,
                out payload))
                return;

            if (kind == CommandNetworkPolicy.RequestKind)
                ReceiveRequest(photonEvent.Sender, requestId, payload);
            else if (!IsFromCurrentMaster(photonEvent.Sender))
                return;
            else if (kind == CommandNetworkPolicy.ResponseKind)
                ReceiveResponse(requestId, payload);
            else if (kind == CommandNetworkPolicy.NoticeKind && resultSink != null)
                resultSink(payload);
        }

        private void ReceiveRequest(int senderActorNumber, string requestId, string command)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || senderActorNumber <= 0)
                return;
            permissions.UpdateSession();
            bool validRequestId = CommandNetworkPolicy.IsValidRequestId(requestId);
            float receivedAt = Time.realtimeSinceStartup;
            // Charge every request at the trust boundary before parsing,
            // authorization, duplicate checks, or executor admission. Otherwise
            // rejected traffic could bypass the advertised per-player budget.
            if (!rateLimiter.TryConsume(senderActorNumber, receivedAt))
            {
                if (rateLimitNoticeGate.ShouldNotify(senderActorNumber, receivedAt))
                {
                    SendResponse(
                        senderActorNumber,
                        validRequestId ? requestId : string.Empty,
                        "ERROR Command rate limit exceeded; wait a moment and try again.");
                }
                return;
            }

            if (!validRequestId)
            {
                SendResponse(senderActorNumber, string.Empty, "ERROR Malformed command request ID.");
                return;
            }

            CommandRequestValidation validation =
                CommandNetworkPolicy.ValidateRemoteCommand(
                    command,
                    permissions.IsAllowed(senderActorNumber));
            if (!validation.Allowed)
            {
                SendResponse(senderActorNumber, requestId, validation.Error);
                return;
            }

            string requestKey = senderActorNumber + ":" + requestId;
            if (seenRemoteRequests.Contains(requestKey) ||
                acceptedRemoteRequests.Contains(requestKey))
            {
                SendResponse(senderActorNumber, requestId,
                    "ERROR Duplicate command request ID.");
                return;
            }

            int actorOutstanding;
            outstandingByActor.TryGetValue(senderActorNumber, out actorOutstanding);
            if (actorOutstanding >= MaximumOutstandingPerActor ||
                acceptedRemoteRequests.Count >= MaximumOutstandingGlobal)
            {
                SendResponse(senderActorNumber, requestId,
                    "ERROR Too many commands are already waiting for the host executor.");
                return;
            }

            long requiredSessionRevision = permissions.SessionRevision;
            bool requiresGrant = !CommandNetworkPolicy.IsPublicVerb(command);
            RememberRemoteRequest(requestKey);
            acceptedRemoteRequests.Add(requestKey);
            outstandingByActor[senderActorNumber] = actorOutstanding + 1;
            Bridge.Enqueue(new ControlRequest(
                command,
                CommandRequestSource.RemoteClient,
                senderActorNumber,
                result =>
                {
                    ReleaseRemoteRequest(senderActorNumber, requestKey);
                    if (permissions.SessionRevision == requiredSessionRevision)
                        SendResponse(senderActorNumber, requestId, result);
                },
                requiredSessionRevision,
                () => permissions.SessionRevision == requiredSessionRevision &&
                      (!requiresGrant || permissions.IsAllowed(senderActorNumber))));
        }

        private void ReceiveResponse(string requestId, string response)
        {
            if (!pendingRequests.TryComplete(requestId))
                return;
            if (resultSink != null)
                resultSink(response);
        }

        private void SendResponse(int targetActorNumber, string requestId, string response)
        {
            SendToActor(
                CommandNetworkPolicy.ResponseKind,
                requestId,
                response,
                targetActorNumber);
        }

        private void SendToActor(
            string kind,
            string requestId,
            string payload,
            int targetActorNumber)
        {
            if (!PhotonNetwork.InRoom || targetActorNumber <= 0)
                return;

            string boundedPayload = payload ?? string.Empty;
            if (boundedPayload.Length > CommandNetworkPolicy.MaximumResponseLength)
            {
                boundedPayload = boundedPayload.Substring(
                    0,
                    CommandNetworkPolicy.MaximumResponseLength);
            }
            bool sent = PhotonNetwork.RaiseEvent(
                eventCode,
                CommandNetworkPolicy.Envelope(kind, requestId, boundedPayload),
                new RaiseEventOptions { TargetActors = new[] { targetActorNumber } },
                SendOptions.SendReliable);
            if (!sent && Plugin.Log != null)
            {
                Plugin.Log.LogWarning(
                    "Photon did not accept a " + kind +
                    " event for actor " + targetActorNumber + ".");
            }
        }

        private void ReleaseRemoteRequest(int actorNumber, string requestKey)
        {
            acceptedRemoteRequests.Remove(requestKey);
            int outstanding;
            if (!outstandingByActor.TryGetValue(actorNumber, out outstanding))
                return;
            if (outstanding <= 1)
                outstandingByActor.Remove(actorNumber);
            else
                outstandingByActor[actorNumber] = outstanding - 1;
        }

        private void RememberRemoteRequest(string requestKey)
        {
            seenRemoteRequests.Add(requestKey);
            seenRemoteRequestOrder.Enqueue(requestKey);
            while (seenRemoteRequestOrder.Count > MaximumRememberedRequestIds)
            {
                string oldest = seenRemoteRequestOrder.Dequeue();
                if (acceptedRemoteRequests.Contains(oldest))
                {
                    seenRemoteRequestOrder.Enqueue(oldest);
                    continue;
                }
                seenRemoteRequests.Remove(oldest);
            }
        }

        private static bool IsFromCurrentMaster(int senderActorNumber)
        {
            return PhotonNetwork.InRoom && PhotonNetwork.MasterClient != null &&
                   PhotonNetwork.MasterClient.ActorNumber == senderActorNumber;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            callbackRegistration.Dispose(
                () => PhotonNetwork.RemoveCallbackTarget(this));
            pendingRequests.Clear();
            rateLimiter.Clear();
            rateLimitNoticeGate.Clear();
            acceptedRemoteRequests.Clear();
            seenRemoteRequests.Clear();
            seenRemoteRequestOrder.Clear();
            outstandingByActor.Clear();
        }
    }
}
