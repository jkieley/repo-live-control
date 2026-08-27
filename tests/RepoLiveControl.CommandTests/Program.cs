using System;
using System.Collections.Generic;
using RepoLiveControl.Commands;

internal static class Program
{
    private static int failures;

    private static readonly CompletionCatalog Catalog = new CompletionCatalog(
        new[]
        {
            "item:all",
            "valuable:all",
            "enemy:all",
            "item:Health Pack",
            "item:Baseball Bat",
            "valuable:Diamond Display",
            "enemy:Headman"
        },
        new[] { "Alice", "Bob Builder" });

    private static int Main()
    {
        Run("spawn defaults", SpawnDefaults);
        Run("despawn defaults", DespawnDefaults);
        Run("quoted target and token spans", QuotedTargetAndTokenSpans);
        Run("quoted player", QuotedPlayer);
        Run("unterminated quote", UnterminatedQuote);
        Run("invalid spawn counts", InvalidSpawnCounts);
        Run("invalid despawn counts", InvalidDespawnCounts);
        Run("invalid spawn placement", InvalidSpawnPlacement);
        Run("other command grammar", OtherCommandGrammar);
        Run("fuzzy command typo", FuzzyCommandTypo);
        Run("fuzzy spawn target", FuzzySpawnTarget);
        Run("all target completion is despawn-only", AllTargetCompletionIsDespawnOnly);
        Run("fuzzy spawn count", FuzzySpawnCount);
        Run("fuzzy spawn location", FuzzySpawnLocation);
        Run("contextual player completion", ContextualPlayerCompletion);
        Run("completion replacement quotes target", CompletionReplacementQuotesTarget);
        Run("completion replaces existing quotes", CompletionReplacesExistingQuotes);
        Run("completion can append space", CompletionCanAppendSpace);

        if (failures == 0)
        {
            Console.WriteLine("PASS: all RepoLiveControl command-core tests passed.");
            return 0;
        }

        Console.Error.WriteLine("FAIL: " + failures + " command-core test(s) failed.");
        return 1;
    }

    private static void SpawnDefaults()
    {
        CommandParseResult result = SlashCommandParser.Parse("/spawn item:Gun");
        True(result.Success, result.ErrorMessage);
        Equal(SlashCommandKind.Spawn, result.Command.Kind);
        Equal("item:Gun", result.Command.Target);
        Equal(CommandTargetKind.Item, result.Command.TargetKind);
        Equal("Gun", result.Command.TargetName);
        Equal(1, result.Command.Count.Value);
        Equal(CommandLocations.PlayerLocation, result.Command.Location);
    }

    private static void DespawnDefaults()
    {
        CommandParseResult result = SlashCommandParser.Parse("/despawn enemy:Headman");
        True(result.Success, result.ErrorMessage);
        Equal(SlashCommandKind.Despawn, result.Command.Kind);
        True(result.Command.IsAllCount, "Despawn should default to all.");
        True(!result.Command.Count.HasValue, "All should be represented by a null Count.");

        result = SlashCommandParser.Parse("/despawn valuable:Diamond 12");
        True(result.Success, result.ErrorMessage);
        Equal(12, result.Command.Count.Value);

        result = SlashCommandParser.Parse("/despawn item:Gun ALL");
        True(result.Success, result.ErrorMessage);
        True(result.Command.IsAllCount, "The all keyword should be case-insensitive.");
    }

    private static void QuotedTargetAndTokenSpans()
    {
        const string input = "/spawn \"item:Health Pack\" 2 random-non-collision-location";
        CommandTokenization tokenization = CommandTokenizer.Tokenize(input);
        Equal(4, tokenization.Tokens.Count);
        Equal("item:Health Pack", tokenization.Tokens[1].Value);
        True(tokenization.Tokens[1].IsQuoted, "Target should be marked quoted.");
        int quotedStart = input.IndexOf('"');
        int quotedEnd = input.IndexOf('"', quotedStart + 1) + 1;
        Equal(quotedStart, tokenization.Tokens[1].Start);
        Equal(quotedEnd - quotedStart, tokenization.Tokens[1].Length);
        Equal(quotedEnd, tokenization.Tokens[1].End);

        CommandParseResult result = SlashCommandParser.Parse(input);
        True(result.Success, result.ErrorMessage);
        Equal("item:Health Pack", result.Command.Target);
        Equal(2, result.Command.Count.Value);
        Equal(CommandLocations.RandomNonCollisionLocation, result.Command.Location);

        CommandTokenization escaped = CommandTokenizer.Tokenize(
            "/spawn \"item:Bob\\\"s Bat\"");
        Equal("item:Bob\"s Bat", escaped.Tokens[1].Value);
    }

    private static void QuotedPlayer()
    {
        CommandParseResult result = SlashCommandParser.Parse("/grant \"Bob Builder\"");
        True(result.Success, result.ErrorMessage);
        Equal(SlashCommandKind.Grant, result.Command.Kind);
        Equal("Bob Builder", result.Command.Player);
    }

    private static void UnterminatedQuote()
    {
        CommandParseResult result = SlashCommandParser.Parse("/spawn \"item:Health Pack");
        True(!result.Success, "An unterminated quote must fail parsing.");
        Equal(CommandParseErrorCode.UnterminatedQuote, result.ErrorCode);

        CommandTokenization tokens = CommandTokenizer.Tokenize("/spawn \"item:Hea");
        Equal(2, tokens.Tokens.Count);
        Equal("item:Hea", tokens.Tokens[1].Value);
        True(tokens.HasUnterminatedQuote, "Partial quoted input should remain completable.");
    }

    private static void InvalidSpawnCounts()
    {
        ParseFails("/spawn item:Gun 0", CommandParseErrorCode.CountOutOfRange);
        ParseFails("/spawn item:Gun 501", CommandParseErrorCode.CountOutOfRange);
        ParseFails("/spawn item:Gun two", CommandParseErrorCode.InvalidCount);
        ParseFails("/spawn item:Gun -1", CommandParseErrorCode.InvalidCount);
    }

    private static void InvalidDespawnCounts()
    {
        ParseFails("/despawn enemy:Headman 0", CommandParseErrorCode.CountOutOfRange);
        ParseFails("/despawn enemy:Headman 501", CommandParseErrorCode.CountOutOfRange);
        ParseFails("/despawn enemy:Headman many", CommandParseErrorCode.InvalidCount);
    }

    private static void InvalidSpawnPlacement()
    {
        ParseFails("/spawn item:Gun 1 safe", CommandParseErrorCode.InvalidLocation);
        ParseFails("/spawn item:Gun 1 near-player", CommandParseErrorCode.InvalidLocation);
    }

    private static void OtherCommandGrammar()
    {
        True(SlashCommandParser.Parse("/revoke Alice").Success, "Revoke should parse.");
        True(SlashCommandParser.Parse("/permissions").Success, "Permissions should parse.");
        True(SlashCommandParser.Parse("/help").Success, "Help should parse.");
        ParseFails("/help extra", CommandParseErrorCode.TooManyArguments);
        ParseFails("spawn item:Gun", CommandParseErrorCode.MissingSlash);
        ParseFails("/unknown", CommandParseErrorCode.UnknownCommand);
    }

    private static void FuzzyCommandTypo()
    {
        IReadOnlyList<CompletionItem> matches = Complete("/spwan", Catalog);
        HasAny(matches, "The command typo should have a match.");
        Equal("/spawn", matches[0].Value);
        Equal(0, matches[0].ArgumentIndex);
    }

    private static void FuzzySpawnTarget()
    {
        IReadOnlyList<CompletionItem> matches = Complete("/spawn bsbl", Catalog);
        HasAny(matches, "The target subsequence should have a match.");
        Equal("item:Baseball Bat", matches[0].Value);
        Equal(1, matches[0].ArgumentIndex);
    }

    private static void AllTargetCompletionIsDespawnOnly()
    {
        IReadOnlyList<CompletionItem> spawnMatches = Complete("/spawn ", Catalog);
        for (int index = 0; index < spawnMatches.Count; index++)
        {
            True(!spawnMatches[index].Value.EndsWith(":all", StringComparison.OrdinalIgnoreCase),
                "Spawn autocomplete must not offer synthetic :all targets.");
        }

        IReadOnlyList<CompletionItem> despawnMatches = Complete("/despawn all", Catalog);
        HasAny(despawnMatches, "Despawn autocomplete should offer synthetic :all targets.");
        True(despawnMatches[0].Value.EndsWith(":all", StringComparison.OrdinalIgnoreCase),
            "A despawn :all selector should be the leading fuzzy match.");
    }

    private static void FuzzySpawnCount()
    {
        IReadOnlyList<CompletionItem> matches = Complete("/spawn item:Gun 205x", Catalog);
        HasAny(matches, "The count typo should have a match.");
        Equal("205", matches[0].Value);
        Equal(2, matches[0].ArgumentIndex);
    }

    private static void FuzzySpawnLocation()
    {
        IReadOnlyList<CompletionItem> matches = Complete("/spawn item:Gun 2 rncl", Catalog);
        HasAny(matches, "The location subsequence should have a match.");
        Equal(CommandLocations.RandomNonCollisionLocation, matches[0].Value);
        Equal(3, matches[0].ArgumentIndex);
    }

    private static void ContextualPlayerCompletion()
    {
        IReadOnlyList<CompletionItem> matches = Complete("/grant alce", Catalog);
        HasAny(matches, "The player typo should have a match.");
        Equal("Alice", matches[0].Value);
        Equal(1, matches[0].ArgumentIndex);
    }

    private static void CompletionReplacementQuotesTarget()
    {
        const string input = "/spawn item:hea 2";
        int caret = input.IndexOf("item:hea", StringComparison.Ordinal) + "item:hea".Length;
        IReadOnlyList<CompletionItem> matches = CommandCompletionEngine.GetCompletions(
            input,
            caret,
            Catalog,
            10);
        HasAny(matches, "The partial target should have a completion.");
        CompletionApplication applied = CommandCompletionEngine.ApplyCompletion(input, matches[0]);
        Equal("/spawn \"item:Health Pack\" 2", applied.Text);
        Equal(applied.Text.IndexOf('"', applied.Text.IndexOf('"') + 1) + 1, applied.CaretPosition);
        True(SlashCommandParser.Parse(applied.Text).Success, "Applied text should parse.");
    }

    private static void CompletionReplacesExistingQuotes()
    {
        const string input = "/spawn \"item:hea\" 2";
        int caret = input.IndexOf("item:hea", StringComparison.Ordinal) + "item:hea".Length;
        IReadOnlyList<CompletionItem> matches = CommandCompletionEngine.GetCompletions(
            input,
            caret,
            Catalog,
            10);
        HasAny(matches, "The quoted partial target should have a completion.");
        CompletionApplication applied = CommandCompletionEngine.ApplyCompletion(input, matches[0]);
        Equal("/spawn \"item:Health Pack\" 2", applied.Text);
    }

    private static void CompletionCanAppendSpace()
    {
        const string input = "/spa";
        IReadOnlyList<CompletionItem> matches = Complete(input, Catalog);
        HasAny(matches, "The command prefix should have a completion.");
        CompletionApplication applied = CommandCompletionEngine.ApplyCompletion(
            input,
            matches[0],
            true);
        Equal("/spawn ", applied.Text);
        Equal(applied.Text.Length, applied.CaretPosition);

        string quoted = CommandTokenizer.QuoteArgument("item:Bob's Bat");
        Equal("\"item:Bob's Bat\"", quoted);
        Equal("item:Bob's Bat", CommandTokenizer.Tokenize(quoted).Tokens[0].Value);
    }

    private static IReadOnlyList<CompletionItem> Complete(
        string input,
        CompletionCatalog catalog)
    {
        return CommandCompletionEngine.GetCompletions(input, input.Length, catalog, 10);
    }

    private static void ParseFails(string input, CommandParseErrorCode expected)
    {
        CommandParseResult result = SlashCommandParser.Parse(input);
        True(!result.Success, "Expected parsing to fail for: " + input);
        Equal(expected, result.ErrorCode);
    }

    private static void HasAny<T>(IReadOnlyList<T> values, string message)
    {
        True(values != null && values.Count > 0, message);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                "Expected <" + expected + "> but got <" + actual + ">.");
        }
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
        }
    }
}
