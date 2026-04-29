using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using WorldClock.Models;
using Xunit;

namespace WorldClock.Tests.Integration;

/// <summary>
/// Integration tests that validate UTC/timezone logic against the public
/// timeapi.io REST API (https://timeapi.io/api/time/current/zone?timeZone=UTC).
/// Tests are skipped gracefully when the network is unavailable.
/// </summary>
public class UtcApiTests
{
    private const string ApiUrl = "https://timeapi.io/api/time/current/zone?timeZone=UTC";
    private static readonly TimeSpan NetworkTimeout   = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ClockTolerance   = TimeSpan.FromSeconds(10);

    // ── Helper ────────────────────────────────────────────────────────────────

    private static async Task<JsonDocument?> FetchUtcAsync()
    {
        using var client = new HttpClient { Timeout = NetworkTimeout };
        try
        {
            var response = await client.GetAsync(ApiUrl);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null; // network unavailable — caller will skip
        }
    }

    // ── API availability & shape ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UtcApi_ReturnsSuccessStatusCode()
    {
        using var client = new HttpClient { Timeout = NetworkTimeout };
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(ApiUrl);
        }
        catch
        {
            return; // skip when offline
        }

        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET {ApiUrl} should return 2xx");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UtcApi_ResponseContains_RequiredFields()
    {
        var doc = await FetchUtcAsync();
        if (doc is null) return; // skip when offline

        var root = doc.RootElement;
        root.TryGetProperty("year",    out _).Should().BeTrue();
        root.TryGetProperty("month",   out _).Should().BeTrue();
        root.TryGetProperty("day",     out _).Should().BeTrue();
        root.TryGetProperty("hour",    out _).Should().BeTrue();
        root.TryGetProperty("minute",  out _).Should().BeTrue();
        root.TryGetProperty("seconds", out _).Should().BeTrue();
        root.TryGetProperty("timeZone", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UtcApi_ReportsTimeZone_AsUtc()
    {
        var doc = await FetchUtcAsync();
        if (doc is null) return;

        var tz = doc.RootElement.GetProperty("timeZone").GetString();
        tz.Should().Be("UTC");
    }

    // ── Date / time value checks ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UtcApi_ReportsValidDateComponents()
    {
        var doc = await FetchUtcAsync();
        if (doc is null) return;

        var root  = doc.RootElement;
        var year  = root.GetProperty("year").GetInt32();
        var month = root.GetProperty("month").GetInt32();
        var day   = root.GetProperty("day").GetInt32();

        year .Should().BeGreaterThanOrEqualTo(2026);
        month.Should().BeInRange(1, 12);
        day  .Should().BeInRange(1, 31);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UtcApi_ReportsValidTimeComponents()
    {
        var doc = await FetchUtcAsync();
        if (doc is null) return;

        var root    = doc.RootElement;
        var hour    = root.GetProperty("hour").GetInt32();
        var minute  = root.GetProperty("minute").GetInt32();
        var seconds = root.GetProperty("seconds").GetInt32();

        hour   .Should().BeInRange(0, 23);
        minute .Should().BeInRange(0, 59);
        seconds.Should().BeInRange(0, 59);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UtcApi_Time_MatchesLocalUtcClock_WithinTolerance()
    {
        var localBefore = DateTime.UtcNow;
        var doc = await FetchUtcAsync();
        var localAfter  = DateTime.UtcNow;

        if (doc is null) return;

        var root    = doc.RootElement;
        var hour    = root.GetProperty("hour").GetInt32();
        var minute  = root.GetProperty("minute").GetInt32();
        var seconds = root.GetProperty("seconds").GetInt32();

        var apiTime   = new TimeSpan(hour, minute, seconds);
        var midLocal  = localBefore + (localAfter - localBefore) / 2;
        var localTime = midLocal.TimeOfDay;

        // Handle midnight wrap-around
        var diff = (localTime - apiTime).Duration();
        if (diff > TimeSpan.FromHours(23))
            diff = TimeSpan.FromHours(24) - diff;

        diff.Should().BeLessThanOrEqualTo(ClockTolerance,
            "API UTC time should match local UTC clock within 10 seconds");
    }

    // ── Local timezone logic (no network needed) ──────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void LocalUtcTimezone_HasZeroOffset()
    {
        var tz     = TimeZoneInfo.FindSystemTimeZoneById("UTC");
        var offset = tz.GetUtcOffset(DateTime.UtcNow);
        offset.Should().Be(TimeSpan.Zero, "UTC timezone must have a zero offset");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LocalUtcNow_EqualsConvertedUtcTime()
    {
        var tz   = TimeZoneInfo.FindSystemTimeZoneById("UTC");
        var conv = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        conv.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("Eastern Standard Time",      -5)]
    [InlineData("Arabian Standard Time",       4)]
    [InlineData("Tokyo Standard Time",         9)]
    [InlineData("AUS Eastern Standard Time",  10)]
    public void Timezone_StandardOffset_IsAsExpected(string tzId, int expectedHours)
    {
        var tz             = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var standardOffset = tz.BaseUtcOffset;
        standardOffset.TotalHours.Should().Be(expectedHours,
            $"{tzId} base UTC offset should be UTC{expectedHours:+0;-0}");
    }

    // ── ClockLocation.Refresh integration ────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClockLocation_UtcTime_MatchesPublicApi_WithinTolerance()
    {
        // Record local time and API time at roughly the same moment
        var localBefore = DateTime.UtcNow;
        var doc = await FetchUtcAsync();
        var localAfter  = DateTime.UtcNow;

        if (doc is null) return;

        // Build location and refresh
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Cyan);
        brush.Freeze();
        var location = new ClockLocation
        {
            CityName    = "UTC",
            CountryFlag = "🌐",
            TimeZoneId  = "UTC",
            TeamLabel   = "Universal Time",
            AccentBrush = brush
        };
        location.Refresh();

        var parsedLocation = TimeSpan.Parse(location.CurrentTime);

        var root    = doc.RootElement;
        var apiTime = new TimeSpan(
            root.GetProperty("hour").GetInt32(),
            root.GetProperty("minute").GetInt32(),
            root.GetProperty("seconds").GetInt32());

        var diff = (parsedLocation - apiTime).Duration();
        if (diff > TimeSpan.FromHours(23))
            diff = TimeSpan.FromHours(24) - diff;

        diff.Should().BeLessThanOrEqualTo(ClockTolerance,
            "ClockLocation.Refresh() UTC time must match public API within tolerance");
    }
}
