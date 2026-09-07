using Mono.Cecil;
using Mono.Cecil.Cil;

// Inspect the actual compiled plugin and installed Unity API without loading or
// executing game code. These safety checks protect the preview isolation boundary.
string repo = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string game = args.Length > 1 ? args[1] : @"C:\Program Files (x86)\Steam\steamapps\common\REPO";
string pluginPath = Path.Combine(repo, "src", "RepoLiveControl", "bin", "Debug", "netstandard2.1", "RepoCommandConsole.dll");
if (args.Length > 2) pluginPath = Path.GetFullPath(args[2]);
using var plugin = AssemblyDefinition.ReadAssembly(pluginPath);
using var core = AssemblyDefinition.ReadAssembly(Path.Combine(game, "REPO_Data", "Managed", "UnityEngine.CoreModule.dll"));
using var gameAssembly = AssemblyDefinition.ReadAssembly(Path.Combine(game, "REPO_Data", "Managed", "Assembly-CSharp.dll"));
var service = plugin.MainModule.Types.Single(type => type.Name == "RuntimeTargetPreviewService");
int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
{
    yield return type;
    foreach (var child in type.NestedTypes.SelectMany(AllTypes)) yield return child;
}
var methods = AllTypes(service).SelectMany(type => type.Methods).Where(method => method.HasBody).ToArray();
var calls = methods.SelectMany(method => method.Body.Instructions)
    .Select(instruction => instruction.Operand).OfType<MethodReference>().ToArray();

Check(!calls.Any(call => call.Name == "Instantiate"), "Preview service must not instantiate prefabs or components.");
Check(!calls.Any(call => call.DeclaringType.FullName == "UnityEngine.GameObject" &&
                        (call.Name == ".ctor" || call.Name == "AddComponent" || call.Name == "SetActive")),
      "Preview service must not create or activate scene objects.");
Check(!calls.Any(call => call.Name == "set_enabled"), "Preview service must not enable source components.");
Check(!calls.Any(call => call.DeclaringType.FullName.StartsWith("Photon.") ||
                        call.DeclaringType.FullName.StartsWith("REPOLib.Modules.")),
      "Preview service must not call network or spawn modules.");
Check(!calls.Any(call => call.DeclaringType.Name == "SemiIconMaker" ||
                        call.DeclaringType.Name == "ItemAttributes" && call.Name == "GenerateIcon"),
      "Game icon-generation routines mutate objects and cannot be called by previews.");
Check(!calls.Any(call => call.DeclaringType.FullName == "System.IO.File" && call.Name.StartsWith("Write")),
      "Native icon reuse must not write game-cache files.");
Check(!methods.SelectMany(method => method.Body.Instructions).Any(instruction =>
        instruction.OpCode == OpCodes.Stfld && instruction.Operand is FieldReference field &&
        !field.DeclaringType.FullName.StartsWith(service.FullName)),
      "Preview service must not mutate game fields.");

var getter = service.Methods.Single(method => method.Name == "GetOrQueue");
var update = service.Methods.Single(method => method.Name == "Update");
Check(getter.ReturnType.FullName == "UnityEngine.Texture" && getter.Parameters.Count == 1 &&
      getter.Parameters[0].ParameterType.FullName == "System.String", "Preview lookup contract changed.");
Check(!getter.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call &&
      (call.Name == "Generate" || call.Name == "ExecuteCommandBuffer" || call.Name == "ReadPixels")),
      "IMGUI lookup must not perform render/readback work.");
Check(update.Body.Instructions.Count(instruction => instruction.Operand is MethodReference call && call.Name == "Generate") == 1,
      "Update must contain one generation call, not a queue-draining batch.");
Check(update.Body.ExceptionHandlers.Any(handler => handler.HandlerType == ExceptionHandlerType.Catch &&
      handler.CatchType.FullName == "System.Exception"), "Preview failures must be isolated from the game update.");
Check(service.Methods.Any(method => method.Name == "Dispose"), "Preview service must release its owned cache.");
Check((int)service.Fields.Single(field => field.Name == "CacheCapacity").Constant <= 256,
      "Thumbnail cache exceeded its memory cap.");
Check((int)service.Fields.Single(field => field.Name == "QueueCapacity").Constant <= 128,
      "Thumbnail request queue must stay bounded.");
Check((int)service.Fields.Single(field => field.Name == "TextureSize").Constant <= 128,
      "Thumbnail render resolution exceeded the compact-row budget.");

TypeDefinition Core(string name) => core.MainModule.Types.Single(type => type.FullName == name);
Check(Core("UnityEngine.Sprite").Methods.Any(method => method.Name == "get_vertices") &&
      Core("UnityEngine.Sprite").Methods.Any(method => method.Name == "get_uv"),
      "Sprite atlas-aware geometry API is unavailable.");
Check(Core("UnityEngine.SkinnedMeshRenderer").Methods.Any(method => method.Name == "BakeMesh" &&
      method.Parameters.Count == 1 && method.Parameters[0].ParameterType.Name == "Mesh"),
      "Read-only skinned mesh baking API changed.");
Check(Core("UnityEngine.Graphics").Methods.Any(method => method.Name == "ExecuteCommandBuffer"),
      "Offscreen command-buffer rendering API is unavailable.");
Check(Core("UnityEngine.Rendering.CommandBuffer").Methods.Any(method => method.Name == "SetViewProjectionMatrices"),
      "Offscreen view/projection API is unavailable.");
var materialFactory = service.Methods.Single(method => method.Name == "MakePreviewMaterial");
Check(materialFactory.Body.Instructions.Any(instruction => instruction.OpCode == OpCodes.Ldstr &&
      (string)instruction.Operand == "Standard"),
      "Mesh previews must use the forward shader verified against GPU-only game meshes.");
Check(materialFactory.Body.Instructions.Any(instruction => instruction.OpCode == OpCodes.Ldstr &&
      (string)instruction.Operand == "_EmissionMap"),
      "Preview albedo must remain readable without altering the scene lighting.");
Check(calls.Any(call => call.Name == "FindPassTagValue"),
      "Mesh draws must select the forward color pass instead of a shadow/grab pass.");
Check(!calls.Any(call => call.DeclaringType.FullName == "UnityEngine.Shader" && call.Name.StartsWith("SetGlobal")),
      "Preview lighting must not mutate global game shader state.");
var attributes = gameAssembly.MainModule.Types.Single(type => type.Name == "ItemAttributes");
Check(attributes.Fields.Any(field => field.Name == "icon" && field.IsPublic && field.FieldType.Name == "Sprite"),
      "Native item icon field changed.");
var loader = gameAssembly.MainModule.Types.Single(type => type.Name == "SemiFunc").Methods.Single(method => method.Name == "LoadSpriteFromFile");
Check(loader.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call &&
      call.DeclaringType.FullName == "System.IO.File" && call.Name == "ReadAllBytes"),
      "Native icon loader changed; review the read-only cache path.");
Console.WriteLine($"PASS {checks} preview isolation/API checks against compiled plugin {plugin.MainModule.Mvid}.");
