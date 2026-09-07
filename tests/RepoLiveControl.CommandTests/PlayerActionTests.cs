using System;
using System.Linq;
using RepoLiveControl.Commands;
using RepoLiveControl.Networking;

internal static partial class Program
{
    private static readonly string[] PlayerVerbs = {
        "kill", "revive", "heal", "summon", "truck", "knockback", "damage", "expression", "speak",
        "wings", "tumble", "maxhealth", "flicker", "animationspeed", "pupils", "falling", "resetpush", "chain"
    };

    private static void PlayerCommandGrammarAndPermissions()
    {
        Equal(PlayerVerbs.Length, PlayerActionCommands.Names.Count());
        foreach (string verb in PlayerVerbs)
        {
            foreach (string target in new[] { "all", "\"Bob Builder#12\"" })
            {
                string command = "/" + verb + " " + target + (verb == "chain" ? " revive heal truck" : "");
                var parsed = SlashCommandParser.Parse(command);
                True(parsed.Success, command);
                Equal(SlashCommandKind.PlayerAction, parsed.Command.Kind);
                True(CommandNetworkPolicy.ValidateRemoteCommand(command, true).Allowed, command);
                True(!CommandNetworkPolicy.ValidateRemoteCommand(command, false).Allowed, "Grant required: " + command);
                True(!CommandNetworkPolicy.IsPublicVerb(command), "Player effects are mutations");
                True(SlashCommandParser.Parse(command.ToUpperInvariant()).Success, "Case-insensitive verbs");
            }
            ParseFails("/" + verb, CommandParseErrorCode.MissingArgument);
            ParseFails("/" + verb + " \"\"", CommandParseErrorCode.MissingArgument);
        }
    }

    private static void PlayerArgumentRoundTrips()
    {
        string[] commands = {
            "/heal all", "/heal all 25", "/damage all 1000000", "/maxhealth all 1", "/expression all 0",
            "/animationspeed all off", "/animationspeed all 2", "/animationspeed all 0.5 0.05 0.2 3",
            "/pupils all off", "/pupils all 2 10 25 0.8 12 0.8 30", "/falling all", "/wings all pink", "/tumble all 0.1"
        };
        foreach (string input in commands)
        {
            var parsed = SlashCommandParser.Parse(input);
            True(parsed.Success, input + ": " + parsed.ErrorMessage);
            var first = parsed.Command.PlayerAction;
            var next = SlashCommandParser.Parse(first.ForPlayer("Quote \"Pilot\"#9"));
            True(next.Success, input);
            Equal("Quote \"Pilot\"#9", next.Command.Player);
            Equal(string.Join("|", first.Arguments), string.Join("|", next.Command.PlayerAction.Arguments));
        }
        Equal("full", SlashCommandParser.Parse("/heal all").Command.PlayerAction.Arguments[0]);
        Equal(200, SlashCommandParser.Parse("/maxhealth all").Command.PlayerAction.Integer(0));
        Equal(3f, SlashCommandParser.Parse("/tumble all").Command.PlayerAction.Number(0));
    }

    private static void PlayerArgumentRejection()
    {
        foreach (string input in new[] {
            "/heal all 0", "/damage all -1", "/maxhealth all 0", "/maxhealth all 1000001", "/damage all 2.5",
            "/expression all -1", "/flicker all NaN", "/knockback all Infinity", "/tumble all 0",
            "/animationspeed all 1e100", "/animationspeed all 1 NaN", "/pupils all 1 2.5",
            "/pupils all 1 10 25 0.8 12 0.8 Infinity", "/wings all purple", "/falling all true" })
            ParseFails(input, CommandParseErrorCode.InvalidArgument);
        foreach (string input in new[] { "/revive all 1", "/kill all Bob", "/wings all on pink", "/animationspeed all off 2", "/pupils all off 3" })
            ParseFails(input, CommandParseErrorCode.TooManyArguments);
    }

    private static void IndividualPlayerResolution()
    {
        var players = new[] { new PlayerChoice("Host", 1), new PlayerChoice("Bob Builder", 2), new PlayerChoice("Bob Builder", 3),
            new PlayerChoice("all", 4), new PlayerChoice("Quote \"Pilot\"", 5) };
        Equal(5, PlayerSelection.Resolve("ALL", players).Count);
        Equal(3, PlayerSelection.Resolve("Bob Builder#3", players)[0].ActorNumber);
        Equal(4, PlayerSelection.Resolve("all#4", players)[0].ActorNumber);
        Equal(1, PlayerSelection.Resolve("host", players)[0].ActorNumber);
        Throws<InvalidOperationException>(() => PlayerSelection.Resolve("Bob Builder", players), "Duplicate nicknames must not kill both");
        Throws<InvalidOperationException>(() => PlayerSelection.Resolve("Bob", players), "Fuzzy execution must require explicit completion");
        Throws<InvalidOperationException>(() => PlayerSelection.Resolve("Departed#3", players), "Stale labels must not silently retarget");
        Throws<InvalidOperationException>(() => PlayerSelection.Resolve("Host#99", players), "Departed actors must fail");
        Equal(-1, PlayerSelection.Resolve("Local Player#local", new[] { new PlayerChoice("Local Player", -1) })[0].ActorNumber);
    }

    private static void PlayerActionCompletion()
    {
        var players = Enumerable.Range(1, 32).Select(actor => "Player " + actor + "#" + actor).ToArray();
        var catalog = new CompletionCatalog(new string[0], new string[0], new string[0], players, false);
        foreach (string verb in PlayerVerbs)
        {
            string input = "/" + verb + " ";
            var matches = CommandCompletionEngine.GetCompletions(input, input.Length, catalog, 512);
            Equal(33, matches.Count);
            ContainsValue(matches, "all", "Bulk option");
            foreach (string player in players) ContainsValue(matches, player, "Each character including host");
        }
        string fuzzy = "/revive pl32";
        var match = CommandCompletionEngine.GetCompletions(fuzzy, fuzzy.Length, catalog, 512).First(item => item.Value == "Player 32#32");
        var applied = CommandCompletionEngine.ApplyCompletion(fuzzy, match);
        Equal("Player 32#32", SlashCommandParser.Parse(applied.Text).Command.Player);
        ContainsValue(CommandCompletionEngine.GetCompletions("/rvive", 6, catalog, 512), "/revive", "Full-word typo");
        foreach (string input in new[] { "/wings all ", "/wings \"Player 1#1\" " })
            ContainsValue(CommandCompletionEngine.GetCompletions(input, input.Length, catalog, 512), "pink", "Mode completion");
        Equal(0, CommandCompletionEngine.GetCompletions("/animationspeed all off ", 24, catalog, 512).Count);
        Equal(0, CommandCompletionEngine.GetCompletions("/grant ", 7, catalog, 512).Count);
        DoesNotContainValue(CommandCompletionEngine.GetCompletions("/", 1, catalog, 512), "/grant", "Non-host cannot manage grants");
    }

    private static void PlayerChainsAndSpeech()
    {
        var chain = SlashCommandParser.Parse("/chain all kill revive heal summon truck");
        True(chain.Success, "Full-word chains");
        Equal("kill|revive|heal|summon|truck", string.Join("|", chain.Command.PlayerAction.Arguments));
        foreach (string input in new[] { "/chain all", "/chain all grant", "/chain all /revive", "/chain all ra ha",
            "/chain all revive revive revive revive revive revive revive revive revive" })
            ParseFails(input, CommandParseErrorCode.InvalidArgument);
        string text = "/grant Bob; /kill all | status";
        var speech = SlashCommandParser.Parse("/speak all " + CommandTokenizer.QuoteArgument(text));
        Equal(text, speech.Command.PlayerAction.Arguments[0]);
        Equal("Hello everyone", SlashCommandParser.Parse("/speak all Hello everyone").Command.PlayerAction.Arguments[0]);
        ParseFails("/speak all \"\"", CommandParseErrorCode.InvalidArgument);
    }

    private static void OwnerEffectTrustBoundary()
    {
        foreach (string verb in new[] { "expression", "animationspeed", "pupils", "falling" })
        {
            string payload = "2001|10000|/" + verb + " \"Bob Builder#2\"";
            True(OwnerActionPolicy.Validate(payload, 1, 1, "Bob Builder#2", 2001, 10001) != null, "Approved owner effect");
            True(OwnerActionPolicy.Validate(payload, 3, 1, "Bob Builder#2", 2001, 10001) == null, "Peer cannot forge host");
            True(OwnerActionPolicy.Validate(payload, 1, 3, "Bob Builder#2", 2001, 10001) == null, "Old master rejected");
            True(OwnerActionPolicy.Validate(payload, 1, 1, "Host#1", 1001, 10001) == null, "Wrong target rejected");
            True(OwnerActionPolicy.Validate(payload, 1, 1, "Bob Builder#2", 2002, 10001) == null, "Old character view rejected");
            True(OwnerActionPolicy.Validate(payload, 1, 1, "Bob Builder#2", 2001, 15000) == null, "Expired commands cannot execute late");
            True(OwnerActionPolicy.Validate(payload, 1, 1, "Bob Builder#2", 2001, 9999) == null, "Future commands rejected");
            True(OwnerActionPolicy.Validate("2001|10000|/" + verb + " all", 1, 1, "Bob Builder#2", 2001, 10001) == null, "Owner relay cannot fan out");
        }
        foreach (string command in new[] { "/grant Bob", "/kill \"Bob Builder#2\"", "/chain \"Bob Builder#2\" kill revive", "/spawn item:all", "/permissions" })
            True(OwnerActionPolicy.Validate("2001|10000|" + command, 1, 1, "Bob Builder#2", 2001, 10001) == null, "Relay is not a generic command endpoint");
        True(OwnerActionPolicy.Validate("2001|2147483640|/falling \"Bob Builder#2\"", 1, 1, "Bob Builder#2", 2001, unchecked(2147483640 + 100)) != null,
            "Photon server timestamp wraparound");
    }
}
