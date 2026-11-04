using System;
using System.Collections.Generic;
using Xunit;

namespace Starflow.Tests;

public class ReactionTests
{
    private static Unit Healthy(string name, int hp = 80000) => new()
    {
        Name = name, Hp = hp, MaxHp = 100000, Mp = 5000, MaxMp = 10000, Gp = 600, MaxGp = 900,
        Effects = ["Medicated", "Regen"],
    };

    private static World Fight(double hurtAgo = 1, bool byPlayer = false) => new()
    {
        LoggedIn = true,
        Me = Healthy("You", 40000),
        TargetUnit = Healthy("Boss", 100000) with { Effects = ["Vulnerability Up"] },
        Party = [Healthy("Healer"), Healthy("Tank", 0) with { Effects = [] }],
        EnemiesOnMe = 2,
        HurtAgoSec = hurtAgo,
        HurtAmount = 12000,
        HurtByPlayer = byPlayer,
        Chat =
        [
            new ChatLine("tell", "Friend Name", "please come to the house", 2, false),
            new ChatLine("say", "You", "hello", 1, true),
            new ChatLine("party", "Stranger", "pull now", 30, false),
        ],
    };

    private static Card C(string type, string value, string option = "", string extra = "", int number = 0, bool own = false) =>
        new() { Section = Section.When, Type = type, Value = value, Option = option, Extra = extra, Number = number, Permanent = own };

    [Theory]
    [InlineData("hp", "me", "<50%", true)]
    [InlineData("hp", "me", ">50%", false)]
    [InlineData("hp", "me", "40000", true)]
    [InlineData("mp", "me", "=50%", true)]
    [InlineData("gp", "me", ">=600", true)]
    [InlineData("hp", "target", "=100%", true)]
    [InlineData("hp", "party-any", "=0", true)]
    [InlineData("hp", "party-all", ">=10%", false)]
    [InlineData("hp", "party-all", "<=80%", true)]
    public void Vitals(string stat, string who, string spec, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(C(CardType.Vitals, spec, stat, who), Fight()));

    [Theory]
    [InlineData("me", "Regen", true)]
    [InlineData("me", "regen | Weakness", true)]
    [InlineData("me", "Weakness", false)]
    [InlineData("target", "/vulnerab/", true)]
    [InlineData("party-others", "Medicated", true)]
    [InlineData("party", "Vulnerability Up", false)]
    public void Effects(string who, string value, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(C(CardType.Effect, value, extra: who), Fight()));

    [Fact]
    public void EnmityAndDown()
    {
        Assert.True(Eval.CardMatches(C(CardType.Enmity, ">=1"), Fight()));
        Assert.False(Eval.CardMatches(C(CardType.Enmity, "=0"), Fight()));
        Assert.True(Eval.CardMatches(C(CardType.PartyDown, ">=1"), Fight()));
        Assert.Equal(1, Fight().PartyDown);
    }

    [Theory]
    [InlineData("any", "", 3, 1, false, true)]
    [InlineData("any", "", 3, 5, false, false)]
    [InlineData("player", "", 3, 1, false, false)]
    [InlineData("player", "", 3, 1, true, true)]
    [InlineData("npc", "10%", 3, 1, false, true)]
    [InlineData("npc", "20%", 3, 1, false, false)]
    [InlineData("any", "12000", 3, 1, false, true)]
    public void Hurt(string from, string min, int window, double ago, bool byPlayer, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(C(CardType.Hurt, min, from, number: window), Fight(ago, byPlayer)));

    [Theory]
    [InlineData("tell", "", "", 5, false, true)]
    [InlineData("tell", "Friend Name", "house", 5, false, true)]
    [InlineData("tell", "Friend", "house", 5, false, false)] // exact names only
    [InlineData("tell", "/^Friend/", "house", 5, false, true)]
    [InlineData("tell", "", "/^please/", 5, false, true)]
    [InlineData("tell", "", "garden", 5, false, false)]
    [InlineData("tell", "", "", 1, false, false)] // too old
    [InlineData("say", "", "hello", 5, false, false)] // own line
    [InlineData("say", "", "hello", 5, true, true)]
    [InlineData("party", "", "", 5, false, false)]
    [InlineData("any", "", "pull", 60, false, true)]
    [InlineData("say|tell", "", "", 5, false, true)]
    public void Chat(string channels, string sender, string text, int window, bool own, bool expected) =>
        Assert.Equal(expected, Eval.CardMatches(C(CardType.Chat, text, channels, sender, window, own), Fight()));

    [Fact]
    public void ChatTokens()
    {
        var w = Fight();
        Assert.Equal("Friend Name: please come to the house (tell)", Tokens.Fill("{chatsender}: {chatmessage} ({chatchannel})", w));
        Assert.Equal("40", Tokens.Fill("{hpp}", w));
    }

    [Fact]
    public void TokenCannotBeTheCommand()
    {
        Assert.True(Tokens.HeadIsToken("{chatmessage}"));
        Assert.True(Tokens.HeadIsToken(" {x} rest"));
        Assert.False(Tokens.HeadIsToken("/say {chatmessage}"));
        Assert.Equal("command word that is not a token", Catalog.Missing(new Card { Section = Section.Do, Type = CardType.Command, Value = "{chatmessage}" }));
    }

    [Fact]
    public void ReactiveFlowsCannotUseActions()
    {
        var flow = new FlowItem();
        flow.Cards.Add(C(CardType.Vitals, "<30%", "hp", "me"));
        flow.Cards.Add(new Card { Section = Section.Do, Type = CardType.Command, Value = "/ac \"Second Wind\"" });
        Assert.Contains(Catalog.Missing(flow), m => m.Contains("action or macro"));

        flow.Cards[1].Value = "/macro 3";
        Assert.Contains(Catalog.Missing(flow), m => m.Contains("action or macro"));

        flow.Cards[1].Value = "/gearset change 3";
        Assert.Empty(Catalog.Missing(flow));

        // The same action in a flow that only reads the zone is a normal command.
        flow.Cards[0] = C(CardType.Zone, "Limsa");
        flow.Cards[1].Value = "/ac Sprint";
        Assert.Empty(Catalog.Missing(flow));
    }

    [Fact]
    public void ReactiveCardsAreInTheAddList()
    {
        var types = new HashSet<string>();
        foreach (var d in Catalog.For(Section.When)) types.Add(d.Type);
        foreach (var t in new[] { CardType.Vitals, CardType.Effect, CardType.Enmity, CardType.Hurt, CardType.PartyDown, CardType.Chat })
            Assert.Contains(t, types);
        Assert.Contains(Catalog.For(Section.Do), d => d.Type == CardType.Lifestream);
        Assert.Null(Catalog.Missing(Catalog.Make(Section.When, CardType.Chat)));
        Assert.Null(Catalog.Missing(Catalog.Make(Section.When, CardType.Hurt)));
        Assert.Equal("value", Catalog.Missing(Catalog.Make(Section.When, CardType.Effect)));
    }
}

public class OptionGuideTests
{
    [Theory]
    [InlineData("SoundMaster", "Sound", "Master volume")]
    [InlineData("IsSndBgm", "Sound", "mutes music")]
    [InlineData("NamePlateDispTypeParty", "Nameplates", "party members")]
    [InlineData("NamePlateHpTypeEngagedEmemy", "Nameplates", "enemies fighting you")]
    [InlineData("LogChatBubbleTellShowType", "Chat", "Tell")]
    [InlineData("ColorFCompany", "Chat", "Free Company")]
    [InlineData("SSAO_DX11", "Graphics", "DirectX 11")]
    [InlineData("PadButton_L2", "Controls", "L2")]
    [InlineData("HotbarCommon03", "Hotbars", "hotbar 3")]
    [InlineData("ContentsFinderUseLangTypeDE", "Duty Finder", "German")]
    [InlineData("IsLogLs3", "Chat", "linkshell 3")]
    [InlineData("BattleEffectParty", "Graphics", "party members")]
    public void Describes(string name, string group, string piece)
    {
        var entry = OptionGuide.Describe(name);
        Assert.Equal(group, entry.Group);
        Assert.Contains(piece, entry.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InternalOptionsAreMarked() => Assert.True(OptionGuide.Describe("DktSessionId").Internal);

    [Theory]
    [InlineData("DktSessionId", true)]
    [InlineData("LastLogin0", true)]
    [InlineData("WorldId", true)]
    [InlineData("PadGuid", true)]
    [InlineData("SoundMaster", false)]
    [InlineData("NamePlateDispTypeParty", false)]
    public void BookkeepingIsBlocked(string name, bool blocked) => Assert.Equal(blocked, OptionGuide.Blocked(name));

    [Fact]
    public void SettingCardRefusesBookkeeping() =>
        Assert.Equal("option Starflow is allowed to write", Catalog.Missing(new Card { Section = Section.Do, Type = CardType.Setting, Option = "System", Value = "DktSessionId", Extra = "0" }));

    [Fact]
    public void FallbackReadsTheName() => Assert.Equal("display cooldown time", OptionGuide.Words("DispRecastTime"));
}
