using System;
using System.Collections.Generic;
using RepoLiveControl.Commands;
using RepoLiveControl.Networking;
using RepoLiveControl.Runtime;

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

    private static readonly CompletionCatalog ProductionShapeCatalog = new CompletionCatalog(
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
        new[] { "Alice#3", "Bob Builder#12", "Quote \"Pilot\"#9" });

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
        Run("empty and missing input matrix", EmptyAndMissingInputMatrix);
        Run("command case and whitespace", CommandCaseAndWhitespace);
        Run("too many arguments matrix", TooManyArgumentsMatrix);
        Run("target kind metadata", TargetKindMetadata);
        Run("exhaustive valid counts", ExhaustiveValidCounts);
        Run("invalid count format matrix", InvalidCountFormatMatrix);
        Run("location without explicit count", LocationWithoutExplicitCount);
        Run("tokenizer boundary matrix", TokenizerBoundaryMatrix);
        Run("tokenizer quote and escape matrix", TokenizerQuoteAndEscapeMatrix);
        Run("quote argument round trips", QuoteArgumentRoundTrips);
        Run("fuzzy precedence", FuzzyPrecedence);
        Run("fuzzy ranking deduplicates and limits", FuzzyRankingDeduplicatesAndLimits);
        Run("fuzzy normalization and no match", FuzzyNormalizationAndNoMatch);
        Run("completion offers every valid count", CompletionOffersEveryValidCount);
        Run("completion offers location without count", CompletionOffersLocationWithoutCount);
        Run("completion cursor preserves suffix", CompletionCursorPreservesSuffix);
        Run("completion whitespace insertion", CompletionWhitespaceInsertion);
        Run("completion invalid command recovery", CompletionInvalidCommandRecovery);
        Run("completion repairs unterminated quote", CompletionRepairsUnterminatedQuote);
        Run("completion production player selectors", CompletionProductionPlayerSelectors);
        Run("completion arity and argument validation", CompletionArityAndArgumentValidation);
        Run("network envelope validation", NetworkEnvelopeValidation);
        Run("network request ID validation", NetworkRequestIdValidation);
        Run("photon callback offline lifecycle", PhotonCallbackOfflineLifecycle);
        Run("photon callback room transition lifecycle", PhotonCallbackRoomTransitionLifecycle);
        Run("photon callback registered disposal", PhotonCallbackRegisteredDisposal);
        Run("photon callback unregistered disposal", PhotonCallbackUnregisteredDisposal);
        Run("photon callback argument validation", PhotonCallbackArgumentValidation);
        Run("photon callback failure state lifecycle", PhotonCallbackFailureStateLifecycle);
        Run("remote command policy matrix", RemoteCommandPolicyMatrix);
        Run("rolling rate limiter lifecycle", RollingRateLimiterLifecycle);
        Run("rate-limit response suppression", RateLimitResponseSuppression);
        Run("pending command registry lifecycle", PendingCommandRegistryLifecycle);
        Run("session grant ledger lifecycle", SessionGrantLedgerLifecycle);
        Run("role-aware completion catalog", RoleAwareCompletionCatalog);
        Run("non-host fuzzy mutation workflows", NonHostFuzzyMutationWorkflows);
        Run("canonical parse-to-execution pipeline", CanonicalParseToExecutionPipeline);
        Run("remote mutation permission matrix", RemoteMutationPermissionMatrix);
        Run("spawn execution translation matrix", SpawnExecutionTranslationMatrix);
        Run("despawn execution translation matrix", DespawnExecutionTranslationMatrix);
        Run("grouped enemy acceptance policy", GroupedEnemyAcceptancePolicy);
        Run("enemy clearance floor policy", EnemyClearanceFloorPolicy);
        Run("bounded spawn name summary", BoundedSpawnNameSummary);
        Run("ingress session policy", IngressSessionPolicy);
        Run("network session scene activation", NetworkSessionSceneActivation);
        Run("console toggle input fallback", ConsoleToggleInputFallback);

        if (failures == 0)
        {
            Console.WriteLine("PASS: all RepoLiveControl command/network core tests passed.");
            return 0;
        }

        Console.Error.WriteLine("FAIL: " + failures + " command/network core test(s) failed.");
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

    private static void EmptyAndMissingInputMatrix()
    {
        ParseFails(null, CommandParseErrorCode.EmptyInput);
        ParseFails(string.Empty, CommandParseErrorCode.EmptyInput);
        ParseFails(" \t\r\n ", CommandParseErrorCode.EmptyInput);

        ParseFails("/spawn", CommandParseErrorCode.MissingArgument);
        ParseFails("/spawn \"\"", CommandParseErrorCode.MissingArgument);
        ParseFails("/despawn", CommandParseErrorCode.MissingArgument);
        ParseFails("/despawn ''", CommandParseErrorCode.MissingArgument);
        ParseFails("/grant", CommandParseErrorCode.MissingArgument);
        ParseFails("/grant \"\"", CommandParseErrorCode.MissingArgument);
        ParseFails("/revoke", CommandParseErrorCode.MissingArgument);
        ParseFails("/revoke ''", CommandParseErrorCode.MissingArgument);

        ParseFails("spawn item:Gun", CommandParseErrorCode.MissingSlash);
        ParseFails("unknown", CommandParseErrorCode.MissingSlash);
        ParseFails("/does-not-exist", CommandParseErrorCode.UnknownCommand);
        ParseFails("/unknown \"unfinished", CommandParseErrorCode.UnterminatedQuote);
    }

    private static void CommandCaseAndWhitespace()
    {
        CommandParseResult spawn = SlashCommandParser.Parse(
            " \t/SPaWn\t\"item:Health Pack\"\t2\tRANDOM-NON-COLLISION-LOCATION\r\n");
        True(spawn.Success, spawn.ErrorMessage);
        Equal(SlashCommandKind.Spawn, spawn.Command.Kind);
        Equal("item:Health Pack", spawn.Command.Target);
        Equal(2, spawn.Command.Count.Value);
        Equal(CommandLocations.RandomNonCollisionLocation, spawn.Command.Location);

        CommandParseResult despawn = SlashCommandParser.Parse(
            "\r\n/DeSpAwN\tenemy:Headman\tALL  ");
        True(despawn.Success, despawn.ErrorMessage);
        True(despawn.Command.IsAllCount, "Despawn ALL should be case-insensitive.");

        Equal(SlashCommandKind.Grant,
            SlashCommandParser.Parse("/GrAnT Alice").Command.Kind);
        Equal(SlashCommandKind.Revoke,
            SlashCommandParser.Parse("/rEvOkE Alice").Command.Kind);
        Equal(SlashCommandKind.Permissions,
            SlashCommandParser.Parse("/PeRmIsSiOnS").Command.Kind);
        Equal(SlashCommandKind.Help,
            SlashCommandParser.Parse("/HeLp").Command.Kind);
    }

    private static void TooManyArgumentsMatrix()
    {
        ParseFails(
            "/spawn item:Gun 1 player-location extra",
            CommandParseErrorCode.TooManyArguments);
        ParseFails(
            "/despawn item:Gun 1 extra",
            CommandParseErrorCode.TooManyArguments);
        ParseFails("/grant Alice extra", CommandParseErrorCode.TooManyArguments);
        ParseFails("/revoke Alice extra", CommandParseErrorCode.TooManyArguments);
        ParseFails("/permissions extra", CommandParseErrorCode.TooManyArguments);
        ParseFails("/help extra", CommandParseErrorCode.TooManyArguments);
    }

    private static void TargetKindMetadata()
    {
        CommandParseResult item = SlashCommandParser.Parse("/spawn item:Gun");
        True(item.Success, item.ErrorMessage);
        Equal(CommandTargetKind.Item, item.Command.TargetKind);
        Equal("Gun", item.Command.TargetName);

        CommandParseResult valuable = SlashCommandParser.Parse(
            "/spawn \"VALUABLE:Diamond Display\"");
        True(valuable.Success, valuable.ErrorMessage);
        Equal(CommandTargetKind.Valuable, valuable.Command.TargetKind);
        Equal("Diamond Display", valuable.Command.TargetName);

        CommandParseResult enemy = SlashCommandParser.Parse("/despawn Enemy:Headman 1");
        True(enemy.Success, enemy.ErrorMessage);
        Equal(CommandTargetKind.Enemy, enemy.Command.TargetKind);
        Equal("Headman", enemy.Command.TargetName);

        CommandParseResult unqualified = SlashCommandParser.Parse("/spawn Gun");
        True(unqualified.Success, unqualified.ErrorMessage);
        Equal(CommandTargetKind.Unspecified, unqualified.Command.TargetKind);
        Equal("Gun", unqualified.Command.TargetName);
    }

    private static void ExhaustiveValidCounts()
    {
        for (int count = SlashCommandParser.MinimumCount;
            count <= SlashCommandParser.MaximumCount;
            count++)
        {
            string text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);

            CommandParseResult spawn = SlashCommandParser.Parse(
                "/spawn item:Gun " + text);
            True(spawn.Success, "Spawn count should parse: " + text);
            Equal(count, spawn.Command.Count.Value);
            Equal(CommandLocations.PlayerLocation, spawn.Command.Location);

            CommandParseResult despawn = SlashCommandParser.Parse(
                "/despawn item:Gun " + text);
            True(despawn.Success, "Despawn count should parse: " + text);
            Equal(count, despawn.Command.Count.Value);
            True(!despawn.Command.IsAllCount,
                "A numeric despawn count must not be represented as all: " + text);
        }
    }

    private static void InvalidCountFormatMatrix()
    {
        string[] invalidFormats =
        {
            "two",
            "+1",
            "-1",
            "1.0",
            "1e2",
            "1,000",
            "1_000",
            "2147483648"
        };
        foreach (string value in invalidFormats)
        {
            ParseFails(
                "/spawn item:Gun " + value,
                CommandParseErrorCode.InvalidCount);
            ParseFails(
                "/despawn item:Gun " + value,
                CommandParseErrorCode.InvalidCount);
        }

        foreach (string value in new[] { "0", "000", "501", "0501" })
        {
            ParseFails(
                "/spawn item:Gun " + value,
                CommandParseErrorCode.CountOutOfRange);
            ParseFails(
                "/despawn item:Gun " + value,
                CommandParseErrorCode.CountOutOfRange);
        }

        ParseFails("/spawn item:Gun all", CommandParseErrorCode.InvalidCount);

        CommandParseResult leadingZeros = SlashCommandParser.Parse(
            "/spawn item:Gun 0001");
        True(leadingZeros.Success, leadingZeros.ErrorMessage);
        Equal(1, leadingZeros.Command.Count.Value);
    }

    private static void LocationWithoutExplicitCount()
    {
        CommandParseResult random = SlashCommandParser.Parse(
            "/spawn item:Gun random-non-collision-location");
        True(random.Success,
            "An explicit location should be accepted while count defaults to one: " +
            random.ErrorMessage);
        Equal(1, random.Command.Count.Value);
        Equal(CommandLocations.RandomNonCollisionLocation, random.Command.Location);

        CommandParseResult player = SlashCommandParser.Parse(
            "/spawn item:Gun PLAYER-LOCATION");
        True(player.Success,
            "The default location keyword should be accepted without an explicit count: " +
            player.ErrorMessage);
        Equal(1, player.Command.Count.Value);
        Equal(CommandLocations.PlayerLocation, player.Command.Location);

        CommandParseResult explicitCount = SlashCommandParser.Parse(
            "/spawn item:Gun 7 random-non-collision-location");
        True(explicitCount.Success, explicitCount.ErrorMessage);
        Equal(7, explicitCount.Command.Count.Value);
        Equal(CommandLocations.RandomNonCollisionLocation, explicitCount.Command.Location);

        ParseFails(
            "/spawn item:Gun player-location random-non-collision-location",
            CommandParseErrorCode.TooManyArguments);
    }

    private static void TokenizerBoundaryMatrix()
    {
        CommandTokenization empty = CommandTokenizer.Tokenize(null);
        Equal(0, empty.Tokens.Count);
        True(!empty.HasUnterminatedQuote, "Null input must tokenize as empty input.");

        CommandTokenization whitespace = CommandTokenizer.Tokenize(" \t\r\n ");
        Equal(0, whitespace.Tokens.Count);
        True(!whitespace.HasUnterminatedQuote,
            "Whitespace-only input must not report an unterminated quote.");

        const string input = " \t/spawn\titem:Gun  2 \r\n";
        CommandTokenization tokens = CommandTokenizer.Tokenize(input);
        Equal(3, tokens.Tokens.Count);
        Equal("/spawn", tokens.Tokens[0].Value);
        Equal(input.IndexOf("/spawn", StringComparison.Ordinal), tokens.Tokens[0].Start);
        Equal("/spawn".Length, tokens.Tokens[0].Length);
        Equal("item:Gun", tokens.Tokens[1].Value);
        Equal(input.IndexOf("item:Gun", StringComparison.Ordinal), tokens.Tokens[1].Start);
        Equal("2", tokens.Tokens[2].Value);
        Equal(input.IndexOf("2", StringComparison.Ordinal), tokens.Tokens[2].Start);
        True(!tokens.HasUnterminatedQuote, "A fully unquoted command should be terminated.");

        const string adjacentInput = "/spawn pre\"middle value\"post";
        CommandTokenization adjacent = CommandTokenizer.Tokenize(adjacentInput);
        Equal(2, adjacent.Tokens.Count);
        Equal("premiddle valuepost", adjacent.Tokens[1].Value);
        True(adjacent.Tokens[1].IsQuoted,
            "A token containing a quoted segment should be marked quoted.");
        Equal(adjacentInput.IndexOf("pre", StringComparison.Ordinal), adjacent.Tokens[1].Start);
        Equal(adjacentInput.Length - adjacent.Tokens[1].Start, adjacent.Tokens[1].Length);

        CommandTokenization rawBackslash = CommandTokenizer.Tokenize(
            "/spawn item:Back\\Slash");
        Equal("item:Back\\Slash", rawBackslash.Tokens[1].Value);
    }

    private static void TokenizerQuoteAndEscapeMatrix()
    {
        CommandTokenization singleQuoted = CommandTokenizer.Tokenize(
            "/spawn 'item:Health Pack'");
        Equal(2, singleQuoted.Tokens.Count);
        Equal("item:Health Pack", singleQuoted.Tokens[1].Value);
        True(singleQuoted.Tokens[1].IsQuoted, "Single quotes should mark a token quoted.");

        CommandTokenization doubleEscapes = CommandTokenizer.Tokenize(
            "/spawn \"item:Bob\\\"s \\\\ Bat\"");
        Equal("item:Bob\"s \\ Bat", doubleEscapes.Tokens[1].Value);
        True(!doubleEscapes.HasUnterminatedQuote,
            "Escaped quotes and backslashes must not terminate a token early.");

        CommandTokenization singleEscapes = CommandTokenizer.Tokenize(
            "/grant 'Pilot\\'s Chair'");
        Equal("Pilot's Chair", singleEscapes.Tokens[1].Value);

        CommandTokenization unknownEscape = CommandTokenizer.Tokenize(
            "/spawn \"item:Odd\\q Name\"");
        Equal("item:Odd\\q Name", unknownEscape.Tokens[1].Value);

        CommandTokenization emptyQuotes = CommandTokenizer.Tokenize("/grant \"\"");
        Equal(2, emptyQuotes.Tokens.Count);
        Equal(string.Empty, emptyQuotes.Tokens[1].Value);
        True(emptyQuotes.Tokens[1].IsQuoted, "An empty quoted token must remain a token.");

        CommandTokenization unterminatedSingle = CommandTokenizer.Tokenize(
            "/grant 'Pilot");
        Equal("Pilot", unterminatedSingle.Tokens[1].Value);
        True(unterminatedSingle.HasUnterminatedQuote,
            "An unterminated single quote must be reported.");
    }

    private static void QuoteArgumentRoundTrips()
    {
        string[] values =
        {
            string.Empty,
            "simple",
            "/spawn",
            "item:Health Pack",
            "Alice#42",
            " leading",
            "trailing ",
            "two\tcolumns",
            "two\r\nlines",
            "Bob's Bat",
            "Bob\"s Bat",
            "Back\\Slash",
            "Back\\Slash and \"Quote\"",
            "敵:Headman"
        };

        foreach (string value in values)
        {
            string formatted = CommandTokenizer.QuoteArgument(value);
            CommandTokenization tokenization = CommandTokenizer.Tokenize(formatted);
            Equal(1, tokenization.Tokens.Count);
            Equal(value, tokenization.Tokens[0].Value);
            Equal(0, tokenization.Tokens[0].Start);
            Equal(formatted.Length, tokenization.Tokens[0].Length);
            True(!tokenization.HasUnterminatedQuote,
                "QuoteArgument produced unterminated text for: " + value);
        }

        Equal("simple", CommandTokenizer.QuoteArgument("simple"));
        Equal("\"\"", CommandTokenizer.QuoteArgument(string.Empty));
        Equal("\"Bob's Bat\"", CommandTokenizer.QuoteArgument("Bob's Bat"));
        Equal("\"Bob\\\"s Bat\"", CommandTokenizer.QuoteArgument("Bob\"s Bat"));
    }

    private static void FuzzyPrecedence()
    {
        int exact = FuzzyMatcher.Score("cat", "cat");
        int prefix = FuzzyMatcher.Score("cat", "catalog");
        int substring = FuzzyMatcher.Score("cat", "scat");
        int subsequence = FuzzyMatcher.Score("cat", "coat");
        int typo = FuzzyMatcher.Score("cat", "cut");
        int noMatch = FuzzyMatcher.Score("cat", "dog");

        Greater(exact, prefix, "Exact matches must outrank prefixes.");
        Greater(prefix, substring, "Prefix matches must outrank substrings.");
        Greater(substring, subsequence, "Substring matches must outrank subsequences.");
        Greater(subsequence, typo, "Subsequence matches must outrank typo matches.");
        Equal(FuzzyMatcher.NoMatch, noMatch);
    }

    private static void FuzzyRankingDeduplicatesAndLimits()
    {
        string[] candidates = { "Beta", "beta", "Alpha", "Gamma", "ALPHA" };
        IReadOnlyList<FuzzyMatch> all = FuzzyMatcher.Rank(string.Empty, candidates, 10);
        Equal(3, all.Count);
        Equal("Beta", all[0].Value);
        Equal("Alpha", all[1].Value);
        Equal("Gamma", all[2].Value);

        IReadOnlyList<FuzzyMatch> limited = FuzzyMatcher.Rank(
            string.Empty,
            candidates,
            2);
        Equal(2, limited.Count);
        Equal("Beta", limited[0].Value);
        Equal("Alpha", limited[1].Value);

        Equal(0, FuzzyMatcher.Rank(string.Empty, candidates, 0).Count);
        Equal(0, FuzzyMatcher.Rank(string.Empty, candidates, -1).Count);
        Throws<ArgumentNullException>(
            delegate { FuzzyMatcher.Rank("x", null, 1); },
            "A null candidate sequence should be rejected.");
    }

    private static void FuzzyNormalizationAndNoMatch()
    {
        Equal(100000, FuzzyMatcher.Score("  /SPAWN  ", "/spawn"));
        True(FuzzyMatcher.Score("spawn", "/spawn") != FuzzyMatcher.NoMatch,
            "A slashless query should match a slash command.");
        True(FuzzyMatcher.Score("spwan", "/spawn") != FuzzyMatcher.NoMatch,
            "An adjacent transposition should fuzzy-match a command.");
        True(FuzzyMatcher.Score("strg", "item:Strength Upgrade") != FuzzyMatcher.NoMatch,
            "A query should match a target-name segment after its kind prefix.");
        True(FuzzyMatcher.Score("rncl", CommandLocations.RandomNonCollisionLocation) !=
            FuzzyMatcher.NoMatch,
            "A subsequence should match a hyphenated location.");
        Equal(FuzzyMatcher.NoMatch, FuzzyMatcher.Score("zz", "item:Health Pack"));
        Equal(FuzzyMatcher.NoMatch, FuzzyMatcher.Score("anything", null));
        Equal(1, FuzzyMatcher.Score(null, "candidate"));
    }

    private static void CompletionOffersEveryValidCount()
    {
        for (int count = SlashCommandParser.MinimumCount;
            count <= SlashCommandParser.MaximumCount;
            count++)
        {
            string value = count.ToString(System.Globalization.CultureInfo.InvariantCulture);

            IReadOnlyList<CompletionItem> spawn = Complete(
                "/spawn item:Gun " + value,
                Catalog);
            HasAny(spawn, "Spawn count completion missing for " + value + ".");
            Equal(value, spawn[0].Value);
            Equal(2, spawn[0].ArgumentIndex);

            IReadOnlyList<CompletionItem> despawn = Complete(
                "/despawn item:Gun " + value,
                Catalog);
            HasAny(despawn, "Despawn count completion missing for " + value + ".");
            Equal(value, despawn[0].Value);
            Equal(2, despawn[0].ArgumentIndex);
        }

        IReadOnlyList<CompletionItem> all = Complete(
            "/despawn item:Gun all",
            Catalog);
        HasAny(all, "Despawn count completion should include all.");
        Equal("all", all[0].Value);
    }

    private static void CompletionOffersLocationWithoutCount()
    {
        const string blank = "/spawn item:Gun ";
        IReadOnlyList<CompletionItem> every = CommandCompletionEngine.GetCompletions(
            blank,
            blank.Length,
            Catalog,
            SlashCommandParser.MaximumCount + 10);
        ContainsValue(every, "1", "The count candidates should remain available at argument two.");
        ContainsValue(
            every,
            CommandLocations.PlayerLocation,
            "Player location should be offered while count defaults to one.");
        ContainsValue(
            every,
            CommandLocations.RandomNonCollisionLocation,
            "Random collision-free location should be offered while count defaults to one.");

        const string randomInput = "/spawn item:Gun rncl";
        IReadOnlyList<CompletionItem> random = Complete(randomInput, Catalog);
        HasAny(random, "A fuzzy location should complete without an explicit count.");
        Equal(CommandLocations.RandomNonCollisionLocation, random[0].Value);
        Equal(2, random[0].ArgumentIndex);
        CompletionApplication applied = CommandCompletionEngine.ApplyCompletion(
            randomInput,
            random[0]);
        Equal(
            "/spawn item:Gun random-non-collision-location",
            applied.Text);
        CommandParseResult parsed = SlashCommandParser.Parse(applied.Text);
        True(parsed.Success, parsed.ErrorMessage);
        Equal(1, parsed.Command.Count.Value);
        Equal(CommandLocations.RandomNonCollisionLocation, parsed.Command.Location);

        const string playerInput = "/spawn item:Gun pl";
        IReadOnlyList<CompletionItem> player = Complete(playerInput, Catalog);
        HasAny(player, "Player location should fuzzy-complete without an explicit count.");
        Equal(CommandLocations.PlayerLocation, player[0].Value);
    }

    private static void CompletionCursorPreservesSuffix()
    {
        const string targetInput =
            "/spawn item:hea 2 random-non-collision-location";
        int targetCaret = targetInput.IndexOf("item:hea", StringComparison.Ordinal) + 5;
        IReadOnlyList<CompletionItem> targetMatches = CommandCompletionEngine.GetCompletions(
            targetInput,
            targetCaret,
            Catalog,
            10);
        HasAny(targetMatches, "A target should complete when the caret is inside its token.");
        Equal("item:Health Pack", targetMatches[0].Value);
        CompletionApplication targetApplied = CommandCompletionEngine.ApplyCompletion(
            targetInput,
            targetMatches[0]);
        Equal(
            "/spawn \"item:Health Pack\" 2 random-non-collision-location",
            targetApplied.Text);

        const string countInput =
            "/spawn item:Gun 205x random-non-collision-location";
        int countCaret = countInput.IndexOf("205x", StringComparison.Ordinal) + 2;
        IReadOnlyList<CompletionItem> countMatches = CommandCompletionEngine.GetCompletions(
            countInput,
            countCaret,
            Catalog,
            10);
        HasAny(countMatches, "A count should complete when the caret is inside its token.");
        Equal("205", countMatches[0].Value);
        CompletionApplication countApplied = CommandCompletionEngine.ApplyCompletion(
            countInput,
            countMatches[0]);
        Equal(
            "/spawn item:Gun 205 random-non-collision-location",
            countApplied.Text);

        const string commandInput = "/spawn item:hea";
        IReadOnlyList<CompletionItem> atStart = CommandCompletionEngine.GetCompletions(
            commandInput,
            0,
            Catalog,
            10);
        HasAny(atStart, "The command token should be active when the caret is at its start.");
        Equal("/spawn", atStart[0].Value);
        Equal(0, atStart[0].ArgumentIndex);
    }

    private static void CompletionWhitespaceInsertion()
    {
        const string missingTarget = "/spawn  2";
        int caret = missingTarget.IndexOf("  ", StringComparison.Ordinal) + 1;
        IReadOnlyList<CompletionItem> targetMatches = CommandCompletionEngine.GetCompletions(
            missingTarget,
            caret,
            Catalog,
            10);
        HasAny(targetMatches, "Autocomplete should insert into whitespace between tokens.");
        Equal(1, targetMatches[0].ArgumentIndex);
        CompletionApplication targetApplied = CommandCompletionEngine.ApplyCompletion(
            missingTarget,
            targetMatches[0]);
        Equal("/spawn \"item:Health Pack\" 2", targetApplied.Text);
        True(SlashCommandParser.Parse(targetApplied.Text).Success,
            "Whitespace insertion should produce a parseable command.");

        const string existingWhitespace = "/spa   ";
        IReadOnlyList<CompletionItem> commandMatches = CommandCompletionEngine.GetCompletions(
            existingWhitespace,
            4,
            Catalog,
            10);
        HasAny(commandMatches, "The partial command should complete.");
        CompletionApplication commandApplied = CommandCompletionEngine.ApplyCompletion(
            existingWhitespace,
            commandMatches[0],
            true);
        Equal("/spawn   ", commandApplied.Text);
        Equal(7, commandApplied.CaretPosition);

        const string existingTab = "/spawn item:hea\t2";
        int targetEnd = existingTab.IndexOf('\t');
        IReadOnlyList<CompletionItem> matches = CommandCompletionEngine.GetCompletions(
            existingTab,
            targetEnd,
            Catalog,
            10);
        CompletionApplication applied = CommandCompletionEngine.ApplyCompletion(
            existingTab,
            matches[0],
            true);
        Equal("/spawn \"item:Health Pack\"\t2", applied.Text);
        Equal(applied.Text.IndexOf('\t') + 1, applied.CaretPosition);
    }

    private static void CompletionInvalidCommandRecovery()
    {
        const string input = "/spwan item:hea";
        IReadOnlyList<CompletionItem> commandMatches = Complete(input, Catalog);
        HasAny(commandMatches,
            "An invalid command should be repaired before completing its arguments.");
        Equal("/spawn", commandMatches[0].Value);
        Equal(0, commandMatches[0].ArgumentIndex);
        Equal(0, commandMatches[0].ReplacementStart);
        Equal("/spwan".Length, commandMatches[0].ReplacementLength);

        CompletionApplication commandApplied = CommandCompletionEngine.ApplyCompletion(
            input,
            commandMatches[0]);
        Equal("/spawn item:hea", commandApplied.Text);

        IReadOnlyList<CompletionItem> targetMatches = Complete(commandApplied.Text, Catalog);
        HasAny(targetMatches, "The target should complete after repairing the command.");
        Equal("item:Health Pack", targetMatches[0].Value);
        Equal(1, targetMatches[0].ArgumentIndex);
    }

    private static void CompletionRepairsUnterminatedQuote()
    {
        const string input = "/spawn \"item:hea";
        IReadOnlyList<CompletionItem> matches = Complete(input, Catalog);
        HasAny(matches, "An unterminated quoted target should remain completable.");
        Equal("item:Health Pack", matches[0].Value);
        CompletionApplication applied = CommandCompletionEngine.ApplyCompletion(
            input,
            matches[0]);
        Equal("/spawn \"item:Health Pack\"", applied.Text);
        CommandParseResult parsed = SlashCommandParser.Parse(applied.Text);
        True(parsed.Success, parsed.ErrorMessage);
        Equal("item:Health Pack", parsed.Command.Target);

        var quoteCatalog = new CompletionCatalog(
            new[] { "item:Bob\"s Bat" },
            new string[0]);
        const string quoteInput = "/spawn bob";
        IReadOnlyList<CompletionItem> quoteMatches = Complete(quoteInput, quoteCatalog);
        HasAny(quoteMatches, "A target containing a quote should complete.");
        CompletionApplication quoteApplied = CommandCompletionEngine.ApplyCompletion(
            quoteInput,
            quoteMatches[0]);
        Equal("/spawn \"item:Bob\\\"s Bat\"", quoteApplied.Text);
        CommandParseResult quoteParsed = SlashCommandParser.Parse(quoteApplied.Text);
        True(quoteParsed.Success, quoteParsed.ErrorMessage);
        Equal("item:Bob\"s Bat", quoteParsed.Command.Target);
    }

    private static void CompletionProductionPlayerSelectors()
    {
        const string grantInput = "/grant bb12";
        IReadOnlyList<CompletionItem> grantMatches = Complete(
            grantInput,
            ProductionShapeCatalog);
        HasAny(grantMatches, "A production-shaped player selector should fuzzy-complete.");
        Equal("Bob Builder#12", grantMatches[0].Value);
        Equal(1, grantMatches[0].ArgumentIndex);
        CompletionApplication grantApplied = CommandCompletionEngine.ApplyCompletion(
            grantInput,
            grantMatches[0],
            true);
        Equal("/grant \"Bob Builder#12\" ", grantApplied.Text);
        CommandParseResult granted = SlashCommandParser.Parse(grantApplied.Text);
        True(granted.Success, granted.ErrorMessage);
        Equal("Bob Builder#12", granted.Command.Player);

        const string revokeInput = "/revoke pilot9";
        IReadOnlyList<CompletionItem> revokeMatches = Complete(
            revokeInput,
            ProductionShapeCatalog);
        HasAny(revokeMatches, "A quoted production player selector should fuzzy-complete.");
        Equal("Quote \"Pilot\"#9", revokeMatches[0].Value);
        CompletionApplication revokeApplied = CommandCompletionEngine.ApplyCompletion(
            revokeInput,
            revokeMatches[0]);
        CommandParseResult revoked = SlashCommandParser.Parse(revokeApplied.Text);
        True(revoked.Success, revoked.ErrorMessage);
        Equal("Quote \"Pilot\"#9", revoked.Command.Player);
    }

    private static void CompletionArityAndArgumentValidation()
    {
        Equal(0, Complete("/help ", Catalog).Count);
        Equal(0, Complete("/permissions extra", Catalog).Count);
        Equal(0, Complete(
            "/spawn item:Gun 1 player-location extra",
            Catalog).Count);
        Equal(0, Complete(
            "/spawn item:Gun player-location ",
            Catalog).Count);
        Equal(0, Complete(
            "/spawn item:Gun random-non-collision-location ",
            Catalog).Count);
        Equal(0, Complete("/grant Alice extra", Catalog).Count);

        Equal(0, CommandCompletionEngine.GetCompletions(
            "/",
            1,
            Catalog,
            0).Count);
        Equal(0, CommandCompletionEngine.GetCompletions(
            "/",
            1,
            Catalog,
            -1).Count);

        IReadOnlyList<CompletionItem> nullCatalog = CommandCompletionEngine.GetCompletions(
            "/spa",
            4,
            null,
            10);
        HasAny(nullCatalog, "Command completion should not require a dynamic catalog.");
        Equal("/spawn", nullCatalog[0].Value);

        Throws<ArgumentOutOfRangeException>(
            delegate
            {
                CommandCompletionEngine.GetCompletions("/", -1, Catalog, 10);
            },
            "A negative caret should be rejected.");
        Throws<ArgumentOutOfRangeException>(
            delegate
            {
                CommandCompletionEngine.GetCompletions("/", 2, Catalog, 10);
            },
            "A caret beyond the input should be rejected.");
        Throws<ArgumentNullException>(
            delegate
            {
                CommandCompletionEngine.ApplyCompletion("/", null);
            },
            "A null completion should be rejected.");

        IReadOnlyList<CompletionItem> longSpan = Complete(
            "/spawn item:hea",
            Catalog);
        Throws<ArgumentException>(
            delegate
            {
                CommandCompletionEngine.ApplyCompletion("/", longSpan[0]);
            },
            "A completion span from another input should be rejected.");
    }

    private static void NetworkEnvelopeValidation()
    {
        const string requestId = "0123456789abcdef0123456789abcdef";
        string[] kinds =
        {
            CommandNetworkPolicy.RequestKind,
            CommandNetworkPolicy.ResponseKind,
            CommandNetworkPolicy.NoticeKind
        };

        foreach (string expectedKind in kinds)
        {
            object[] envelope = CommandNetworkPolicy.Envelope(
                expectedKind,
                requestId,
                "/help");
            string kind;
            string parsedRequestId;
            string payload;
            True(CommandNetworkPolicy.TryReadEnvelope(
                    envelope,
                    out kind,
                    out parsedRequestId,
                    out payload),
                "A valid " + expectedKind + " envelope should parse.");
            Equal(expectedKind, kind);
            Equal(requestId, parsedRequestId);
            Equal("/help", payload);
        }

        object[] nullValues = CommandNetworkPolicy.Envelope(
            CommandNetworkPolicy.NoticeKind,
            null,
            null);
        string nullKind;
        string nullRequestId;
        string nullPayload;
        True(CommandNetworkPolicy.TryReadEnvelope(
                nullValues,
                out nullKind,
                out nullRequestId,
                out nullPayload),
            "Envelope should normalize null request IDs and payloads.");
        Equal(string.Empty, nullRequestId);
        Equal(string.Empty, nullPayload);

        object[] valid = CommandNetworkPolicy.Envelope(
            CommandNetworkPolicy.RequestKind,
            requestId,
            "/help");
        EnvelopeFails(null, "A null envelope must be rejected.");
        EnvelopeFails(new object[0], "An empty envelope must be rejected.");
        EnvelopeFails(new object[]
        {
            CommandNetworkPolicy.Magic,
            CommandNetworkPolicy.ProtocolVersion,
            CommandNetworkPolicy.RequestKind,
            requestId
        }, "A short envelope must be rejected.");
        EnvelopeFails(new object[]
        {
            CommandNetworkPolicy.Magic,
            CommandNetworkPolicy.ProtocolVersion,
            CommandNetworkPolicy.RequestKind,
            requestId,
            "/help",
            "extra"
        }, "A long envelope must be rejected.");

        object[] wrongMagic = (object[])valid.Clone();
        wrongMagic[0] = "another.mod";
        EnvelopeFails(wrongMagic, "An envelope with the wrong magic must be rejected.");

        object[] nonStringMagic = (object[])valid.Clone();
        nonStringMagic[0] = 123;
        EnvelopeFails(nonStringMagic, "Envelope magic must be a string.");

        object[] wrongVersion = (object[])valid.Clone();
        wrongVersion[1] = CommandNetworkPolicy.ProtocolVersion + 1;
        EnvelopeFails(wrongVersion, "A different protocol version must be rejected.");

        object[] stringVersion = (object[])valid.Clone();
        stringVersion[1] = CommandNetworkPolicy.ProtocolVersion.ToString();
        EnvelopeFails(stringVersion, "Protocol version must use the declared integer wire type.");

        object[] invalidVersion = (object[])valid.Clone();
        invalidVersion[1] = new object();
        EnvelopeFails(invalidVersion, "A non-convertible protocol version must be rejected.");

        object[] unknownKind = (object[])valid.Clone();
        unknownKind[2] = "unknown";
        EnvelopeFails(unknownKind, "An unknown envelope kind must be rejected.");

        object[] wrongCaseKind = (object[])valid.Clone();
        wrongCaseKind[2] = "REQUEST";
        EnvelopeFails(wrongCaseKind, "Envelope kinds must use the exact protocol spelling.");

        for (int index = 2; index <= 4; index++)
        {
            object[] wrongType = (object[])valid.Clone();
            wrongType[index] = 123;
            EnvelopeFails(
                wrongType,
                "Envelope field " + index + " must be a string.");
        }
    }

    private static void NetworkRequestIdValidation()
    {
        True(CommandNetworkPolicy.IsValidRequestId(
                "0123456789abcdef0123456789abcdef"),
            "A lowercase 32-character hexadecimal request ID should be valid.");
        True(CommandNetworkPolicy.IsValidRequestId(
                "ABCDEF0123456789abcdef0123456789"),
            "Mixed-case hexadecimal request IDs should be valid.");
        True(CommandNetworkPolicy.IsValidRequestId(Guid.NewGuid().ToString("N")),
            "Guid N-format IDs should be valid.");

        True(!CommandNetworkPolicy.IsValidRequestId(null),
            "A null request ID must be rejected.");
        True(!CommandNetworkPolicy.IsValidRequestId(string.Empty),
            "An empty request ID must be rejected.");
        True(!CommandNetworkPolicy.IsValidRequestId(new string('a', 31)),
            "A 31-character request ID must be rejected.");
        True(!CommandNetworkPolicy.IsValidRequestId(new string('a', 33)),
            "A 33-character request ID must be rejected.");
        True(!CommandNetworkPolicy.IsValidRequestId(
                "0123456789abcdef0123456789abcdeg"),
            "A non-hexadecimal request ID must be rejected.");
        True(!CommandNetworkPolicy.IsValidRequestId(Guid.NewGuid().ToString("D")),
            "A hyphenated Guid must be rejected.");
        True(!CommandNetworkPolicy.IsValidRequestId(
                "0123456789abcdef 123456789abcdef"),
            "Whitespace must not be accepted in a request ID.");
    }

    private static void PhotonCallbackOfflineLifecycle()
    {
        var lifecycle = new PhotonCallbackRegistrationLifecycle();
        int registerCalls = 0;
        int unregisterCalls = 0;
        Action register = delegate { registerCalls++; };
        Action unregister = delegate { unregisterCalls++; };

        True(!lifecycle.IsRegistered,
            "A new callback lifecycle must not register during plugin construction.");
        True(!lifecycle.IsDisposed,
            "A new callback lifecycle must remain usable until explicitly disposed.");

        // Offline, connecting, and disconnecting all present as roomActive=false.
        // Repeated synchronization must not touch the Photon callback registry.
        lifecycle.Synchronize(false, register, unregister);
        lifecycle.Synchronize(false, register, unregister);
        lifecycle.Synchronize(false, register, unregister);

        Equal(0, registerCalls);
        Equal(0, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "Non-room states must leave the Photon callback target unregistered.");
        True(!lifecycle.IsDisposed,
            "Synchronizing an offline state must not dispose the lifecycle.");
    }

    private static void PhotonCallbackRoomTransitionLifecycle()
    {
        var lifecycle = new PhotonCallbackRegistrationLifecycle();
        int registerCalls = 0;
        int unregisterCalls = 0;
        Action register = delegate { registerCalls++; };
        Action unregister = delegate { unregisterCalls++; };

        lifecycle.Synchronize(true, register, unregister);
        Equal(1, registerCalls);
        Equal(0, unregisterCalls);
        True(lifecycle.IsRegistered,
            "Entering a room must register the callback target exactly once.");

        for (int update = 0; update < 10; update++)
            lifecycle.Synchronize(true, register, unregister);
        Equal(1, registerCalls);
        Equal(0, unregisterCalls);
        True(lifecycle.IsRegistered,
            "Repeated in-room updates must preserve one callback registration.");

        lifecycle.Synchronize(false, register, unregister);
        Equal(1, registerCalls);
        Equal(1, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "Leaving the room must unregister the callback target.");

        for (int update = 0; update < 10; update++)
            lifecycle.Synchronize(false, register, unregister);
        Equal(1, registerCalls);
        Equal(1, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "Repeated out-of-room updates must not unregister twice.");

        lifecycle.Synchronize(true, register, unregister);
        lifecycle.Synchronize(true, register, unregister);
        Equal(2, registerCalls);
        Equal(1, unregisterCalls);
        True(lifecycle.IsRegistered,
            "Rejoining a room must create one new callback registration.");

        lifecycle.Synchronize(false, register, unregister);
        lifecycle.Synchronize(false, register, unregister);
        Equal(2, registerCalls);
        Equal(2, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "A second room exit must remove only the second registration.");
        True(!lifecycle.IsDisposed,
            "Ordinary room transitions must not dispose the lifecycle.");
    }

    private static void PhotonCallbackRegisteredDisposal()
    {
        var lifecycle = new PhotonCallbackRegistrationLifecycle();
        int registerCalls = 0;
        int unregisterCalls = 0;
        Action register = delegate { registerCalls++; };
        Action unregister = delegate { unregisterCalls++; };

        lifecycle.Synchronize(true, register, unregister);
        lifecycle.Dispose(unregister);

        Equal(1, registerCalls);
        Equal(1, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "Disposing an active lifecycle must clear its registration state.");
        True(lifecycle.IsDisposed,
            "Disposing an active lifecycle must make the lifecycle terminal.");

        lifecycle.Dispose(unregister);
        lifecycle.Synchronize(true, register, unregister);
        lifecycle.Synchronize(false, register, unregister);
        lifecycle.Dispose(unregister);

        Equal(1, registerCalls);
        Equal(1, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "A disposed lifecycle must never register again.");
        True(lifecycle.IsDisposed,
            "Repeated disposal and synchronization must preserve disposed state.");
    }

    private static void PhotonCallbackUnregisteredDisposal()
    {
        var lifecycle = new PhotonCallbackRegistrationLifecycle();
        int registerCalls = 0;
        int unregisterCalls = 0;
        Action register = delegate { registerCalls++; };
        Action unregister = delegate { unregisterCalls++; };

        lifecycle.Synchronize(false, register, unregister);
        lifecycle.Dispose(unregister);

        Equal(0, registerCalls);
        Equal(0, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "Disposing outside a room must remain unregistered.");
        True(lifecycle.IsDisposed,
            "Disposing outside a room must make the lifecycle terminal.");

        lifecycle.Synchronize(true, register, unregister);
        lifecycle.Synchronize(false, register, unregister);
        lifecycle.Dispose(unregister);

        Equal(0, registerCalls);
        Equal(0, unregisterCalls);
        True(!lifecycle.IsRegistered,
            "A lifecycle disposed before joining must ignore later room activity.");
    }

    private static void PhotonCallbackArgumentValidation()
    {
        var lifecycle = new PhotonCallbackRegistrationLifecycle();

        // No callback is required when the requested state already matches.
        lifecycle.Synchronize(false, null, null);
        True(!lifecycle.IsRegistered,
            "An offline no-op must not require callback delegates.");

        Throws<ArgumentNullException>(
            delegate { lifecycle.Synchronize(true, null, null); },
            "Entering a room without a register callback must fail explicitly.");
        True(!lifecycle.IsRegistered,
            "A missing register callback must not claim registration succeeded.");
        True(!lifecycle.IsDisposed,
            "A failed registration attempt must leave the lifecycle retryable.");

        lifecycle.Synchronize(true, delegate { }, null);
        lifecycle.Synchronize(true, null, null);
        True(lifecycle.IsRegistered,
            "An already-registered in-room no-op must not require callback delegates.");

        Throws<ArgumentNullException>(
            delegate { lifecycle.Synchronize(false, null, null); },
            "Leaving a room without an unregister callback must fail explicitly.");
        True(lifecycle.IsRegistered,
            "A missing unregister callback must preserve registration for retry.");
        True(!lifecycle.IsDisposed,
            "A failed room-exit cleanup must leave the lifecycle retryable.");

        Throws<ArgumentNullException>(
            delegate { lifecycle.Dispose(null); },
            "Disposing a registered lifecycle without cleanup must fail explicitly.");
        True(lifecycle.IsRegistered,
            "Rejected disposal must preserve registration for a later cleanup attempt.");
        True(!lifecycle.IsDisposed,
            "Rejected disposal must not make cleanup permanently unreachable.");

        lifecycle.Dispose(delegate { });
        True(!lifecycle.IsRegistered,
            "A valid disposal retry must clear the registration.");
        True(lifecycle.IsDisposed,
            "A valid disposal retry must make the lifecycle terminal.");

        // A lifecycle that never registered has no cleanup callback to invoke.
        var neverRegistered = new PhotonCallbackRegistrationLifecycle();
        neverRegistered.Dispose(null);
        True(neverRegistered.IsDisposed,
            "Disposal without a callback should succeed when nothing was registered.");
        True(!neverRegistered.IsRegistered,
            "Disposal without prior registration must remain unregistered.");
    }

    private static void PhotonCallbackFailureStateLifecycle()
    {
        var lifecycle = new PhotonCallbackRegistrationLifecycle();
        int registerAttempts = 0;
        int unregisterAttempts = 0;

        Throws<InvalidOperationException>(
            delegate
            {
                lifecycle.Synchronize(
                    true,
                    delegate
                    {
                        registerAttempts++;
                        throw new InvalidOperationException("register failed");
                    },
                    delegate { unregisterAttempts++; });
            },
            "A Photon registration failure must propagate to the caller.");
        Equal(1, registerAttempts);
        Equal(0, unregisterAttempts);
        True(!lifecycle.IsRegistered,
            "A failed Photon registration must remain unregistered for retry.");
        True(!lifecycle.IsDisposed,
            "A failed Photon registration must not dispose the lifecycle.");

        lifecycle.Synchronize(
            true,
            delegate { registerAttempts++; },
            delegate { unregisterAttempts++; });
        Equal(2, registerAttempts);
        True(lifecycle.IsRegistered,
            "A registration retry should succeed after the callback recovers.");

        Throws<InvalidOperationException>(
            delegate
            {
                lifecycle.Synchronize(
                    false,
                    delegate { registerAttempts++; },
                    delegate
                    {
                        unregisterAttempts++;
                        throw new InvalidOperationException("unregister failed");
                    });
            },
            "A Photon room-exit cleanup failure must propagate to the caller.");
        Equal(1, unregisterAttempts);
        True(lifecycle.IsRegistered,
            "A failed room-exit cleanup must preserve registration for retry.");
        True(!lifecycle.IsDisposed,
            "A failed room-exit cleanup must not dispose the lifecycle.");

        lifecycle.Synchronize(
            false,
            delegate { registerAttempts++; },
            delegate { unregisterAttempts++; });
        Equal(2, unregisterAttempts);
        True(!lifecycle.IsRegistered,
            "A room-exit cleanup retry should clear registration after recovery.");

        lifecycle.Synchronize(
            true,
            delegate { registerAttempts++; },
            delegate { unregisterAttempts++; });
        Equal(3, registerAttempts);
        True(lifecycle.IsRegistered,
            "The lifecycle should still permit a later room registration.");

        Throws<InvalidOperationException>(
            delegate
            {
                lifecycle.Dispose(
                    delegate
                    {
                        unregisterAttempts++;
                        throw new InvalidOperationException("dispose cleanup failed");
                    });
            },
            "A disposal cleanup failure must propagate to the caller.");
        Equal(3, unregisterAttempts);
        True(!lifecycle.IsRegistered,
            "Disposal must clear local registration state even if cleanup throws.");
        True(lifecycle.IsDisposed,
            "Disposal must remain terminal even if cleanup throws.");

        lifecycle.Synchronize(
            true,
            delegate { registerAttempts++; },
            delegate { unregisterAttempts++; });
        lifecycle.Dispose(delegate { unregisterAttempts++; });
        Equal(3, registerAttempts);
        Equal(3, unregisterAttempts);
        True(!lifecycle.IsRegistered,
            "A disposed lifecycle must ignore retries after cleanup failure.");
    }

    private static void RemoteCommandPolicyMatrix()
    {
        CommandRequestValidation grantedSpawn =
            CommandNetworkPolicy.ValidateRemoteCommand(
                "/spawn item:Gun 2 player-location",
                true);
        True(grantedSpawn.Allowed,
            "A granted client should be allowed to submit a valid spawn.");
        Equal<string>(null, grantedSpawn.Error);

        CommandRequestValidation deniedSpawn =
            CommandNetworkPolicy.ValidateRemoteCommand(
                "/spawn item:Gun",
                false);
        ValidationDenied(deniedSpawn, "not granted");

        CommandRequestValidation publicHelp =
            CommandNetworkPolicy.ValidateRemoteCommand("/help", false);
        True(publicHelp.Allowed, "/help should be public.");
        CommandRequestValidation publicPermissions =
            CommandNetworkPolicy.ValidateRemoteCommand("  /PERMISSIONS", false);
        True(publicPermissions.Allowed, "/permissions should be public and case-insensitive.");

        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand("/grant Alice", true),
            "only be run locally");
        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand("/revoke Alice", false),
            "only be run locally");
        True(CommandNetworkPolicy.IsHostOnlyVerb(" \t/GrAnT Alice"),
            "Host-only verb detection should ignore leading whitespace and case.");
        True(!CommandNetworkPolicy.IsHostOnlyVerb("/permissions"),
            "A public permission query is not a host-only mutation.");

        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand(null, true),
            "Malformed");
        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand("   ", true),
            "Malformed");
        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand("spawn item:Gun", true),
            "slash-command");
        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand("/unknown", true),
            "Unknown command");
        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand("/spawn item:Gun 0", true),
            "Count must be");

        string maximumCommand = "/spawn item:" + new string('a', 500);
        Equal(CommandNetworkPolicy.MaximumCommandLength, maximumCommand.Length);
        True(CommandNetworkPolicy.ValidateRemoteCommand(maximumCommand, true).Allowed,
            "A valid command exactly at the length limit should be allowed.");

        string oversizedCommand = maximumCommand + "a";
        ValidationDenied(
            CommandNetworkPolicy.ValidateRemoteCommand(oversizedCommand, true),
            "Malformed");
        True(CommandNetworkPolicy.IsPublicVerb("\r\n/help"),
            "Public verb detection should ignore leading line whitespace.");
        True(!CommandNetworkPolicy.IsPublicVerb("/spawn item:Gun"),
            "Spawn must require a grant.");
    }

    private static void RollingRateLimiterLifecycle()
    {
        Throws<ArgumentOutOfRangeException>(
            delegate { new SlidingWindowRateLimiter(0, 3f); },
            "A zero event limit should be rejected.");
        Throws<ArgumentOutOfRangeException>(
            delegate { new SlidingWindowRateLimiter(1, 0f); },
            "A zero-second window should be rejected.");

        var limiter = new SlidingWindowRateLimiter(2, 3f);
        True(!limiter.TryConsume(0, 0f),
            "Actor zero must not consume rate-limit capacity.");
        True(!limiter.TryConsume(-1, 0f),
            "Negative actor numbers must not consume rate-limit capacity.");

        True(limiter.TryConsume(1, 0f), "Actor one event one should pass.");
        True(limiter.TryConsume(1, 1f), "Actor one event two should pass.");
        True(!limiter.TryConsume(1, 2f), "Actor one's third in-window event should fail.");

        True(limiter.TryConsume(2, 2f),
            "A second actor should have isolated rate-limit capacity.");
        True(limiter.TryConsume(2, 2.5f),
            "The second actor should receive its full independent allowance.");
        True(!limiter.TryConsume(2, 2.75f),
            "The second actor should be limited independently.");

        True(!limiter.TryConsume(1, 3f),
            "An event exactly on the inclusive rolling-window boundary should remain limited.");
        True(limiter.TryConsume(1, 3.001f),
            "An event just beyond the rolling-window boundary should pass.");

        limiter.Clear();
        True(limiter.TryConsume(1, 3.001f),
            "Clear should restore actor one capacity.");
        True(limiter.TryConsume(1, 3.001f),
            "Clear should restore the full allowance.");
        True(!limiter.TryConsume(1, 3.001f),
            "The restored allowance should still enforce its maximum.");
    }

    private static void RateLimitResponseSuppression()
    {
        var gate = new RateLimitNoticeGate(3f);
        True(!gate.ShouldNotify(0, 0f), "Invalid actors must never receive notices.");
        True(gate.ShouldNotify(7, 10f), "The first excess request should receive a notice.");
        True(!gate.ShouldNotify(7, 10.1f),
            "Repeated excess requests in the silence window must be dropped.");
        True(gate.ShouldNotify(8, 10.1f),
            "Notice suppression must be isolated per actor.");
        True(gate.ShouldNotify(7, 13f),
            "A notice should be allowed when the silence window expires.");
        gate.Clear();
        True(gate.ShouldNotify(7, 13.1f), "Clearing a lobby session must clear notice state.");

        Throws<ArgumentOutOfRangeException>(() => new RateLimitNoticeGate(0f),
            "A non-positive notice interval must be rejected.");
    }

    private static void PendingCommandRegistryLifecycle()
    {
        const string requestA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string requestB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string requestC = "cccccccccccccccccccccccccccccccc";
        const string requestD = "dddddddddddddddddddddddddddddddd";
        const string requestE = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

        Throws<ArgumentOutOfRangeException>(
            delegate { new PendingCommandRegistry(0f); },
            "A zero pending timeout should be rejected.");

        var pending = new PendingCommandRegistry(30f);
        True(!pending.TryAdd("bad", 1, 7, 0f),
            "An invalid request ID must not be tracked.");
        True(!pending.TryAdd(requestA, 0, 7, 0f),
            "A request without a valid master actor must not be tracked.");
        Equal(0, pending.Count);

        True(pending.TryAdd(requestA, 1, 7, 0f),
            "A valid pending request should be tracked.");
        True(!pending.TryAdd(requestA, 1, 7, 1f),
            "A duplicate pending request ID must be rejected.");
        Equal(1, pending.Count);
        Equal(0, pending.CollectFailures(29.999f, true, 1, 7).Count);
        Equal(1, pending.Count);
        True(!pending.TryComplete(requestB),
            "Completing an unknown request must not remove another request.");
        Equal(1, pending.Count);
        True(pending.TryComplete(requestA),
            "The matching response should complete its request.");
        True(!pending.TryComplete(requestA),
            "A completed request must not complete twice.");
        Equal(0, pending.Count);

        True(pending.TryAdd(requestA, 1, 7, 0f),
            "A completed ID may be reused after removal.");
        IReadOnlyList<PendingCommandFailure> timeout =
            pending.CollectFailures(30f, true, 1, 7);
        SingleFailure(timeout, requestA, "Timed out");
        Equal(0, pending.Count);

        True(pending.TryAdd(requestB, 1, 7, 10f),
            "Room-leave request setup should succeed.");
        SingleFailure(
            pending.CollectFailures(10f, false, -1, 8),
            requestB,
            "room closed");

        True(pending.TryAdd(requestC, 1, 7, 10f),
            "Session-change request setup should succeed.");
        SingleFailure(
            pending.CollectFailures(10f, true, 1, 8),
            requestC,
            "room changed");

        True(pending.TryAdd(requestD, 1, 8, 10f),
            "Master-change request setup should succeed.");
        SingleFailure(
            pending.CollectFailures(10f, true, 2, 8),
            requestD,
            "host changed");

        True(pending.TryAdd(requestE, 2, 8, 10f),
            "Clear request setup should succeed.");
        Equal(1, pending.Count);
        pending.Clear();
        Equal(0, pending.Count);
        True(!pending.Remove(requestE),
            "A cleared request should no longer be removable.");
    }

    private static void SessionGrantLedgerLifecycle()
    {
        var ledger = new SessionGrantLedger();
        Equal(0L, ledger.Revision);
        True(!ledger.Grant(2), "A grant cannot be created outside a room.");
        True(!ledger.Revoke(2), "A revoke outside a room should be a no-op.");

        True(ledger.Synchronize(true, "Room A", 1, new[] { 1, 2, 3 }),
            "Joining a room should create a new session revision.");
        Equal(1L, ledger.Revision);
        True(!ledger.Synchronize(true, "Room A", 1, new[] { 1, 2, 3 }),
            "Synchronizing the same room and master should be stable.");
        Equal(1L, ledger.Revision);

        True(ledger.Grant(2), "A room actor should be grantable.");
        True(!ledger.Grant(2), "Granting the same actor twice should be idempotent.");
        True(ledger.IsGranted(2), "The granted actor should be authorized.");
        Equal(1L, ledger.Revision);
        True(ledger.Revoke(2), "The granted actor should be revocable.");
        True(!ledger.Revoke(2), "Revoking twice should be idempotent.");
        True(!ledger.IsGranted(2), "The revoked actor should no longer be authorized.");
        Equal(1L, ledger.Revision);

        True(ledger.Grant(2), "The actor should be grantable again.");
        True(!ledger.Synchronize(true, "Room A", 1, new[] { 1, 3 }),
            "Pruning a departed actor should not create a new room session.");
        True(!ledger.IsGranted(2), "A departed actor's grant must be pruned.");
        Equal(1L, ledger.Revision);

        True(ledger.Grant(3), "A remaining actor should be grantable.");
        True(ledger.Synchronize(true, "Room A", 4, new[] { 3, 4 }),
            "A master change should create a new session revision.");
        Equal(2L, ledger.Revision);
        True(!ledger.IsGranted(3), "A master change must clear all grants.");

        True(ledger.Grant(3), "An actor should be grantable under the new master.");
        True(ledger.Synchronize(true, "Room B", 4, new[] { 3, 4 }),
            "A room-name change should create a new session revision.");
        Equal(3L, ledger.Revision);
        True(!ledger.IsGranted(3), "A room change must clear all grants.");

        True(ledger.Grant(3), "A room-B actor should be grantable.");
        True(ledger.Synchronize(false, null, -1, null),
            "Leaving a room should create a new session revision.");
        Equal(4L, ledger.Revision);
        True(!ledger.IsGranted(3), "Leaving must clear all grants.");
        True(!ledger.Grant(3), "Actors cannot be granted while out of room.");
        True(!ledger.Synchronize(false, string.Empty, -1, new int[0]),
            "Repeated out-of-room synchronization should be stable.");
        Equal(4L, ledger.Revision);

        True(ledger.Synchronize(true, "Room B", 4, new[] { 2, 3, 5 }),
            "Rejoining the same-named room must still create a new session.");
        Equal(5L, ledger.Revision);
        True(ledger.Grant(5), "Actor five should be grantable after rejoin.");
        True(ledger.Grant(2), "Actor two should be grantable after rejoin.");
        IReadOnlyList<int> sorted = ledger.GetGrantedActors();
        Equal(2, sorted.Count);
        Equal(2, sorted[0]);
        Equal(5, sorted[1]);

        True(!ledger.Synchronize(true, "Room B", 4, null),
            "A null current-actor set should prune without changing session.");
        Equal(0, ledger.GetGrantedActors().Count);
        Equal(5L, ledger.Revision);
    }

    private static void RoleAwareCompletionCatalog()
    {
        var clientCatalog = new CompletionCatalog(
            new[] { "item:Gun", "enemy:Headman" },
            new[] { "Host#1", "Peer#3" },
            false);
        IReadOnlyList<CompletionItem> clientCommands =
            CommandCompletionEngine.GetCompletions("/", 1, clientCatalog, 20);
        ContainsValue(clientCommands, "/spawn",
            "Client command completion should retain spawn.");
        ContainsValue(clientCommands, "/despawn",
            "Client command completion should retain despawn.");
        ContainsValue(clientCommands, "/help",
            "Client command completion should retain public help.");
        DoesNotContainValue(clientCommands, "/grant",
            "Client command completion must suppress host-only grant.");
        DoesNotContainValue(clientCommands, "/revoke",
            "Client command completion must suppress host-only revoke.");

        IReadOnlyList<CompletionItem> fuzzyGrant =
            CommandCompletionEngine.GetCompletions("/gr", 3, clientCatalog, 20);
        DoesNotContainValue(fuzzyGrant, "/grant",
            "Fuzzy client completion must not recover host-only grant.");
        Equal(0, CommandCompletionEngine.GetCompletions(
            "/grant ",
            "/grant ".Length,
            clientCatalog,
            20).Count);
        Equal(0, CommandCompletionEngine.GetCompletions(
            "/revoke p",
            "/revoke p".Length,
            clientCatalog,
            20).Count);

        var hostCatalog = new CompletionCatalog(
            new[] { "item:Gun" },
            new[] { "Client#2", "NotGranted#3" },
            new[] { "Client#2" },
            true);
        IReadOnlyList<CompletionItem> hostCommands =
            CommandCompletionEngine.GetCompletions("/", 1, hostCatalog, 20);
        ContainsValue(hostCommands, "/grant",
            "Host command completion should retain grant.");
        ContainsValue(hostCommands, "/revoke",
            "Host command completion should retain revoke.");
        IReadOnlyList<CompletionItem> hostPlayers =
            CommandCompletionEngine.GetCompletions(
                "/grant c",
                "/grant c".Length,
                hostCatalog,
                20);
        ContainsValue(hostPlayers, "Client#2",
            "Host grant completion should retain player candidates.");
        IReadOnlyList<CompletionItem> ungrantedPlayers =
            CommandCompletionEngine.GetCompletions(
                "/grant ntg",
                "/grant ntg".Length,
                hostCatalog,
                20);
        ContainsValue(ungrantedPlayers, "NotGranted#3",
            "Grant completion should include eligible ungranted players.");
        IReadOnlyList<CompletionItem> revokePlayers =
            CommandCompletionEngine.GetCompletions(
                "/revoke ",
                "/revoke ".Length,
                hostCatalog,
                20);
        ContainsValue(revokePlayers, "Client#2",
            "Revoke completion should include granted players.");
        DoesNotContainValue(revokePlayers, "NotGranted#3",
            "Revoke completion must not include ungranted players.");
    }

    private static void NonHostFuzzyMutationWorkflows()
    {
        var clientCatalog = new CompletionCatalog(
            new[]
            {
                "item:Strength Upgrade",
                "valuable:Diamond Display",
                "enemy:Gnome"
            },
            new string[0],
            false);

        const string fuzzyCommand = "/spwan";
        IReadOnlyList<CompletionItem> commandMatches = Complete(
            fuzzyCommand,
            clientCatalog);
        HasAny(commandMatches, "A non-host should receive fuzzy command completion.");
        Equal("/spawn", commandMatches[0].Value);
        string spawn = CommandCompletionEngine.ApplyCompletion(
            fuzzyCommand,
            commandMatches[0],
            true).Text;

        spawn += "strenth";
        IReadOnlyList<CompletionItem> targetMatches = Complete(spawn, clientCatalog);
        HasAny(targetMatches, "A non-host should receive fuzzy target completion.");
        Equal("item:Strength Upgrade", targetMatches[0].Value);
        spawn = CommandCompletionEngine.ApplyCompletion(
            spawn,
            targetMatches[0],
            true).Text;

        spawn += "205x";
        IReadOnlyList<CompletionItem> countMatches = Complete(spawn, clientCatalog);
        HasAny(countMatches, "A non-host should receive fuzzy count completion.");
        Equal("205", countMatches[0].Value);
        spawn = CommandCompletionEngine.ApplyCompletion(
            spawn,
            countMatches[0],
            true).Text;

        spawn += "rncl";
        IReadOnlyList<CompletionItem> locationMatches = Complete(spawn, clientCatalog);
        HasAny(locationMatches, "A non-host should receive fuzzy location completion.");
        Equal(CommandLocations.RandomNonCollisionLocation, locationMatches[0].Value);
        spawn = CommandCompletionEngine.ApplyCompletion(spawn, locationMatches[0]).Text;

        CommandParseResult parsedSpawn = SlashCommandParser.Parse(spawn);
        True(parsedSpawn.Success, parsedSpawn.ErrorMessage);
        Equal("item:Strength Upgrade", parsedSpawn.Command.Target);
        Equal(205, parsedSpawn.Command.Count.Value);
        Equal(CommandLocations.RandomNonCollisionLocation, parsedSpawn.Command.Location);
        Equal(
            "item|Strength Upgrade|205|safe",
            CommandExecutionTranslation.TranslateSpawn(
                parsedSpawn.Command.TargetKind,
                parsedSpawn.Command.TargetName,
                parsedSpawn.Command.Count.Value,
                parsedSpawn.Command.Location));

        const string fuzzyDespawn = "/despwan gnom al";
        int commandCaret = fuzzyDespawn.IndexOf(' ');
        IReadOnlyList<CompletionItem> despawnCommandMatches =
            CommandCompletionEngine.GetCompletions(
                fuzzyDespawn,
                commandCaret,
                clientCatalog,
                10);
        HasAny(despawnCommandMatches,
            "A non-host should receive fuzzy despawn command completion.");
        Equal("/despawn", despawnCommandMatches[0].Value);
        string despawn = CommandCompletionEngine.ApplyCompletion(
            fuzzyDespawn,
            despawnCommandMatches[0]).Text;

        int targetCaret = despawn.IndexOf("gnom", StringComparison.Ordinal) + "gnom".Length;
        IReadOnlyList<CompletionItem> despawnTargetMatches =
            CommandCompletionEngine.GetCompletions(
                despawn,
                targetCaret,
                clientCatalog,
                10);
        HasAny(despawnTargetMatches,
            "A non-host should receive fuzzy despawn target completion.");
        Equal("enemy:Gnome", despawnTargetMatches[0].Value);
        despawn = CommandCompletionEngine.ApplyCompletion(
            despawn,
            despawnTargetMatches[0]).Text;

        IReadOnlyList<CompletionItem> allMatches = Complete(despawn, clientCatalog);
        HasAny(allMatches, "A non-host should receive fuzzy despawn-count completion.");
        Equal("all", allMatches[0].Value);
        despawn = CommandCompletionEngine.ApplyCompletion(despawn, allMatches[0]).Text;

        CommandParseResult parsedDespawn = SlashCommandParser.Parse(despawn);
        True(parsedDespawn.Success, parsedDespawn.ErrorMessage);
        Equal(CommandTargetKind.Enemy, parsedDespawn.Command.TargetKind);
        True(parsedDespawn.Command.IsAllCount,
            "The completed despawn count should preserve all semantics.");
        Equal(
            "despawnspawned|enemy|Gnome|-1",
            CommandExecutionTranslation.TranslateDespawn(
                parsedDespawn.Command.TargetKind,
                parsedDespawn.Command.TargetName,
                parsedDespawn.Command.Count));
    }

    private static void CanonicalParseToExecutionPipeline()
    {
        var spawnCases = new[]
        {
            new
            {
                Command = "/spawn \"item:Strength Upgrade\"",
                Expected = "item|Strength Upgrade|1|at-player"
            },
            new
            {
                Command = "/spawn \"valuable:Diamond Display\" 500 random-non-collision-location",
                Expected = "loot|Diamond Display|500|safe"
            },
            new
            {
                Command = "/spawn enemy:Gnome 2 player-location",
                Expected = "enemy|Gnome|2|at-player"
            }
        };

        foreach (var value in spawnCases)
        {
            CommandParseResult parsed = SlashCommandParser.Parse(value.Command);
            True(parsed.Success, parsed.ErrorMessage);
            Equal(
                value.Expected,
                CommandExecutionTranslation.TranslateSpawn(
                    parsed.Command.TargetKind,
                    parsed.Command.TargetName,
                    parsed.Command.Count.Value,
                    parsed.Command.Location));
        }

        var despawnCases = new[]
        {
            new
            {
                Command = "/despawn item:all",
                Expected = "despawnspawned|item|all|-1"
            },
            new
            {
                Command = "/despawn \"valuable:Diamond Display\" 500",
                Expected = "despawnspawned|valuable|Diamond Display|500"
            },
            new
            {
                Command = "/despawn enemy:Gnome all",
                Expected = "despawnspawned|enemy|Gnome|-1"
            }
        };

        foreach (var value in despawnCases)
        {
            CommandParseResult parsed = SlashCommandParser.Parse(value.Command);
            True(parsed.Success, parsed.ErrorMessage);
            Equal(
                value.Expected,
                CommandExecutionTranslation.TranslateDespawn(
                    parsed.Command.TargetKind,
                    parsed.Command.TargetName,
                    parsed.Command.Count));
        }
    }

    private static void RemoteMutationPermissionMatrix()
    {
        string[] mutations =
        {
            "/spawn item:Gun",
            "/spawn valuable:Diamond 2 random-non-collision-location",
            "/spawn enemy:Gnome 1 player-location",
            "/despawn item:all",
            "/despawn valuable:all 500",
            "/despawn enemy:Gnome all"
        };

        foreach (string command in mutations)
        {
            CommandRequestValidation granted =
                CommandNetworkPolicy.ValidateRemoteCommand(command, true);
            True(granted.Allowed,
                "A granted non-host mutation should be allowed: " + command);
            Equal<string>(null, granted.Error);

            ValidationDenied(
                CommandNetworkPolicy.ValidateRemoteCommand(command, false),
                "not granted");
        }
    }

    private static void SpawnExecutionTranslationMatrix()
    {
        var cases = new[]
        {
            new { Kind = CommandTargetKind.Item, Name = "Gun", Action = "item" },
            new { Kind = CommandTargetKind.Valuable, Name = "Diamond Display", Action = "loot" },
            new { Kind = CommandTargetKind.Enemy, Name = "Headman", Action = "enemy" }
        };

        foreach (var value in cases)
        {
            Equal(
                value.Action + "|" + value.Name + "|1|at-player",
                CommandExecutionTranslation.TranslateSpawn(
                    value.Kind,
                    value.Name,
                    1,
                    CommandLocations.PlayerLocation));
            Equal(
                value.Action + "|" + value.Name + "|500|safe",
                CommandExecutionTranslation.TranslateSpawn(
                    value.Kind,
                    value.Name,
                    500,
                    CommandLocations.RandomNonCollisionLocation));
        }

        CommandParseResult locationOnly = SlashCommandParser.Parse(
            "/spawn item:Gun random-non-collision-location");
        True(locationOnly.Success, locationOnly.ErrorMessage);
        Equal(
            "item|Gun|1|safe",
            CommandExecutionTranslation.TranslateSpawn(
                locationOnly.Command.TargetKind,
                locationOnly.Command.TargetName,
                locationOnly.Command.Count.Value,
                locationOnly.Command.Location));

        Throws<ArgumentOutOfRangeException>(() =>
            CommandExecutionTranslation.TranslateSpawn(
                CommandTargetKind.Unspecified,
                "Gun",
                1,
                CommandLocations.PlayerLocation),
            "Unspecified target kinds must not reach the executor.");
        Throws<ArgumentException>(() =>
            CommandExecutionTranslation.TranslateSpawn(
                CommandTargetKind.Item,
                " ",
                1,
                CommandLocations.PlayerLocation),
            "Blank target names must be rejected.");
        Throws<ArgumentException>(() =>
            CommandExecutionTranslation.TranslateSpawn(
                CommandTargetKind.Item,
                "Gun|status",
                1,
                CommandLocations.PlayerLocation),
            "Protocol delimiters must not enter translated target names.");
        Throws<ArgumentOutOfRangeException>(() =>
            CommandExecutionTranslation.TranslateSpawn(
                CommandTargetKind.Item,
                "Gun",
                0,
                CommandLocations.PlayerLocation),
            "Counts below one must be rejected by the translation boundary.");
        Throws<ArgumentOutOfRangeException>(() =>
            CommandExecutionTranslation.TranslateSpawn(
                CommandTargetKind.Enemy,
                "Headman",
                501,
                CommandLocations.PlayerLocation),
            "Counts above 500 must be rejected by the translation boundary.");
        Throws<ArgumentException>(() =>
            CommandExecutionTranslation.TranslateSpawn(
                CommandTargetKind.Item,
                "Gun",
                1,
                "near-player"),
            "Only public spawn locations may cross the translation boundary.");
    }

    private static void DespawnExecutionTranslationMatrix()
    {
        Equal(
            "despawnspawned|item|Gun|-1",
            CommandExecutionTranslation.TranslateDespawn(
                CommandTargetKind.Item,
                "Gun",
                null));
        Equal(
            "despawnspawned|valuable|Diamond Display|1",
            CommandExecutionTranslation.TranslateDespawn(
                CommandTargetKind.Valuable,
                "Diamond Display",
                1));
        Equal(
            "despawnspawned|enemy|all|500",
            CommandExecutionTranslation.TranslateDespawn(
                CommandTargetKind.Enemy,
                "all",
                500));
        Throws<ArgumentOutOfRangeException>(() =>
            CommandExecutionTranslation.TranslateDespawn(
                CommandTargetKind.Unspecified,
                "Gun",
                null),
            "Unspecified despawn target kinds must be rejected.");
        Throws<ArgumentOutOfRangeException>(() =>
            CommandExecutionTranslation.TranslateDespawn(
                CommandTargetKind.Item,
                "Gun",
                0),
            "Despawn counts below one must be rejected.");
        Throws<ArgumentException>(() =>
            CommandExecutionTranslation.TranslateDespawn(
                CommandTargetKind.Item,
                "Gun\nstatus",
                null),
            "Line breaks must not enter translated target names.");
    }

    private static void GroupedEnemyAcceptancePolicy()
    {
        Equal(1, CommandExecutionTranslation.AcceptedEnemyCountForSetup(5, 3, true));
        Equal(1, CommandExecutionTranslation.AcceptedEnemyCountForSetup(1, 3, true));
        Equal(3, CommandExecutionTranslation.AcceptedEnemyCountForSetup(5, 3, false));
        Equal(2, CommandExecutionTranslation.AcceptedEnemyCountForSetup(2, 3, false));
        Throws<ArgumentOutOfRangeException>(() =>
            CommandExecutionTranslation.AcceptedEnemyCountForSetup(0, 3, true),
            "Enemy setup policy requires remaining demand.");
        Throws<ArgumentOutOfRangeException>(() =>
            CommandExecutionTranslation.AcceptedEnemyCountForSetup(1, 0, true),
            "Enemy setup policy requires at least one live spawn.");
    }

    private static void EnemyClearanceFloorPolicy()
    {
        Equal(0.15f, EnemyClearancePolicy.MinimumProbeBottomOffset);
        Equal(
            EnemyClearancePolicy.MinimumProbeBottomOffset,
            EnemyClearancePolicy.ClampProbeBottomOffset(-4f));
        Equal(
            EnemyClearancePolicy.MinimumProbeBottomOffset,
            EnemyClearancePolicy.ClampProbeBottomOffset(0f));
        Equal(
            EnemyClearancePolicy.MinimumProbeBottomOffset,
            EnemyClearancePolicy.ClampProbeBottomOffset(
                EnemyClearancePolicy.MinimumProbeBottomOffset));
        Equal(0.75f, EnemyClearancePolicy.ClampProbeBottomOffset(0.75f));

        Throws<ArgumentOutOfRangeException>(() =>
            EnemyClearancePolicy.ClampProbeBottomOffset(float.NaN),
            "A NaN clearance bound must be rejected.");
        Throws<ArgumentOutOfRangeException>(() =>
            EnemyClearancePolicy.ClampProbeBottomOffset(float.PositiveInfinity),
            "A positive-infinite clearance bound must be rejected.");
        Throws<ArgumentOutOfRangeException>(() =>
            EnemyClearancePolicy.ClampProbeBottomOffset(float.NegativeInfinity),
            "A negative-infinite clearance bound must be rejected.");

        var layers = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Default", 0 },
            { "StaticGrabObject", 8 },
            { "Enemy", 9 },
            { "Player", 10 },
            { "PhysGrabObject", 16 },
            { "PhysGrabObjectCart", 17 },
            { "PhysGrabObjectHinge", 18 },
            // These infrastructure layers must not be queried as obstacles.
            { "RoomVolume", 14 },
            { "NavmeshOnly", 19 }
        };
        int mask = EnemyClearancePolicy.BuildGameplaySolidMask(name =>
        {
            int layer;
            return layers.TryGetValue(name, out layer) ? layer : -1;
        });
        int expectedMask = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 10) |
            (1 << 16) | (1 << 17) | (1 << 18);
        Equal(expectedMask, mask);
        True((mask & (1 << 14)) == 0, "RoomVolume must not block a safe placement.");
        True((mask & (1 << 19)) == 0, "NavmeshOnly must not block a safe placement.");
        Throws<ArgumentNullException>(() =>
            EnemyClearancePolicy.BuildGameplaySolidMask(null),
            "A missing layer lookup must be rejected.");

        True(EnemyClearancePolicy.IsBodyGeometryEligible(true, false, true, false),
            "An enabled solid in the active prefab hierarchy is body geometry.");
        True(EnemyClearancePolicy.IsBodyGeometryEligible(true, false, false, true),
            "An inactive solid attached to a Rigidbody remains body geometry.");
        True(!EnemyClearancePolicy.IsBodyGeometryEligible(false, false, true, true),
            "A disabled collider must not inflate the body envelope.");
        True(!EnemyClearancePolicy.IsBodyGeometryEligible(true, true, true, true),
            "A trigger volume must not inflate the body envelope.");
        True(!EnemyClearancePolicy.IsBodyGeometryEligible(true, false, false, false),
            "An inactive unowned helper must not inflate the body envelope.");

        True(EnemyClearancePolicy.IsNavigationEnvelopeUsable(0.5f, 2f, 0f, 1f, 1f),
            "A finite positive NavMeshAgent envelope should be usable.");
        True(EnemyClearancePolicy.IsNavigationEnvelopeUsable(1.5f, 4f, -0.25f, 2f, 0.5f),
            "Finite offsets and non-uniform positive scales should be supported.");
        True(!EnemyClearancePolicy.IsNavigationEnvelopeUsable(0f, 2f, 0f, 1f, 1f),
            "A zero navigation radius must fall back to body geometry.");
        True(!EnemyClearancePolicy.IsNavigationEnvelopeUsable(0.5f, 0f, 0f, 1f, 1f),
            "A zero navigation height must fall back to body geometry.");
        True(!EnemyClearancePolicy.IsNavigationEnvelopeUsable(
                float.NaN, 2f, 0f, 1f, 1f),
            "A non-finite navigation radius must be rejected.");
        True(!EnemyClearancePolicy.IsNavigationEnvelopeUsable(
                0.5f, 2f, float.PositiveInfinity, 1f, 1f),
            "A non-finite navigation offset must be rejected.");
        True(!EnemyClearancePolicy.IsNavigationEnvelopeUsable(
                0.5f, 2f, 0f, 0f, 1f),
            "A zero horizontal scale must be rejected.");
        True(!EnemyClearancePolicy.IsNavigationEnvelopeUsable(
                0.5f, 2f, 0f, 1f, float.NegativeInfinity),
            "A non-finite vertical scale must be rejected.");
    }

    private static void BoundedSpawnNameSummary()
    {
        var repeated = new SpawnNameSummary();
        repeated.Add("Gnome", 2);
        repeated.Add("gnome", 3);
        repeated.Add("", 5);
        repeated.Add("Ignored", 0);
        Equal("Gnome x5", repeated.Format());

        var many = new SpawnNameSummary();
        for (int index = 1; index <= 10; index++)
            many.Add("Enemy" + index, index);

        string formatted = many.Format();
        True(formatted.IndexOf("Enemy1", StringComparison.Ordinal) >= 0,
            "The first distinct name should be retained.");
        True(formatted.IndexOf("Enemy8 x8", StringComparison.Ordinal) >= 0,
            "The eighth distinct name should be retained with its count.");
        True(formatted.IndexOf("Enemy9", StringComparison.Ordinal) < 0,
            "Names beyond the display bound must be omitted.");
        True(formatted.EndsWith("+2 more name(s)", StringComparison.Ordinal),
            "The summary must report how many distinct names were omitted.");
    }

    private static void IngressSessionPolicy()
    {
        Equal<string>(null, CommandIngressSessionPolicy.Validate(false, null, null));
        Equal<string>(null, CommandIngressSessionPolicy.Validate(false, 7, 7));
        True(CommandIngressSessionPolicy.Validate(true, 7, 7).IndexOf(
                "cancelled",
                StringComparison.OrdinalIgnoreCase) >= 0,
            "Caller cancellation must invalidate queued and active work.");
        True(CommandIngressSessionPolicy.Validate(false, 7, 8).IndexOf(
                "expired",
                StringComparison.OrdinalIgnoreCase) >= 0,
            "A different session revision must invalidate queued work.");
        True(CommandIngressSessionPolicy.Validate(false, 7, null).IndexOf(
                "expired",
                StringComparison.OrdinalIgnoreCase) >= 0,
            "Losing the permission runtime must invalidate bound work.");
    }

    private static void ConsoleToggleInputFallback()
    {
        var gate = new ConsoleInputGate();
        True(gate.TryAccept(ConsoleInputAction.Toggle, 100, true, false, false),
            "The legacy input edge should toggle the console.");
        True(!gate.TryAccept(ConsoleInputAction.Toggle, 100, false, true, false),
            "A second input backend in the same frame must not toggle twice.");
        True(!gate.TryAccept(ConsoleInputAction.Toggle, 101, false, false, false),
            "A frame without an input edge must not toggle the console.");
        True(gate.TryAccept(ConsoleInputAction.Toggle, 102, false, true, false),
            "The Input System edge must toggle when focused IMGUI hides the legacy edge.");
        True(gate.TryAccept(ConsoleInputAction.Toggle, 103, false, false, true),
            "The IMGUI event remains a supported fallback.");
        True(gate.TryAccept(ConsoleInputAction.Submit, 103, false, true, false),
            "Distinct console actions may be accepted in the same frame.");
        True(!gate.TryAccept(ConsoleInputAction.Submit, 103, false, false, true),
            "The same action must not be accepted twice in one frame.");
        True(gate.TryAccept(ConsoleInputAction.AcceptCompletion, 104, false, true, false),
            "Tab must be accepted through the Input System fallback.");
        True(gate.TryAccept(ConsoleInputAction.SelectPrevious, 105, false, true, false),
            "Up-arrow selection must be accepted through the Input System fallback.");
        True(gate.TryAccept(ConsoleInputAction.SelectNext, 106, false, true, false),
            "Down-arrow selection must be accepted through the Input System fallback.");
        True(gate.TryAccept(ConsoleInputAction.Close, 107, false, true, false),
            "Escape must be accepted through the Input System fallback.");

        Equal("F2", ConsoleToggleKeyMapping.ToInputSystemKeyName("F2"));
        Equal("Digit7", ConsoleToggleKeyMapping.ToInputSystemKeyName("Alpha7"));
        Equal("NumpadEnter", ConsoleToggleKeyMapping.ToInputSystemKeyName("KeypadEnter"));
        Equal("Enter", ConsoleToggleKeyMapping.ToInputSystemKeyName("Return"));
        Equal("LeftCtrl", ConsoleToggleKeyMapping.ToInputSystemKeyName("LeftControl"));
        Equal("RightMeta", ConsoleToggleKeyMapping.ToInputSystemKeyName("RightWindows"));
        Equal("PrintScreen", ConsoleToggleKeyMapping.ToInputSystemKeyName("SysReq"));
        Equal(string.Empty, ConsoleToggleKeyMapping.ToInputSystemKeyName(null));
    }

    private static void NetworkSessionSceneActivation()
    {
        True(!NetworkSessionSceneActivationPolicy.ShouldActivate(
                false, false, false, false, false, false),
            "Networking must remain inactive before RunManager exists.");
        True(!NetworkSessionSceneActivationPolicy.ShouldActivate(
                true, false, false, false, false, false),
            "Networking must remain inactive before the current level exists.");
        True(!NetworkSessionSceneActivationPolicy.ShouldActivate(
                true, true, false, false, false, false),
            "Menu and region-selection levels must not activate Photon callbacks.");
        True(NetworkSessionSceneActivationPolicy.ShouldActivate(
                true, true, true, false, false, false),
            "The multiplayer lobby must activate Photon callbacks.");
        True(NetworkSessionSceneActivationPolicy.ShouldActivate(
                true, true, false, true, false, false),
            "A gameplay level must activate Photon callbacks.");
        True(NetworkSessionSceneActivationPolicy.ShouldActivate(
                true, true, false, false, true, false),
            "A shop level must activate Photon callbacks.");
        True(NetworkSessionSceneActivationPolicy.ShouldActivate(
                true, true, false, false, false, true),
            "An arena level must activate Photon callbacks.");
        True(!NetworkSessionSceneActivationPolicy.ShouldActivate(
                false, true, true, true, true, true),
            "Scene matches must not activate networking without RunManager.");
        True(!NetworkSessionSceneActivationPolicy.ShouldActivate(
                true, false, true, true, true, true),
            "Scene matches must not activate networking without a current level.");
    }

    private static IReadOnlyList<CompletionItem> Complete(
        string input,
        CompletionCatalog catalog)
    {
        return CommandCompletionEngine.GetCompletions(input, input.Length, catalog, 10);
    }

    private static void EnvelopeFails(object[] values, string message)
    {
        string kind;
        string requestId;
        string payload;
        True(!CommandNetworkPolicy.TryReadEnvelope(
                values,
                out kind,
                out requestId,
                out payload),
            message);
    }

    private static void ValidationDenied(
        CommandRequestValidation validation,
        string expectedErrorFragment)
    {
        True(validation != null, "Validation should always produce a result.");
        True(!validation.Allowed, "The remote command should be denied.");
        True(!string.IsNullOrWhiteSpace(validation.Error),
            "A denied command should include an error.");
        True(validation.Error.IndexOf(
                expectedErrorFragment,
                StringComparison.OrdinalIgnoreCase) >= 0,
            "Expected validation error to contain <" + expectedErrorFragment +
            "> but got <" + validation.Error + ">.");
    }

    private static void SingleFailure(
        IReadOnlyList<PendingCommandFailure> failures,
        string expectedRequestId,
        string expectedErrorFragment)
    {
        Equal(1, failures.Count);
        Equal(expectedRequestId, failures[0].RequestId);
        True(failures[0].Error.IndexOf(
                expectedErrorFragment,
                StringComparison.OrdinalIgnoreCase) >= 0,
            "Expected pending failure to contain <" + expectedErrorFragment +
            "> but got <" + failures[0].Error + ">.");
    }

    private static void ContainsValue(
        IReadOnlyList<CompletionItem> values,
        string expected,
        string message)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index].Value, expected, StringComparison.Ordinal))
                return;
        }
        throw new InvalidOperationException(message + " Missing <" + expected + ">.");
    }

    private static void DoesNotContainValue(
        IReadOnlyList<CompletionItem> values,
        string unexpected,
        string message)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index].Value, unexpected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    message + " Unexpected <" + unexpected + ">.");
            }
        }
    }

    private static void Greater(int left, int right, string message)
    {
        if (left <= right)
        {
            throw new InvalidOperationException(
                message + " Expected <" + left + "> to be greater than <" + right + ">.");
        }
    }

    private static void Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                message + " Expected " + typeof(TException).Name +
                " but got " + exception.GetType().Name + ".");
        }

        throw new InvalidOperationException(
            message + " Expected " + typeof(TException).Name + ".");
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
