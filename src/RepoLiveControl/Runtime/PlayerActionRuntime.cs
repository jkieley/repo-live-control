using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using RepoLiveControl.Commands;
using RepoLiveControl.Networking;
using UnityEngine;

namespace RepoLiveControl.Runtime
{
    internal static class PlayerActionRuntime
    {
        private sealed class Job
        {
            internal ControlRequest Request;
            internal List<PlayerAvatar> Targets;
            internal List<PlayerActionCommand> Steps;
            internal int Step, Next, Waiting, Applied, Skipped, Failed;
            internal float Deadline, NextStepAt;
            internal float UpgradeResetAt;
            internal bool Finished;
            internal Vector3 Origin;
            internal Quaternion Rotation;
            internal readonly List<string> Errors = new List<string>();
            internal readonly List<Observation> Observations = new List<Observation>();
        }
        private sealed class Observation
        {
            internal Func<bool> Check;
            internal float Deadline;
            internal string Label;
        }
        private sealed class Effect
        {
            internal PlayerAvatar Player;
            internal PlayerActionCommand Action;
            internal ControlRequest Request;
            internal float End, Next;
            internal bool Pending;
        }
        private static readonly List<Job> Jobs = new List<Job>();
        private static readonly List<Effect> Effects = new List<Effect>();
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private static readonly MethodInfo FallingSet = AccessTools.Method(typeof(PlayerAvatar), "FallingSet");
        // Explicit vanilla list: third-party upgrade dictionaries may have their own effects.
        private static readonly string[] UpgradeNames = {
            "CrouchRest", "DeathHeadBattery", "ExtraJump", "Health", "Launch", "MapPlayerCount",
            "Range", "Speed", "Stamina", "Strength", "Throw", "TumbleClimb", "TumbleWings"
        };
        private static PlayerAvatar fallingPlayer;
        private static object fallingRoom;
        private static int fallingMaster;
        private static float fallingUntil;

        internal static void Begin(ControlRequest request, PlayerActionCommand command)
        {
            RequireHost();
            if (Jobs.Count >= 32) throw new InvalidOperationException("Too many player commands are active.");
            List<PlayerAvatar> targets = RuntimePlayerCatalog.Resolve(command.Player);
            if (targets.Count == 0) throw new InvalidOperationException("No player characters are available in this scene.");
            bool resetUpgrades = command.Name == "resetupgrades";
            if (resetUpgrades && (LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated))
                throw new InvalidOperationException("Wait for the level to finish generating before resetting upgrades.");
            var steps = command.Name == "chain"
                ? command.Arguments.Select(name => SlashCommandParser.Parse("/" + name + " " + CommandTokenizer.QuoteArgument(command.Player)).Command.PlayerAction).ToList()
                : new List<PlayerActionCommand> { command };
            Vector3 origin = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (steps.Any(step => step.Name == "summon" || step.Name == "knockback"))
            {
                PlayerAvatar requester = Bridge.RequireRequestPlayer(request);
                Transform anchor = IsDead(requester) ? RequireDeathBody(requester).transform : requester.transform;
                origin = anchor.position; rotation = anchor.rotation;
            }
            Jobs.Add(new Job { Request = request, Targets = targets, Steps = steps,
                Deadline = Time.realtimeSinceStartup + 25f, Origin = origin, Rotation = rotation,
                // Vanilla applies saved upgrades in LateStart after a 0.2s scaled wait.
                UpgradeResetAt = resetUpgrades ? Time.time + 0.5f : 0f });
        }

        internal static void ProcessFrame()
        {
            CommandConsoleRuntime console = Plugin.Instance == null ? null : Plugin.Instance.CommandConsole;
            if (!CommandConsoleRuntime.IsNetworkSessionSceneActive())
            {
                foreach (Job job in Jobs.ToArray()) { Fail(job, "The gameplay session ended."); Finish(job); }
                Effects.Clear(); fallingPlayer = null;
                if (console != null) console.Network.PlayerEffects.Reset();
                return;
            }
            if (console != null) console.Network.PlayerEffects.ProcessFrame();
            ExpireOwnerFalling();
            ProcessEffects();
            foreach (Job job in Jobs.ToArray())
            {
                string invalid = Bridge.GetInvalidExecutionReason(job.Request);
                if (invalid != null || Time.realtimeSinceStartup >= job.Deadline)
                {
                    Fail(job, invalid ?? "Player command timed out.");
                    Finish(job);
                    continue;
                }
                foreach (Observation observation in job.Observations.ToArray())
                {
                    try
                    {
                        if (observation.Check())
                        {
                            job.Applied++; job.Observations.Remove(observation);
                        }
                        else if (Time.realtimeSinceStartup >= observation.Deadline)
                        {
                            Fail(job, observation.Label + ": the game did not apply the requested change.");
                            job.Observations.Remove(observation);
                        }
                    }
                    catch (Exception exception)
                    {
                        Fail(job, exception.Message); job.Observations.Remove(observation);
                    }
                }
                if (Time.realtimeSinceStartup < job.NextStepAt) continue;
                if (Time.time < job.UpgradeResetAt) continue;
                PlayerActionCommand action = job.Steps[job.Step];
                for (int batch = 0; batch < 4 && job.Next < job.Targets.Count; batch++)
                {
                    PlayerAvatar player = job.Targets[job.Next++];
                    string label = player == null ? "Departed player" : RuntimePlayerCatalog.Choice(player).Selector;
                    try
                    {
                        RequireCurrent(player);
                        if (action.Name == "revive" && !IsDead(player) || action.Name == "kill" && IsDead(player))
                        {
                            job.Skipped++; continue;
                        }
                        if (PlayerActionCommands.RequiresOwner(action.Name))
                        {
                            ConfigureEffect(job.Request, action, player);
                            job.Waiting++;
                            try
                            {
                                SendOwner(job.Request, action, player, result =>
                                {
                                    if (job.Finished) return;
                                    job.Waiting--;
                                    if (result.StartsWith("OK", StringComparison.Ordinal)) job.Applied++;
                                    else
                                    {
                                        RemoveEffect(player, action.Name);
                                        Fail(job, label + ": " + result);
                                    }
                                });
                            }
                            catch { job.Waiting--; RemoveEffect(player, action.Name); throw; }
                        }
                        else
                        {
                            Func<bool> observed = ApplyHost(job, action, player);
                            if (observed == null || observed()) job.Applied++;
                            else job.Observations.Add(new Observation { Check = observed,
                                Deadline = Time.realtimeSinceStartup + 3f, Label = label });
                        }
                    }
                    catch (Exception exception) { Fail(job, label + ": " + exception.Message); }
                }
                if (job.Next >= job.Targets.Count && job.Waiting == 0 && job.Observations.Count == 0)
                {
                    if (job.Failed > 0 || job.Step + 1 >= job.Steps.Count) Finish(job);
                    else
                    {
                        job.Step++; job.Next = 0;
                        // Let owner death/head and health messages settle before the next whole-party step.
                        job.NextStepAt = Time.realtimeSinceStartup + (action.Name == "kill" ? 0.85f : 0.15f);
                    }
                }
            }
        }

        private static Func<bool> ApplyHost(Job job, PlayerActionCommand action, PlayerAvatar player)
        {
            RequireHost();
            switch (action.Name)
            {
                case "kill":
                    player.PlayerDeath(-1);
                    return () => player != null && IsDead(player);
                case "revive":
                    RequireDeathBody(player);
                    player.Revive(false);
                    return () => player != null && !IsDead(player);
                case "heal":
                {
                    RequireLiving(player);
                    PlayerHealth health = RequireHealth(player);
                    int before = Field<int>(health, "health"), max = Field<int>(health, "maxHealth");
                    int amount = action.Arguments[0] == "full" ? Math.Max(0, max - before) : action.Integer(0);
                    int expected = Math.Min(max, before + amount);
                    health.HealOther(amount, true);
                    return () => health != null && Field<int>(health, "health") >= expected;
                }
                case "damage":
                {
                    RequireLiving(player);
                    PlayerHealth health = RequireHealth(player);
                    int expected = Math.Max(0, Field<int>(health, "health") - action.Integer(0));
                    health.HurtOther(action.Integer(0), Vector3.zero, false, -1, false);
                    return () => player != null && (IsDead(player) || Field<int>(health, "health") <= expected);
                }
                case "maxhealth":
                {
                    PlayerHealth health = RequireHealth(player);
                    int max = action.Integer(0), current = Mathf.Clamp(Field<int>(health, "health"), 0, max);
                    PhotonView view = health.GetComponent<PhotonView>();
                    if (PhotonNetwork.InRoom)
                    {
                        if (view == null) throw new InvalidOperationException("Player health has no network view.");
                        view.RPC("UpdateHealthRPC", RpcTarget.All, current, max, true, false);
                    }
                    else health.UpdateHealthRPC(current, max, true, false);
                    return () => health != null && Field<int>(health, "maxHealth") == max;
                }
                case "resetupgrades": return ResetUpgrades(player);
                case "summon":
                    Teleport(player, job.Origin, job.Rotation); return null;
                case "truck":
                    if (TruckSafetySpawnPoint.instance == null) throw new InvalidOperationException("No truck safety spawn exists in this scene.");
                    Teleport(player, TruckSafetySpawnPoint.instance.transform.position, TruckSafetySpawnPoint.instance.transform.rotation); return null;
                case "knockback":
                    RequireLiving(player);
                    Vector3 direction = player.transform.position - job.Origin;
                    if (direction.sqrMagnitude < 0.01f) direction = job.Rotation * Vector3.forward;
                    player.ForceImpulse((direction.normalized + Vector3.up * 0.2f) * action.Number(0)); return null;
                case "speak":
                    // Prevent text from invoking vanilla DebugCommandHandler on the host.
                    string message = action.Arguments[0];
                    player.ChatMessageSend(message.TrimStart().StartsWith("/") ? " " + message : message); return null;
                case "flicker":
                    if (player.flashlightController == null) throw new InvalidOperationException("Player flashlight is unavailable.");
                    player.FlashlightFlicker(action.Number(0)); return null;
                case "resetpush": ResetPush(player); return null;
                case "tumble":
                    RequireLiving(player);
                    ConfigureEffect(job.Request, action, player);
                    SetTumble(player, action.Arguments[0] != "off"); return null;
                case "wings":
                    if (player.upgradeTumbleWingsLogic == null) throw new InvalidOperationException("Player wings visuals are unavailable.");
                    ConfigureEffect(job.Request, action, player);
                    player.UpgradeTumbleWingsVisualsActive(action.Arguments[0] != "off", action.Arguments[0] == "pink"); return null;
                default: throw new InvalidOperationException("Unsupported player action: " + action.Name);
            }
        }

        private static Func<bool> ResetUpgrades(PlayerAvatar player)
        {
            StatsManager stats = StatsManager.instance;
            PunManager pun = PunManager.instance;
            if (stats == null || pun == null || Field<StatsManager>(pun, "statsManager") != stats)
                throw new InvalidOperationException("Player upgrade stats are not ready yet.");
            string steamId = SemiFunc.PlayerGetSteamID(player);
            if (string.IsNullOrWhiteSpace(steamId) || SemiFunc.PlayerAvatarGetFromSteamID(steamId) != player ||
                RuntimePlayerCatalog.Players().Count(candidate => SemiFunc.PlayerGetSteamID(candidate) == steamId) != 1)
                throw new InvalidOperationException("The selected player's upgrade identity is unavailable or ambiguous.");
            PlayerHealth health = RequireHealth(player);
            PlayerTumble tumble = RequireTumble(player);
            if (LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
                !Field<bool>(health, "healthSet") || !Field<bool>(tumble, "setup"))
                throw new InvalidOperationException("The selected player's upgrade components are still initializing.");
            if (player.physGrabber == null)
                throw new InvalidOperationException("The selected player's grabber is not ready yet.");
            PhotonView punView = pun.GetComponent<PhotonView>();
            PhotonView healthView = health.GetComponent<PhotonView>();
            if (PhotonNetwork.InRoom && (punView == null || healthView == null))
                throw new InvalidOperationException("Player upgrades have no network view.");

            // Resolve and validate every dictionary before changing any of this player's stats.
            var registered = Field<SortedDictionary<string, Dictionary<string, int>>>(stats, "dictionaryOfDictionaries");
            var upgrades = new List<Dictionary<string, int>>();
            foreach (string name in UpgradeNames)
            {
                string key = "playerUpgrade" + name;
                Dictionary<string, int> values;
                if (registered == null || !registered.TryGetValue(key, out values) || values == null ||
                    !ReferenceEquals(values, Field<Dictionary<string, int>>(stats, key)))
                    throw new InvalidOperationException("Player upgrade dictionary is unavailable: " + key + ".");
                int count;
                if (values.TryGetValue(steamId, out count) && count < 0)
                    throw new InvalidOperationException("Player upgrade count is invalid: " + key + ".");
                upgrades.Add(values);
            }

            foreach (string name in UpgradeNames)
            {
                if (name == "Health") continue;
                // Vanilla clamps count + delta to zero and removes only the effective delta
                // from live stats. This also clears peers whose counts exceed the host's.
                // Do not set dictionaries first: that would suppress the live-stat subtraction.
                if (PhotonNetwork.InRoom)
                    punView.RPC("TesterUpgradeCommandRPC", RpcTarget.All, steamId, name, int.MinValue);
                else pun.TesterUpgradeCommandRPC(steamId, name, int.MinValue);
            }
            // The vanilla negative health-upgrade path calls Hurt on the owner. Set the
            // stored level and cap HP directly instead, preserving death and low health.
            pun.UpdateStat("playerUpgradeHealth", steamId, 0);
            int current = Mathf.Clamp(Field<int>(health, "health"), 0, 100);
            if (PhotonNetwork.InRoom)
                healthView.RPC("UpdateHealthRPC", RpcTarget.All, current, 100, false, false);
            else health.UpdateHealthRPC(current, 100, false, false);
            return () => stats != null && health != null && Field<int>(health, "maxHealth") == 100 &&
                upgrades.All(values => !values.ContainsKey(steamId) || values[steamId] == 0);
        }

        internal static void ApplyOwner(PlayerActionCommand action, PlayerAvatar player)
        {
            RequireCurrent(player);
            if (PhotonNetwork.InRoom && (player.photonView == null || !player.photonView.IsMine))
                throw new InvalidOperationException("Player effect must execute on its owner.");
            switch (action.Name)
            {
                case "expression":
                    int index = action.Integer(0);
                    if (player.playerExpression == null || player.playerExpression.expressions == null || index >= player.playerExpression.expressions.Count)
                        throw new InvalidOperationException("Expression index is outside this character's expression list.");
                    player.PlayerExpressionSet(index, 1f); break;
                case "animationspeed":
                    bool animate = action.Arguments[0] != "off";
                    if (animate) player.OverrideAnimationSpeed(action.Number(0), action.Number(1), action.Number(2), action.Number(3));
                    else
                    {
                        // The public setter refreshes the timer; expiry must be applied explicitly on the owner.
                        SetField(player, "overrrideAnimationSpeedTimer", 0f);
                        if (PhotonNetwork.InRoom) player.photonView.RPC("OverrideAnimationSpeedActivateRPC", RpcTarget.All, false, 1f, 0.05f, 0.2f, 0f);
                        else player.OverrideAnimationSpeedActivateRPC(false, 1f, 0.05f, 0.2f, 0f);
                    }
                    break;
                case "pupils":
                    bool pupils = action.Arguments[0] != "off";
                    if (pupils) player.OverridePupilSize(action.Number(0), action.Integer(1), action.Number(2), action.Number(3), action.Number(4), action.Number(5), action.Number(6));
                    else
                    {
                        SetField(player, "overridePupilSizeTimer", 0f);
                        if (PhotonNetwork.InRoom) player.photonView.RPC("OverridePupilSizeActivateRPC", RpcTarget.All, false, 1f, 10, 25f, 0.8f, 12f, 0.8f, 0f);
                        else player.OverridePupilSizeActivateRPC(false, 1f, 10, 25f, 0.8f, 12f, 0.8f, 0f);
                    }
                    break;
                case "falling":
                    RequireLiving(player);
                    fallingPlayer = action.Arguments[0] == "on" ? player : null;
                    fallingRoom = PhotonNetwork.CurrentRoom;
                    fallingMaster = PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber;
                    fallingUntil = Time.realtimeSinceStartup + 2f;
                    FallingSet.Invoke(player, new object[] { action.Arguments[0] == "on" });
                    break;
                default: throw new InvalidOperationException("The host cannot relay this action to an owner.");
            }
        }

        private static void SendOwner(ControlRequest request, PlayerActionCommand action, PlayerAvatar player, Action<string> complete)
        {
            if (!PhotonNetwork.InRoom || player.photonView.IsMine)
            {
                ApplyOwner(action, player); complete("OK Owner applied effect."); return;
            }
            if (Plugin.Instance == null || Plugin.Instance.CommandConsole == null)
                throw new InvalidOperationException("Player effect relay is unavailable.");
            Plugin.Instance.CommandConsole.Network.PlayerEffects.SendEffect(player, action,
                () => Bridge.GetInvalidExecutionReason(request) == null && IsCurrent(player), complete);
        }

        private static void ConfigureEffect(ControlRequest request, PlayerActionCommand action, PlayerAvatar player)
        {
            if (action.Name != "tumble" && action.Name != "wings" && action.Name != "falling") return;
            RemoveEffect(player, action.Name);
            if (action.Arguments[0] == "off") return;
            float duration = action.Name == "tumble" && action.Arguments[0] != "on" ? action.Number(0) : float.PositiveInfinity;
            Effects.Add(new Effect { Player = player, Request = request, Action = action,
                End = Time.realtimeSinceStartup + duration, Next = Time.realtimeSinceStartup + 0.5f });
        }

        private static void RemoveEffect(PlayerAvatar player, string name)
        {
            Effects.RemoveAll(effect => effect.Player == player && effect.Action.Name == name);
        }

        private static void ProcessEffects()
        {
            foreach (Effect effect in Effects.ToArray())
            {
                bool authority = !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
                bool current = IsCurrent(effect.Player);
                bool expired = Time.realtimeSinceStartup >= effect.End ||
                    Bridge.GetInvalidExecutionReason(effect.Request) != null || !current || IsDead(effect.Player);
                if (expired)
                {
                    Effects.Remove(effect);
                    // Never issue cleanup RPCs into another room or under a different host.
                    if (authority && current && SameRoom(effect.Request))
                    {
                        try
                        {
                            if (effect.Action.Name == "tumble") SetTumble(effect.Player, false);
                            if (effect.Action.Name == "wings") effect.Player.UpgradeTumbleWingsVisualsActive(false, false);
                            // Falling is an owner lease and expires without further host traffic.
                        }
                        catch (Exception exception) { LogEffectError(exception.Message); }
                    }
                    continue;
                }
                if (effect.Pending || Time.realtimeSinceStartup < effect.Next) continue;
                effect.Next = Time.realtimeSinceStartup + 0.5f;
                try
                {
                    if (effect.Action.Name == "tumble") RequireTumble(effect.Player).TumbleOverrideTime(1f);
                    else if (effect.Action.Name == "wings") effect.Player.UpgradeTumbleWingsVisualsActive(true, effect.Action.Arguments[0] == "pink");
                    else
                    {
                        effect.Pending = true;
                        SendOwner(effect.Request, effect.Action, effect.Player, result =>
                        {
                            effect.Pending = false;
                            if (!result.StartsWith("OK", StringComparison.Ordinal))
                            {
                                Effects.Remove(effect); LogEffectError(result);
                            }
                        });
                    }
                }
                catch (Exception exception) { Effects.Remove(effect); LogEffectError(exception.Message); }
            }
        }

        internal static bool OverrideFalling(PlayerAvatar player)
        {
            return fallingPlayer == player && Time.realtimeSinceStartup < fallingUntil &&
                ReferenceEquals(fallingRoom, PhotonNetwork.CurrentRoom) &&
                fallingMaster == (PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber);
        }

        private static void ExpireOwnerFalling()
        {
            if (fallingPlayer == null || OverrideFalling(fallingPlayer)) return;
            PlayerAvatar previous = fallingPlayer;
            fallingPlayer = null;
            if (IsCurrent(previous) && ReferenceEquals(fallingRoom, PhotonNetwork.CurrentRoom) &&
                fallingMaster == (PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber))
                FallingSet.Invoke(previous, new object[] { false });
        }

        private static bool SameRoom(ControlRequest request)
        {
            return request.ExecutionStartedInRoom == PhotonNetwork.InRoom &&
                ReferenceEquals(request.ExecutionRoomIdentity, PhotonNetwork.CurrentRoom) &&
                request.ExecutionMasterActorNumber == (PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber);
        }

        private static void Teleport(PlayerAvatar player, Vector3 position, Quaternion rotation)
        {
            if (IsDead(player)) { RequireDeathBody(player).Teleport(position, rotation); return; }
            RemoveEffect(player, "tumble"); RemoveEffect(player, "falling");
            SetTumble(player, false);
            ResetPush(player);
            player.FallDamageResetSet(2f);
            player.Spawn(position, rotation);
        }

        private static void SetTumble(PlayerAvatar player, bool active)
        {
            PlayerTumble tumble = RequireTumble(player);
            tumble.TumbleOverrideTime(active ? 1f : 0f);
            tumble.TumbleSet(active, false);
        }

        private static void ResetPush(PlayerAvatar player)
        {
            if (PhotonNetwork.InRoom) player.photonView.RPC("ResetPhysPusher", RpcTarget.All);
            else player.ResetPhysPusher();
        }

        internal static bool IsDead(PlayerAvatar player) { return player == null || Field<bool>(player, "deadSet"); }
        private static void RequireLiving(PlayerAvatar player)
        {
            if (IsDead(player)) throw new InvalidOperationException("Player is dead; revive them first.");
        }
        private static PlayerHealth RequireHealth(PlayerAvatar player)
        {
            if (player.playerHealth == null) throw new InvalidOperationException("Player health is unavailable.");
            return player.playerHealth;
        }
        private static PlayerTumble RequireTumble(PlayerAvatar player)
        {
            PlayerTumble tumble = Field<PlayerTumble>(player, "tumble");
            if (tumble == null) throw new InvalidOperationException("Player tumble body is unavailable.");
            return tumble;
        }
        private static PhysGrabObject RequireDeathBody(PlayerAvatar player)
        {
            PlayerDeathHead head = Field<PlayerDeathHead>(player, "playerDeathHead");
            PhysGrabObject body = head == null ? null : Field<PhysGrabObject>(head, "physGrabObject");
            if (body == null || !body.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Player death head is not available yet.");
            return body;
        }
        private static bool IsCurrent(PlayerAvatar player)
        {
            return player != null && player.gameObject.scene.IsValid() && player.gameObject.scene.isLoaded &&
                (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom != null &&
                    PhotonNetwork.CurrentRoom.Players.ContainsKey(RuntimePlayerCatalog.Actor(player)));
        }
        private static void RequireCurrent(PlayerAvatar player)
        {
            if (!IsCurrent(player)) throw new InvalidOperationException("Player left or the character's scene unloaded.");
        }
        private static void RequireHost()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) throw new InvalidOperationException("Only the host executor can run player commands.");
        }
        private static FieldInfo FindField(object instance, string name)
        {
            string key = instance.GetType().FullName + "." + name;
            FieldInfo field;
            if (!Fields.TryGetValue(key, out field))
            {
                field = AccessTools.Field(instance.GetType(), name);
                if (field == null) throw new MissingFieldException(key);
                Fields.Add(key, field);
            }
            return field;
        }
        private static T Field<T>(object instance, string name) { return (T)FindField(instance, name).GetValue(instance); }
        private static void SetField(object instance, string name, object value) { FindField(instance, name).SetValue(instance, value); }
        private static void Fail(Job job, string error)
        {
            job.Failed++;
            if (job.Errors.Count < 3) job.Errors.Add(error);
        }
        private static void Finish(Job job)
        {
            job.Finished = true; Jobs.Remove(job);
            Bridge.Complete(job.Request, (job.Failed > 0 ? "ERROR " : "OK ") +
                "Player actions: applied=" + job.Applied + ", skipped=" + job.Skipped + ", failed=" + job.Failed +
                ", planned=" + job.Targets.Count * job.Steps.Count + "." +
                (job.Errors.Count == 0 ? "" : " " + string.Join("; ", job.Errors)));
        }
        private static void LogEffectError(string error)
        {
            if (Plugin.Log != null) Plugin.Log.LogWarning("Player effect stopped: " + error);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "FallingSet")]
    internal static class CommandFallingOverridePatch
    {
        private static void Prefix(PlayerAvatar __instance, ref bool _falling)
        {
            if (PlayerActionRuntime.OverrideFalling(__instance)) _falling = true;
        }
    }
}
