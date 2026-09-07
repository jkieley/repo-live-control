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
var falling = Type("PlayerAvatar").Methods.Single(method => method.Name == "FallingSet");
if (falling.Parameters.Count != 1 || falling.Parameters[0].Name != "_falling" || falling.Parameters[0].ParameterType.Name != "Boolean")
    throw new Exception("FallingSet Harmony target changed.");
Console.WriteLine($"PASS {checks + 1} player API signature/authority contracts against {assembly.MainModule.Mvid}.");
