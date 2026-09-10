using Mono.Cecil;

// Read metadata/IL without loading or executing any game code. The normal plugin build
// checks public calls; this checks the private fields and RPC wire signatures it cannot check.
string game = args.Length == 0 ? @"C:\Program Files (x86)\Steam\steamapps\common\REPO" : args[0];
using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(game, "REPO_Data", "Managed", "Assembly-CSharp.dll"));
int checks = 0;
TypeDefinition Type(string name) => assembly.MainModule.Types.Single(type => type.Name == name);
void Field(string type, string name, string fieldType)
{
    var field = Type(type).Fields.Single(field => field.Name == name);
    if (field.FieldType.Name != fieldType) throw new Exception(type + "." + name + " changed type.");
    checks++;
}
void Rpc(string type, string name, string guard, params string[] wireTypes)
{
    var method = Type(type).Methods.Single(method => method.Name == name);
    var parameters = method.Parameters.Where(parameter => parameter.ParameterType.Name != "PhotonMessageInfo").Select(parameter => parameter.ParameterType.Name);
    if (!parameters.SequenceEqual(wireTypes) || !method.CustomAttributes.Any(attribute => attribute.AttributeType.Name == "PunRPC"))
        throw new Exception(type + "." + name + " changed its RPC signature.");
    if (guard != null && !method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == guard))
        throw new Exception(type + "." + name + " changed authority checks; review the host/owner route.");
    checks++;
}
Field("PlayerAvatar", "deadSet", "Boolean");
Field("PlayerAvatar", "tumble", "PlayerTumble");
Field("PlayerAvatar", "playerDeathHead", "PlayerDeathHead");
Field("PlayerAvatar", "overrrideAnimationSpeedTimer", "Single");
Field("PlayerAvatar", "overridePupilSizeTimer", "Single");
Field("PlayerHealth", "health", "Int32");
Field("PlayerHealth", "maxHealth", "Int32");
Field("PlayerHealth", "healthSet", "Boolean");
Field("PlayerTumble", "setup", "Boolean");
Field("PlayerDeathHead", "physGrabObject", "PhysGrabObject");
Rpc("PlayerHealth", "UpdateHealthRPC", "MasterAndOwnerOnlyRPC", "Int32", "Int32", "Boolean", "Boolean");
Rpc("PlayerAvatar", "OverrideAnimationSpeedActivateRPC", "OwnerOnlyRPC", "Boolean", "Single", "Single", "Single", "Single");
Rpc("PlayerAvatar", "OverridePupilSizeActivateRPC", "MasterAndOwnerOnlyRPC", "Boolean", "Single", "Int32", "Single", "Single", "Single", "Single", "Single");
Rpc("PlayerAvatar", "PlayerExpressionSetRPC", "OwnerOnlyRPC", "Int32", "Single");
Rpc("PlayerAvatar", "FallingSetRPC", "OwnerOnlyRPC", "Boolean");
Rpc("PlayerAvatar", "ResetPhysPusher", null);
Rpc("PlayerAvatar", "SpawnRPC", "MasterOnlyRPC", "Vector3", "Quaternion");
Rpc("PlayerAvatar", "ReviveRPC", "MasterOnlyRPC", "Boolean");
Rpc("PlayerAvatar", "PlayerDeathRPC", "MasterAndOwnerOnlyRPC", "Int32");
Rpc("PlayerAvatar", "ForceImpulseRPC", "MasterAndOwnerOnlyRPC", "Vector3");
Rpc("PlayerAvatar", "ChatMessageSendRPC", "MasterAndOwnerOnlyRPC", "String", "Boolean");
Rpc("PlayerAvatar", "FlashlightFlickerRPC", "MasterOnlyRPC", "Single");
Rpc("PlayerAvatar", "UpgradeTumbleWingsVisualsActiveRPC", "MasterAndOwnerOnlyRPC", "Boolean", "Boolean");
Rpc("PlayerTumble", "TumbleSetRPC", "MasterAndOwnerOnlyRPC", "Boolean", "Boolean");
Rpc("PlayerTumble", "TumbleOverrideTimeRPC", "MasterAndOwnerOnlyRPC", "Single");

// Upgrade reset uses the game's master-authorized delta RPC so unmodded owners
// also recalculate their movement and grab state. Stats synchronization alone
// updates only dictionaries and cannot remove already-applied live bonuses.
Rpc("PunManager", "TesterUpgradeCommandRPC", "MasterOnlyRPC", "String", "String", "Int32");
Rpc("PunManager", "UpdateStatRPC", "MasterOnlyRPC", "String", "String", "Int32");
Field("PunManager", "statsManager", "StatsManager");
Field("StatsManager", "dictionaryOfDictionaries", "SortedDictionary`2");
var upgradeRpc = Type("PunManager").Methods.Single(method => method.Name == "TesterUpgradeCommandRPC");
var upgrades = new[]
{
    ("CrouchRest", "UpgradePlayerCrouchRest", "UpdateCrouchRestRightAway"),
    ("DeathHeadBattery", "UpgradeDeathHeadBattery", "UpdateDeathHeadBatteryRightAway"),
    ("ExtraJump", "UpgradePlayerExtraJump", "UpdateExtraJumpRightAway"),
    ("Health", "UpgradePlayerHealth", "UpdateHealthRightAway"),
    ("Launch", "UpgradePlayerTumbleLaunch", "UpdateTumbleLaunchRightAway"),
    ("MapPlayerCount", "UpgradeMapPlayerCount", "UpdateMapPlayerCountRightAway"),
    ("Range", "UpgradePlayerGrabRange", "UpdateGrabRangeRightAway"),
    ("Speed", "UpgradePlayerSprintSpeed", "UpdateSprintSpeedRightAway"),
    ("Stamina", "UpgradePlayerEnergy", "UpdateEnergyRightAway"),
    ("Strength", "UpgradePlayerGrabStrength", "UpdateGrabStrengthRightAway"),
    ("Throw", "UpgradePlayerThrowStrength", "UpdateThrowStrengthRightAway"),
    ("TumbleClimb", "UpgradePlayerTumbleClimb", "UpdateTumbleClimbRightAway"),
    ("TumbleWings", "UpgradePlayerTumbleWings", "UpdateTumbleWingsRightAway")
};
foreach (var (name, apply, live) in upgrades)
{
    Field("StatsManager", "playerUpgrade" + name, "Dictionary`2");
    if (!upgradeRpc.Body.Instructions.Any(instruction => instruction.Operand is string value && value == name) ||
        !upgradeRpc.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == apply))
        throw new Exception("TesterUpgradeCommandRPC no longer handles " + name + ".");
    var method = Type("PunManager").Methods.Single(method => method.Name == apply);
    if (method.ReturnType.Name != "Int32" ||
        !method.Parameters.Select(parameter => parameter.ParameterType.Name).SequenceEqual(new[] { "String", "Int32" }) ||
        !method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.DeclaringType.FullName == "System.Math" && call.Name == "Max") ||
        !method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == live))
        throw new Exception(apply + " changed its clamped count/live-update contract; review negative upgrade deltas.");
    checks++;
}
var updateStat = Type("PunManager").Methods.Single(method => method.Name == "UpdateStatRPC");
if (!updateStat.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "DictionaryUpdateValue"))
    throw new Exception("UpdateStatRPC no longer updates the registered statistics dictionary.");
var dictionaryUpdate = Type("StatsManager").Methods.Single(method => method.Name == "DictionaryUpdateValue");
if (!dictionaryUpdate.Body.Instructions.Any(instruction => instruction.Operand is FieldReference field && field.Name == "stripTheseDictionaries") ||
    !dictionaryUpdate.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "Remove") ||
    !dictionaryUpdate.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "set_Item"))
    throw new Exception("DictionaryUpdateValue changed its default-value removal contract; missing upgrade keys must count as zero.");
checks++;
var steamId = Type("SemiFunc").Methods.Single(method => method.Name == "PlayerGetSteamID");
var avatarFromId = Type("SemiFunc").Methods.Single(method => method.Name == "PlayerAvatarGetFromSteamID");
if (!steamId.Body.Instructions.Any(instruction => instruction.Operand is FieldReference field && field.Name == "steamID") ||
    !avatarFromId.Body.Instructions.Any(instruction => instruction.Operand is FieldReference field && field.Name == "steamID"))
    throw new Exception("Player upgrade identity mapping no longer uses PlayerAvatar.steamID.");
checks++;
var updateHealth = Type("PlayerHealth").Methods.Single(method => method.Name == "UpdateHealthRPC");
foreach (string field in new[] { "maxHealth", "health" })
{
    if (!updateHealth.Body.Instructions.Any(instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && instruction.Operand is FieldReference target && target.Name == field))
        throw new Exception("UpdateHealthRPC no longer sets " + field + ".");
}
if (updateHealth.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call &&
    (call.Name == "Hurt" || call.Name == "PlayerDeath" || call.Name == "Revive")))
    throw new Exception("UpdateHealthRPC gained death/revive side effects; review nonlethal upgrade reset.");
checks++;
var falling = Type("PlayerAvatar").Methods.Single(method => method.Name == "FallingSet");
if (falling.Parameters.Count != 1 || falling.Parameters[0].Name != "_falling" || falling.Parameters[0].ParameterType.Name != "Boolean")
    throw new Exception("FallingSet Harmony target changed.");
Console.WriteLine($"PASS {checks + 1} player API signature/authority contracts against {assembly.MainModule.Mvid}.");
