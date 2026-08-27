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
        private const string Magic = "com.jameskieley.repo.commandconsole";
        private const int ProtocolVersion = 2;
        private const string RequestKind = "request";
        private const string ResponseKind = "response";
        private const string NoticeKind = "notice";
        private const int MaximumCommandLength = 512;
        private const int MaximumResponseLength = 2048;

        private readonly byte eventCode;
        private readonly PermissionService permissions;
        private readonly Action<string> resultSink;
        private readonly HashSet<string> pendingRequests =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, Queue<float>> requestTimes =
            new Dictionary<int, Queue<float>>();
        private bool disposed;

        internal CommandNetworkRouter(
            byte eventCode,
            PermissionService permissions,
            Action<string> resultSink)
        {
            this.eventCode = eventCode;
            this.permissions = permissions;
            this.resultSink = resultSink;
            PhotonNetwork.AddCallbackTarget(this);
        }

        internal string SendRequest(string command)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null)
                throw new InvalidOperationException("No multiplayer host is available.");
            if (command == null || command.Length == 0 || command.Length > MaximumCommandLength)
                throw new InvalidOperationException("Command length must be between 1 and 512 characters.");
            if (!IsSlashCommandPayload(command))
                throw new InvalidOperationException("Network commands must use the slash-command interface.");
            CommandParseResult parsed = SlashCommandParser.Parse(command);
            if (!parsed.Success)
                throw new InvalidOperationException(parsed.ErrorMessage);

            string requestId = Guid.NewGuid().ToString("N");
            pendingRequests.Add(requestId);
            bool sent = PhotonNetwork.RaiseEvent(
                eventCode,
                Envelope(RequestKind, requestId, command),
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
            if (!sent)
            {
                pendingRequests.Remove(requestId);
                throw new InvalidOperationException("Photon did not accept the command request.");
            }
            return requestId;
        }

        internal void SendNotice(int targetActorNumber, string message)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || targetActorNumber <= 0)
                return;
            SendToActor(NoticeKind, string.Empty, message, targetActorNumber);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (disposed || photonEvent == null || photonEvent.Code != eventCode)
                return;

            object[] envelope = photonEvent.CustomData as object[];
            string kind;
            string requestId;
            string payload;
            if (!TryReadEnvelope(envelope, out kind, out requestId, out payload))
                return;

            if (kind == RequestKind)
                ReceiveRequest(photonEvent.Sender, requestId, payload);
            else if (!IsFromCurrentMaster(photonEvent.Sender))
                return;
            else if (kind == ResponseKind)
                ReceiveResponse(requestId, payload);
            else if (kind == NoticeKind && resultSink != null)
                resultSink(payload);
        }

        private void ReceiveRequest(int senderActorNumber, string requestId, string command)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || senderActorNumber <= 0)
                return;
            if (string.IsNullOrEmpty(requestId) || string.IsNullOrWhiteSpace(command) ||
                command.Length > MaximumCommandLength)
            {
                SendResponse(senderActorNumber, requestId, "ERROR Malformed command request.");
                return;
            }
            if (!IsSlashCommandPayload(command))
            {
                SendResponse(senderActorNumber, requestId,
                    "ERROR Network commands must use the slash-command interface.");
                return;
            }
            CommandParseResult parsed = SlashCommandParser.Parse(command);
            if (!parsed.Success)
            {
                SendResponse(senderActorNumber, requestId, "ERROR " + parsed.ErrorMessage);
                return;
            }
            if (IsHostOnlyVerb(command))
            {
                SendResponse(senderActorNumber, requestId,
                    "ERROR /grant and /revoke can only be run locally by the host.");
                return;
            }
            if (!permissions.IsAllowed(senderActorNumber) && !IsPublicVerb(command))
            {
                SendResponse(senderActorNumber, requestId,
                    "ERROR The host has not granted you command permission.");
                return;
            }
            if (!ConsumeRateLimit(senderActorNumber))
            {
                SendResponse(senderActorNumber, requestId,
                    "ERROR Command rate limit exceeded; wait a moment and try again.");
                return;
            }

            Bridge.Enqueue(new ControlRequest(
                command,
                CommandRequestSource.RemoteClient,
                senderActorNumber,
                result => SendResponse(senderActorNumber, requestId, result)));
        }

        private void ReceiveResponse(string requestId, string response)
        {
            if (string.IsNullOrEmpty(requestId) || !pendingRequests.Remove(requestId))
                return;
            if (resultSink != null)
                resultSink(response);
        }

        private void SendResponse(int targetActorNumber, string requestId, string response)
        {
            SendToActor(ResponseKind, requestId, response, targetActorNumber);
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
            if (boundedPayload.Length > MaximumResponseLength)
                boundedPayload = boundedPayload.Substring(0, MaximumResponseLength);
            PhotonNetwork.RaiseEvent(
                eventCode,
                Envelope(kind, requestId ?? string.Empty, boundedPayload),
                new RaiseEventOptions { TargetActors = new[] { targetActorNumber } },
                SendOptions.SendReliable);
        }

        private static object[] Envelope(string kind, string requestId, string payload)
        {
            return new object[] { Magic, ProtocolVersion, kind, requestId, payload };
        }

        private static bool TryReadEnvelope(
            object[] values,
            out string kind,
            out string requestId,
            out string payload)
        {
            kind = string.Empty;
            requestId = string.Empty;
            payload = string.Empty;
            if (values == null || values.Length != 5 || !(values[0] is string) ||
                !string.Equals((string)values[0], Magic, StringComparison.Ordinal))
                return false;

            int version;
            try
            {
                version = Convert.ToInt32(values[1]);
            }
            catch
            {
                return false;
            }
            if (version != ProtocolVersion || !(values[2] is string) ||
                !(values[3] is string) || !(values[4] is string))
                return false;

            kind = (string)values[2];
            requestId = (string)values[3];
            payload = (string)values[4];
            return kind == RequestKind || kind == ResponseKind || kind == NoticeKind;
        }

        private bool ConsumeRateLimit(int actorNumber)
        {
            Queue<float> times;
            if (!requestTimes.TryGetValue(actorNumber, out times))
            {
                times = new Queue<float>();
                requestTimes[actorNumber] = times;
            }

            float now = Time.realtimeSinceStartup;
            while (times.Count > 0 && now - times.Peek() > 3f)
                times.Dequeue();
            if (times.Count >= 5)
                return false;
            times.Enqueue(now);
            return true;
        }

        private static bool IsHostOnlyVerb(string command)
        {
            string verb = GetVerb(command);
            return verb == "grant" || verb == "revoke";
        }

        private static bool IsSlashCommandPayload(string command)
        {
            string trimmed = (command ?? string.Empty).TrimStart();
            return trimmed.StartsWith("/", StringComparison.Ordinal);
        }

        private static bool IsPublicVerb(string command)
        {
            string verb = GetVerb(command);
            return verb == "help" || verb == "permissions";
        }

        private static bool IsFromCurrentMaster(int senderActorNumber)
        {
            return PhotonNetwork.InRoom && PhotonNetwork.MasterClient != null &&
                   PhotonNetwork.MasterClient.ActorNumber == senderActorNumber;
        }

        private static string GetVerb(string command)
        {
            string trimmed = (command ?? string.Empty).TrimStart();
            if (trimmed.StartsWith("/", StringComparison.Ordinal))
                trimmed = trimmed.Substring(1);
            int separator = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return (separator < 0 ? trimmed : trimmed.Substring(0, separator)).ToLowerInvariant();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            PhotonNetwork.RemoveCallbackTarget(this);
            pendingRequests.Clear();
            requestTimes.Clear();
        }
    }
}
