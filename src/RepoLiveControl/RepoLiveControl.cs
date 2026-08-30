using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using RepoLiveControl.Commands;
using RepoLiveControl.Networking;
using RepoLiveControl.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace RepoLiveControl
{
    [BepInPlugin("com.jameskieley.repo.commandconsole", "REPO Command Console", "2.0.0")]
    [BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.jameskieley.repo.commandconsole";
        internal const string PluginName = "REPO Command Console";
        internal const string PluginVersion = "2.0.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }
        internal CommandConsoleRuntime CommandConsole { get { return commandConsole; } }

        private CommandConsoleRuntime commandConsole;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            commandConsole = new CommandConsoleRuntime(this);
            Bridge.PublishPermissionSessionRevision(
                commandConsole.Permissions.SessionRevision);
            Bridge.Start();
            Logger.LogInfo(
                PluginName + " " + PluginVersion +
                " loaded. Press " + commandConsole.ToggleKeyLabel + " to open the command console.");
        }

        private void Update()
        {
            if (commandConsole != null)
                commandConsole.Update();
        }

        private void OnGUI()
        {
            if (commandConsole != null)
                commandConsole.OnGUI();
        }

        private void OnDestroy()
        {
            if (commandConsole != null)
                commandConsole.Dispose();
            commandConsole = null;
            Instance = null;
        }
    }

    public static class Loader
    {
        public static void Load()
        {
            Bridge.Start();
        }
    }

    internal sealed class ControlRequest
    {
        internal readonly string Command;
        internal readonly CommandRequestSource Source;
        internal readonly int RequesterActorNumber;
        internal readonly Action<string> CompletionCallback;
        internal readonly long? RequiredSessionRevision;
        internal readonly Func<bool> AuthorizationValidator;
        internal readonly ManualResetEventSlim Completed = new ManualResetEventSlim(false);
        internal string Result = "ERROR No result was produced.";
        internal bool ExecutionContextBound;
        internal bool ExecutionStartedInRoom;
        internal object ExecutionRoomIdentity;
        internal int ExecutionMasterActorNumber = -1;
        internal long ExecutionSessionRevision = -1;
        private int completionState;
        private int cancellationState;

        internal ControlRequest(string command)
            : this(
                command,
                CommandRequestSource.NamedPipe,
                -1,
                null,
                CapturePublishedSessionRevision(),
                null)
        {
        }

        internal ControlRequest(
            string command,
            CommandRequestSource source,
            int requesterActorNumber,
            Action<string> completionCallback)
            : this(
                command,
                source,
                requesterActorNumber,
                completionCallback,
                null,
                null)
        {
        }

        internal ControlRequest(
            string command,
            CommandRequestSource source,
            int requesterActorNumber,
            Action<string> completionCallback,
            long? requiredSessionRevision,
            Func<bool> authorizationValidator)
        {
            Command = command;
            Source = source;
            RequesterActorNumber = requesterActorNumber;
            CompletionCallback = completionCallback;
            RequiredSessionRevision = requiredSessionRevision;
            AuthorizationValidator = authorizationValidator;
        }

        internal void Complete(string result)
        {
            if (Interlocked.Exchange(ref completionState, 1) != 0)
                return;
            Result = result;
            Completed.Set();
            if (CompletionCallback != null)
            {
                try
                {
                    CompletionCallback(result);
                }
                catch (Exception exception)
                {
                    if (Plugin.Log != null)
                        Plugin.Log.LogError("Command completion callback failed: " + exception);
                }
            }
        }

        internal bool IsCancelled
        {
            get { return Volatile.Read(ref cancellationState) != 0; }
        }

        internal void Cancel(string result)
        {
            Interlocked.Exchange(ref cancellationState, 1);
            if (Interlocked.Exchange(ref completionState, 1) != 0)
                return;
            Result = result;
            Completed.Set();
        }

        private static long? CapturePublishedSessionRevision()
        {
            long revision = Bridge.GetPublishedPermissionSessionRevision();
            return revision >= 0 ? (long?)revision : null;
        }
    }

    internal enum CommandRequestSource
    {
        NamedPipe,
        LocalConsole,
        RemoteClient
    }

    internal enum SpawnKind
    {
        Enemy,
        Loot,
        Item,
        Cart
    }

    internal sealed class EnemyPlacementReservation
    {
        internal readonly Vector3 Position;
        internal readonly float HorizontalRadius;

        internal EnemyPlacementReservation(Vector3 position, float horizontalRadius)
        {
            Position = position;
            HorizontalRadius = horizontalRadius;
        }
    }

    internal sealed class EnemyClearanceVolume
    {
        internal readonly Vector3 CenterOffset;
        internal readonly Vector3 HalfExtents;
        internal readonly float HorizontalRadius;

        internal EnemyClearanceVolume(
            Vector3 centerOffset,
            Vector3 halfExtents,
            float horizontalRadius)
        {
            CenterOffset = centerOffset;
            HalfExtents = halfExtents;
            HorizontalRadius = horizontalRadius;
        }
    }

    internal sealed class SpawnJob
    {
        internal readonly ControlRequest Request;
        internal readonly SpawnKind Kind;
        internal readonly string Selector;
        internal readonly string Placement;
        internal readonly int Requested;
        internal readonly Vector3 Anchor;
        internal readonly List<Vector3> ReservedPositions = new List<Vector3>();
        internal readonly List<EnemyPlacementReservation> EnemyReservations =
            new List<EnemyPlacementReservation>();
        internal int Spawned;
        internal bool Finished;
        internal readonly SpawnNameSummary NameSummary = new SpawnNameSummary();

        internal SpawnJob(
            ControlRequest request,
            SpawnKind kind,
            string selector,
            string placement,
            int requested,
            Vector3 anchor)
        {
            Request = request;
            Kind = kind;
            Selector = selector;
            Placement = placement;
            Requested = requested;
            Anchor = anchor;
        }
    }

    internal sealed class SpawnedObjectRecord
    {
        internal GameObject Instance;
        internal string Name;
        internal SpawnKind Kind;
        internal bool IsWeapon;
    }

    internal sealed class DuplicateLootJob
    {
        internal readonly ControlRequest Request;
        internal readonly List<PrefabRef> Prefabs;
        internal readonly Vector3 Anchor;
        internal readonly List<Vector3> Positions = new List<Vector3>();
        internal int Spawned;
        internal bool Finished;

        internal DuplicateLootJob(ControlRequest request, List<PrefabRef> prefabs, Vector3 anchor)
        {
            Request = request;
            Prefabs = prefabs;
            Anchor = anchor;
        }
    }

    internal sealed class ItemBatchJob
    {
        internal readonly ControlRequest Request;
        internal readonly List<Item> Items;
        internal readonly List<string> TypeNames;
        internal readonly string Placement;
        internal readonly int CountPerType;
        internal readonly Vector3 Anchor;
        internal readonly List<Vector3> ReservedPositions = new List<Vector3>();
        internal int Spawned;
        internal bool Finished;

        internal ItemBatchJob(
            ControlRequest request,
            List<Item> items,
            List<string> typeNames,
            string placement,
            int countPerType,
            Vector3 anchor)
        {
            Request = request;
            Items = items;
            TypeNames = typeNames;
            Placement = placement;
            CountPerType = countPerType;
            Anchor = anchor;
        }
    }

    internal sealed class BalancedItemJob
    {
        internal readonly ControlRequest Request;
        internal readonly List<Item> Items;
        internal readonly List<string> TypeNames;
        internal readonly Dictionary<string, int> TypeCounts;
        internal readonly string Placement;
        internal readonly Vector3 Anchor;
        internal readonly List<Vector3> ReservedPositions = new List<Vector3>();
        internal int Spawned;
        internal bool Finished;

        internal BalancedItemJob(
            ControlRequest request,
            List<Item> items,
            List<string> typeNames,
            Dictionary<string, int> typeCounts,
            string placement,
            Vector3 anchor)
        {
            Request = request;
            Items = items;
            TypeNames = typeNames;
            TypeCounts = typeCounts;
            Placement = placement;
            Anchor = anchor;
        }
    }

    internal static class Bridge
    {
        internal const string PipeName = "CodexRepoCommandConsoleV2";
        private const string HarmonyId = "com.jameskieley.repo.commandconsole.harmony";
        private static readonly ConcurrentQueue<ControlRequest> Requests = new ConcurrentQueue<ControlRequest>();
        private static readonly List<SpawnedObjectRecord> SpawnedObjects =
            new List<SpawnedObjectRecord>();
        private static readonly System.Reflection.FieldInfo EnemyFirstSpawnPointField =
            AccessTools.Field(typeof(EnemyParent), "firstSpawnPoint");
        private static readonly System.Reflection.FieldInfo EnemyFirstSpawnPointsField =
            AccessTools.Field(typeof(EnemyDirector), "enemyFirstSpawnPoints");
        private static readonly string[] ExpensiveLootNames =
        {
            "Diamond Display",
            "Griffin Statue",
            "Dragon Skull",
            "GoldTooth",
            "Server Rack"
        };
        private static readonly string[] WeaponTerms =
        {
            "weapon", "melee", "ranged", "gun", "pistol", "rifle", "shotgun",
            "revolver", "blaster", "cannon", "launcher", "sword", "blade",
            "knife", "dagger", "axe", "hatchet", "bat", "hammer", "mace",
            "spear", "bow", "crossbow", "grenade", "mine", "bomb", "pan", "taser"
        };

        private static int started;
        private static long publishedPermissionSessionRevision = -1;
        private static SpawnJob activeJob;
        private static DuplicateLootJob activeDuplicateLootJob;
        private static ItemBatchJob activeItemBatchJob;
        private static BalancedItemJob activeBalancedItemJob;

        internal static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                return;

            Harmony.UnpatchID("Codex.REPO.SpawnBridge");
            Harmony.UnpatchID("Codex.REPO.SpawnBridge.V2");
            Harmony.UnpatchID("Codex.REPO.ControlBridge");
            Harmony.UnpatchID("Codex.REPO.LiveControl");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V2");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V3");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V4");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V5");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V6");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V7");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V8");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V9");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V10");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V11");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V12");
            Harmony.UnpatchID("Codex.REPO.LiveControl.V13");
            Harmony.UnpatchID(HarmonyId);
            new Harmony(HarmonyId).PatchAll(typeof(Bridge).Assembly);

            var serverThread = new Thread(ListenForRequests)
            {
                IsBackground = true,
                Name = "Codex REPO Live Control"
            };
            serverThread.Start();
        }

        internal static void Enqueue(ControlRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");
            Requests.Enqueue(request);
        }

        internal static void PublishPermissionSessionRevision(long revision)
        {
            Interlocked.Exchange(ref publishedPermissionSessionRevision, revision);
        }

        internal static long GetPublishedPermissionSessionRevision()
        {
            return Interlocked.Read(ref publishedPermissionSessionRevision);
        }

        private static void ListenForRequests()
        {
            while (true)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1))
                    {
                        pipe.WaitForConnection();
                        string command;
                        using (var reader = new StreamReader(
                            pipe,
                            new System.Text.UTF8Encoding(false),
                            false,
                            1024,
                            true))
                        {
                            command = reader.ReadLine();
                        }

                        string response;
                        if (string.IsNullOrWhiteSpace(command))
                        {
                            response = "ERROR Empty command.";
                        }
                        else
                        {
                            var request = new ControlRequest(command);
                            Requests.Enqueue(request);
                            if (request.Completed.Wait(TimeSpan.FromSeconds(30)))
                            {
                                response = request.Result;
                            }
                            else
                            {
                                response = "ERROR Command timed out waiting for the game thread; " +
                                    "the queued request was cancelled.";
                                request.Cancel(response);
                            }
                        }

                        using (var writer = new StreamWriter(
                            pipe,
                            new System.Text.UTF8Encoding(false),
                            1024,
                            true))
                        {
                            writer.AutoFlush = true;
                            writer.WriteLine(response);
                        }
                    }
                }
                catch (Exception exception)
                {
                    try
                    {
                        File.AppendAllText(
                            Path.Combine(Path.GetTempPath(), "RepoLiveControl-pipe.log"),
                            DateTime.UtcNow.ToString("O") + " " + exception + Environment.NewLine);
                    }
                    catch
                    {
                    }
                    Thread.Sleep(250);
                }
            }
        }

        internal static void ProcessFrame()
        {
            if (HasActiveJob())
            {
                RefreshPermissionSession();
                if (AbortActiveJobsIfAuthorityLost())
                    return;
            }

            if (activeBalancedItemJob != null)
            {
                ProcessBalancedItemJob(activeBalancedItemJob);
                if (activeBalancedItemJob.Finished)
                    activeBalancedItemJob = null;
                return;
            }

            if (activeItemBatchJob != null)
            {
                ProcessItemBatchJob(activeItemBatchJob);
                if (activeItemBatchJob.Finished)
                    activeItemBatchJob = null;
                return;
            }

            if (activeDuplicateLootJob != null)
            {
                ProcessDuplicateLootJob(activeDuplicateLootJob);
                if (activeDuplicateLootJob.Finished)
                    activeDuplicateLootJob = null;
                return;
            }

            if (activeJob != null)
            {
                ProcessSpawnJob(activeJob);
                if (activeJob.Finished)
                    activeJob = null;
                return;
            }

            ControlRequest request;
            if (!Requests.TryDequeue(out request))
                return;
            if (request.IsCancelled)
                return;

            try
            {
                RefreshPermissionSession();
                BindExecutionContext(request);
                string invalidReason = GetInvalidExecutionReason(request);
                if (invalidReason != null)
                    throw new InvalidOperationException(invalidReason);

                Dispatch(request);
            }
            catch (Exception exception)
            {
                Complete(request, "ERROR " + exception.Message);
            }
        }

        private static bool AbortActiveJobsIfAuthorityLost()
        {
            bool aborted = false;
            if (activeBalancedItemJob != null)
            {
                string reason = GetInvalidExecutionReason(activeBalancedItemJob.Request);
                if (!activeBalancedItemJob.Finished && reason != null)
                {
                    activeBalancedItemJob.Finished = true;
                    activeBalancedItemJob.ReservedPositions.Clear();
                    Complete(activeBalancedItemJob.Request, string.Format(
                        "ERROR {2} Balanced item spread stopped after {0}/{1}.",
                        activeBalancedItemJob.Spawned,
                        activeBalancedItemJob.Items.Count,
                        reason));
                    aborted = true;
                }
                if (activeBalancedItemJob.Finished)
                    activeBalancedItemJob = null;
            }

            if (activeItemBatchJob != null)
            {
                string reason = GetInvalidExecutionReason(activeItemBatchJob.Request);
                if (!activeItemBatchJob.Finished && reason != null)
                {
                    activeItemBatchJob.Finished = true;
                    activeItemBatchJob.ReservedPositions.Clear();
                    Complete(activeItemBatchJob.Request, string.Format(
                        "ERROR {2} Item batch stopped after {0}/{1}.",
                        activeItemBatchJob.Spawned,
                        activeItemBatchJob.Items.Count,
                        reason));
                    aborted = true;
                }
                if (activeItemBatchJob.Finished)
                    activeItemBatchJob = null;
            }

            if (activeDuplicateLootJob != null)
            {
                string reason = GetInvalidExecutionReason(activeDuplicateLootJob.Request);
                if (!activeDuplicateLootJob.Finished && reason != null)
                {
                    activeDuplicateLootJob.Finished = true;
                    activeDuplicateLootJob.Positions.Clear();
                    Complete(activeDuplicateLootJob.Request, string.Format(
                        "ERROR {2} Loot duplication stopped after {0}/{1}.",
                        activeDuplicateLootJob.Spawned,
                        activeDuplicateLootJob.Prefabs.Count,
                        reason));
                    aborted = true;
                }
                if (activeDuplicateLootJob.Finished)
                    activeDuplicateLootJob = null;
            }

            if (activeJob != null)
            {
                string reason = GetInvalidExecutionReason(activeJob.Request);
                if (!activeJob.Finished && reason != null)
                {
                    activeJob.Finished = true;
                    activeJob.ReservedPositions.Clear();
                    activeJob.EnemyReservations.Clear();
                    Complete(activeJob.Request, string.Format(
                        "ERROR {2} Spawn stopped after {0}/{1}.",
                        activeJob.Spawned,
                        activeJob.Requested,
                        reason));
                    aborted = true;
                }
                if (activeJob.Finished)
                    activeJob = null;
            }

            return aborted;
        }

        private static void RefreshPermissionSession()
        {
            PermissionService permissions = GetPermissionService();
            if (permissions != null)
            {
                permissions.UpdateSession();
                PublishPermissionSessionRevision(permissions.SessionRevision);
            }
        }

        private static bool HasActiveJob()
        {
            return activeBalancedItemJob != null ||
                activeItemBatchJob != null ||
                activeDuplicateLootJob != null ||
                activeJob != null;
        }

        private static PermissionService GetPermissionService()
        {
            return Plugin.Instance != null && Plugin.Instance.CommandConsole != null
                ? Plugin.Instance.CommandConsole.Permissions
                : null;
        }

        private static void BindExecutionContext(ControlRequest request)
        {
            if (request.ExecutionContextBound)
                return;

            PermissionService permissions = GetPermissionService();
            request.ExecutionStartedInRoom =
                PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
            request.ExecutionRoomIdentity = PhotonNetwork.CurrentRoom;
            request.ExecutionMasterActorNumber = PhotonNetwork.MasterClient == null
                ? -1
                : PhotonNetwork.MasterClient.ActorNumber;
            request.ExecutionSessionRevision = permissions == null
                ? -1
                : permissions.SessionRevision;
            request.ExecutionContextBound = true;
        }

        private static string GetInvalidExecutionReason(ControlRequest request)
        {
            if (request == null || !request.ExecutionContextBound)
                return "The command has no valid execution session.";

            PermissionService permissions = GetPermissionService();
            string ingressError = CommandIngressSessionPolicy.Validate(
                request.IsCancelled,
                request.RequiredSessionRevision,
                permissions == null ? (long?)null : permissions.SessionRevision);
            if (ingressError != null)
                return ingressError;

            if (request.AuthorizationValidator != null)
            {
                bool authorized;
                try
                {
                    authorized = request.AuthorizationValidator();
                }
                catch
                {
                    authorized = false;
                }
                if (!authorized)
                    return "The requester is no longer authorized in this lobby.";
            }

            if (request.ExecutionStartedInRoom)
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                    return "The original multiplayer room closed.";
                if (!object.ReferenceEquals(
                    request.ExecutionRoomIdentity,
                    PhotonNetwork.CurrentRoom))
                {
                    return "The multiplayer room changed.";
                }
                if (!PhotonNetwork.IsMasterClient)
                    return "Host authority was lost.";
                int currentMaster = PhotonNetwork.MasterClient == null
                    ? -1
                    : PhotonNetwork.MasterClient.ActorNumber;
                if (currentMaster != request.ExecutionMasterActorNumber)
                    return "The lobby host changed.";
                if (permissions != null &&
                    request.ExecutionSessionRevision != permissions.SessionRevision)
                {
                    return "The multiplayer session changed.";
                }
            }
            else if (request.Source == CommandRequestSource.RemoteClient)
            {
                return "Remote commands require their original multiplayer room.";
            }
            else if (PhotonNetwork.InRoom)
            {
                return "The multiplayer session changed after the command began.";
            }
            return null;
        }

        private static void Dispatch(ControlRequest request)
        {
            string command = request.Command;
            if (command.StartsWith("/", StringComparison.Ordinal))
            {
                if (!SlashCommandRuntime.TryTranslateOrComplete(request, command, out command))
                    return;
            }

            string[] parts = command.Split('|');
            string action = Part(parts, 0, string.Empty).ToLowerInvariant();

            switch (action)
            {
                case "enemy":
                    BeginSpawn(
                        request,
                        SpawnKind.Enemy,
                        parts,
                        SlashCommandParser.MaximumCount,
                        "near-player");
                    return;
                case "loot":
                    BeginSpawn(
                        request,
                        SpawnKind.Loot,
                        parts,
                        SlashCommandParser.MaximumCount,
                        "safe");
                    return;
                case "item":
                    BeginSpawn(request, SpawnKind.Item, parts, 500, "safe");
                    return;
                case "cart":
                    BeginSpawn(request, SpawnKind.Cart, parts, 20, "at-player");
                    return;
                case "itemeach":
                    BeginItemEach(request, parts);
                    return;
                case "itemspread":
                    BeginBalancedItems(request, parts);
                    return;
                case "despawn":
                    DespawnEnemies(request, Part(parts, 1, "all"), ParseInt(parts, 2, 0));
                    return;
                case "despawnitem":
                    DespawnItems(request, Part(parts, 1, "all"));
                    return;
                case "despawnspawned":
                    DespawnSpawnedObjects(
                        request,
                        Part(parts, 1, "all"),
                        Part(parts, 2, "all"),
                        ParseInt(parts, 3, -1));
                    return;
                case "auto":
                    SetAutomaticEnemies(request, Part(parts, 1, "on"));
                    return;
                case "unstick":
                    UnstickLoot(request);
                    return;
                case "duplicate":
                    DuplicateLoot(request, Part(parts, 1, "loot"));
                    return;
                case "topup3":
                    TopUpLootAfterOneDuplicate(request, Part(parts, 1, "loot"));
                    return;
                case "inspect":
                    InspectLoot(request, Part(parts, 1, "loot"));
                    return;
                case "status":
                    ReportStatus(request);
                    return;
                default:
                    throw new InvalidOperationException("Unknown action '" + action + "'.");
            }
        }

        private static void BeginItemEach(ControlRequest request, string[] parts)
        {
            string selector = Part(parts, 1, "upgrade");
            int countPerType = Mathf.Clamp(ParseInt(parts, 2, 1), 1, 50);
            string placement = Part(parts, 3, "safe").ToLowerInvariant();
            PlayerAvatar player = RequireRequestPlayer(request);

            var items = new List<Item>();
            var typeNames = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in Items.AllItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemName) ||
                    item.itemName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) < 0 ||
                    !seen.Add(item.itemName))
                    continue;

                typeNames.Add(item.itemName);
                for (int index = 0; index < countPerType; index++)
                    items.Add(item);
            }

            if (items.Count == 0)
                throw new InvalidOperationException("No item types match '" + selector + "'.");

            activeItemBatchJob = new ItemBatchJob(
                request,
                items,
                typeNames,
                placement,
                countPerType,
                player.transform.position);
            ProcessItemBatchJob(activeItemBatchJob);
        }

        private static void BeginBalancedItems(ControlRequest request, string[] parts)
        {
            string selector = Part(parts, 1, "upgrade");
            int requested = Mathf.Clamp(ParseInt(parts, 2, 1), 1, 500);
            string placement = Part(parts, 3, "safe").ToLowerInvariant();
            PlayerAvatar player = RequireRequestPlayer(request);

            var candidates = new List<Item>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool weaponsOnly = selector.Equals("weapon", StringComparison.OrdinalIgnoreCase) ||
                               selector.Equals("weapons", StringComparison.OrdinalIgnoreCase);
            foreach (Item item in Items.AllItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemName))
                    continue;

                bool matches = weaponsOnly
                    ? IsWeaponItem(item)
                    : item.itemName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches || !seen.Add(item.itemName))
                    continue;
                candidates.Add(item);
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException("No item types match '" + selector + "'.");

            Shuffle(candidates);
            var items = new List<Item>(requested);
            var typeNames = new List<string>();
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < requested; index++)
            {
                Item item = candidates[index % candidates.Count];
                items.Add(item);
                if (!typeCounts.ContainsKey(item.itemName))
                    typeNames.Add(item.itemName);
                typeCounts[item.itemName] = typeCounts.ContainsKey(item.itemName)
                    ? typeCounts[item.itemName] + 1
                    : 1;
            }

            activeBalancedItemJob = new BalancedItemJob(
                request,
                items,
                typeNames,
                typeCounts,
                placement,
                player.transform.position);
            ProcessBalancedItemJob(activeBalancedItemJob);
        }

        private static void ProcessItemBatchJob(ItemBatchJob job)
        {
            try
            {
                int operations = 0;
                while (!job.Finished && operations < 10)
                {
                    Item item = job.Items[job.Spawned];
                    Vector3 position = GetPlacement(
                        job.Placement,
                        job.Anchor,
                        job.ReservedPositions);
                    GameObject spawned = Items.SpawnItem(item, position, Quaternion.identity);
                    if (spawned == null)
                        throw new InvalidOperationException(
                            "REPOLib returned no spawned item object for '" + item.itemName + "'.");

                    SpawnedObjects.Add(new SpawnedObjectRecord
                    {
                        Instance = spawned,
                        Name = item.itemName,
                        Kind = SpawnKind.Item,
                        IsWeapon = IsWeaponItem(item)
                    });
                    job.Spawned++;
                    operations++;

                    if (job.Spawned >= job.Items.Count)
                    {
                        job.Finished = true;
                        Complete(job.Request, string.Format(
                            "OK Spawned {0} item object(s): {1} each of {2} matching type(s): {3}.",
                            job.Spawned,
                            job.CountPerType,
                            job.TypeNames.Count,
                            string.Join(", ", job.TypeNames.ToArray())));
                    }
                }
            }
            catch (Exception exception)
            {
                job.Finished = true;
                Complete(job.Request, string.Format(
                    "ERROR Item batch stopped after {0}/{1}: {2}",
                    job.Spawned,
                    job.Items.Count,
                    exception.Message));
            }
        }

        private static void ProcessBalancedItemJob(BalancedItemJob job)
        {
            try
            {
                int operations = 0;
                while (!job.Finished && operations < 10)
                {
                    Item item = job.Items[job.Spawned];
                    Vector3 position = GetPlacement(
                        job.Placement,
                        job.Anchor,
                        job.ReservedPositions);
                    GameObject spawned = Items.SpawnItem(item, position, Quaternion.identity);
                    if (spawned == null)
                        throw new InvalidOperationException(
                            "REPOLib returned no spawned item object for '" + item.itemName + "'.");

                    SpawnedObjects.Add(new SpawnedObjectRecord
                    {
                        Instance = spawned,
                        Name = item.itemName,
                        Kind = SpawnKind.Item,
                        IsWeapon = IsWeaponItem(item)
                    });
                    job.Spawned++;
                    operations++;

                    if (job.Spawned >= job.Items.Count)
                    {
                        job.Finished = true;
                        var summary = new List<string>();
                        foreach (string typeName in job.TypeNames)
                            summary.Add(typeName + " x" + job.TypeCounts[typeName]);
                        Complete(job.Request, string.Format(
                            "OK Spawned {0} balanced item object(s) across {1} type(s): {2}.",
                            job.Spawned,
                            job.TypeNames.Count,
                            string.Join(", ", summary.ToArray())));
                    }
                }
            }
            catch (Exception exception)
            {
                job.Finished = true;
                Complete(job.Request, string.Format(
                    "ERROR Balanced item spread stopped after {0}/{1}: {2}",
                    job.Spawned,
                    job.Items.Count,
                    exception.Message));
            }
        }

        private static void InspectLoot(ControlRequest request, string target)
        {
            if (!target.Equals("loot", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Inspect target must be loot.");

            ValuableDirector director = ValuableDirector.instance;
            IList tracked = GetField(director, "valuableList") as IList;
            if (tracked == null)
                throw new InvalidOperationException("The tracked loot list is unavailable.");

            var trackedNames = new List<string>();
            foreach (object entry in tracked)
            {
                ValuableObject valuable = entry as ValuableObject;
                if (valuable != null && !trackedNames.Contains(valuable.gameObject.name))
                    trackedNames.Add(valuable.gameObject.name);
            }

            var registeredNames = new List<string>();
            foreach (PrefabRef prefab in Valuables.AllValuables)
            {
                if (prefab.Prefab != null)
                    registeredNames.Add(prefab.Prefab.name);
            }

            Complete(request,
                "OK Loot inspection: tracked=[" + string.Join(", ", trackedNames.ToArray()) +
                "]; registered=[" + string.Join(", ", registeredNames.ToArray()) + "].");
        }

        private static void DuplicateLoot(ControlRequest request, string target)
        {
            if (!target.Equals("loot", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Duplicate target must be loot.");

            ValuableDirector director = ValuableDirector.instance;
            IList tracked = GetField(director, "valuableList") as IList;
            if (tracked == null)
                throw new InvalidOperationException("The tracked loot list is unavailable.");

            var prefabs = new List<PrefabRef>();
            foreach (object entry in tracked)
            {
                ValuableObject valuable = entry as ValuableObject;
                if (valuable == null)
                    continue;

                PrefabRef prefab = FindValuablePrefab(valuable);
                if (prefab == null)
                    throw new InvalidOperationException(
                        "No registered valuable prefab matches existing loot '" +
                        valuable.gameObject.name + "'. No copies were spawned.");
                prefabs.Add(prefab);
            }

            if (prefabs.Count == 0)
            {
                Complete(request, "OK Duplicated 0 loot object(s); the map had no tracked loot.");
                return;
            }

            PlayerAvatar player = RequireRequestPlayer(request);
            Shuffle(prefabs);
            activeDuplicateLootJob = new DuplicateLootJob(request, prefabs, player.transform.position);
            ProcessDuplicateLootJob(activeDuplicateLootJob);
        }

        private static void TopUpLootAfterOneDuplicate(ControlRequest request, string target)
        {
            if (!target.Equals("loot", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Top-up target must be loot.");

            ValuableDirector director = ValuableDirector.instance;
            IList tracked = GetField(director, "valuableList") as IList;
            if (tracked == null)
                throw new InvalidOperationException("The tracked loot list is unavailable.");

            var groupedPrefabs = new List<PrefabRef>();
            var groupedCounts = new List<int>();
            foreach (object entry in tracked)
            {
                ValuableObject valuable = entry as ValuableObject;
                if (valuable == null)
                    continue;

                PrefabRef prefab = FindValuablePrefab(valuable);
                if (prefab == null)
                    throw new InvalidOperationException(
                        "No registered valuable prefab matches existing loot '" +
                        valuable.gameObject.name + "'. No copies were spawned.");

                int groupIndex = -1;
                for (int index = 0; index < groupedPrefabs.Count; index++)
                {
                    if (object.ReferenceEquals(groupedPrefabs[index], prefab))
                    {
                        groupIndex = index;
                        break;
                    }
                }

                if (groupIndex < 0)
                {
                    groupedPrefabs.Add(prefab);
                    groupedCounts.Add(1);
                }
                else
                {
                    groupedCounts[groupIndex]++;
                }
            }

            var prefabs = new List<PrefabRef>();
            for (int index = 0; index < groupedPrefabs.Count; index++)
            {
                int copies = groupedCounts[index] / 2;
                for (int copy = 0; copy < copies; copy++)
                    prefabs.Add(groupedPrefabs[index]);
            }

            if (prefabs.Count == 0)
            {
                Complete(request, "OK Added 0 loot object(s); no complete duplicated pairs were found.");
                return;
            }

            PlayerAvatar player = RequireRequestPlayer(request);
            Shuffle(prefabs);
            activeDuplicateLootJob = new DuplicateLootJob(request, prefabs, player.transform.position);
            ProcessDuplicateLootJob(activeDuplicateLootJob);
        }

        private static void ProcessDuplicateLootJob(DuplicateLootJob job)
        {
            try
            {
                int operations = 0;
                while (!job.Finished && operations < 10)
                {
                    if (job.Positions.Count < job.Prefabs.Count)
                    {
                        Vector3 position;
                        if (!TryFindClearPosition(job.Anchor, job.Positions, out position))
                            throw new InvalidOperationException(
                                "Could not reserve collision-free locations for all copies. " +
                                "No copies were spawned.");
                        job.Positions.Add(position);
                    }
                    else
                    {
                        PrefabRef prefab = job.Prefabs[job.Spawned];
                        GameObject spawned = Valuables.SpawnValuable(
                            prefab,
                            job.Positions[job.Spawned],
                            Quaternion.identity);
                        if (spawned == null)
                            throw new InvalidOperationException(
                                "REPOLib returned no spawned loot object for '" +
                                prefab.Prefab.name + "'.");
                        SpawnedObjects.Add(new SpawnedObjectRecord
                        {
                            Instance = spawned,
                            Name = prefab.Prefab.name,
                            Kind = SpawnKind.Loot,
                            IsWeapon = false
                        });
                        job.Spawned++;

                        if (job.Spawned >= job.Prefabs.Count)
                        {
                            job.Finished = true;
                            Complete(job.Request, string.Format(
                                "OK Duplicated {0} loot object(s) into distinct collision-free random locations.",
                                job.Spawned));
                        }
                    }
                    operations++;
                }
            }
            catch (Exception exception)
            {
                job.Finished = true;
                Complete(job.Request, string.Format(
                    "ERROR Loot duplication stopped after {0}/{1}: {2}",
                    job.Spawned,
                    job.Prefabs.Count,
                    exception.Message));
            }
        }

        private static void BeginSpawn(
            ControlRequest request,
            SpawnKind kind,
            string[] parts,
            int maximum,
            string defaultPlacement)
        {
            PlayerAvatar player = RequireRequestPlayer(request);
            string selector = Part(parts, 1, "random");
            int count = Mathf.Clamp(ParseInt(parts, 2, 1), 1, maximum);
            string placement = Part(parts, 3, defaultPlacement).ToLowerInvariant();
            activeJob = new SpawnJob(
                request,
                kind,
                selector,
                placement,
                count,
                player.transform.position);
            ProcessSpawnJob(activeJob);
        }

        private static void ProcessSpawnJob(SpawnJob job)
        {
            try
            {
                int operations = 0;
                while (!job.Finished && operations < 10)
                {
                    switch (job.Kind)
                    {
                        case SpawnKind.Enemy:
                            SpawnEnemyStep(job);
                            break;
                        case SpawnKind.Loot:
                            SpawnLootStep(job);
                            break;
                        case SpawnKind.Item:
                            SpawnItemStep(job);
                            break;
                        case SpawnKind.Cart:
                            SpawnCartStep(job);
                            break;
                    }

                    operations++;
                    if (job.Spawned >= job.Requested)
                    {
                        job.Finished = true;
                        string nameSummary = job.NameSummary.Format();
                        string message = string.Format(
                            "OK Spawned {0} {1} object(s){2}.",
                            job.Spawned,
                            job.Kind.ToString().ToLowerInvariant(),
                            nameSummary.Length == 0 ? string.Empty : ": " + nameSummary);
                        Complete(job.Request, message);
                    }
                }
            }
            catch (Exception exception)
            {
                job.Finished = true;
                Complete(job.Request, string.Format(
                    "ERROR Spawn stopped after {0}/{1}: {2}",
                    job.Spawned,
                    job.Requested,
                    exception.Message));
            }
        }

        private static void SpawnEnemyStep(SpawnJob job)
        {
            EnemySetup setup = FindEnemy(job.Selector);
            if (setup == null)
                throw new InvalidOperationException("No enemy matches '" + job.Selector + "'.");

            Vector3 position;
            if (job.Placement == "safe")
            {
                if (!TryFindClearEnemyPosition(
                    job.Anchor,
                    job.EnemyReservations,
                    GetEnemyClearanceVolume(setup),
                    out position))
                {
                    throw new InvalidOperationException(
                        "No additional collision-free enemy placement was found.");
                }
            }
            else if (job.Placement == "at-player")
            {
                position = SemiFunc.EnemyRoamFindPoint(job.Anchor);
            }
            else
            {
                Vector3 offset = UnityEngine.Random.insideUnitSphere * 4f;
                offset.y = 0f;
                position = SemiFunc.EnemyRoamFindPoint(job.Anchor + offset);
            }
            List<EnemyParent> spawned = Enemies.SpawnEnemy(setup, position, Quaternion.identity, false);
            if (spawned == null || spawned.Count == 0)
                throw new InvalidOperationException("The enemy setup spawned no objects.");

            var liveSpawned = new List<EnemyParent>();
            foreach (EnemyParent enemy in spawned)
            {
                if (enemy != null)
                    liveSpawned.Add(enemy);
            }
            if (liveSpawned.Count == 0)
                throw new InvalidOperationException("The enemy setup returned no live objects.");

            int needed = job.Requested - job.Spawned;
            int accepted = CommandExecutionTranslation.AcceptedEnemyCountForSetup(
                needed,
                liveSpawned.Count,
                job.Placement == "safe");
            EnemyDirector director = EnemyDirector.instance;

            for (int index = accepted; index < liveSpawned.Count; index++)
            {
                DestroyEnemyInstance(liveSpawned[index], director);
            }

            EnemyParent parent = GetEnemyParent(setup);
            string enemyName = parent == null ? "unknown" : parent.enemyName;
            for (int index = 0; index < accepted; index++)
            {
                EnemyParent acceptedEnemy = liveSpawned[index];
                SpawnedObjects.Add(new SpawnedObjectRecord
                {
                    Instance = acceptedEnemy.gameObject,
                    Name = enemyName,
                    Kind = SpawnKind.Enemy,
                    IsWeapon = false
                });
            }
            AppendName(job, enemyName, accepted);
            job.Spawned += accepted;
        }

        private static void DestroyEnemyInstance(
            EnemyParent enemy,
            EnemyDirector director)
        {
            if (enemy == null)
                return;
            if (director != null)
            {
                director.enemiesSpawned.Remove(enemy);
                LevelPoint firstSpawnPoint = EnemyFirstSpawnPointField == null
                    ? null
                    : (LevelPoint)EnemyFirstSpawnPointField.GetValue(enemy);
                List<LevelPoint> firstSpawnPoints = EnemyFirstSpawnPointsField == null
                    ? null
                    : (List<LevelPoint>)EnemyFirstSpawnPointsField.GetValue(director);
                if (firstSpawnPoint != null && firstSpawnPoints != null)
                    firstSpawnPoints.Remove(firstSpawnPoint);
            }
            if (PhotonNetwork.InRoom)
                PhotonNetwork.Destroy(enemy.gameObject);
            else
                UnityEngine.Object.Destroy(enemy.gameObject);
        }

        private static void SpawnLootStep(SpawnJob job)
        {
            PrefabRef prefab = FindValuable(job.Selector, job.Spawned);
            if (prefab == null)
                throw new InvalidOperationException("No loot matches '" + job.Selector + "'.");

            Vector3 position = GetPlacement(job);
            GameObject spawned = Valuables.SpawnValuable(prefab, position, Quaternion.identity);
            if (spawned == null)
                throw new InvalidOperationException("REPOLib returned no spawned loot object.");

            SpawnedObjects.Add(new SpawnedObjectRecord
            {
                Instance = spawned,
                Name = prefab.Prefab.name,
                Kind = SpawnKind.Loot,
                IsWeapon = false
            });
            AppendName(job, prefab.Prefab.name, 1);
            job.Spawned++;
        }

        private static void SpawnItemStep(SpawnJob job)
        {
            Item item = FindItem(job.Selector);
            if (item == null)
                throw new InvalidOperationException("No item matches '" + job.Selector + "'.");

            Vector3 position = GetPlacement(job);
            GameObject spawned = Items.SpawnItem(item, position, Quaternion.identity);
            if (spawned == null)
                throw new InvalidOperationException("REPOLib returned no spawned item object.");

            SpawnedObjects.Add(new SpawnedObjectRecord
            {
                Instance = spawned,
                Name = item.itemName,
                Kind = SpawnKind.Item,
                IsWeapon = IsWeaponItem(item)
            });
            AppendName(job, item.itemName, 1);
            job.Spawned++;
        }

        private static void SpawnCartStep(SpawnJob job)
        {
            string itemName = FindCartItemName(job.Selector);
            if (itemName == null)
                throw new InvalidOperationException("No cart item matches '" + job.Selector + "'.");

            Vector3 position = GetPlacement(job);
            GameObject spawned = PhotonNetwork.InstantiateRoomObject(
                "Items/" + itemName,
                position,
                Quaternion.identity,
                0);
            if (spawned == null)
                throw new InvalidOperationException(
                    "Photon could not spawn the cart item '" + itemName + "'.");

            SpawnedObjects.Add(new SpawnedObjectRecord
            {
                Instance = spawned,
                Name = itemName,
                Kind = SpawnKind.Cart,
                IsWeapon = false
            });
            AppendName(job, itemName, 1);
            job.Spawned++;
        }

        private static Vector3 GetPlacement(SpawnJob job)
        {
            return GetPlacement(job.Placement, job.Anchor, job.ReservedPositions);
        }

        private static Vector3 GetPlacement(
            string placement,
            Vector3 anchor,
            List<Vector3> reservedPositions)
        {
            if (placement == "at-player")
                return anchor + Vector3.up * 1.5f;

            if (placement == "near-player")
            {
                Vector3 offset = UnityEngine.Random.insideUnitSphere * 3f;
                offset.y = Math.Abs(offset.y) + 1f;
                return anchor + offset;
            }

            Vector3 safe;
            if (!TryFindClearPosition(anchor, reservedPositions, out safe))
                throw new InvalidOperationException("No additional collision-free placement was found.");
            reservedPositions.Add(safe);
            return safe;
        }

        private static bool TryFindClearPosition(
            Vector3 origin,
            List<Vector3> reserved,
            out Vector3 result)
        {
            LevelGenerator generator = LevelGenerator.Instance;
            List<LevelPoint> levelPoints = generator == null ? null : generator.LevelPathPoints;
            int collisionMask = EnemyClearancePolicy.BuildGameplaySolidMask(
                LayerMask.NameToLayer);

            for (int attempt = 0; attempt < 500; attempt++)
            {
                Vector3 seed;
                if (levelPoints != null && levelPoints.Count > 0 && attempt % 2 == 0)
                {
                    LevelPoint point = levelPoints[UnityEngine.Random.Range(0, levelPoints.Count)];
                    seed = point.transform.position;
                }
                else
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float radius = UnityEngine.Random.Range(4f, 30f);
                    seed = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                }

                Vector3 floor = SemiFunc.EnemyRoamFindPoint(seed);
                Vector3 candidate = floor + Vector3.up * 1.75f;
                bool tooClose = false;
                foreach (Vector3 existing in reserved)
                {
                    if (Vector3.Distance(candidate, existing) < 4f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                Collider[] overlaps = Physics.OverlapBox(
                    candidate,
                    new Vector3(1.35f, 1.25f, 1.35f),
                    Quaternion.identity,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);

                bool blocked = false;
                foreach (Collider overlap in overlaps)
                {
                    if (overlap != null && !overlap.isTrigger)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked)
                {
                    result = candidate;
                    return true;
                }
            }

            result = Vector3.zero;
            return false;
        }

        private static bool TryFindClearEnemyPosition(
            Vector3 origin,
            List<EnemyPlacementReservation> reserved,
            EnemyClearanceVolume clearance,
            out Vector3 result)
        {
            LevelGenerator generator = LevelGenerator.Instance;
            List<LevelPoint> levelPoints = generator == null ? null : generator.LevelPathPoints;
            int collisionMask = EnemyClearancePolicy.BuildGameplaySolidMask(
                LayerMask.NameToLayer);
            var rejectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int attempt = 0; attempt < 500; attempt++)
            {
                Vector3 seed;
                if (levelPoints != null && levelPoints.Count > 0 && attempt % 2 == 0)
                {
                    LevelPoint point = levelPoints[UnityEngine.Random.Range(0, levelPoints.Count)];
                    seed = point.transform.position;
                }
                else
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float radius = UnityEngine.Random.Range(4f, 30f);
                    seed = origin + new Vector3(
                        Mathf.Cos(angle),
                        0f,
                        Mathf.Sin(angle)) * radius;
                }

                Vector3 finalRoamPoint = SemiFunc.EnemyRoamFindPoint(seed);
                bool tooClose = false;
                foreach (EnemyPlacementReservation existing in reserved)
                {
                    float deltaX = finalRoamPoint.x - existing.Position.x;
                    float deltaZ = finalRoamPoint.z - existing.Position.z;
                    float minimumDistance =
                        clearance.HorizontalRadius + existing.HorizontalRadius + 0.5f;
                    if (deltaX * deltaX + deltaZ * deltaZ <
                        minimumDistance * minimumDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                Collider[] overlaps = Physics.OverlapBox(
                    finalRoamPoint + clearance.CenterOffset,
                    clearance.HalfExtents,
                    Quaternion.identity,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);
                bool blocked = false;
                foreach (Collider overlap in overlaps)
                {
                    if (overlap != null && !overlap.isTrigger)
                    {
                        string layerName = LayerMask.LayerToName(overlap.gameObject.layer);
                        string rejectionKey =
                            (string.IsNullOrEmpty(layerName) ?
                                overlap.gameObject.layer.ToString() : layerName) +
                            ":" + overlap.name;
                        int rejectionCount;
                        rejectionCounts.TryGetValue(rejectionKey, out rejectionCount);
                        rejectionCounts[rejectionKey] = rejectionCount + 1;
                        blocked = true;
                        break;
                    }
                }
                if (blocked)
                    continue;

                reserved.Add(new EnemyPlacementReservation(
                    finalRoamPoint,
                    clearance.HorizontalRadius));
                result = finalRoamPoint;
                return true;
            }

            string rejectionSummary = string.Empty;
            int rejectionKinds = 0;
            foreach (KeyValuePair<string, int> rejection in rejectionCounts)
            {
                if (rejectionKinds >= 8)
                    break;
                if (rejectionSummary.Length > 0)
                    rejectionSummary += ", ";
                rejectionSummary += rejection.Key + " x" + rejection.Value;
                rejectionKinds++;
            }
            Plugin.Log.LogWarning(string.Format(
                "Enemy clearance rejected all candidates. center={0}, halfExtents={1}, " +
                "radius={2:0.00}, mask={3}, blockers=[{4}]",
                clearance.CenterOffset,
                clearance.HalfExtents,
                clearance.HorizontalRadius,
                collisionMask,
                rejectionSummary));
            result = Vector3.zero;
            return false;
        }

        private static EnemyClearanceVolume GetEnemyClearanceVolume(EnemySetup setup)
        {
            Vector3 envelopeMin = new Vector3(-0.9f, 0.1f, -0.9f);
            Vector3 envelopeMax = new Vector3(0.9f, 2.4f, 0.9f);
            if (setup != null && setup.spawnObjects != null)
            {
                foreach (PrefabRef spawnObject in setup.spawnObjects)
                {
                    GameObject prefab = spawnObject == null ? null : spawnObject.Prefab;
                    if (prefab == null)
                        continue;

                    Bounds aggregate;
                    if (!TryGetAggregatePrefabBounds(prefab, out aggregate))
                        continue;

                    Vector3 rootPosition = prefab.transform.position;
                    envelopeMin = Vector3.Min(envelopeMin, aggregate.min - rootPosition);
                    envelopeMax = Vector3.Max(envelopeMax, aggregate.max - rootPosition);
                }
            }

            Vector3 padding = new Vector3(0.2f, 0.2f, 0.2f);
            envelopeMin -= padding;
            envelopeMax += padding;
            // The roam point is on the navigation floor. Probing below it
            // makes every valid location overlap the floor collider.
            envelopeMin.y = EnemyClearancePolicy.ClampProbeBottomOffset(
                envelopeMin.y);
            Vector3 centerOffset = (envelopeMin + envelopeMax) * 0.5f;
            Vector3 halfExtents = (envelopeMax - envelopeMin) * 0.5f;
            float horizontalX = Mathf.Max(
                Mathf.Abs(envelopeMin.x),
                Mathf.Abs(envelopeMax.x));
            float horizontalZ = Mathf.Max(
                Mathf.Abs(envelopeMin.z),
                Mathf.Abs(envelopeMax.z));
            float horizontalRadius = Mathf.Sqrt(
                horizontalX * horizontalX + horizontalZ * horizontalZ);
            return new EnemyClearanceVolume(
                centerOffset,
                halfExtents,
                horizontalRadius);
        }

        private static bool TryGetAggregatePrefabBounds(
            GameObject prefab,
            out Bounds aggregate)
        {
            aggregate = new Bounds();
            bool found = false;
            // A NavMeshAgent is the vanilla-authored traversal footprint for
            // an enemy. Prefer it over generic child colliders, which can
            // include large inactive query helpers unrelated to body size.
            foreach (NavMeshAgent agent in prefab.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent == null)
                    continue;
                Vector3 scale = agent.transform.lossyScale;
                float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float verticalScale = Mathf.Abs(scale.y);
                if (!EnemyClearancePolicy.IsNavigationEnvelopeUsable(
                    agent.radius,
                    agent.height,
                    agent.baseOffset,
                    horizontalScale,
                    verticalScale))
                {
                    continue;
                }

                float radius = agent.radius * horizontalScale;
                float height = agent.height * verticalScale;
                float baseOffset = agent.baseOffset * verticalScale;
                Bounds navigationBounds = new Bounds(
                    agent.transform.position +
                        Vector3.up * (baseOffset + height * 0.5f),
                    new Vector3(radius * 2f, height, radius * 2f));
                EncapsulateBounds(ref aggregate, ref found, navigationBounds);
            }
            if (found && HasUsableBounds(aggregate))
                return true;

            aggregate = new Bounds();
            found = false;
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null ||
                    !EnemyClearancePolicy.IsBodyGeometryEligible(
                        collider.enabled,
                        collider.isTrigger,
                        IsActiveInPrefabHierarchy(collider.transform, prefab.transform),
                        collider.attachedRigidbody != null))
                    continue;
                Bounds colliderBounds = collider.bounds;
                if (HasUsableBounds(colliderBounds))
                    EncapsulateBounds(ref aggregate, ref found, colliderBounds);
            }

            if (found && !HasUsableBounds(aggregate))
            {
                aggregate = new Bounds();
                found = false;
            }

            if (!found)
            {
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null ||
                        !EnemyClearancePolicy.IsBodyGeometryEligible(
                            renderer.enabled,
                            false,
                            IsActiveInPrefabHierarchy(renderer.transform, prefab.transform),
                            false))
                        continue;
                    Bounds rendererBounds = renderer.bounds;
                    if (HasUsableBounds(rendererBounds))
                        EncapsulateBounds(ref aggregate, ref found, rendererBounds);
                }
            }
            return found && HasUsableBounds(aggregate);
        }

        private static bool IsActiveInPrefabHierarchy(
            Transform componentTransform,
            Transform prefabRoot)
        {
            if (componentTransform == null || prefabRoot == null)
                return false;

            Transform current = componentTransform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    return false;
                if (current == prefabRoot)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool HasUsableBounds(Bounds candidate)
        {
            Vector3 center = candidate.center;
            Vector3 size = candidate.size;
            if (float.IsNaN(center.x) || float.IsInfinity(center.x) ||
                float.IsNaN(center.y) || float.IsInfinity(center.y) ||
                float.IsNaN(center.z) || float.IsInfinity(center.z) ||
                float.IsNaN(size.x) || float.IsInfinity(size.x) ||
                float.IsNaN(size.y) || float.IsInfinity(size.y) ||
                float.IsNaN(size.z) || float.IsInfinity(size.z))
            {
                return false;
            }
            return size.sqrMagnitude > 0.000001f;
        }

        private static void EncapsulateBounds(
            ref Bounds aggregate,
            ref bool found,
            Bounds candidate)
        {
            if (!found)
            {
                aggregate = candidate;
                found = true;
                return;
            }
            aggregate.Encapsulate(candidate);
        }

        private static void DespawnEnemies(ControlRequest request, string selector, int keep)
        {
            EnemyDirector director = EnemyDirector.instance;
            if (director == null)
                throw new InvalidOperationException("The enemy director is unavailable.");

            keep = Math.Max(0, keep);
            var matches = new List<EnemyParent>();
            foreach (EnemyParent enemy in director.enemiesSpawned.ToArray())
            {
                if (enemy == null)
                    continue;
                if (selector.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                    enemy.enemyName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    matches.Add(enemy);
            }

            int destroyed = 0;
            for (int index = keep; index < matches.Count; index++)
            {
                EnemyParent enemy = matches[index];
                director.enemiesSpawned.Remove(enemy);
                PhotonNetwork.Destroy(enemy.gameObject);
                destroyed++;
            }

            Complete(request, string.Format(
                "OK Despawned {0} matching enemy object(s); kept {1}.",
                destroyed,
                Math.Min(keep, matches.Count)));
        }

        private static void DespawnItems(ControlRequest request, string selector)
        {
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
                throw new InvalidOperationException("Only the host can despawn network items.");

            bool weaponsOnly = selector.Equals("weapon", StringComparison.OrdinalIgnoreCase) ||
                               selector.Equals("weapons", StringComparison.OrdinalIgnoreCase);
            int destroyed = 0;
            for (int index = SpawnedObjects.Count - 1; index >= 0; index--)
            {
                SpawnedObjectRecord item = SpawnedObjects[index];
                if (item.Instance == null)
                {
                    SpawnedObjects.RemoveAt(index);
                    continue;
                }

                bool itemKind = item.Kind == SpawnKind.Item || item.Kind == SpawnKind.Cart;
                bool matches = itemKind && (weaponsOnly ? item.IsWeapon :
                    selector.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                    item.Name.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!matches)
                    continue;

                DestroySpawnedObject(item);
                SpawnedObjects.RemoveAt(index);
                destroyed++;
            }

            Complete(request, string.Format(
                "OK Despawned {0} matching bridge-spawned item object(s) for '{1}'.",
                destroyed,
                selector));
        }

        private static void DespawnSpawnedObjects(
            ControlRequest request,
            string kindText,
            string selector,
            int requested)
        {
            SpawnKind? requestedKind = ParseSpawnKind(kindText);
            if (!requestedKind.HasValue && !kindText.Equals("all", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unknown spawned-object kind '" + kindText + "'.");

            int maximum = requested < 0 ? int.MaxValue : Mathf.Clamp(requested, 1, 500);
            int destroyed = 0;
            for (int index = SpawnedObjects.Count - 1;
                 index >= 0 && destroyed < maximum;
                 index--)
            {
                SpawnedObjectRecord record = SpawnedObjects[index];
                if (record.Instance == null)
                {
                    SpawnedObjects.RemoveAt(index);
                    continue;
                }

                bool kindMatches = !requestedKind.HasValue || record.Kind == requestedKind.Value ||
                    (requestedKind.Value == SpawnKind.Item && record.Kind == SpawnKind.Cart);
                bool nameMatches = selector.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                    record.Name.Equals(selector, StringComparison.OrdinalIgnoreCase);
                if (!kindMatches || !nameMatches)
                    continue;

                DestroySpawnedObject(record);
                SpawnedObjects.RemoveAt(index);
                destroyed++;
            }

            Complete(request, string.Format(
                "OK Despawned {0} matching mod-spawned {1} object(s) for '{2}'.",
                destroyed,
                kindText,
                selector));
        }

        private static SpawnKind? ParseSpawnKind(string value)
        {
            if (value.Equals("enemy", StringComparison.OrdinalIgnoreCase))
                return SpawnKind.Enemy;
            if (value.Equals("valuable", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("loot", StringComparison.OrdinalIgnoreCase))
                return SpawnKind.Loot;
            if (value.Equals("item", StringComparison.OrdinalIgnoreCase))
                return SpawnKind.Item;
            if (value.Equals("cart", StringComparison.OrdinalIgnoreCase))
                return SpawnKind.Cart;
            return null;
        }

        private static void DestroySpawnedObject(SpawnedObjectRecord record)
        {
            if (record == null || record.Instance == null)
                return;

            if (record.Kind == SpawnKind.Enemy)
            {
                EnemyParent enemy = record.Instance.GetComponent<EnemyParent>() ??
                    record.Instance.GetComponentInChildren<EnemyParent>();
                if (enemy != null)
                {
                    // Keep the director's enemy and first-spawn-point ledgers in
                    // sync just as grouped-overage cleanup does.
                    DestroyEnemyInstance(enemy, EnemyDirector.instance);
                    return;
                }
            }
            else if (record.Kind == SpawnKind.Loot && ValuableDirector.instance != null)
            {
                IList tracked = GetField(ValuableDirector.instance, "valuableList") as IList;
                ValuableObject valuable = record.Instance.GetComponent<ValuableObject>() ??
                    record.Instance.GetComponentInChildren<ValuableObject>();
                if (tracked != null && valuable != null)
                    tracked.Remove(valuable);
            }
            else if ((record.Kind == SpawnKind.Item || record.Kind == SpawnKind.Cart) &&
                     ItemManager.instance != null)
            {
                ItemAttributes attributes = record.Instance.GetComponent<ItemAttributes>() ??
                    record.Instance.GetComponentInChildren<ItemAttributes>();
                if (attributes != null)
                    ItemManager.instance.spawnedItems.Remove(attributes);
            }

            if (PhotonNetwork.InRoom)
                PhotonNetwork.Destroy(record.Instance);
            else
                UnityEngine.Object.Destroy(record.Instance);
        }

        private static void SetAutomaticEnemies(ControlRequest request, string setting)
        {
            EnemyDirector director = EnemyDirector.instance;
            if (director == null)
                throw new InvalidOperationException("The enemy director is unavailable.");

            bool enabled;
            if (setting.Equals("on", StringComparison.OrdinalIgnoreCase) || setting == "1" ||
                setting.Equals("true", StringComparison.OrdinalIgnoreCase))
                enabled = true;
            else if (setting.Equals("off", StringComparison.OrdinalIgnoreCase) || setting == "0" ||
                setting.Equals("false", StringComparison.OrdinalIgnoreCase))
                enabled = false;
            else
                throw new InvalidOperationException("Auto setting must be on or off.");

            director.enabled = enabled;
            Complete(request, "OK Automatic enemy spawning is " + (enabled ? "enabled." : "disabled."));
        }

        private static void UnstickLoot(ControlRequest request)
        {
            ValuableDirector director = ValuableDirector.instance;
            PlayerAvatar player = RequireRequestPlayer(request);
            IList tracked = GetField(director, "valuableList") as IList;
            if (tracked == null)
                throw new InvalidOperationException("The tracked loot list is unavailable.");

            var stuck = new List<PhysGrabObject>();
            foreach (object entry in tracked)
            {
                ValuableObject valuable = entry as ValuableObject;
                if (valuable == null)
                    continue;
                PhysGrabObject phys = valuable.GetComponent<PhysGrabObject>() ??
                    valuable.GetComponentInParent<PhysGrabObject>();
                if (phys != null && IsStuck(phys))
                    stuck.Add(phys);
            }

            var reserved = new List<Vector3>();
            int moved = 0;
            foreach (PhysGrabObject phys in stuck)
            {
                Vector3 destination;
                if (!TryFindClearPosition(player.transform.position, reserved, out destination))
                    break;
                reserved.Add(destination);
                phys.Teleport(destination, Quaternion.identity);
                if (phys.rb != null)
                {
                    phys.rb.velocity = Vector3.zero;
                    phys.rb.angularVelocity = Vector3.zero;
                }
                moved++;
            }

            Complete(request, "OK Moved " + moved + " stuck loot object(s) to clear positions.");
        }

        private static bool IsStuck(PhysGrabObject phys)
        {
            foreach (Collider own in phys.GetComponentsInChildren<Collider>())
            {
                if (own == null || !own.enabled || own.isTrigger)
                    continue;

                Bounds bounds = own.bounds;
                Collider[] overlaps = Physics.OverlapBox(
                    bounds.center,
                    bounds.extents * 0.95f,
                    own.transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                foreach (Collider other in overlaps)
                {
                    if (other == null || other == own || other.attachedRigidbody != null ||
                        other.transform.IsChildOf(phys.transform))
                        continue;

                    Vector3 direction;
                    float distance;
                    if (Physics.ComputePenetration(
                        own,
                        own.transform.position,
                        own.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance) &&
                        distance > 0.05f &&
                        (Mathf.Abs(direction.y) < 0.75f || distance > 0.5f))
                        return true;
                }
            }
            return false;
        }

        private static void ReportStatus(ControlRequest request)
        {
            EnemyDirector enemies = EnemyDirector.instance;
            ValuableDirector loot = ValuableDirector.instance;
            int enemyCount = enemies == null ? 0 : enemies.enemiesSpawned.Count;
            int lootCount = loot == null ? 0 : GetListCount(loot, "valuableList");
            bool automatic = enemies != null && enemies.enabled;
            Complete(request, string.Format(
                "OK Status: enemies={0}, loot={1}, automaticEnemySpawning={2}.",
                enemyCount,
                lootCount,
                automatic));
        }

        private static EnemySetup FindEnemy(string selector)
        {
            bool randomHigh = selector.Equals("random", StringComparison.OrdinalIgnoreCase) ||
                selector.Equals("randomhigh", StringComparison.OrdinalIgnoreCase);
            bool high = randomHigh || selector.Equals("high", StringComparison.OrdinalIgnoreCase);
            EnemySetup selected = null;
            EnemySetup partial = null;
            int matches = 0;

            foreach (EnemySetup candidate in Enemies.AllEnemies)
            {
                EnemyParent parent = GetEnemyParent(candidate);
                if (parent == null)
                    continue;
                if (!high)
                {
                    if (parent.enemyName.Equals(selector, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                    if (partial == null &&
                        parent.enemyName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                        partial = candidate;
                }
                if (high && parent.difficulty == EnemyParent.Difficulty.Difficulty3)
                {
                    if (!randomHigh && parent.enemyName.IndexOf("Reaper", StringComparison.OrdinalIgnoreCase) >= 0)
                        return candidate;
                    matches++;
                    if (UnityEngine.Random.Range(0, matches) == 0)
                        selected = candidate;
                }
            }
            return high ? selected : partial;
        }

        private static PrefabRef FindValuable(string selector, int index)
        {
            if (selector.Equals("expensive", StringComparison.OrdinalIgnoreCase))
                selector = ExpensiveLootNames[index % ExpensiveLootNames.Length];

            if (selector.Equals("medium", StringComparison.OrdinalIgnoreCase))
            {
                IList mediumPool = GetField(ValuableDirector.instance, "mediumValuables") as IList;
                if (mediumPool != null)
                {
                    var candidates = new List<PrefabRef>();
                    foreach (object entry in mediumPool)
                    {
                        PrefabRef prefab = entry as PrefabRef;
                        if (prefab != null && prefab.Prefab != null)
                            candidates.Add(prefab);
                    }

                    if (candidates.Count > 0)
                        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
                }

                return null;
            }

            var prefabs = Valuables.AllValuables;
            if (selector.Equals("random", StringComparison.OrdinalIgnoreCase))
                return prefabs.Count == 0 ? null : prefabs[UnityEngine.Random.Range(0, prefabs.Count)];

            PrefabRef partial = null;
            foreach (PrefabRef prefab in prefabs)
            {
                GameObject gameObject = prefab.Prefab;
                if (gameObject == null)
                    continue;
                string name = NormalizeObjectName(gameObject.name);
                if (name.Equals(selector, StringComparison.OrdinalIgnoreCase))
                    return prefab;
                if (partial == null &&
                    name.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial = prefab;
            }
            return partial;
        }

        private static PrefabRef FindValuablePrefab(ValuableObject valuable)
        {
            var names = new List<string>();
            AddObjectName(names, valuable.gameObject.name);
            AddObjectName(names, valuable.transform.root.gameObject.name);
            PhysGrabObject phys = valuable.GetComponent<PhysGrabObject>() ??
                valuable.GetComponentInParent<PhysGrabObject>();
            if (phys != null)
                AddObjectName(names, phys.gameObject.name);

            foreach (PrefabRef prefab in Valuables.AllValuables)
            {
                if (prefab.Prefab == null)
                    continue;
                string prefabName = NormalizeObjectName(prefab.Prefab.name);
                foreach (string name in names)
                {
                    if (prefabName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return prefab;
                }
            }

            PrefabRef best = null;
            int bestLength = 0;
            foreach (PrefabRef prefab in Valuables.AllValuables)
            {
                if (prefab.Prefab == null)
                    continue;
                string prefabName = NormalizeObjectName(prefab.Prefab.name);
                foreach (string name in names)
                {
                    int matchLength = Math.Min(name.Length, prefabName.Length);
                    if ((name.StartsWith(prefabName, StringComparison.OrdinalIgnoreCase) ||
                         prefabName.StartsWith(name, StringComparison.OrdinalIgnoreCase)) &&
                        matchLength > bestLength)
                    {
                        best = prefab;
                        bestLength = matchLength;
                    }
                }
            }
            return best;
        }

        private static void AddObjectName(List<string> names, string name)
        {
            string normalized = NormalizeObjectName(name);
            if (normalized.Length > 0 && !names.Contains(normalized))
                names.Add(normalized);
        }

        private static string NormalizeObjectName(string name)
        {
            string normalized = (name ?? string.Empty).Trim();
            const string cloneSuffix = "(Clone)";
            while (normalized.EndsWith(cloneSuffix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).Trim();
            return normalized;
        }

        private static void Shuffle<T>(List<T> values)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int other = UnityEngine.Random.Range(0, index + 1);
                T value = values[index];
                values[index] = values[other];
                values[other] = value;
            }
        }

        private static Item FindItem(string selector)
        {
            var items = Items.AllItems;
            if (selector.Equals("random", StringComparison.OrdinalIgnoreCase))
                return items.Count == 0 ? null : items[UnityEngine.Random.Range(0, items.Count)];

            if (selector.Equals("weapon", StringComparison.OrdinalIgnoreCase) ||
                selector.Equals("weapons", StringComparison.OrdinalIgnoreCase))
            {
                var weapons = new List<Item>();
                foreach (Item candidate in items)
                {
                    if (IsWeaponItem(candidate))
                        weapons.Add(candidate);
                }
                return weapons.Count == 0 ? null : weapons[UnityEngine.Random.Range(0, weapons.Count)];
            }

            Item partial = null;
            foreach (Item item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemName))
                    continue;
                if (item.itemName.Equals(selector, StringComparison.OrdinalIgnoreCase))
                    return item;
                if (partial == null &&
                    item.itemName.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial = item;
            }
            return partial;
        }

        private static string FindCartItemName(string selector)
        {
            StatsManager stats = StatsManager.instance;
            if (stats == null || stats.itemDictionary == null)
                throw new InvalidOperationException("The game item dictionary is unavailable.");

            bool small = selector.Equals("small", StringComparison.OrdinalIgnoreCase) ||
                         selector.Equals("pocket", StringComparison.OrdinalIgnoreCase) ||
                         selector.IndexOf("pocket", StringComparison.OrdinalIgnoreCase) >= 0;
            string preferred = small ? "Item Cart Small" : "Item Cart Medium";
            if (stats.itemDictionary.ContainsKey(preferred))
                return preferred;

            foreach (string name in stats.itemDictionary.Keys)
            {
                if (name.IndexOf("cart", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                bool isSmall = name.IndexOf("small", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("pocket", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isSmall == small)
                    return name;
            }

            return null;
        }

        private static bool IsWeaponItem(Item item)
        {
            if (item == null)
                return false;
            if (IsWeaponDescriptor(item.itemName))
                return true;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            foreach (System.Reflection.FieldInfo field in item.GetType().GetFields(flags))
            {
                if (!IsCategoryMember(field.Name))
                    continue;
                try
                {
                    object value = field.GetValue(item);
                    if (value != null && IsWeaponDescriptor(value.ToString()))
                        return true;
                }
                catch
                {
                }
            }

            foreach (System.Reflection.PropertyInfo property in item.GetType().GetProperties(flags))
            {
                if (!IsCategoryMember(property.Name) || property.GetIndexParameters().Length != 0)
                    continue;
                try
                {
                    object value = property.GetValue(item, null);
                    if (value != null && IsWeaponDescriptor(value.ToString()))
                        return true;
                }
                catch
                {
                }
            }
            return false;
        }

        private static bool IsCategoryMember(string name)
        {
            string lower = (name ?? string.Empty).ToLowerInvariant();
            return lower.Contains("type") || lower.Contains("category") ||
                   lower.Contains("class") || lower.Contains("kind") ||
                   lower.Contains("tag") || lower.Contains("weapon");
        }

        private static bool IsWeaponDescriptor(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            char[] normalized = value.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < normalized.Length; i++)
            {
                if (!char.IsLetterOrDigit(normalized[i]))
                    normalized[i] = ' ';
            }
            string padded = " " + new string(normalized) + " ";
            foreach (string term in WeaponTerms)
            {
                if (padded.IndexOf(" " + term + " ", StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        internal static EnemyParent GetEnemyParent(EnemySetup setup)
        {
            foreach (PrefabRef spawnObject in setup.spawnObjects)
            {
                GameObject prefab = spawnObject.Prefab;
                if (prefab == null)
                    continue;
                EnemyParent parent = prefab.GetComponent<EnemyParent>();
                if (parent != null)
                    return parent;
            }
            return null;
        }

        private static PlayerAvatar RequireLocalPlayer()
        {
            PlayerAvatar player = SemiFunc.PlayerAvatarLocal();
            if (player == null)
                throw new InvalidOperationException("The local player is unavailable.");
            return player;
        }

        private static PlayerAvatar RequireRequestPlayer(ControlRequest request)
        {
            if (request != null && request.RequesterActorNumber > 0 && PhotonNetwork.InRoom)
            {
                List<PlayerAvatar> players = SemiFunc.PlayerGetList();
                if (players != null)
                {
                    foreach (PlayerAvatar player in players)
                    {
                        if (player == null)
                            continue;

                        PhotonView view = player.photonView != null
                            ? player.photonView
                            : player.GetComponent<PhotonView>();
                        if (view != null && view.Owner != null &&
                            view.Owner.ActorNumber == request.RequesterActorNumber)
                            return player;
                    }
                }

                throw new InvalidOperationException(
                    "The requesting player (actor " + request.RequesterActorNumber + ") is unavailable.");
            }

            return RequireLocalPlayer();
        }

        private static object GetField(object instance, string name)
        {
            if (instance == null)
                return null;
            var field = AccessTools.Field(instance.GetType(), name);
            return field == null ? null : field.GetValue(instance);
        }

        private static int GetListCount(object instance, string name)
        {
            ICollection collection = GetField(instance, name) as ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static string Part(string[] parts, int index, string fallback)
        {
            return parts.Length > index && !string.IsNullOrWhiteSpace(parts[index])
                ? parts[index].Trim()
                : fallback;
        }

        private static int ParseInt(string[] parts, int index, int fallback)
        {
            int value;
            return int.TryParse(Part(parts, index, fallback.ToString()), out value) ? value : fallback;
        }

        private static void AppendName(SpawnJob job, string name, int count)
        {
            job.NameSummary.Add(name, count);
        }

        internal static void Complete(ControlRequest request, string result)
        {
            Debug.Log("[Codex Live Control] " + result);
            request.Complete(result);
        }
    }

    [HarmonyPatch(typeof(RunManager), "Update")]
    internal static class MainThreadPatch
    {
        private static void Prefix()
        {
            Bridge.ProcessFrame();
        }
    }
}
