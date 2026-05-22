using System.Web;
using FluentAssertions;
using WorldClock.Helpers;
using Xunit;

namespace WorldClock.Tests.Helpers;

/// <summary>
/// Tests for <see cref="TeamsDeepLinkBuilder"/>:
///   • URI scheme (msteams vs https fallback).
///   • Subject, start/end times, body content.
///   • Proper percent-encoding in the query string.
///   • Edge cases: single-slot selection, midnight boundary, multi-city body.
/// </summary>
public sealed class TeamsDeepLinkBuilderTests
{
    private static readonly DateTime Day = new(2026, 4, 30);

    // ── Subject line ──────────────────────────────────────────────────────────

    [Fact]
    public void Subject_ContainsWorldSync()
    {
        var (teams, _) = TeamsDeepLinkBuilder.Build(Day, 16, 17,
            [("New York", "08:00", "08:30"), ("London", "13:00", "13:30")]);

        QueryOf(teams)["subject"].Should().Contain("Sync");
    }

    // ── Start / end times ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  0,  "00:00",  "00:30")]  // midnight, single slot
    [InlineData(16, 16, "08:00",  "08:30")]  // slot 16 = 08:00, end exclusive = 08:30
    [InlineData(16, 17, "08:00",  "09:00")]  // two slots
    [InlineData(46, 47, "23:00",  "00:00")]  // spans midnight (end wraps next day)
    public void StartEndTimes_MapCorrectlyFromSlots(int startSlot, int endSlot,
                                                     string expectedStart, string expectedEnd)
    {
        var (teams, _) = TeamsDeepLinkBuilder.Build(Day, startSlot, endSlot, []);

        var qs = QueryOf(teams);
        DateTimeOffset.Parse(qs["startTime"]!).ToString("HH:mm").Should().Be(expectedStart);

        // End time = slot-end + 30 min (exclusive upper bound for calendar events)
        var endDt = DateTimeOffset.Parse(qs["endTime"]!);
        endDt.ToString("HH:mm").Should().Be(expectedEnd,
            $"endSlot {endSlot} should end at {expectedEnd}");
    }

    [Fact]
    public void StartTime_UsesViewedDate()
    {
        var specificDay = new DateTime(2026, 6, 15);
        var (teams, _) = TeamsDeepLinkBuilder.Build(specificDay, 20, 20, []);

        var qs = QueryOf(teams);
        DateTimeOffset.Parse(qs["startTime"]!).Date.Should().Be(specificDay.Date);
    }

    // ── Body / content ────────────────────────────────────────────────────────

    [Fact]
    public void Body_ContainsAllCityNames()
    {
        var cities = new[]
        {
            ("New York",  "08:00", "09:00"),
            ("London",    "13:00", "14:00"),
            ("Tokyo",     "22:00", "23:00"),
        };
        var (teams, _) = TeamsDeepLinkBuilder.Build(Day, 16, 17, cities);

        var body = QueryOf(teams)["content"]!;
        body.Should().Contain("New York");
        body.Should().Contain("London");
        body.Should().Contain("Tokyo");
    }

    [Fact]
    public void Body_ContainsCityTimes()
    {
        var cities = new[] { ("Madrid", "15:00", "15:30") };
        var (teams, _) = TeamsDeepLinkBuilder.Build(Day, 30, 30, cities);

        var body = QueryOf(teams)["content"]!;
        body.Should().Contain("15:00");
    }

    [Fact]
    public void Body_EmptyCityList_DoesNotThrow()
    {
        var act = () => TeamsDeepLinkBuilder.Build(Day, 0, 0, []);
        act.Should().NotThrow();
    }

    // ── URI scheme ────────────────────────────────────────────────────────────

    [Fact]
    public void TeamsUri_StartsWith_msteams()
    {
        var (teams, _) = TeamsDeepLinkBuilder.Build(Day, 16, 17, []);
        teams.Should().StartWith("msteams://");
    }

    [Fact]
    public void FallbackUri_StartsWith_https_teams()
    {
        var (_, browser) = TeamsDeepLinkBuilder.Build(Day, 16, 17, []);
        browser.Should().StartWith("https://teams.microsoft.com/");
    }

    [Fact]
    public void BothUris_ContainSameQueryParameters()
    {
        var (teams, browser) = TeamsDeepLinkBuilder.Build(Day, 16, 17,
            [("NYC", "08:00", "09:00")]);

        var tq = QueryOf(teams);
        var bq = QueryOf(browser);

        tq["subject"].Should().Be(bq["subject"]);
        tq["startTime"].Should().Be(bq["startTime"]);
        tq["endTime"].Should().Be(bq["endTime"]);
        tq["content"].Should().Be(bq["content"]);
    }

    // ── Encoding ──────────────────────────────────────────────────────────────

    [Fact]
    public void Uri_DoesNotContainRawSpaces()
    {
        var (teams, browser) = TeamsDeepLinkBuilder.Build(Day, 16, 17,
            [("New York", "08:00", "09:00")]);

        teams.Should().NotContain(" ", "URI must be percent-encoded");
        browser.Should().NotContain(" ", "URI must be percent-encoded");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static System.Collections.Specialized.NameValueCollection QueryOf(string uri)
    {
        var idx = uri.IndexOf('?');
        var raw = idx >= 0 ? uri[(idx + 1)..] : string.Empty;
        return HttpUtility.ParseQueryString(raw);
    }
}
