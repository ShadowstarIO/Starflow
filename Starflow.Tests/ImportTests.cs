using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace Starflow.Tests;

public class ImportTests
{
    // AndThen writes enums as numbers. Kind 7 is Duty, 16 Housing, 3 Zone, 10 Weather. Then 3 is Config, 5 Logout, 7 PenumbraMod.
    private const string AndThenRule = """
    {
      "Id": "abc", "Name": "Duty setup", "Folder": "Combat", "Enabled": true, "Mode": 2, "DelaySec": 1.5,
      "AndChips": [ { "Kind": 7, "Value": "solid" }, { "Kind": 16, "Value": "Apartment|Mist|5|12" } ],
      "OrChips": [ { "Kind": 3, "Value": "Limsa" }, { "Kind": 10, "Value": "Rain" } ],
      "NotChips": [ { "Kind": 0, "Value": "InCombat" } ],
      "Then": [
        { "Kind": 0, "Value": "/echo hi" },
        { "Kind": 1, "WaitMs": 500 },
        { "Kind": 3, "Section": 1, "Option": "BattleEffectOther", "Value": "2" },
        { "Kind": 5 },
        { "Kind": 7, "Option": "dir", "Value": "Mod", "Flag": false, "Number": 4, "Extra": "{\"Settings\":{\"Color\":[\"Red\"]}}" }
      ]
    }
    """;

    private const string StatusShiftConfig = """
    {
      "ApplyMode": 0,
      "Rules": [
        {
          "Name": "Evening RP", "Priority": 7, "RevertWhenFalse": true,
          "ChangeSearchComment": true, "SearchComment": "In {zone}",
          "OnlineStatus": 4, "FallbackStatus": 2,
          "Command": "/echo on", "CommandDelaySeconds": 2,
          "Schedule": { "Mode": 2, "AllDay": false, "StartHour": 20, "StartMinute": 0, "EndHour": 23, "EndMinute": 30, "Days": [5, 6] },
          "AndChips": [ { "Kind": 4, "Value": "House|Mist|5|12" }, { "Kind": 8, "Value": "Some Player" } ],
          "States": [ { "Flag": 1, "Op": 2 } ]
        },
        { "Name": "Sticky", "RevertWhenFalse": false, "OnlineStatus": 3, "AndChips": [ { "Kind": 13, "Value": "x.*" } ] }
      ]
    }
    """;

    [Fact]
    public void AndThenRuleBecomesOffKeepFlow()
    {
        Assert.True(Importers.TryAndThen(AndThenRule, out var flows, out var message), message);
        var flow = Assert.Single(flows);
        Assert.False(flow.Enabled);
        Assert.Equal(RunMode.Off, flow.Mode);
        Assert.Equal(StopMode.Keep, flow.OnStop);
        Assert.Equal(Join.All, flow.WhenJoin);
        Assert.Equal(1.5f, flow.HoldSec);
        Assert.Equal("Combat", flow.Folder);

        var when = flow.In(Section.When);
        Assert.Equal(5, when.Count);
        Assert.Equal((CardType.Duty, "solid"), (when[0].Type, when[0].Value));
        Assert.Equal(CardType.Housing, when[1].Type);
        Assert.Equal(CardType.Zone, when[2].Type);
        Assert.Equal(CardType.Weather, when[3].Type);
        Assert.True(when[4].Not);
        Assert.Equal("InCombat", when[4].Value);
        Assert.Contains("OR", flow.Notes);

        var doCards = flow.In(Section.Do);
        Assert.Equal([CardType.Command, CardType.Wait, CardType.Setting, CardType.Logout, CardType.PenumbraMod], doCards.Select(c => c.Type));
        Assert.Equal(("Ui", "BattleEffectOther", "2"), (doCards[2].Option, doCards[2].Value, doCards[2].Extra));
        Assert.True(doCards[3].Flag);
        Assert.Equal("Color = Red", doCards[4].Extra);
        Assert.False(doCards[4].Flag);
        Assert.Equal(4, doCards[4].Number);
    }

    [Fact]
    public void AndThenOnlyOrBecomesAny()
    {
        const string json = """[{ "Name": "Either", "OrChips": [ { "Kind": 0, "Value": "Mounted" }, { "Kind": 0, "Value": "Flying" } ] }]""";
        Assert.True(Importers.TryAndThen(json, out var flows, out _));
        Assert.Equal(Join.Any, flows[0].WhenJoin);
    }

    [Fact]
    public void AndThenShareCode()
    {
        var code = "AT1." + Gzip(AndThenRule);
        Assert.True(Importers.TryAndThen(code, out var flows, out var message), message);
        Assert.Equal("Duty setup", flows[0].Name);
    }

    [Theory]
    [InlineData("day", "et 6-18")]
    [InlineData("et >=18", "et 18-24")]
    [InlineData("lt <6", "0-6")]
    [InlineData("12", "et 12-13")]
    public void AndThenTimeChips(string value, string expected)
    {
        var json = $$"""{ "Name": "T", "AndChips": [ { "Kind": 9, "Value": "{{value}}" } ] }""";
        Assert.True(Importers.TryAndThen(json, out var flows, out _));
        var card = flows[0].In(Section.When)[0];
        Assert.Equal(CardType.Time, card.Type);
        Assert.Equal(expected, card.Value);
    }

    [Fact]
    public void StatusShiftRules()
    {
        Assert.True(Importers.TryStatusShift(StatusShiftConfig, out var flows, out var message), message);
        Assert.Equal(2, flows.Count);
        Assert.Contains("chip Regex", message);

        var rp = flows[0];
        Assert.False(rp.Enabled);
        Assert.Equal(RunMode.Ask, rp.Mode);
        Assert.Equal(StopMode.Revert, rp.OnStop);
        Assert.Equal(7, rp.Priority);

        var when = rp.In(Section.When);
        Assert.Equal((CardType.Schedule, "weekly fri,sat 20:00-23:30"), (when[0].Type, when[0].Value));
        Assert.Equal(CardType.Housing, when[1].Type);
        Assert.Equal(CardType.NearbyName, when[2].Type);
        Assert.Equal(("InCombat", true), (when[3].Value, when[3].Not));

        var doCards = rp.In(Section.Do);
        Assert.Equal([CardType.Notice, CardType.SearchComment, CardType.Online, CardType.Wait, CardType.Command], doCards.Select(c => c.Type));
        Assert.Equal("rp", doCards[2].Value);
        Assert.Equal(2000, doCards[3].WaitMs);

        var after = Assert.Single(rp.In(Section.After));
        Assert.Equal((CardType.Online, "away"), (after.Type, after.Value));

        Assert.Equal(StopMode.Keep, flows[1].OnStop);
        Assert.Empty(flows[1].In(Section.When));
    }

    [Fact]
    public void StatusShiftModes()
    {
        string Config(int mode) => $$"""{ "ApplyMode": {{mode}}, "Rules": [ { "Name": "R" } ] }""";
        RunMode Mode(int m) { Importers.TryStatusShift(Config(m), out var f, out _); return f[0].Mode; }
        Assert.Equal(RunMode.Auto, Mode(1));
        Assert.Equal(RunMode.Off, Mode(2));
        Assert.Equal(RunMode.Ask, Mode(3));
        Assert.Equal(RunMode.Ask, Mode(0));
    }

    [Fact]
    public void ImportsRoundTripThroughShare()
    {
        Assert.True(Importers.TryAndThen(AndThenRule, out var a, out _));
        Assert.True(Importers.TryStatusShift(StatusShiftConfig, out var s, out _));
        var all = a.Concat(s).ToList();
        var json = Share.All(all);
        Assert.True(Share.TryRead(json, out var back, out var error), error);
        Assert.Equal(all.Count, back.Count);
        Assert.Equal(json, Share.All(back));
    }

    [Fact]
    public void ShareReadsOlderJson()
    {
        // Written by 0.1: no IgnoreQuiet, Keep, or Inherit fields.
        const string old = """
        { "Id": "x", "Name": "Old", "Mode": "Auto", "OnStop": "Revert", "WhenJoin": "All", "UntilJoin": "Any",
          "Cards": [ { "Section": "When", "Type": "state", "Value": "InDuty" }, { "Section": "Do", "Type": "command", "Value": "/echo", "Repeat": 0 } ] }
        """;
        Assert.True(Share.TryRead(old, out var flows, out var error), error);
        Assert.False(flows[0].IgnoreQuiet);
        Assert.Equal(1, flows[0].Cards[1].Repeat);
    }

    private static string Gzip(string text)
    {
        var raw = Encoding.UTF8.GetBytes(text);
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.SmallestSize, true)) gz.Write(raw, 0, raw.Length);
        return Convert.ToBase64String(ms.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
