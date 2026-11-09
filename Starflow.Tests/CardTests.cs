using System;
using System.Collections.Generic;
using System.Text.Json;
using Starflow;
using Xunit;

namespace Starflow.Tests;

public class CardTests
{
    private static World Here() => new()
    {
        LoggedIn = true,
        Now = new DateTime(2026, 11, 3, 20, 0, 0),
        TerritoryId = 129,
        TerritoryName = "Limsa Lominsa Lower Decks",
        RegionName = "La Noscea",
        WorldName = "Alpha",
        HomeWorldName = "Alpha",
        JobAbbr = "WHM",
        JobName = "White Mage",
        CharacterName = "Some \"Quoted\" Name",
        Role = "Healer",
        PartySize = 3,
        Companions = 2,
        Weather = "Fair Skies",
        Target = "player",
        TargetName = "Rhea Sun",
        NearbyNames = ["Alpha One", "Beta Two"],
    };

    private static Card When(string type, string value, string option = "", string extra = "") =>
        new() { Section = Section.When, Type = type, Value = value, Option = option, Extra = extra };

    [Theory]
    [InlineData(CardType.Zone, "/^limsa/", true)]
    [InlineData(CardType.Zone, "re:Lower Decks$", true)]
    [InlineData(CardType.Zone, "/^Gridania/", false)]
    [InlineData(CardType.Zone, "/(Gridania|Limsa)/", true)]
    [InlineData(CardType.Region, "re:^la noscea$", true)]
    [InlineData(CardType.Weather, "/skies$/", true)]
    [InlineData(CardType.Weather, "/rain/", false)]
    [InlineData(CardType.NearbyName, "/^beta/", true)]
    [InlineData(CardType.NearbyName, "/^gamma/", false)]
    [InlineData(CardType.Zone, "/[unclosed/", false)]
    public void RegexMatches(string type, string value, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(When(type, value), Here()));

    [Fact]
    public void RegexErrorsAreReported()
    {
        Assert.Null(Match.RegexError("Limsa | Gridania"));
        Assert.Null(Match.RegexError("/^Lim/"));
        Assert.NotNull(Match.RegexError("/[unclosed/"));
        Assert.NotNull(Match.RegexError("re:"));
    }

    [Theory]
    [InlineData("WHM", true)]
    [InlineData("SCH|WHM", true)]
    [InlineData("white mage", true)]
    [InlineData("SCH|AST", false)]
    [InlineData("WH", false)]
    [InlineData("/^W/", true)]
    public void Jobs(string value, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(When(CardType.Job, value), Here()));

    [Theory]
    [InlineData("Healer", true)]
    [InlineData("Tank|Healer", true)]
    [InlineData("Tank|DPS", false)]
    public void Roles(string value, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(When(CardType.Role, value), Here()));

    [Fact]
    public void PartyCountsPetsOnlyWhenAsked()
    {
        Assert.True(Eval.CardMatches(When(CardType.Party, "=3"), Here()));
        Assert.False(Eval.CardMatches(When(CardType.Party, "=5"), Here()));
        Assert.True(Eval.CardMatches(When(CardType.Party, "=5", "pets"), Here()));
    }

    [Theory]
    [InlineData("player", "", true)]
    [InlineData("player", "Rhea Sun", true)]
    [InlineData("player", "/^rhea/", true)]
    [InlineData("player", "Someone Else", false)]
    [InlineData("npc", "Rhea Sun", false)]
    public void TargetName(string kind, string name, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(When(CardType.Target, kind, extra: name), Here()));

    [Fact]
    public void ForecastRollIsStableAndInRange()
    {
        var seen = new HashSet<int>();
        for (long t = 1_700_000_000; t < 1_700_000_000 + Forecast.WindowSeconds * 200; t += Forecast.WindowSeconds)
        {
            var chance = Forecast.Chance(t);
            Assert.InRange(chance, 0, 99);
            seen.Add(chance);
            // The roll belongs to the window, not the second.
            Assert.Equal(chance, Forecast.Chance(Forecast.WindowStart(t) + 7));
        }
        Assert.True(seen.Count > 20);
    }

    [Fact]
    public void ForecastPicksCumulativeSlot()
    {
        byte[] rates = [20, 50, 100];
        Assert.Equal(0, Forecast.Pick(0, rates));
        Assert.Equal(0, Forecast.Pick(19, rates));
        Assert.Equal(1, Forecast.Pick(20, rates));
        Assert.Equal(2, Forecast.Pick(99, rates));
    }

    [Fact]
    public void WebhookBodyStaysValidJson()
    {
        var body = WebhookText.FillBody(WebhookText.DiscordTemplate, Here(), true);
        using var doc = JsonDocument.Parse(body);
        Assert.Contains("Some \"Quoted\" Name", doc.RootElement.GetProperty("content").GetString());
        Assert.Null(WebhookText.JsonError(body, "application/json"));
        Assert.NotNull(WebhookText.JsonError("{ nope", "application/json"));
        Assert.Null(WebhookText.JsonError("plain text", "text/plain"));
    }

    [Theory]
    [InlineData("https://discord.com/api/webhooks/1/x", true)]
    [InlineData("http://localhost:8080/hook", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("not a url", false)]
    public void WebhookUrls(string url, bool ok) => Assert.Equal(ok, WebhookText.ValidUrl(url, out _));

    [Fact]
    public void NewTokensFill()
    {
        Assert.Equal("White Mage in La Noscea, 3 in party", Tokens.Fill("{jobname} in {region}, {party} in party", Here()));
        Assert.Equal("2026-11-03 Tuesday", Tokens.Fill("{date} {day}", Here()));
        Assert.Equal("Alpha", Tokens.Fill("{homeworld}", Here()));
    }

    [Fact]
    public void AddListsAreSortedAndShared()
    {
        Assert.Equal(Catalog.For(Section.When), Catalog.For(Section.Until));
        Assert.Equal(Catalog.For(Section.Do), Catalog.For(Section.After));
        var labels = new List<string>();
        foreach (var d in Catalog.For(Section.When)) labels.Add(d.Label);
        var sorted = new List<string>(labels);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(sorted, labels);
        Assert.DoesNotContain(Catalog.For(Section.When), d => d.Type == CardType.Party);
    }
}
