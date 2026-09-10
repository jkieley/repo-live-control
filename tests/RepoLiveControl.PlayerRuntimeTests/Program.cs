using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using RepoLiveControl;
using RepoLiveControl.Commands;
using RepoLiveControl.Networking;
using RepoLiveControl.Runtime;
using UnityEngine;

internal static class Program
{
    private static int Main()
    {
        try
        {
            IndividualAndBulk(); ChainAndSpeech(); RevocationAndMigration(); OwnerRelay(); LateOwnerAndForgedReply();
            ResetUpgradeSelectionAndIdempotence(); ResetUpgradePeerMismatch(); ResetUpgradeHealth();
            ResetUpgradePreflight(); ResetUpgradeReadinessAndCancellation(); ResetUpgradeSingleplayer();
            Console.WriteLine("PASS 11 production player-runtime/relay scenarios with simulated game and transport."); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Setup(int players = 3)
    {
        CommandConsoleRuntime.Active = false;
        PlayerActionRuntime.ProcessFrame();
        Time.realtimeSinceStartup = Time.time = 10;
        LevelGenerator.Instance = new LevelGenerator();
        PhotonNetwork.CurrentRoom = new Room();
        PhotonNetwork.Messages.Clear(); FakeGame.Events.Clear(); SemiFunc.Players.Clear();
        for (int id = 1; id <= players; id++)
        {
            var owner = new Player { ActorNumber = id, NickName = "Player " + id };
            owner.CustomProperties[PlayerEffectRelay.Capability] = 1;
            PhotonNetwork.CurrentRoom.Players.Add(id, owner);
            var view = new PhotonView { Owner = owner, ViewID = id * 1000 + 1 };
            var avatar = new PlayerAvatar { steamID = "steam-" + id, photonView = view, NetworkView = view };
            avatar.playerHealth = new PlayerHealth { NetworkView = view }; view.Health = avatar.playerHealth;
            avatar.transform.position = new Vector3(id, 0, 0); SemiFunc.Players.Add(avatar);
        }
        PhotonNetwork.LocalPlayer = PhotonNetwork.MasterClient = PhotonNetwork.CurrentRoom.Players[1];
        FakeGame.InitializeUpgradePeers();
        Plugin.Instance = new Plugin(); CommandConsoleRuntime.Active = true;
        PlayerActionRuntime.ProcessFrame();
    }
    private static ControlRequest Start(string text, int requester = 1)
    {
        var request = new ControlRequest { ExecutionRoomIdentity = PhotonNetwork.CurrentRoom,
            ExecutionStartedInRoom = PhotonNetwork.InRoom,
            ExecutionMasterActorNumber = PhotonNetwork.MasterClient?.ActorNumber ?? -1, RequesterActorNumber = requester };
        var parsed = SlashCommandParser.Parse(text);
        Check(parsed.Success, parsed.ErrorMessage);
        PlayerActionRuntime.Begin(request, parsed.Command.PlayerAction); return request;
    }
    private static void Tick(float seconds = 0.1f) { Time.realtimeSinceStartup += seconds; Time.time += seconds; PlayerActionRuntime.ProcessFrame(); }
    private static void Finish(ControlRequest request)
    {
        for (int i = 0; request.Result == null && i < 300; i++) Tick();
        Check(request.Result != null && request.Result.StartsWith("OK"), request.Result ?? "No completion");
    }
    private static void IndividualAndBulk()
    {
        Setup(); Finish(Start("/heal \"Player 2#2\" 25"));
        Check(SemiFunc.Players[1].playerHealth.health == 75 && SemiFunc.Players[0].playerHealth.health == 50, "Heal must select only the named player");
        Finish(Start("/maxhealth all 200")); Finish(Start("/heal all"));
        Check(SemiFunc.Players.All(p => p.playerHealth.health == 200), "Maximum and full healing must reach every player");
        Finish(Start("/summon all", 2));
        Check(SemiFunc.Players.All(p => p.transform.position.x == 2), "Summon must use the submitting client position");
        foreach (string text in new[] { "/damage all 10", "/knockback all", "/flicker all", "/resetpush all", "/truck all", "/tumble all off", "/wings all off" }) Finish(Start(text));
    }
    private static void ChainAndSpeech()
    {
        Setup(); var chain = Start("/chain all kill revive heal truck"); Finish(chain);
        Check(SemiFunc.Players.All(p => !p.deadSet && p.playerHealth.health == 100), "Chain must revive and heal the same snapshot");
        int lastKill = FakeGame.Events.FindLastIndex(e => e.StartsWith("kill:"));
        int firstRevive = FakeGame.Events.FindIndex(e => e.StartsWith("revive:"));
        Check(firstRevive > lastKill, "Whole-party chain order");
        Finish(Start("/speak all /kill all"));
        Check(FakeGame.Events.Contains("speech: /kill all"), "Speech must be guarded against vanilla slash dispatch");
    }
    private static void RevocationAndMigration()
    {
        Setup(12); var batch = Start("/kill all"); Tick(); batch.Allowed = false; Tick();
        Check(batch.Result.StartsWith("ERROR") && SemiFunc.Players.Count(p => p.deadSet) == 4, "Revoke must stop remaining frame-batched players");
        Setup(); var wings = Start("/wings all pink"); Finish(wings); FakeGame.Events.Clear(); wings.Allowed = false; Tick();
        Check(FakeGame.Events.Count(e => e.StartsWith("wings:") && e.EndsWith("False")) == 3, "Revoke releases sustained wings");
        Setup(); Finish(Start("/wings all pink")); FakeGame.Events.Clear();
        PhotonNetwork.MasterClient = PhotonNetwork.CurrentRoom.Players[2]; Tick();
        Check(FakeGame.Events.Count == 0, "Old host must not send cleanup after host migration");
        Setup(); var loading = Start("/kill all"); FakeGame.Events.Clear(); CommandConsoleRuntime.Active = false; Tick();
        Check(loading.Result.StartsWith("ERROR") && FakeGame.Events.Count == 0, "Scene end cancels queued player work");
    }
    private static readonly string[] VanillaUpgrades = {
        "CrouchRest", "DeathHeadBattery", "ExtraJump", "Health", "Launch", "MapPlayerCount",
        "Range", "Speed", "Stamina", "Strength", "Throw", "TumbleClimb", "TumbleWings"
    };
    private static void SeedUpgrades(FakePeer peer, string steamId, Func<string, int> count)
    {
        foreach (string name in VanillaUpgrades)
            peer.Stats.dictionaryOfDictionaries["playerUpgrade" + name][steamId] = count(name);
        // Model the saved-level initialization separately from the RPC's incremental effects.
        PlayerAvatar player = peer.Players[steamId];
        player.upgradeCrouchRest = count("CrouchRest");
        player.upgradeDeathHeadBattery = count("DeathHeadBattery");
        player.upgradeMapPlayerCount = count("MapPlayerCount");
        player.upgradeTumbleClimb = count("TumbleClimb");
        player.upgradeTumbleWings = count("TumbleWings");
        player.tumble.tumbleLaunch = count("Launch");
        player.physGrabber.grabRange = 4f + count("Range");
        player.physGrabber.grabStrength = 1f + .2f * count("Strength");
        player.physGrabber.throwStrength = .3f * count("Throw");
        player.playerHealth.maxHealth = 100 + 20 * count("Health");
        player.playerHealth.health = Math.Min(125, player.playerHealth.maxHealth);
        if (player.photonView.Owner.ActorNumber == peer.ActorNumber)
        {
            player.Controller.EnergyStart = 100f + 10f * count("Stamina");
            player.Controller.EnergyCurrent = player.Controller.EnergyStart;
            player.Controller.JumpExtra = count("ExtraJump");
            player.Controller.SprintSpeed = 1f + count("Speed");
            player.Controller.playerOriginalSprintSpeed = 1f + count("Speed");
            player.Controller.SprintSpeedUpgrades = count("Speed");
        }
        peer.Stats.playerHealth[steamId] = player.playerHealth.health;
    }
    private static void SeedAllUpgrades()
    {
        foreach (FakePeer peer in FakeGame.Peers)
        {
            foreach (string steamId in peer.Players.Keys)
            {
                SeedUpgrades(peer, steamId, name => Array.IndexOf(VanillaUpgrades, name) + 1);
                peer.Stats.dictionaryOfDictionaries["playerUpgradeCustom"][steamId] = 71;
            }
            peer.Stats.itemBatteryUpgrades["Flashlight"] = 8;
            peer.Stats.itemsUpgradesPurchased["Upgrade Player Strength"] = 27;
        }
    }
    private static void CheckReset(FakePeer peer, string steamId)
    {
        foreach (string name in VanillaUpgrades)
            Check(peer.Stats.dictionaryOfDictionaries["playerUpgrade" + name].GetValueOrDefault(steamId, 0) == 0,
                "Reset saved " + name + " for " + steamId + " on peer " + peer.ActorNumber);
        PlayerAvatar player = peer.Players[steamId];
        Check(player.upgradeCrouchRest == 0 && player.upgradeDeathHeadBattery == 0 && player.upgradeMapPlayerCount == 0 &&
            player.upgradeTumbleClimb == 0 && player.upgradeTumbleWings == 0 && player.tumble.tumbleLaunch == 0,
            "Reset avatar and tumble effects on every replica");
        Check(Math.Abs(player.physGrabber.grabStrength - 1f) < .00001f && Math.Abs(player.physGrabber.throwStrength) < .00001f &&
            Math.Abs(player.physGrabber.grabRange - 4f) < .00001f, "Reset grab effects to baseline without double subtraction");
        Check(player.playerHealth.maxHealth == 100, "Reset health maximum on every replica");
        if (player.photonView.Owner.ActorNumber == peer.ActorNumber)
            Check(player.Controller.EnergyStart == 100f && player.Controller.EnergyCurrent == 100f && player.Controller.JumpExtra == 0 &&
                player.Controller.SprintSpeed == 1f && player.Controller.playerOriginalSprintSpeed == 1f && player.Controller.SprintSpeedUpgrades == 0f,
                "Reset the selected owner's local movement effects");
    }
    private static string PlayerState(PlayerAvatar player)
    {
        return string.Join("|", new object[] { player.steamID, player.deadSet, player.playerHealth?.health, player.playerHealth?.maxHealth,
            player.upgradeCrouchRest, player.upgradeDeathHeadBattery, player.upgradeMapPlayerCount,
            player.upgradeTumbleClimb, player.upgradeTumbleWings, player.tumble?.tumbleLaunch,
            player.physGrabber?.grabRange, player.physGrabber?.grabStrength, player.physGrabber?.throwStrength,
            player.Controller.EnergyStart, player.Controller.EnergyCurrent, player.Controller.JumpExtra,
            player.Controller.SprintSpeed, player.Controller.SprintSpeedUpgrades, player.Controller.playerOriginalSprintSpeed });
    }
    private static string Snapshot(Func<string, bool> includePlayer)
    {
        return string.Join("\n", FakeGame.Peers.Select(peer => peer.ActorNumber + ":" +
            string.Join(";", peer.Stats.dictionaryOfDictionaries.Select(pair => pair.Key + "=" +
                (pair.Value == null ? "null" : string.Join(",", pair.Value.Where(value => includePlayer(value.Key)).OrderBy(value => value.Key)
                    .Select(value => value.Key + ":" + value.Value))))) + ":" +
            string.Join(";", peer.Players.Where(pair => includePlayer(pair.Key)).Select(pair => PlayerState(pair.Value)))));
    }
    private static void ResetUpgradeSelectionAndIdempotence()
    {
        Setup(); SeedAllUpgrades();
        string unaffected = Snapshot(steam => steam != "steam-2");
        Finish(Start("/resetupgrades \"Player 2#2\""));
        Check(Snapshot(steam => steam != "steam-2") == unaffected, "Individual reset must preserve every other player and item stat");
        foreach (FakePeer peer in FakeGame.Peers)
        {
            CheckReset(peer, "steam-2");
            Check(peer.Stats.dictionaryOfDictionaries["playerUpgradeCustom"]["steam-2"] == 71, "Preserve third-party upgrade counts");
            Check(!peer.Stats.playerUpgradeHealth.ContainsKey("steam-2"), "Vanilla absolute zero setter may strip the health entry");
        }
        string firstReset = Snapshot(steam => true);
        Finish(Start("/resetupgrades \"Player 2#2\""));
        Check(Snapshot(steam => true) == firstReset, "Reset must be idempotent for both saved counts and active effects");
        Finish(Start("/resetupgrades all"));
        foreach (FakePeer peer in FakeGame.Peers)
        {
            foreach (string steamId in peer.Players.Keys)
            {
                CheckReset(peer, steamId);
                Check(peer.Stats.dictionaryOfDictionaries["playerUpgradeCustom"][steamId] == 71, "Bulk reset preserves custom upgrades");
            }
            Check(peer.Stats.itemBatteryUpgrades["Flashlight"] == 8 && peer.Stats.itemsUpgradesPurchased["Upgrade Player Strength"] == 27,
                "Bulk reset preserves item upgrades and purchase history");
        }
    }
    private static void ResetUpgradePeerMismatch()
    {
        foreach (int hostCount in new[] { 0, 2 })
        {
            Setup();
            foreach (FakePeer peer in FakeGame.Peers)
                SeedUpgrades(peer, "steam-2", name => peer.ActorNumber == 1 ? hostCount : 9);
            Finish(Start("/resetupgrades \"Player 2#2\""));
            foreach (FakePeer peer in FakeGame.Peers) CheckReset(peer, "steam-2");
        }
        Setup();
        Finish(Start("/resetupgrades all"));
        Check(FakeGame.Peers.All(peer => peer.Stats.playerUpgradeHealth.Count == 0), "Initially absent zero-count entries are valid");
    }
    private static void ResetUpgradeHealth()
    {
        foreach (var state in new[] { (Current: 5, Max: 300, Dead: false), (Current: 250, Max: 300, Dead: false),
            (Current: 0, Max: 300, Dead: true), (Current: 0, Max: 300, Dead: false), (Current: 40, Max: 50, Dead: false) })
        {
            Setup(); SeedAllUpgrades();
            foreach (FakePeer peer in FakeGame.Peers)
            {
                PlayerAvatar player = peer.Players["steam-2"];
                player.playerHealth.health = state.Current; player.playerHealth.maxHealth = state.Max; player.deadSet = state.Dead;
            }
            Finish(Start("/resetupgrades \"Player 2#2\""));
            foreach (FakePeer peer in FakeGame.Peers)
            {
                PlayerAvatar player = peer.Players["steam-2"];
                Check(player.playerHealth.health == Math.Min(state.Current, 100) && player.playerHealth.maxHealth == 100,
                    "Reset caps health without healing or applying the vanilla damaging negative upgrade");
                Check(player.deadSet == state.Dead, "Reset neither revives nor kills the target");
                Check(peer.Stats.playerHealth["steam-2"] == Math.Min(state.Current, 100), "Health RPC updates saved current health");
            }
        }
    }
    private static void ExpectResetFailure(string text)
    {
        ControlRequest request;
        try { request = Start(text); }
        catch (InvalidOperationException) { return; }
        for (int i = 0; request.Result == null && i < 50; i++) Tick();
        Check(request.Result != null && request.Result.StartsWith("ERROR"), request.Result ?? "Expected reset failure");
    }
    private static void ResetUpgradePreflight()
    {
        var cases = new Dictionary<string, Action> {
            { "missing stats", () => StatsManager.instance = null },
            { "missing pun manager", () => PunManager.instance = null },
            { "stale pun stats", () => PunManager.instance.statsManager = new StatsManager() },
            { "missing identity", () => SemiFunc.Players[1].steamID = null },
            { "blank identity", () => SemiFunc.Players[1].steamID = " " },
            { "ambiguous identity", () => SemiFunc.Players[1].steamID = SemiFunc.Players[0].steamID },
            { "duplicate identity after selected player", () => SemiFunc.Players[2].steamID = SemiFunc.Players[1].steamID },
            { "missing health", () => SemiFunc.Players[1].playerHealth = null },
            { "missing tumble", () => SemiFunc.Players[1].tumble = null },
            { "missing grabber", () => SemiFunc.Players[1].physGrabber = null },
            { "missing pun view", () => PunManager.instance.NetworkView = null },
            { "missing health view", () => SemiFunc.Players[1].playerHealth.NetworkView = null },
            { "missing dictionary", () => StatsManager.instance.dictionaryOfDictionaries.Remove("playerUpgradeTumbleWings") },
            { "null dictionary", () => StatsManager.instance.dictionaryOfDictionaries["playerUpgradeTumbleWings"] = null },
            { "stale dictionary registration", () => StatsManager.instance.playerUpgradeTumbleWings = new Dictionary<string, int>() },
            { "negative upgrade count", () => StatsManager.instance.playerUpgradeTumbleWings["steam-2"] = -1 },
            { "health initialization", () => SemiFunc.Players[1].playerHealth.healthSet = false },
            { "tumble initialization", () => SemiFunc.Players[1].tumble.setup = false },
            { "level generation", () => LevelGenerator.Instance.Generated = false },
            { "missing level generator", () => LevelGenerator.Instance = null }
        };
        foreach (var test in cases)
        {
            Setup(); SeedAllUpgrades(); test.Value();
            string before = Snapshot(steam => true);
            ExpectResetFailure("/resetupgrades \"Player 2#2\"");
            Check(FakeGame.Events.Count == 0 && Snapshot(steam => true) == before, test.Key + " must fail before any mutation");
        }
    }
    private static void ResetUpgradeReadinessAndCancellation()
    {
        Setup(); SeedAllUpgrades(); var delayed = Start("/resetupgrades all");
        Time.realtimeSinceStartup += 1f; PlayerActionRuntime.ProcessFrame();
        Check(delayed.Result == null && FakeGame.Events.Count == 0, "Paused scaled time cannot bypass vanilla initialization delay");
        Tick(.4f); Check(delayed.Result == null && FakeGame.Events.Count == 0, "Reset waits at least half a scaled second");
        Finish(delayed);
        foreach (bool migrate in new[] { false, true })
        {
            Setup(12); SeedAllUpgrades(); var batch = Start("/resetupgrades all"); Tick(.6f);
            Check(StatsManager.instance.playerUpgradeStrength.Count(pair => pair.Value == 0) == 4, "Reset uses existing four-player frame batch");
            string afterFirstBatch = Snapshot(steam => true); int events = FakeGame.Events.Count;
            if (migrate) PhotonNetwork.MasterClient = PhotonNetwork.CurrentRoom.Players[2]; else batch.Allowed = false;
            Tick();
            Check(batch.Result.StartsWith("ERROR") && FakeGame.Events.Count == events && Snapshot(steam => true) == afterFirstBatch,
                "Revocation or host migration cancels remaining reset targets");
        }
        Setup(); SeedAllUpgrades(); var loading = Start("/resetupgrades all");
        LevelGenerator.Instance.Generated = false; Tick(.6f);
        Check(loading.Result.StartsWith("ERROR") && FakeGame.Events.Count == 0, "Level readiness is revalidated before mutation");
    }
    private static void ResetUpgradeSingleplayer()
    {
        Setup(1); SeedAllUpgrades();
        PhotonNetwork.CurrentRoom = null; PhotonNetwork.MasterClient = null;
        PunManager.instance.NetworkView = null; SemiFunc.Players[0].playerHealth.NetworkView = null;
        Finish(Start("/resetupgrades all"));
        CheckReset(FakeGame.Peers[0], "steam-1");
        Check(!FakeGame.Events.Any(value => value.StartsWith("TesterUpgradeCommandRPC:") || value.StartsWith("UpdateHealthRPC:")),
            "Singleplayer resets through direct vanilla calls without requiring network views");
    }
    private static Message Take(string kind)
    {
        var message = PhotonNetwork.Messages.First(m => (string)m.Payload[2] == kind);
        PhotonNetwork.Messages.Remove(message); return message;
    }
    private static void Deliver(PlayerEffectRelay relay, Message message, int? sender = null)
    {
        relay.Receive(sender ?? message.Sender, (string)message.Payload[2], (string)message.Payload[3], (string)message.Payload[4]);
    }
    private static void OwnerRelay()
    {
        Setup(); var host = Plugin.Instance.CommandConsole.Network.PlayerEffects;
        foreach (string name in new[] { "expression", "animationspeed", "pupils", "falling" })
        {
            var request = Start("/" + name + " \"Player 2#2\""); Tick();
            Check(request.Result == null, "Host must wait for owner acknowledgement");
            var message = Take(PlayerEffectRelay.EffectKind);
            Check(message.Sender == 1 && message.Target == 2, "Only host dispatches to the selected owner");
            PhotonNetwork.LocalPlayer = PhotonNetwork.CurrentRoom.Players[2];
            var owner = new PlayerEffectRelay(198); Deliver(owner, message); owner.ProcessFrame();
            var reply = Take(PlayerEffectRelay.ResultKind);
            PhotonNetwork.LocalPlayer = PhotonNetwork.MasterClient;
            Deliver(host, reply); Tick(); Finish(request);
        }
        Check(FakeGame.Events.Contains("expression:2") && FakeGame.Events.Contains("animation:2") &&
            FakeGame.Events.Contains("pupils:2") && FakeGame.Events.Contains("falling:2:True"), "All owner routes executed on actor 2");
        SemiFunc.Players[2].photonView.Owner.CustomProperties.Clear();
        var unsupported = Start("/expression \"Player 3#3\""); Tick();
        Check(unsupported.Result.StartsWith("ERROR") && unsupported.Result.Contains("2.1.0"), "Unmodded owner must fail explicitly");
    }
    private static void LateOwnerAndForgedReply()
    {
        Setup(); var host = Plugin.Instance.CommandConsole.Network.PlayerEffects;
        var request = Start("/expression \"Player 2#2\""); Tick(); var message = Take(PlayerEffectRelay.EffectKind);
        host.Receive(3, PlayerEffectRelay.ResultKind, (string)message.Payload[3], "OK forged"); Tick();
        Check(request.Result == null, "Wrong actor cannot forge owner acknowledgement");
        Tick(6f); Check(request.Result.StartsWith("ERROR"), "Owner timeout must reach requester");
        PhotonNetwork.LocalPlayer = PhotonNetwork.CurrentRoom.Players[2];
        var owner = new PlayerEffectRelay(198); Deliver(owner, message); owner.ProcessFrame();
        Check(!FakeGame.Events.Contains("expression:2"), "Expired owner work must not mutate after a timeout");
        PhotonNetwork.LocalPlayer = PhotonNetwork.MasterClient;
    }
}
