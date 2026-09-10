// Minimal game/transport doubles. Production jobs, selectors, lease logic and owner relay
// are linked unchanged; these doubles do not replace the need for a real two-client test.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RepoLiveControl.Networking;
using UnityEngine;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup, time; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3();
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public float sqrMagnitude => x*x+y*y+z*z;
        public Vector3 normalized => sqrMagnitude == 0 ? zero : this * (1f / (float)Math.Sqrt(sqrMagnitude));
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x*f,a.y*f,a.z*f);
    }
    public struct Quaternion
    {
        public static Quaternion identity => new Quaternion();
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;
    }
    public static class Mathf { public static int Clamp(int n, int min, int max) => Math.Clamp(n,min,max); }
    public class Scene { public bool isLoaded = true; public bool IsValid() => true; }
    public class Transform { public Vector3 position; public Quaternion rotation; }
    public class GameObject { public Scene scene = new Scene(); public bool activeInHierarchy = true; }
    public class Component
    {
        public GameObject gameObject = new GameObject(); public Transform transform = new Transform();
        public Photon.Pun.PhotonView NetworkView;
        public T GetComponent<T>() where T : class => NetworkView as T;
    }
    public static class Resources
    {
        public static T[] FindObjectsOfTypeAll<T>() where T : class => SemiFunc.Players.OfType<T>().ToArray();
    }
}
namespace ExitGames.Client.Photon
{
    public class Hashtable : System.Collections.Hashtable { }
    public struct SendOptions { public static SendOptions SendReliable => new SendOptions(); }
}
namespace Photon.Realtime
{
    public class Player
    {
        public string NickName; public int ActorNumber;
        public ExitGames.Client.Photon.Hashtable CustomProperties = new ExitGames.Client.Photon.Hashtable();
        public void SetCustomProperties(ExitGames.Client.Photon.Hashtable properties)
        { foreach (object key in properties.Keys) CustomProperties[key] = properties[key]; }
    }
    public class Room { public Dictionary<int, Player> Players = new Dictionary<int, Player>(); }
    public class RaiseEventOptions { public int[] TargetActors; }
}
namespace Photon.Pun
{
    public enum RpcTarget { All }
    public class PhotonView
    {
        public Photon.Realtime.Player Owner; public int ViewID;
        public bool IsMine => Owner == PhotonNetwork.LocalPlayer;
        public PlayerHealth Health;
        public void RPC(string name, RpcTarget target, params object[] args)
        {
            FakeGame.Events.Add(name + ":" + Owner.ActorNumber);
            if (name == "UpdateHealthRPC")
            {
                foreach (FakePeer peer in FakeGame.Peers)
                {
                    PlayerAvatar player = peer.Players.Values.First(p => p.photonView.Owner.ActorNumber == Owner.ActorNumber);
                    player.playerHealth.UpdateHealthRPC((int)args[0],(int)args[1],(bool)args[2],(bool)args[3]);
                }
            }
            if (name == "TesterUpgradeCommandRPC")
                foreach (FakePeer peer in FakeGame.Peers)
                    PunManager.ApplyUpgrade(peer, (string)args[0], (string)args[1], (int)args[2]);
        }
    }
    public sealed class Message { public int Sender, Target; public object[] Payload; }
    public static class PhotonNetwork
    {
        public static Photon.Realtime.Player LocalPlayer, MasterClient;
        public static Photon.Realtime.Room CurrentRoom;
        public static bool InRoom => CurrentRoom != null;
        public static bool IsMasterClient => InRoom && LocalPlayer == MasterClient;
        public static int ServerTimestamp => (int)(Time.realtimeSinceStartup * 1000f);
        public static List<Message> Messages = new List<Message>();
        public static bool RaiseEvent(byte code, object[] payload, Photon.Realtime.RaiseEventOptions options, ExitGames.Client.Photon.SendOptions send)
        {
            Messages.Add(new Message { Sender = LocalPlayer.ActorNumber, Target = options.TargetActors[0], Payload = payload }); return true;
        }
    }
}
internal static class FakeGame
{
    internal static List<string> Events = new List<string>();
    internal static List<FakePeer> Peers = new List<FakePeer>();
    internal static void InitializeUpgradePeers()
    {
        Peers.Clear();
        foreach (PlayerAvatar owner in SemiFunc.Players)
        {
            var peer = new FakePeer { ActorNumber = owner.photonView.Owner.ActorNumber };
            foreach (PlayerAvatar source in SemiFunc.Players)
            {
                PlayerAvatar replica = source;
                if (peer.ActorNumber != 1)
                {
                    var view = new Photon.Pun.PhotonView { Owner = source.photonView.Owner, ViewID = source.photonView.ViewID };
                    replica = new PlayerAvatar { steamID = source.steamID, photonView = view, NetworkView = view,
                        playerHealth = new PlayerHealth { NetworkView = view } };
                    view.Health = replica.playerHealth;
                }
                peer.Players.Add(source.steamID, replica);
            }
            Peers.Add(peer);
        }
        StatsManager.instance = Peers[0].Stats;
        PunManager.instance = new PunManager { statsManager = StatsManager.instance,
            NetworkView = new Photon.Pun.PhotonView { Owner = Photon.Pun.PhotonNetwork.MasterClient, ViewID = 99 } };
    }
}
internal sealed class FakePeer
{
    internal int ActorNumber;
    internal StatsManager Stats = new StatsManager();
    internal Dictionary<string, PlayerAvatar> Players = new Dictionary<string, PlayerAvatar>();
}
public static class SemiFunc
{
    public static List<PlayerAvatar> Players = new List<PlayerAvatar>();
    public static List<PlayerAvatar> PlayerGetList() => Players;
    public static PlayerAvatar PlayerAvatarLocal() => Players.FirstOrDefault(p => p.photonView.IsMine);
    public static string PlayerGetSteamID(PlayerAvatar player) => player.steamID;
    public static PlayerAvatar PlayerAvatarGetFromSteamID(string steamId) => Players.FirstOrDefault(p => p.steamID == steamId);
    public static bool IsMasterClientOrSingleplayer() => !Photon.Pun.PhotonNetwork.InRoom || Photon.Pun.PhotonNetwork.IsMasterClient;
}
public class PlayerAvatar : Component
{
    public Photon.Pun.PhotonView photonView;
    public PlayerHealth playerHealth;
    public string steamID;
    public PhysGrabber physGrabber = new PhysGrabber();
    public FakePlayerController Controller = new FakePlayerController();
    public int upgradeMapPlayerCount, upgradeTumbleClimb, upgradeDeathHeadBattery, upgradeTumbleWings, upgradeCrouchRest;
    public PlayerExpression playerExpression = new PlayerExpression();
    public object flashlightController = new object(), upgradeTumbleWingsLogic = new object();
    public bool deadSet;
    public PlayerTumble tumble = new PlayerTumble();
    public PlayerDeathHead playerDeathHead = new PlayerDeathHead();
    public float overrrideAnimationSpeedTimer, overridePupilSizeTimer;
    public void PlayerDeath(int enemy) { deadSet = true; playerHealth.health = 0; FakeGame.Events.Add("kill:" + photonView.Owner.ActorNumber); }
    public void Revive(bool truck) { deadSet = false; playerHealth.health = 1; FakeGame.Events.Add("revive:" + photonView.Owner.ActorNumber); }
    public void Spawn(Vector3 p, Quaternion q) { transform.position = p; FakeGame.Events.Add("teleport:" + photonView.Owner.ActorNumber); }
    public void ForceImpulse(Vector3 force) { FakeGame.Events.Add("knockback:" + photonView.Owner.ActorNumber); }
    public void ChatMessageSend(string text) { FakeGame.Events.Add("speech:" + text); }
    public void FlashlightFlicker(float amount) { FakeGame.Events.Add("flicker"); }
    public void ResetPhysPusher() { FakeGame.Events.Add("resetpush"); }
    public void FallDamageResetSet(float time) { }
    public void UpgradeTumbleWingsVisualsActive(bool active, bool pink) { FakeGame.Events.Add("wings:" + photonView.Owner.ActorNumber + ":" + active); }
    public void PlayerExpressionSet(int index, float amount) { FakeGame.Events.Add("expression:" + photonView.Owner.ActorNumber); }
    public void OverrideAnimationSpeed(float a,float b,float c,float d) { FakeGame.Events.Add("animation:" + photonView.Owner.ActorNumber); }
    public void OverrideAnimationSpeedActivateRPC(bool a,float b,float c,float d,float e) { }
    public void OverridePupilSize(float a,int b,float c,float d,float e,float f,float g) { FakeGame.Events.Add("pupils:" + photonView.Owner.ActorNumber); }
    public void OverridePupilSizeActivateRPC(bool a,float b,int c,float d,float e,float f,float g,float h) { }
    private void FallingSet(bool active) { FakeGame.Events.Add("falling:" + photonView.Owner.ActorNumber + ":" + active); }
}
public class PlayerHealth : Component
{
    public int health = 50, maxHealth = 100;
    public bool healthSet = true;
    public void HealOther(int amount, bool effect) { health = Math.Min(health + amount, maxHealth); FakeGame.Events.Add("heal:" + NetworkView.Owner.ActorNumber); }
    public void HurtOther(int damage, Vector3 position, bool saving, int enemy, bool hurtByHeal) { health = Math.Max(0,health-damage); }
    public void UpdateHealthRPC(int current, int max, bool effect, bool hurtByHeal)
    {
        health = current; maxHealth = max; healthSet = true;
        foreach (FakePeer peer in FakeGame.Peers)
        {
            PlayerAvatar player = peer.Players.Values.FirstOrDefault(p => ReferenceEquals(p.playerHealth, this));
            if (player != null) peer.Stats.playerHealth[player.steamID] = current;
        }
    }
}
public class PlayerTumble { public bool setup = true; public int tumbleLaunch; public void TumbleOverrideTime(float duration) { } public void TumbleSet(bool active, bool input) { FakeGame.Events.Add("tumble:" + active); } }
public class LevelGenerator { public static LevelGenerator Instance = new LevelGenerator(); public bool Generated = true; }
public class PhysGrabber { public float grabStrength = 1f, throwStrength, grabRange = 4f; }
public class FakePlayerController
{
    public float EnergyStart = 100f, EnergyCurrent = 100f, SprintSpeed = 1f, SprintSpeedUpgrades, playerOriginalSprintSpeed = 1f;
    public int JumpExtra;
}
public class PlayerDeathHead { public PhysGrabObject physGrabObject = new PhysGrabObject(); }
public class PhysGrabObject : Component { public void Teleport(Vector3 p, Quaternion q) { transform.position = p; FakeGame.Events.Add("head-teleport"); } }
public class PlayerExpression { public List<int> expressions = Enumerable.Range(0, 5).ToList(); }
public class TruckSafetySpawnPoint : Component { public static TruckSafetySpawnPoint instance = new TruckSafetySpawnPoint(); }
public class StatsManager
{
    public static StatsManager instance;
    public SortedDictionary<string, Dictionary<string, int>> dictionaryOfDictionaries = new SortedDictionary<string, Dictionary<string, int>>();
    public Dictionary<string, int> playerUpgradeCrouchRest = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeDeathHeadBattery = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeExtraJump = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeHealth = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeLaunch = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeMapPlayerCount = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeRange = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeSpeed = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeStamina = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeStrength = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeThrow = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeTumbleClimb = new Dictionary<string, int>();
    public Dictionary<string, int> playerUpgradeTumbleWings = new Dictionary<string, int>();
    public Dictionary<string, int> playerHealth = new Dictionary<string, int>();
    public Dictionary<string, int> itemBatteryUpgrades = new Dictionary<string, int>();
    public Dictionary<string, int> itemsUpgradesPurchased = new Dictionary<string, int>();
    public StatsManager()
    {
        foreach (FieldInfo field in GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (field.FieldType == typeof(Dictionary<string, int>))
                dictionaryOfDictionaries.Add(field.Name, (Dictionary<string, int>)field.GetValue(this));
        dictionaryOfDictionaries.Add("playerUpgradeCustom", new Dictionary<string, int>());
    }
    public void DictionaryUpdateValue(string name, string key, int value)
    {
        // Vanilla strips zero player-upgrade entries from this absolute setter.
        if (name.StartsWith("playerUpgrade") && value == 0) dictionaryOfDictionaries[name].Remove(key);
        else dictionaryOfDictionaries[name][key] = value;
    }
}
public class PunManager : Component
{
    public static PunManager instance;
    public StatsManager statsManager;
    public void UpdateStat(string name, string key, int value)
    {
        FakeGame.Events.Add("stat:" + name + ":" + key);
        foreach (FakePeer peer in FakeGame.Peers) peer.Stats.DictionaryUpdateValue(name, key, value);
    }
    public void TesterUpgradeCommandRPC(string steamId, string name, int amount)
    {
        ApplyUpgrade(FakeGame.Peers.First(p => p.Stats == statsManager), steamId, name, amount);
    }
    internal static void ApplyUpgrade(FakePeer peer, string steamId, string name, int amount)
    {
        PlayerAvatar player;
        if (!peer.Players.TryGetValue(steamId, out player)) return;
        Dictionary<string, int> counts = peer.Stats.dictionaryOfDictionaries["playerUpgrade" + name];
        int count = counts.GetValueOrDefault(steamId, 0);
        // Match vanilla UpgradePlayer* effective-delta math, including its zero-count early return.
        int delta = Math.Max(0, unchecked(count + amount)) - count;
        if (delta == 0) return;
        counts[steamId] = count + delta;
        bool local = player.photonView.Owner.ActorNumber == peer.ActorNumber;
        switch (name)
        {
            case "CrouchRest": player.upgradeCrouchRest += delta; break;
            case "DeathHeadBattery": player.upgradeDeathHeadBattery += delta; break;
            case "ExtraJump": if (local) player.Controller.JumpExtra += delta; break;
            case "Health":
                player.playerHealth.maxHealth += 20 * delta;
                if (local)
                {
                    player.playerHealth.health = Math.Clamp(player.playerHealth.health + 20 * delta, 0, player.playerHealth.maxHealth);
                    if (player.playerHealth.health == 0) player.deadSet = true;
                }
                break;
            case "Launch": player.tumble.tumbleLaunch += delta; break;
            case "MapPlayerCount": player.upgradeMapPlayerCount += delta; break;
            case "Range": player.physGrabber.grabRange += delta; break;
            case "Speed":
                if (local)
                {
                    player.Controller.SprintSpeed += delta; player.Controller.SprintSpeedUpgrades += delta;
                    player.Controller.playerOriginalSprintSpeed += delta;
                }
                break;
            case "Stamina":
                if (local)
                {
                    player.Controller.EnergyStart += 10 * delta;
                    player.Controller.EnergyCurrent = player.Controller.EnergyStart;
                }
                break;
            case "Strength": player.physGrabber.grabStrength += .2f * delta; break;
            case "Throw": player.physGrabber.throwStrength += .3f * delta; break;
            case "TumbleClimb": player.upgradeTumbleClimb += delta; break;
            case "TumbleWings": player.upgradeTumbleWings += delta; break;
            default: throw new InvalidOperationException("Fake only models vanilla upgrades: " + name);
        }
    }
}
namespace RepoLiveControl
{
    internal class ControlRequest
    {
        internal bool Allowed = true, IsCancelled = false, ExecutionStartedInRoom = true;
        internal object ExecutionRoomIdentity;
        internal int ExecutionMasterActorNumber, RequesterActorNumber = 1;
        internal string Result;
    }
    internal class CommandConsoleRuntime
    {
        internal static bool Active = true;
        internal static bool IsNetworkSessionSceneActive() => Active;
        internal NetworkFacade Network = new NetworkFacade();
    }
    internal class NetworkFacade { internal PlayerEffectRelay PlayerEffects = new PlayerEffectRelay(198); }
    internal class Plugin
    {
        internal static Plugin Instance;
        internal static Logger Log = new Logger();
        internal CommandConsoleRuntime CommandConsole = new CommandConsoleRuntime();
    }
    internal class Logger { internal void LogWarning(string message) { } }
    internal static class Bridge
    {
        internal static string GetInvalidExecutionReason(ControlRequest request)
        {
            return request.Allowed && !request.IsCancelled && request.ExecutionStartedInRoom == Photon.Pun.PhotonNetwork.InRoom &&
                ReferenceEquals(request.ExecutionRoomIdentity, Photon.Pun.PhotonNetwork.CurrentRoom) &&
                (!Photon.Pun.PhotonNetwork.InRoom ||
                    request.ExecutionMasterActorNumber == Photon.Pun.PhotonNetwork.MasterClient.ActorNumber && Photon.Pun.PhotonNetwork.IsMasterClient)
                ? null : "Authorization expired";
        }
        internal static PlayerAvatar RequireRequestPlayer(ControlRequest request) => SemiFunc.Players.First(p => p.photonView.Owner.ActorNumber == request.RequesterActorNumber);
        internal static void Complete(ControlRequest request, string result) { request.Result = result; }
    }
}
