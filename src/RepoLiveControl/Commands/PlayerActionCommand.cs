using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RepoLiveControl.Commands
{
    public sealed class PlayerActionCommand
    {
        internal PlayerActionCommand(string name, string player, IEnumerable<string> arguments)
        {
            Name = name;
            Player = player;
            Arguments = Array.AsReadOnly(arguments.ToArray());
        }

        public string Name { get; private set; }
        public string Player { get; private set; }
        public IReadOnlyList<string> Arguments { get; private set; }
        public float Number(int index) { return float.Parse(Arguments[index], CultureInfo.InvariantCulture); }
        public int Integer(int index) { return int.Parse(Arguments[index], CultureInfo.InvariantCulture); }
        public string ForPlayer(string player)
        {
            IEnumerable<string> args = Arguments;
            if ((Name == "animationspeed" || Name == "pupils") && Arguments[0] == "off") args = Arguments.Take(1);
            return "/" + Name + " " + CommandTokenizer.QuoteArgument(player) +
                (Arguments.Count == 0 ? "" : " " + string.Join(" ", args.Select(CommandTokenizer.QuoteArgument)));
        }
    }

    /// <summary>One schema drives parsing, help, and contextual argument completion.</summary>
    public static class PlayerActionCommands
    {
        private sealed class Argument
        {
            internal string Default;
            internal double Minimum;
            internal double Maximum;
            internal bool Integer;
            internal string[] Words;
            internal string[] Suggestions;
        }

        private static Argument Numeric(string value, double min, double max, bool integer = false,
            string[] words = null, string[] suggestions = null)
        {
            return new Argument { Default = value, Minimum = min, Maximum = max,
                Integer = integer, Words = words ?? new string[0],
                Suggestions = suggestions ?? new[] { value } };
        }

        private static Argument Word(string value, params string[] words)
        {
            return new Argument { Default = value, Words = words, Suggestions = words,
                Minimum = 1, Maximum = 0 };
        }

        private static readonly Dictionary<string, Argument[]> Schemas =
            new Dictionary<string, Argument[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "kill", new Argument[0] }, { "revive", new Argument[0] },
                { "heal", new[] { Numeric("full", 1, 1000000, true, new[] { "full" }, new[] { "full", "10", "25", "50", "100" }) } },
                { "summon", new Argument[0] }, { "truck", new Argument[0] },
                { "knockback", new[] { Numeric("5", 0, 10000, false, null, new[] { "5", "10", "25" }) } },
                { "damage", new[] { Numeric("10", 1, 1000000, true, null, new[] { "10", "25", "50", "100" }) } },
                { "expression", new[] { Numeric("4", 0, 1000, true, null, new[] { "0", "1", "2", "3", "4" }) } },
                { "speak", new Argument[0] },
                { "wings", new[] { Word("on", "on", "off", "pink") } },
                { "tumble", new[] { Numeric("3", 0.1, 3600, false, new[] { "on", "off" }, new[] { "on", "off", "3", "10", "30" }) } },
                { "maxhealth", new[] { Numeric("200", 1, 1000000, true, null, new[] { "100", "200", "500", "1000" }) } },
                { "flicker", new[] { Numeric("2", 0, 100, false, null, new[] { "1", "2", "5" }) } },
                { "animationspeed", new[] { Numeric("0.5", 0, 100, false, new[] { "off" }, new[] { "off", "0.5", "1", "2" }),
                    Numeric("0.05", 0.001, 100), Numeric("0.2", 0.001, 100), Numeric("3", 0.1, 3600) } },
                { "pupils", new[] { Numeric("1.8", 0, 100, false, new[] { "off" }, new[] { "off", "0.5", "1", "1.8", "3" }),
                    Numeric("10", 0, 1000, true), Numeric("25", 0.001, 1000), Numeric("0.8", 0.001, 100),
                    Numeric("12", 0.001, 1000), Numeric("0.8", 0.001, 100), Numeric("3", 0.1, 3600) } },
                { "falling", new[] { Word("on", "on", "off") } },
                { "resetpush", new Argument[0] }, { "chain", new Argument[0] }
            };

        public static readonly IReadOnlyList<string> ChainActions =
            Array.AsReadOnly(new[] { "kill", "revive", "heal", "summon", "truck" });

        public static IEnumerable<string> Names { get { return Schemas.Keys.Select(name => "/" + name); } }
        public static bool IsCommand(string command) { return command != null && command.StartsWith("/") && Schemas.ContainsKey(command.Substring(1)); }
        public static bool RequiresOwner(string name)
        {
            return name == "expression" || name == "animationspeed" || name == "pupils" || name == "falling";
        }

        internal static CommandParseResult Parse(IReadOnlyList<CommandToken> tokens)
        {
            string name = tokens[0].Value.Substring(1).ToLowerInvariant();
            if (tokens.Count < 2 || string.IsNullOrWhiteSpace(tokens[1].Value))
                return Error(CommandParseErrorCode.MissingArgument, "/" + name + " requires <player|all>. Use Tab to select a player.");
            var args = tokens.Skip(2).Select(token => token.Value).ToList();
            if (name == "speak")
            {
                string message = args.Count == 0 ? "Hello!!!" : string.Join(" ", args);
                if (string.IsNullOrWhiteSpace(message))
                    return Error(CommandParseErrorCode.InvalidArgument, "Speech cannot be empty.");
                args = new List<string> { message };
            }
            else if (name == "chain")
            {
                if (args.Count < 1 || args.Count > 8 || args.Any(arg => !ChainActions.Contains(arg.ToLowerInvariant())))
                    return Error(CommandParseErrorCode.InvalidArgument, "/chain <player|all> accepts 1–8 actions: kill revive heal summon truck.");
                args = args.Select(arg => arg.ToLowerInvariant()).ToList();
            }
            else
            {
                Argument[] schema = Schemas[name];
                bool off = (name == "animationspeed" || name == "pupils") &&
                    args.Count > 0 && args[0].Equals("off", StringComparison.OrdinalIgnoreCase);
                if (args.Count > (off ? 1 : schema.Length))
                    return Error(CommandParseErrorCode.TooManyArguments, "Too many arguments for /" + name + ".");
                for (int index = 0; index < schema.Length; index++)
                {
                    Argument arg = schema[index];
                    if (index >= args.Count) args.Add(arg.Default);
                    string value = args[index];
                    if (arg.Words.Contains(value.ToLowerInvariant()))
                    {
                        args[index] = value.ToLowerInvariant();
                        continue;
                    }
                    double number;
                    int integer;
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                        double.IsNaN(number) || double.IsInfinity(number) || number < arg.Minimum || number > arg.Maximum ||
                        (arg.Integer && !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out integer)))
                    {
                        if (arg.Minimum > arg.Maximum)
                            return Error(CommandParseErrorCode.InvalidArgument, "/" + name + " expects " + string.Join("/", arg.Words) + ".");
                        return Error(CommandParseErrorCode.InvalidArgument, "Invalid argument " + (index + 1) + " for /" + name +
                            ". Expected " + (arg.Integer ? "a whole number" : "a number") + " in " +
                            arg.Minimum.ToString(CultureInfo.InvariantCulture) + ".." + arg.Maximum.ToString(CultureInfo.InvariantCulture) +
                            (arg.Words.Length == 0 ? "." : " or " + string.Join("/", arg.Words) + "."));
                    }
                }
            }
            return new CommandParseResult(new ParsedSlashCommand(SlashCommandKind.PlayerAction,
                null, null, null, tokens[1].Value, new PlayerActionCommand(name, tokens[1].Value, args)),
                CommandParseErrorCode.None, null);
        }

        public static IEnumerable<string> Suggestions(string command, int argumentIndex, IReadOnlyList<CommandToken> tokens)
        {
            string name = command.Substring(1).ToLowerInvariant();
            if (name == "chain") return argumentIndex <= 9 ? ChainActions : new string[0];
            if (name == "speak") return new string[0];
            if ((name == "animationspeed" || name == "pupils") && tokens.Count > 2 &&
                tokens[2].Value.Equals("off", StringComparison.OrdinalIgnoreCase) && argumentIndex > 2)
                return new string[0];
            Argument[] schema = Schemas[name];
            int index = argumentIndex - 2;
            return index >= 0 && index < schema.Length ? schema[index].Suggestions : new string[0];
        }

        private static CommandParseResult Error(CommandParseErrorCode code, string message)
        {
            return new CommandParseResult(null, code, message);
        }
    }
}
