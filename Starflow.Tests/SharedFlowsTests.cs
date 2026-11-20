using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Starflow.Tests;

public class SharedFlowsTests
{
    [Fact]
    public void ParseDropsUnsafeFileNames()
    {
        const string json = """
        [ { "File": "b.json", "Name": "Bee" }, { "File": "a.json" }, { "File": "../x.json" }, { "File": "sub/y.json" }, { "Name": "no file" } ]
        """;
        var list = SharedIndex.Parse(json);
        Assert.Equal(["a.json", "Bee"], list.Select(e => e.Name));
        Assert.EndsWith("/flows/b.json", list[1].Url);
    }

    // Every file the index lists is in the folder and reads as flows. Runs in CI on each push.
    [Fact]
    public void RepoIndexMatchesFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "flows"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var folder = Path.Combine(dir!.FullName, "flows");
        var entries = SharedIndex.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
        {
            var path = Path.Combine(folder, entry.File);
            Assert.True(File.Exists(path), entry.File);
            Assert.True(Share.TryRead(File.ReadAllText(path), out var flows, out var error), entry.File + ": " + error);
            Assert.All(flows, f => Assert.Empty(Catalog.Missing(f)));
        }
    }
}

public class CardOffTests
{
    private static Card C(Section s, string type, string value, bool off = false) => new() { Section = s, Type = type, Value = value, Off = off };

    [Fact]
    public void OffCardsAreSkipped()
    {
        var world = new World { LoggedIn = true, Places = new(StringComparer.OrdinalIgnoreCase) { "Inn" } };
        var flow = new FlowItem { Cards = [C(Section.When, CardType.Place, "Inn"), C(Section.When, CardType.Place, "Town", off: true)] };
        Assert.True(Eval.SectionMatches(flow, Section.When, world));
        Assert.Single(flow.Active(Section.When));
        Assert.Equal(2, flow.In(Section.When).Count);

        flow.Cards[0].Off = true;
        Assert.False(Eval.SectionMatches(flow, Section.When, world));
        Assert.Contains(Eval.Explain(flow, world), l => l.Contains("only cards that are off"));
    }

    [Fact]
    public void OffCardsDoNotBlockOrFlagReactive()
    {
        var flow = new FlowItem
        {
            Cards = [C(Section.When, CardType.Place, "Inn"), C(Section.When, CardType.Chat, "hi", off: true), C(Section.Do, CardType.Command, "", off: true)],
        };
        Assert.Empty(Catalog.Missing(flow));
        Assert.False(Catalog.IsReactive(flow));
        Assert.True(Share.TryRead(Share.One(flow), out var back, out _));
        Assert.True(back[0].Cards[1].Off);
    }
}

public class CardNoteTests
{
    [Fact]
    public void NoteReplacesTitleAndRoundTrips()
    {
        var card = new Card { Section = Section.When, Type = CardType.Place, Value = "Inn" };
        Assert.Equal(card.Label, card.Title);
        Assert.Equal(card.Label, card.Described);
        card.Note = "  Resting  ";
        Assert.Equal("Resting", card.Title);
        Assert.Equal(card.Label + " (Resting)", card.Described);
        var flow = new FlowItem { Cards = [card] };
        Assert.Contains(Eval.Explain(flow, new World { LoggedIn = true }), l => l.Contains("(Resting)"));
        Assert.True(Share.TryRead(Share.One(flow), out var back, out _));
        Assert.Equal("  Resting  ", back[0].Cards[0].Note);
    }
}

public class ShareCodeTests
{
    [Fact]
    public void CodeRoundTripsAndBlanksWebhooks()
    {
        var flow = new FlowItem
        {
            Name = "Code test",
            Cards =
            [
                new Card { Section = Section.When, Type = CardType.Place, Value = "Inn", Note = "Resting", Off = true },
                new Card { Section = Section.Do, Type = CardType.Collection, Value = "Combat", Flag = false },
                new Card { Section = Section.Do, Type = CardType.Webhook, Value = "https://discord.com/api/webhooks/1/secret" },
            ],
        };
        var code = Share.Code([flow], out var blanked);
        Assert.StartsWith(Share.CodePrefix, code);
        Assert.DoesNotContain('\n', code);
        Assert.Equal(1, blanked);
        Assert.Equal("https://discord.com/api/webhooks/1/secret", flow.Cards[2].Value);

        Assert.True(Share.TryRead(code, out var back, out var error), error);
        var cards = Assert.Single(back).Cards;
        Assert.Equal(("Resting", true), (cards[0].Note, cards[0].Off));
        Assert.False(cards[1].Flag);
        Assert.Equal(string.Empty, cards[2].Value);
        Assert.True(code.Length < Share.One(flow).Length);
    }

    [Fact]
    public void SeveralFlowsAndDamagedCodes()
    {
        var code = Share.Code([new FlowItem { Name = "A" }, new FlowItem { Name = "B" }], out _);
        Assert.True(Share.TryRead("  " + code.ToLowerInvariant()[..4].ToUpperInvariant() + code[4..] + "  ", out var back, out _));
        Assert.Equal(["A", "B"], back.Select(f => f.Name));
        Assert.False(Share.TryRead(code[..(code.Length / 2)], out _, out var error));
        Assert.Contains("cut short", error);
    }

    [Fact]
    public void SoundCardNeedsANumber()
    {
        var card = Catalog.Make(Section.Do, CardType.Sound);
        Assert.Null(Catalog.Missing(card));
        Assert.Equal("Sound <se.1>", card.Label);
        card.Number = 0;
        Assert.NotNull(Catalog.Missing(card));
    }
}
