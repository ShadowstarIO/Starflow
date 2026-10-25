using System;
using Xunit;

namespace Flow.Tests;

public class MatchTests
{
    [Theory]
    [InlineData(5, ">=5", true)]
    [InlineData(4, ">=5", false)]
    [InlineData(5, "<=5", true)]
    [InlineData(6, "<=5", false)]
    [InlineData(6, ">5", true)]
    [InlineData(5, ">5", false)]
    [InlineData(4, "<5", true)]
    [InlineData(5, "<5", false)]
    [InlineData(5, "=5", true)]
    [InlineData(5, "5", true)]
    [InlineData(5, "!=5", false)]
    [InlineData(3, "1-4", true)]
    [InlineData(5, "1-4", false)]
    [InlineData(0, ">=abc", false)]
    [InlineData(0, "", false)]
    [InlineData(8, " >= 8 ", true)]
    public void Number(int left, string expr, bool expected) => Assert.Equal(expected, Match.Number(left, expr));

    [Theory]
    [InlineData(23, 0, "22:00-02:00", true)]
    [InlineData(1, 59, "22:00-02:00", true)]
    [InlineData(2, 0, "22:00-02:00", false)]
    [InlineData(21, 59, "22:00-02:00", false)]
    [InlineData(0, 0, "22-2", true)]
    [InlineData(12, 0, "9-17", true)]
    [InlineData(17, 0, "9-17", false)]
    [InlineData(23, 30, "18-24", true)]
    [InlineData(5, 0, "0-24", true)]
    [InlineData(5, 0, "6-6", true)]
    [InlineData(5, 0, "25-3", false)]
    [InlineData(5, 0, "nonsense", false)]
    public void ClockLocal(int hour, int minute, string spec, bool expected) =>
        Assert.Equal(expected, Match.Clock(hour, minute, spec));

    [Theory]
    [InlineData(18, "et 17-22", true)]
    [InlineData(22, "et 17-22", false)]
    [InlineData(3, "et 22-5", true)]
    [InlineData(23, "ET 22-5", true)]
    [InlineData(6, "et 22-5", false)]
    public void ClockEorzea(int eorzeaHour, string spec, bool expected) =>
        Assert.Equal(expected, Match.Clock(eorzeaHour, 0, spec));

    [Theory]
    [InlineData("2026-10-24", "sat,sun", true)]
    [InlineData("2026-10-24", "Saturday", true)]
    [InlineData("2026-10-24", "weekend", true)]
    [InlineData("2026-10-24", "weekday", false)]
    [InlineData("2026-10-26", "weekday", true)]
    [InlineData("2026-10-26", "mon", true)]
    [InlineData("2026-10-26", "tue,wed", false)]
    [InlineData("2026-10-27", "tu", true)]
    [InlineData("2026-10-29", "th", true)]
    [InlineData("2026-10-29", "t", false)]
    [InlineData("2026-10-24", "", false)]
    public void Weekday(string date, string spec, bool expected) =>
        Assert.Equal(expected, Match.Weekday(DateTime.Parse(date), spec));

    [Theory]
    [InlineData(0, "empty", true)]
    [InlineData(3, "few", true)]
    [InlineData(9, "few", false)]
    [InlineData(9, "crowded", true)]
    [InlineData(4, ">=4", true)]
    public void Nearby(int count, string spec, bool expected) => Assert.Equal(expected, Match.Nearby(count, spec));

    [Theory]
    [InlineData(1, false, "solo")]
    [InlineData(0, false, "solo")]
    [InlineData(4, false, "light")]
    [InlineData(5, false, "full")]
    [InlineData(8, false, "full")]
    [InlineData(8, true, "alliance")]
    public void GroupBand(int size, bool alliance, string band) => Assert.Equal(band, Match.GroupBand(size, alliance));

    [Theory]
    [InlineData("light", "light|full", true)]
    [InlineData("full", "party", true)]
    [InlineData("solo", "party", false)]
    [InlineData("alliance", "FULL", false)]
    public void Group(string band, string spec, bool expected) => Assert.Equal(expected, Match.Group(band, spec));
}

public class ScheduleTests
{
    private static DateTime At(string s) => DateTime.Parse(s);

    [Fact]
    public void AlwaysMatches() => Assert.True(Match.Schedule(At("2026-10-24 03:00"), "always"));

    [Theory]
    [InlineData("2026-10-24 09:00", true)]
    [InlineData("2026-10-24 16:59", true)]
    [InlineData("2026-10-24 17:00", false)]
    [InlineData("2026-10-24 08:59", false)]
    public void Daily(string when, bool expected) => Assert.Equal(expected, Match.Schedule(At(when), "daily 09:00-17:00"));

    [Theory]
    // Friday 22:00 to 02:00. Saturday 01:00 still belongs to Friday's window.
    [InlineData("2026-10-23 23:00", true)]
    [InlineData("2026-10-24 01:00", true)]
    [InlineData("2026-10-24 02:00", false)]
    [InlineData("2026-10-24 23:00", false)]
    [InlineData("2026-10-23 01:00", false)]
    public void WeeklyPastMidnight(string when, bool expected) =>
        Assert.Equal(expected, Match.Schedule(At(when), "weekly fri 22:00-02:00"));

    [Fact]
    public void WeeklyAllDay()
    {
        Assert.True(Match.Schedule(At("2026-10-25 12:00"), "weekly sat,sun"));
        Assert.False(Match.Schedule(At("2026-10-26 12:00"), "weekly sat,sun"));
    }

    [Theory]
    [InlineData("2026-12-24 21:00", true)]
    [InlineData("2026-12-25 21:00", true)]
    [InlineData("2026-12-27 00:30", true)]
    [InlineData("2026-12-27 21:00", false)]
    [InlineData("2026-12-23 21:00", false)]
    public void OnceRange(string when, bool expected) =>
        Assert.Equal(expected, Match.Schedule(At(when), "once 2026-12-24..2026-12-26 20:00-01:00"));

    [Theory]
    [InlineData("")]
    [InlineData("sometimes")]
    [InlineData("weekly")]
    [InlineData("weekly funday")]
    [InlineData("once 24-12-2026")]
    [InlineData("daily 9-17 extra")]
    public void Rejects(string spec) => Assert.False(Match.TryParseSchedule(spec, out _));
}
