using System;
using System.Collections.Generic;
using RepoLiveControl.Commands;

namespace RepoLiveControl.Networking
{
    /// <summary>
    /// Dependency-free validation and envelope handling for the Photon command
    /// protocol. Keeping this separate from Photon makes the trust boundary
    /// exhaustively testable in a normal .NET process.
    /// </summary>
    public static class CommandNetworkPolicy
    {
        public const string Magic = "com.jameskieley.repo.commandconsole";
        public const int ProtocolVersion = 2;
        public const string RequestKind = "request";
        public const string ResponseKind = "response";
        public const string NoticeKind = "notice";
        public const int MaximumCommandLength = 512;
        public const int MaximumResponseLength = 2048;

        public static CommandRequestValidation ValidateRemoteCommand(
            string command,
            bool isAllowed)
        {
            if (string.IsNullOrWhiteSpace(command) ||
                command.Length > MaximumCommandLength)
            {
                return CommandRequestValidation.Deny(
                    "ERROR Malformed command request.");
            }

            if (!IsSlashCommandPayload(command))
            {
                return CommandRequestValidation.Deny(
                    "ERROR Network commands must use the slash-command interface.");
            }

            CommandParseResult parsed = SlashCommandParser.Parse(command);
            if (!parsed.Success)
                return CommandRequestValidation.Deny("ERROR " + parsed.ErrorMessage);

            if (IsHostOnlyVerb(command))
            {
                return CommandRequestValidation.Deny(
                    "ERROR /grant and /revoke can only be run locally by the host.");
            }

            if (!isAllowed && !IsPublicVerb(command))
            {
                return CommandRequestValidation.Deny(
                    "ERROR The host has not granted you command permission.");
            }

            return CommandRequestValidation.Allow();
        }

        public static bool IsValidRequestId(string requestId)
        {
            if (string.IsNullOrEmpty(requestId) || requestId.Length != 32)
                return false;

            foreach (char value in requestId)
            {
                bool digit = value >= '0' && value <= '9';
                bool lowerHex = value >= 'a' && value <= 'f';
                bool upperHex = value >= 'A' && value <= 'F';
                if (!digit && !lowerHex && !upperHex)
                    return false;
            }
            return true;
        }

        public static object[] Envelope(string kind, string requestId, string payload)
        {
            return new object[]
            {
                Magic,
                ProtocolVersion,
                kind,
                requestId ?? string.Empty,
                payload ?? string.Empty
            };
        }

        public static bool TryReadEnvelope(
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
            {
                return false;
            }

            if (!(values[1] is int) || (int)values[1] != ProtocolVersion ||
                !(values[2] is string) ||
                !(values[3] is string) || !(values[4] is string))
            {
                return false;
            }

            kind = (string)values[2];
            requestId = (string)values[3];
            payload = (string)values[4];
            return kind == RequestKind || kind == ResponseKind || kind == NoticeKind;
        }

        public static bool IsHostOnlyVerb(string command)
        {
            string verb = GetVerb(command);
            return verb == "grant" || verb == "revoke";
        }

        public static bool IsPublicVerb(string command)
        {
            string verb = GetVerb(command);
            return verb == "help" || verb == "permissions";
        }

        public static bool IsSlashCommandPayload(string command)
        {
            string trimmed = (command ?? string.Empty).TrimStart();
            return trimmed.StartsWith("/", StringComparison.Ordinal);
        }

        public static string GetVerb(string command)
        {
            string trimmed = (command ?? string.Empty).TrimStart();
            if (trimmed.StartsWith("/", StringComparison.Ordinal))
                trimmed = trimmed.Substring(1);
            int separator = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return (separator < 0 ? trimmed : trimmed.Substring(0, separator))
                .ToLowerInvariant();
        }
    }

    public sealed class CommandRequestValidation
    {
        private CommandRequestValidation(bool allowed, string error)
        {
            Allowed = allowed;
            Error = error;
        }

        public bool Allowed { get; private set; }

        public string Error { get; private set; }

        internal static CommandRequestValidation Allow()
        {
            return new CommandRequestValidation(true, null);
        }

        internal static CommandRequestValidation Deny(string error)
        {
            return new CommandRequestValidation(false, error);
        }
    }

    public static class CommandIngressSessionPolicy
    {
        public static string Validate(
            bool cancelled,
            long? requiredSessionRevision,
            long? currentSessionRevision)
        {
            if (cancelled)
                return "The command request was cancelled by its caller.";
            if (requiredSessionRevision.HasValue &&
                (!currentSessionRevision.HasValue ||
                 requiredSessionRevision.Value != currentSessionRevision.Value))
            {
                return "The original lobby authorization expired.";
            }
            return null;
        }
    }

    /// <summary>
    /// Small deterministic rolling-window limiter used by the host receiver.
    /// </summary>
    public sealed class SlidingWindowRateLimiter
    {
        private readonly int maximumEvents;
        private readonly float windowSeconds;
        private readonly Dictionary<int, Queue<float>> eventTimes =
            new Dictionary<int, Queue<float>>();

        public SlidingWindowRateLimiter(int maximumEvents, float windowSeconds)
        {
            if (maximumEvents <= 0)
                throw new ArgumentOutOfRangeException("maximumEvents");
            if (windowSeconds <= 0f)
                throw new ArgumentOutOfRangeException("windowSeconds");
            this.maximumEvents = maximumEvents;
            this.windowSeconds = windowSeconds;
        }

        public bool TryConsume(int actorNumber, float now)
        {
            if (actorNumber <= 0)
                return false;

            Queue<float> times;
            if (!eventTimes.TryGetValue(actorNumber, out times))
            {
                times = new Queue<float>();
                eventTimes[actorNumber] = times;
            }

            while (times.Count > 0 && now - times.Peek() > windowSeconds)
                times.Dequeue();
            if (times.Count >= maximumEvents)
                return false;
            times.Enqueue(now);
            return true;
        }

        public void Clear()
        {
            eventTimes.Clear();
        }
    }

    /// <summary>
    /// Allows one useful rate-limit response per actor and silence interval so
    /// rejected floods cannot turn the host into a reliable-response amplifier.
    /// </summary>
    public sealed class RateLimitNoticeGate
    {
        private readonly float silenceSeconds;
        private readonly Dictionary<int, float> nextNoticeAt =
            new Dictionary<int, float>();

        public RateLimitNoticeGate(float silenceSeconds)
        {
            if (silenceSeconds <= 0f)
                throw new ArgumentOutOfRangeException("silenceSeconds");
            this.silenceSeconds = silenceSeconds;
        }

        public bool ShouldNotify(int actorNumber, float now)
        {
            if (actorNumber <= 0)
                return false;

            float next;
            if (nextNoticeAt.TryGetValue(actorNumber, out next) && now < next)
                return false;

            nextNoticeAt[actorNumber] = now + silenceSeconds;
            return true;
        }

        public void Clear()
        {
            nextNoticeAt.Clear();
        }
    }

    /// <summary>
    /// Tracks one client's outstanding requests so missing responses, room exits,
    /// and host migration become explicit failures instead of permanent PENDING UI.
    /// </summary>
    public sealed class PendingCommandRegistry
    {
        private readonly float timeoutSeconds;
        private readonly Dictionary<string, PendingCommand> pending =
            new Dictionary<string, PendingCommand>(StringComparer.Ordinal);

        public PendingCommandRegistry(float timeoutSeconds)
        {
            if (timeoutSeconds <= 0f)
                throw new ArgumentOutOfRangeException("timeoutSeconds");
            this.timeoutSeconds = timeoutSeconds;
        }

        public int Count { get { return pending.Count; } }

        public bool TryAdd(
            string requestId,
            int masterActorNumber,
            long sessionRevision,
            float sentAt)
        {
            if (!CommandNetworkPolicy.IsValidRequestId(requestId) ||
                masterActorNumber <= 0)
            {
                return false;
            }

            if (pending.ContainsKey(requestId))
                return false;
            pending.Add(requestId, new PendingCommand(
                masterActorNumber,
                sessionRevision,
                sentAt));
            return true;
        }

        public bool TryComplete(string requestId)
        {
            return !string.IsNullOrEmpty(requestId) && pending.Remove(requestId);
        }

        public bool Remove(string requestId)
        {
            return !string.IsNullOrEmpty(requestId) && pending.Remove(requestId);
        }

        public IReadOnlyList<PendingCommandFailure> CollectFailures(
            float now,
            bool inRoom,
            int currentMasterActorNumber,
            long currentSessionRevision)
        {
            var failures = new List<PendingCommandFailure>();
            var removals = new List<string>();
            foreach (KeyValuePair<string, PendingCommand> entry in pending)
            {
                string error = null;
                if (!inRoom || currentMasterActorNumber <= 0)
                    error = "ERROR The multiplayer room closed before the host responded.";
                else if (entry.Value.SessionRevision != currentSessionRevision)
                    error = "ERROR The multiplayer room changed before the host responded.";
                else if (entry.Value.MasterActorNumber != currentMasterActorNumber)
                    error = "ERROR The lobby host changed before the command completed.";
                else if (now - entry.Value.SentAt >= timeoutSeconds)
                    error = "ERROR Timed out waiting for the lobby host to respond.";

                if (error == null)
                    continue;
                removals.Add(entry.Key);
                failures.Add(new PendingCommandFailure(entry.Key, error));
            }

            foreach (string requestId in removals)
                pending.Remove(requestId);
            return failures.AsReadOnly();
        }

        public void Clear()
        {
            pending.Clear();
        }

        private sealed class PendingCommand
        {
            internal PendingCommand(
                int masterActorNumber,
                long sessionRevision,
                float sentAt)
            {
                MasterActorNumber = masterActorNumber;
                SessionRevision = sessionRevision;
                SentAt = sentAt;
            }

            internal int MasterActorNumber { get; private set; }

            internal long SessionRevision { get; private set; }

            internal float SentAt { get; private set; }
        }
    }

    public sealed class PendingCommandFailure
    {
        internal PendingCommandFailure(string requestId, string error)
        {
            RequestId = requestId;
            Error = error;
        }

        public string RequestId { get; private set; }

        public string Error { get; private set; }
    }

    /// <summary>
    /// Pure room-scoped grant state. Synchronization increments Revision whenever
    /// the room or master changes and prunes actors that have left.
    /// </summary>
    public sealed class SessionGrantLedger
    {
        private readonly HashSet<int> grantedActors = new HashSet<int>();
        private bool inRoom;
        private string roomName = string.Empty;
        private int masterActorNumber = -1;

        public long Revision { get; private set; }

        public bool Synchronize(
            bool currentlyInRoom,
            string currentRoomName,
            int currentMasterActorNumber,
            IEnumerable<int> currentActors)
        {
            currentRoomName = currentRoomName ?? string.Empty;
            bool changed = currentlyInRoom != inRoom ||
                (currentlyInRoom &&
                 (!string.Equals(roomName, currentRoomName, StringComparison.Ordinal) ||
                  masterActorNumber != currentMasterActorNumber));
            if (changed)
            {
                Revision++;
                grantedActors.Clear();
            }

            inRoom = currentlyInRoom;
            roomName = currentlyInRoom ? currentRoomName : string.Empty;
            masterActorNumber = currentlyInRoom ? currentMasterActorNumber : -1;

            if (!currentlyInRoom)
            {
                grantedActors.Clear();
                return changed;
            }

            var actors = new HashSet<int>();
            if (currentActors != null)
            {
                foreach (int actorNumber in currentActors)
                {
                    if (actorNumber > 0)
                        actors.Add(actorNumber);
                }
            }

            var departed = new List<int>();
            foreach (int actorNumber in grantedActors)
            {
                if (!actors.Contains(actorNumber))
                    departed.Add(actorNumber);
            }
            foreach (int actorNumber in departed)
                grantedActors.Remove(actorNumber);
            return changed;
        }

        public bool Grant(int actorNumber)
        {
            return inRoom && actorNumber > 0 && grantedActors.Add(actorNumber);
        }

        public bool Revoke(int actorNumber)
        {
            return actorNumber > 0 && grantedActors.Remove(actorNumber);
        }

        public bool IsGranted(int actorNumber)
        {
            return inRoom && actorNumber > 0 && grantedActors.Contains(actorNumber);
        }

        public IReadOnlyList<int> GetGrantedActors()
        {
            var values = new List<int>(grantedActors);
            values.Sort();
            return values.AsReadOnly();
        }
    }
}
