using System;
using System.Collections.Generic;
using Xunit;

namespace Starflow.Tests;

public class EvalTests
{
    private static World Town() => new()
    {
        LoggedIn = true,
        Now = new DateTime(2026, 10, 24, 21, 30, 0),
        LocalHour = 21,
        LocalMinute = 30,
        EorzeaHour = 3,
        TerritoryId = 129,
        TerritoryName = "Limsa Lominsa Lower Decks",
        RegionName = "La Noscea",
        WorldName = "Alpha",
        HomeWorldName = "Beta",
        JobAbbr = "WHM",
        Role = "Healer",
        Level = 100,
        PartySize = 4,
        Weather = "Fair Skies",
        WeatherId = 2,
        OnlineStatus = "Away from Keyboard",
        NearbyNames = ["Some Player", "Other Person"],
        Nearby = 2,
        States = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Town", "Mounted" },
        Places = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Town", "Sanctuary" },
        Mount = "Company Chocobo",
        Housing = Address.None,
    };

    private static Card When(string type, string value, bool not = false) =>
        new() { Section = Section.When, Type = type, Value = value, Not = not };

    [Theory]
    [InlineData(CardType.Weather, "Fair Skies", true)]
    [InlineData(CardType.Weather, "Rain|2", true)]
    [InlineData(CardType.Weather, "Rain", false)]
    [InlineData(CardType.Mount, "any", true)]
    [InlineData(CardType.Mount, "chocobo", true)]
    [InlineData(CardType.Mount, "none", false)]
    [InlineData(CardType.Account, "visiting", true)]
    [InlineData(CardType.Account, "home", false)]
    [InlineData(CardType.Group, "light", true)]
    [InlineData(CardType.NearbyName, "Some Player", true)]
    [InlineData(CardType.NearbyName, "Nobody", false)]
    [InlineData(CardType.Region, "Noscea", true)]
    [InlineData(CardType.Status, "away", true)]
    [InlineData(CardType.Status, "Away from Keyboard", true)]
    [InlineData(CardType.Status, "busy", false)]
    [InlineData(CardType.Time, "21:00-22:00", true)]
    [InlineData(CardType.Time, "et 2-4", true)]
    [InlineData(CardType.Schedule, "weekly sat 21:00-23:00", true)]
    [InlineData(CardType.Duty, "out", true)]
    [InlineData(CardType.Duty, "in", false)]
    [InlineData(CardType.Housing, "any", false)]
    [InlineData(CardType.Zone, "#129", true)]
    public void Cards(string type, string value, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(When(type, value), Town()));

    [Fact]
    public void NotFlips() => Assert.False(Eval.CardMatches(When(CardType.State, "Town", true), Town()));

    [Fact]
    public void AnyWithNot()
    {
        // Any: "not in town" is false, "job WHM" is true, so the section is true.
        var flow = new FlowItem { WhenJoin = Join.Any };
        flow.Cards.Add(When(CardType.State, "Town", true));
        flow.Cards.Add(When(CardType.Job, "WHM"));
        Assert.True(Eval.SectionMatches(flow, Section.When, Town()));

        // Both negated and both present: any of two false cards is false.
        flow.Cards[1].Not = true;
        Assert.False(Eval.SectionMatches(flow, Section.When, Town()));

        // All with one negated miss.
        flow.WhenJoin = Join.All;
        flow.Cards[1].Not = false;
        Assert.False(Eval.SectionMatches(flow, Section.When, Town()));
        flow.Cards[0].Not = false;
        Assert.True(Eval.SectionMatches(flow, Section.When, Town()));
    }

    [Fact]
    public void EmptySectionIsFalse() => Assert.False(Eval.SectionMatches(new FlowItem(), Section.When, Town()));

    [Fact]
    public void PluginUsesHook()
    {
        var old = Eval.PluginLoaded;
        try
        {
            Eval.PluginLoaded = name => name == "Penumbra";
            Assert.True(Eval.CardMatches(When(CardType.Plugin, "Penumbra"), Town()));
            Assert.False(Eval.CardMatches(When(CardType.Plugin, "Other"), Town()));
        }
        finally
        {
            Eval.PluginLoaded = old;
        }
    }
}

public class AddressTests
{
    private static readonly Address Plot = new(Residence.House, "Mist", 5, 12, 0, false);
    private static readonly Address SubPlot = new(Residence.House, "Mist", 5, 42, 0, true);
    private static readonly Address Apartment = new(Residence.Apartment, "The Goblet", 3, 0, 27, false);
    private static readonly Address Chamber = new(Residence.Chamber, "Empyreum", 9, 4, 2, false);

    [Theory]
    [InlineData("House|Mist|5|12", true)]
    [InlineData("House|Mist|5|0", true)]
    [InlineData("House|Mist|0|0", true)]
    [InlineData("House|Mist|5|13", false)]
    [InlineData("House|Mist|5|12|sub", true)] // the plot decides the half; a stale sub flag is ignored
    [InlineData("Apartment|Mist|5|12", false)]
    [InlineData("House|Topmast|5|12", true)]
    [InlineData("Any|Mist|5", true)]
    [InlineData("any", true)]
    public void HousePlot(string spec, bool expected) => Assert.Equal(expected, AddressSpec.Matches(spec, Plot));

    [Fact]
    public void SubdivisionIsNotAPlot()
    {
        Assert.True(AddressSpec.Matches("House|Mist|5|42|sub", SubPlot));
        // Plots 31 to 60 are the subdivision, so the plot alone is enough.
        Assert.True(AddressSpec.Matches("House|Mist|5|42", SubPlot));
        Assert.False(AddressSpec.Matches("House|Mist|5|12", SubPlot));
        Assert.False(AddressSpec.Matches("Any|Mist|5", SubPlot));
        Assert.True(AddressSpec.Matches("Any|Mist|5|sub", SubPlot));
    }

    [Fact]
    public void ApartmentRoomIsNotAPlot()
    {
        // AndThen and StatusShift write the apartment number in the fourth field.
        Assert.True(AddressSpec.Matches("Apartment|The Goblet|3|27", Apartment));
        Assert.False(AddressSpec.Matches("Apartment|The Goblet|3|28", Apartment));
        Assert.False(AddressSpec.Matches("House|The Goblet|3|27", Apartment));
    }

    [Fact]
    public void ChamberAndOldName()
    {
        Assert.True(AddressSpec.Matches("Chamber|Empyreum|9|4|2", Chamber));
        Assert.True(AddressSpec.Matches("FcApartment|Empyreum|9|4|2", Chamber));
        Assert.False(AddressSpec.Matches("Chamber|Empyreum|9|4|3", Chamber));
    }

    [Fact]
    public void NotInWard() => Assert.False(AddressSpec.Matches("any", Address.None));

    [Fact]
    public void EncodeRoundTrips()
    {
        var spec = AddressSpec.Encode("Chamber", "Empyreum", 9, 34, 2, false);
        Assert.True(AddressSpec.TryParse(spec, out var kind, out var district, out var ward, out var plot, out var room, out var sub));
        Assert.Equal(("Chamber", "Empyreum", 9, 34, 2, true), (kind, district, ward, plot, room, sub));
        AddressSpec.TryParse(AddressSpec.Encode("House", "Mist", 5, 12, 0, true), out _, out _, out _, out _, out _, out var low);
        Assert.False(low);
    }
}

public class TokenTests
{
    [Fact]
    public void FillsKnownAndKeepsUnknown()
    {
        var world = new World { TerritoryName = "Limsa", WorldName = "Alpha", HomeWorldName = "Beta", JobAbbr = "WHM", TargetName = "" };
        Assert.Equal("In Limsa on Alpha (Beta) as WHM {mood} {", Tokens.Fill("In {zone} on {world} ({home}) as {JOB} {mood} {", world));
    }

    [Fact]
    public void WardAndPlotBlankOutsideHousing()
    {
        var world = new World();
        Assert.Equal("W P", Tokens.Fill("W{ward} P{plot}", world));
        var home = new World { Housing = new Address(Residence.House, "Mist", 5, 12, 0, false) };
        Assert.Equal("W5 P12", Tokens.Fill("W{ward} P{plot}", home));
    }
}

public class ModOptionsTests
{
    [Fact]
    public void ParseFormatMerge()
    {
        var map = ModOptions.Parse("Color = Red\nParts = Hat | Gloves; Empty =");
        Assert.Equal(["Red"], map["Color"]);
        Assert.Equal(["Hat", "Gloves"], map["Parts"]);
        Assert.Empty(map["Empty"]);

        var current = new Dictionary<string, List<string>> { ["Color"] = ["Blue"], ["Size"] = ["Large"] };
        var merged = ModOptions.Merge(current, map);
        Assert.Equal(["Red"], merged["Color"]);
        Assert.Equal(["Large"], merged["Size"]);
        Assert.Equal(ModOptions.Format(map), ModOptions.Format(ModOptions.Parse(ModOptions.Format(map))));
    }
}
