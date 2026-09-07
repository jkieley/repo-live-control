using System;
using System.Collections.Generic;
using System.Linq;
using RepoLiveControl.Commands;

internal static partial class Program
{
    private static CompletionCatalog BrowserCatalog()
    {
        return new CompletionCatalog(
            Enumerable.Range(0, 1200).Select(index => "item:Test Item " + index.ToString("D4"))
                .Concat(new[] { "item:all", "valuable:all", "enemy:all", "valuable:Diamond Display", "enemy:Headman" }),
            new string[0]);
    }

    private static void SpawnBrowserIncludesFullCatalog()
    {
        CompletionCatalog catalog = BrowserCatalog();
        foreach (string input in new[] { "/spawn", "/spawn ", "  /SPAWN", "/spawn  " })
        {
            var matches = CommandCompletionEngine.GetCompletions(input, input.Length, catalog);
            Equal(1202, matches.Count);
            Equal("item:Test Item 0000", matches[0].Value);
            ContainsValue(matches, "item:Test Item 1199", "Items beyond the former 512-result cap must remain browsable");
            ContainsValue(matches, "valuable:Diamond Display", "Valuables remain in the full spawn catalog");
            ContainsValue(matches, "enemy:Headman", "Enemies remain in the full spawn catalog");
            True(matches.All(match => match.ArgumentIndex == 1 && !match.Value.EndsWith(":all")),
                "The browser exposes spawnable targets, never despawn-only all selectors");
            CompletionApplication accepted = CommandCompletionEngine.ApplyCompletion(input, matches[1199], true);
            var parsed = SlashCommandParser.Parse(accepted.Text);
            True(parsed.Success, accepted.Text);
            Equal("item:Test Item 1199", parsed.Command.Target);
            Equal(1, parsed.Command.Count.Value);
            Equal(accepted.Text.Length, accepted.CaretPosition);
        }
        Equal(7, CommandCompletionEngine.GetCompletions("/spawn ", 7, catalog, 7).Count);
        var partialVerb = CommandCompletionEngine.GetCompletions("/spa", 4, catalog);
        Equal("/spawn", partialVerb[0].Value);
        Equal(0, partialVerb[0].ArgumentIndex);
        var commandAtStart = CommandCompletionEngine.GetCompletions("/spawn", 0, catalog);
        Equal(0, commandAtStart[0].ArgumentIndex);
    }

    private static void SpawnBrowserFuzzySelection()
    {
        const string input = "/spawn itm1199";
        var matches = CommandCompletionEngine.GetCompletions(input, input.Length, BrowserCatalog());
        ContainsValue(matches, "item:Test Item 1199", "Fuzzy search finds entries anywhere in the catalog");
        var match = matches.First(item => item.Value == "item:Test Item 1199");
        string selected = CommandCompletionEngine.ApplyCompletion(input, match, true).Text;
        var counts = CommandCompletionEngine.GetCompletions(selected, selected.Length, BrowserCatalog());
        Equal(502, counts.Count);
        ContainsValue(counts, "500", "Every valid count remains reachable after target selection");
        string command = CommandCompletionEngine.ApplyCompletion(selected, counts.First(item => item.Value == "5"), true).Text;
        var parsed = SlashCommandParser.Parse(command);
        True(parsed.Success, command);
        Equal("item:Test Item 1199", parsed.Command.Target);
        Equal(5, parsed.Command.Count.Value);
        Equal(0, CommandCompletionEngine.GetCompletions("/spawn zzzzzzzzzzz", 18, BrowserCatalog()).Count);
    }

    private static void CompletionViewportNavigation()
    {
        const int count = 1202;
        const float row = 28f, height = 8f * row;
        float scroll = 0f;
        int selected = 0;
        for (int index = 1; index < count; index++)
        {
            selected = CompletionListNavigation.MoveSelection(selected, 1, count);
            scroll = CompletionListNavigation.RevealSelection(scroll, selected, count, row, height);
            True(selected >= CompletionListNavigation.FirstVisible(scroll, count, row, height) &&
                selected < CompletionListNavigation.EndVisible(scroll, count, row, height),
                "Every keyboard-selected item must be visible, including beyond the old caps");
        }
        Equal(count - 1, selected);
        Equal(count, CompletionListNavigation.EndVisible(scroll, count, row, height));
        Equal(count - 8, CompletionListNavigation.FirstVisible(scroll, count, row, height));
        selected = CompletionListNavigation.MoveSelection(selected, 1, count);
        Equal(0, selected);
        Equal(0f, CompletionListNavigation.RevealSelection(scroll, selected, count, row, height));
        Equal(count - 1, CompletionListNavigation.MoveSelection(0, -1, count));

        // Mouse-wheel / scrollbar movement may browse away from keyboard selection.
        float wheelScroll = CompletionListNavigation.ClampScroll(700f * row, count, row, height);
        Equal(700, CompletionListNavigation.FirstVisible(wheelScroll, count, row, height));
        Equal(wheelScroll, CompletionListNavigation.ClampScroll(wheelScroll, count, row, height));
        Equal(0, CompletionListNavigation.MoveSelection(0, 1, 0));
    }

    private static void CompletionTargetAliases()
    {
        var aliases = new Dictionary<string, string[]>
        {
            { "item:Gun", new[] { "item:Pistol", "item:pistol" } },
            { "item:Defibro", new[] { "item:Defib", "item:Defibrillator" } },
            { "item:Phase Bridge", new[] { "item:Light Bridge" } },
            { "item:Missing", new[] { "item:Nonexistent" } }
        };
        var catalog = new CompletionCatalog(
            new[] { "item:Gun", "item:Defibro", "item:Phase Bridge" },
            new string[0], new string[0], new string[0], false, aliases);
        // The catalog takes a snapshot; later alias edits must not change this UI refresh.
        aliases["item:Gun"][0] = "item:Changed";
        Equal(3, CommandCompletionEngine.GetCompletions("/spawn", 6, catalog).Count);
        foreach (string verb in new[] { "/spawn", "/despawn" })
        {
            string input = verb + " Pistol";
            var matches = CommandCompletionEngine.GetCompletions(input, input.Length, catalog);
            Equal(1, matches.Count);
            Equal("item:Gun", matches[0].Value);
            var parsed = SlashCommandParser.Parse(CommandCompletionEngine.ApplyCompletion(input, matches[0]).Text);
            True(parsed.Success, "An alias must insert a canonical, executable selector");
            Equal("item:Gun", parsed.Command.Target);
        }
        foreach (var pair in new[] { new[] { "defibrilator", "item:Defibro" }, new[] { "lightbridge", "item:Phase Bridge" } })
        {
            string input = "/spawn " + pair[0];
            var matches = CommandCompletionEngine.GetCompletions(input, input.Length, catalog);
            ContainsValue(matches, pair[1], "Fuzzy matching must also search guide-name aliases");
            Equal(matches.Count, matches.Select(match => match.Value).Distinct().Count());
        }
        const string missing = "/spawn Nonexistent";
        Equal(0, CommandCompletionEngine.GetCompletions(missing, missing.Length, catalog).Count);
    }

    private static void CompletionViewportFiltering()
    {
        const float row = 28f, height = 224f;
        Equal(0f, CompletionListNavigation.ClampScroll(28000f, 3, row, height));
        Equal(3, CompletionListNavigation.EndVisible(28000f, 3, row, height));
        Equal(0, CompletionListNavigation.FirstVisible(100f, 0, row, height));
        Equal(0, CompletionListNavigation.EndVisible(100f, 0, row, height));
        Equal(10, CompletionListNavigation.FirstVisible(10.5f * row, 1000, row, height));
        Equal(19, CompletionListNavigation.EndVisible(10.5f * row, 1000, row, height));
        Equal(0f, CompletionListNavigation.ClampScroll(float.NaN, 1000, row, height));
        Equal(0f, CompletionListNavigation.ClampScroll(-1f, 1000, row, height));
        Throws<ArgumentOutOfRangeException>(() => CompletionListNavigation.ClampScroll(0f, 10, 0f, height),
            "Invalid geometry must not divide by zero");
    }
}
