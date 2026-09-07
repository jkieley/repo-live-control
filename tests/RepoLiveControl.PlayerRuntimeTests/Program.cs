using System;
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
            Console.WriteLine("PASS 5 production player-runtime/relay scenarios with simulated game and transport."); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Setup(int players = 3)
    {
        CommandConsoleRuntime.Active = false;
        PlayerActionRuntime.ProcessFrame();
        Time.realtimeSinceStartup = 10;
        PhotonNetwork.CurrentRoom = new Room();
        PhotonNetwork.Messages.Clear(); FakeGame.Events.Clear(); SemiFunc.Players.Clear();
        for (int id = 1; id <= players; id++)
        {
            var owner = new Player { ActorNumber = id, NickName = "Player " + id };
            owner.CustomProperties[PlayerEffectRelay.Capability] = 1;
            PhotonNetwork.CurrentRoom.Players.Add(id, owner);
            var view = new PhotonView { Owner = owner, ViewID = id * 1000 + 1 };
            var avatar = new PlayerAvatar { photonView = view, NetworkView = view };
            avatar.playerHealth = new PlayerHealth { NetworkView = view }; view.Health = avatar.playerHealth;
            avatar.transform.position = new Vector3(id, 0, 0); SemiFunc.Players.Add(avatar);
        }
        PhotonNetwork.LocalPlayer = PhotonNetwork.MasterClient = PhotonNetwork.CurrentRoom.Players[1];
        Plugin.Instance = new Plugin(); CommandConsoleRuntime.Active = true;
        PlayerActionRuntime.ProcessFrame();
    }
    private static ControlRequest Start(string text, int requester = 1)
    {
        var request = new ControlRequest { ExecutionRoomIdentity = PhotonNetwork.CurrentRoom,
            ExecutionMasterActorNumber = 1, RequesterActorNumber = requester };
        var parsed = SlashCommandParser.Parse(text);
        Check(parsed.Success, parsed.ErrorMessage);
        PlayerActionRuntime.Begin(request, parsed.Command.PlayerAction); return request;
    }
    private static void Tick(float seconds = 0.1f) { Time.realtimeSinceStartup += seconds; PlayerActionRuntime.ProcessFrame(); }
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
