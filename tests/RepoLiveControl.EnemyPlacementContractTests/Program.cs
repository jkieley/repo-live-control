using Mono.Cecil;
using Mono.Cecil.Cil;

// Follow the shipped command's pre-spawn IL with controlled native outcomes.
// No game assembly is loaded or executed. The pure catalog suite separately
// executes the real placement helper against a simulated NavMesh API.
if (args.Length != 2) throw new ArgumentException("Expected Release DLL and game directory.");
using var plugin = AssemblyDefinition.ReadAssembly(Path.GetFullPath(args[0]));
using var ai = AssemblyDefinition.ReadAssembly(Path.Combine(args[1], "REPO_Data", "Managed", "UnityEngine.AIModule.dll"));
var step = plugin.MainModule.Types.SelectMany(type => type.Methods)
    .Single(method => method.Name == "SpawnEnemyStep");
var helper = plugin.MainModule.Types.Single(type => type.Name == "PlayerEnemyPlacement");
var find = helper.Methods.Single(method => method.Name == "TryFind");
int checks = 0;
void Check(bool valid, string message)
{
    if (!valid) throw new Exception(message);
    checks++;
}
bool Calls(Route route, string owner, string name) => route.Calls.Any(call => call.DeclaringType.Name == owner && call.Name == name);
bool ValidAtPlayer(bool nativeSuccess)
{
    var route = Follow(step, "at-player", nativeSuccess);
    return route.Calls.Count(call => call.DeclaringType.Name == "PlayerEnemyPlacement" && call.Name == "TryFind") == 1 &&
        !Calls(route, "SemiFunc", "EnemyRoamFindPoint") && !Calls(route, "Random", "get_insideUnitSphere") &&
        route.HelperAnchor == "player-anchor" &&
        (nativeSuccess ? route.Terminal == "spawn" && route.Position == "local-navmesh-point" : route.Terminal == "throw");
}

Check(ValidAtPlayer(true), "At-player success must spawn the helper's local result without roaming or random offsets.");
Check(ValidAtPlayer(false), "At-player failure must throw before spawning instead of falling back to another position.");
var safe = Follow(step, "safe", true);
Check(safe.Terminal == "spawn" && safe.Position == "clear-point" && Calls(safe, "Bridge", "TryFindClearEnemyPosition") &&
      !Calls(safe, "PlayerEnemyPlacement", "TryFind"), "Safe placement must retain its clearance route.");
var random = Follow(step, "random", true);
Check(random.Terminal == "spawn" && Calls(random, "SemiFunc", "EnemyRoamFindPoint") &&
      !Calls(random, "PlayerEnemyPlacement", "TryFind"), "Random placement must retain its separate roaming route.");

var nativeCall = find.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>()
    .Single(call => call.DeclaringType.FullName == "UnityEngine.AI.NavMesh" && call.Name == "SamplePosition");
var native = ai.MainModule.Types.Single(type => type.FullName == "UnityEngine.AI.NavMesh");
Check(native.Methods.Any(method => method.FullName == nativeCall.FullName), "Installed NavMesh.SamplePosition signature does not match the compiled call.");
Check(native.Fields.Any(field => field.Name == "AllAreas" && field.HasConstant && Convert.ToInt32(field.Constant) == -1),
      "Installed NavMesh.AllAreas contract changed.");
Check(!find.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>().Any(call =>
      call.Name == "EnemyRoamFindPoint" || call.DeclaringType.FullName == "UnityEngine.Random"),
      "The bounded helper must not call a distant or random placement API.");

// Mutation checks establish that these assertions reject the two actual risks,
// even when the bounded helper remains present in the assembly. Changes exist
// only in the in-memory Cecil graph and are restored before the next check.
var atPlayerLiteral = step.Body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Ldstr && (string)instruction.Operand == "at-player");
var placementBranch = atPlayerLiteral.Next.Next;
var placementCode = placementBranch.OpCode;
try
{
    placementBranch.OpCode = OpCodes.Br;
    Check(!ValidAtPlayer(true), "Regression detector failed to reject routing at-player into distant roaming.");
}
finally { placementBranch.OpCode = placementCode; }
var helperCall = step.Body.Instructions.Single(instruction => instruction.Operand is MethodReference call && call.DeclaringType.Name == "PlayerEnemyPlacement");
var failureGuard = helperCall.Next;
var guardCode = failureGuard.OpCode;
try
{
    failureGuard.OpCode = OpCodes.Br;
    Check(!ValidAtPlayer(false), "Regression detector failed to reject bypassing the failed-placement guard.");
}
finally { failureGuard.OpCode = guardCode; }
Console.WriteLine($"PASS: {checks} compiled enemy-placement route/API contracts.");

static Route Follow(MethodDefinition step, string placement, bool nativeSuccess)
{
    var literal = step.Body.Instructions.First(instruction => instruction.OpCode == OpCodes.Ldstr && (string)instruction.Operand == "safe");
    var current = literal.Previous.Previous;
    var stack = new Stack<object>();
    var locals = new Dictionary<int, object> { [0] = "enemy-prefab" };
    var route = new Route();
    object Read(object value) => value is Address address ? locals.GetValueOrDefault(address.Index, "uninitialized") : value;
    void Write(object address, object value) => locals[((Address)address).Index] = value;
    for (int remaining = 150; remaining > 0; remaining--)
    {
        var next = current.Next;
        switch (current.OpCode.Code)
        {
            case Code.Nop: break;
            case Code.Ldarg_0: stack.Push("job"); break;
            case Code.Ldstr: stack.Push(current.Operand); break;
            case Code.Ldfld:
                stack.Pop();
                var field = (FieldReference)current.Operand;
                stack.Push(field.Name == "Placement" ? placement : field.Name == "Anchor" ? "player-anchor" : "reservations");
                break;
            case Code.Stfld: stack.Pop(); stack.Pop(); break;
            case Code.Ldloc_0: stack.Push(locals.GetValueOrDefault(0, "uninitialized")); break;
            case Code.Ldloc_1: stack.Push(locals.GetValueOrDefault(1, "uninitialized")); break;
            case Code.Ldloc_2: stack.Push(locals.GetValueOrDefault(2, "uninitialized")); break;
            case Code.Ldloc_3: stack.Push(locals.GetValueOrDefault(3, "uninitialized")); break;
            case Code.Ldloc: case Code.Ldloc_S: stack.Push(locals.GetValueOrDefault(((VariableDefinition)current.Operand).Index, "uninitialized")); break;
            case Code.Ldloca: case Code.Ldloca_S: stack.Push(new Address(((VariableDefinition)current.Operand).Index)); break;
            case Code.Stloc_0: locals[0] = stack.Pop(); break;
            case Code.Stloc_1: locals[1] = stack.Pop(); break;
            case Code.Stloc_2: locals[2] = stack.Pop(); break;
            case Code.Stloc_3: locals[3] = stack.Pop(); break;
            case Code.Stloc: case Code.Stloc_S: locals[((VariableDefinition)current.Operand).Index] = stack.Pop(); break;
            case Code.Ldc_R4: stack.Push(current.Operand); break;
            case Code.Ldc_I4_0: stack.Push(0); break;
            case Code.Br: case Code.Br_S: next = (Instruction)current.Operand; break;
            case Code.Brtrue: case Code.Brtrue_S: if (Convert.ToBoolean(stack.Pop())) next = (Instruction)current.Operand; break;
            case Code.Brfalse: case Code.Brfalse_S: if (!Convert.ToBoolean(stack.Pop())) next = (Instruction)current.Operand; break;
            case Code.Newobj:
                var ctor = (MethodReference)current.Operand;
                for (int i = 0; i < ctor.Parameters.Count; i++) stack.Pop();
                stack.Push("exception");
                break;
            case Code.Throw: route.Terminal = "throw"; return route;
            case Code.Call: case Code.Callvirt:
                var call = (MethodReference)current.Operand;
                route.Calls.Add(call);
                var values = new object[call.Parameters.Count];
                for (int i = values.Length - 1; i >= 0; i--) values[i] = stack.Pop();
                if (call.HasThis) stack.Pop();
                if (call.DeclaringType.FullName == "REPOLib.Modules.Enemies" && call.Name == "SpawnEnemy")
                {
                    route.Terminal = "spawn";
                    route.Position = Read(values[1]).ToString();
                    return route;
                }
                object result = "opaque-result";
                if (call.DeclaringType.FullName == "System.String" && call.Name == "op_Equality") result = Equals(values[0], values[1]);
                else if (call.DeclaringType.Name == "PlayerEnemyPlacement" && call.Name == "TryFind")
                {
                    route.HelperAnchor = Read(values[0]).ToString();
                    Write(values[1], nativeSuccess ? "local-navmesh-point" : "world-zero");
                    result = nativeSuccess;
                }
                else if (call.Name == "TryFindClearEnemyPosition") { Write(values[^1], "clear-point"); result = true; }
                else if (call.Name == "EnemyRoamFindPoint") result = "distant-roam-point";
                if (call.ReturnType.FullName != "System.Void") stack.Push(result);
                break;
            default: throw new Exception($"Unsupported pre-spawn IL: {current}. Review the changed route contract.");
        }
        current = next ?? throw new Exception("Placement route ended without spawn or error.");
    }
    throw new Exception("Placement route exceeded bounded instruction budget.");
}

sealed record Address(int Index);
sealed class Route
{
    public List<MethodReference> Calls { get; } = new();
    public string Terminal { get; set; } = "";
    public string Position { get; set; } = "";
    public string HelperAnchor { get; set; } = "";
}
