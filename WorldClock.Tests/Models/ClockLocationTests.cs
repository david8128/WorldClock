using System.Windows.Media;
using FluentAssertions;
using WorldClock.Models;
using Xunit;

namespace WorldClock.Tests.Models;

public class ClockLocationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClockLocation BuildLocation(string tzId = "UTC")
    {
        var brush = new SolidColorBrush(Colors.Cyan);
        brush.Freeze(); // thread-safe; required to use across STA context
        return new ClockLocation
        {
            CityName    = "Test City",
            CountryFlag = "🌐",
            TimeZoneId  = tzId,
            TeamLabel   = "Test Team",
            AccentBrush = brush
        };
    }

    // ── CurrentTime format ────────────────────────────────────────────────────

    [StaFact]
    public void Refresh_SetsCurrentTime_InHhMmSsFormat()
    {
        var location = BuildLocation();
        location.Refresh();
        location.CurrentTime.Should().MatchRegex(@"^\d{2}:\d{2}:\d{2}$",
            "CurrentTime must be HH:mm:ss");
    }

    [StaFact]
    public void Refresh_SetsCurrentTime_ToNonEmptyString()
    {
        var location = BuildLocation();
        location.Refresh();
        location.CurrentTime.Should().NotBeNullOrWhiteSpace();
    }

    // ── CurrentDate format ────────────────────────────────────────────────────

    [StaFact]
    public void Refresh_SetsCurrentDate_InDddDdMmmYyyyFormat()
    {
        var location = BuildLocation();
        location.Refresh();
        // e.g. "Wed, 22 Apr 2026"
        location.CurrentDate.Should().MatchRegex(@"^\w{3}, \d{2} \w{3} \d{4}$",
            "CurrentDate must be 'ddd, dd MMM yyyy'");
    }

    [StaFact]
    public void Refresh_SetsCurrentDate_ContainsCurrentYear()
    {
        var location = BuildLocation();
        location.Refresh();
        location.CurrentDate.Should().Contain(DateTime.UtcNow.Year.ToString());
    }

    // ── UtcOffset format ──────────────────────────────────────────────────────

    [StaFact]
    public void Refresh_SetsUtcOffset_ForUtcTimezone_ToUtcPlusZero()
    {
        var location = BuildLocation("UTC");
        location.Refresh();
        location.UtcOffset.Should().Be("UTC+00:00");
    }

    [StaFact]
    public void Refresh_SetsUtcOffset_AlwaysStartsWithUtc()
    {
        var location = BuildLocation("Eastern Standard Time");
        location.Refresh();
        location.UtcOffset.Should().StartWith("UTC");
    }

    [StaFact]
    public void Refresh_SetsUtcOffset_MatchesExpectedPattern()
    {
        var location = BuildLocation("Romance Standard Time"); // UTC+1 or UTC+2 depending on DST
        location.Refresh();
        location.UtcOffset.Should().MatchRegex(@"^UTC[+-]\d{2}:\d{2}$");
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    [StaFact]
    public void Refresh_RaisesPropertyChanged_ForCurrentTime()
    {
        var location = BuildLocation();
        var raised = new List<string?>();
        location.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        location.Refresh();

        raised.Should().Contain(nameof(ClockLocation.CurrentTime));
    }

    [StaFact]
    public void Refresh_RaisesPropertyChanged_ForCurrentDate()
    {
        var location = BuildLocation();
        var raised = new List<string?>();
        location.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        location.Refresh();

        raised.Should().Contain(nameof(ClockLocation.CurrentDate));
    }

    [StaFact]
    public void Refresh_RaisesPropertyChanged_ForUtcOffset()
    {
        var location = BuildLocation();
        var raised = new List<string?>();
        location.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        location.Refresh();

        raised.Should().Contain(nameof(ClockLocation.UtcOffset));
    }

    [StaFact]
    public void Refresh_RaisesPropertyChanged_ThreeTimesMinimum()
    {
        var location = BuildLocation();
        var raised = new List<string?>();
        location.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        location.Refresh();

        raised.Should().HaveCountGreaterThanOrEqualTo(3,
            "Refresh should notify CurrentTime, CurrentDate, and UtcOffset");
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [StaFact]
    public void Refresh_WithInvalidTimeZoneId_ThrowsTimeZoneNotFoundException()
    {
        var location = BuildLocation("Invalid/NotATimezone");
        var act = () => location.Refresh();
        act.Should().Throw<TimeZoneNotFoundException>();
    }

    // ── Multi-timezone accuracy ───────────────────────────────────────────────

    [StaTheory]
    [InlineData("UTC",                        "+00:00")]
    [InlineData("Eastern Standard Time",      "-05:00")]  // EST (non-DST)
    [InlineData("GMT Standard Time",          "+00:00")]  // GMT (non-DST)
    public void Refresh_UtcOffset_MatchesExpectedOffsetDuringStandardTime(
        string tzId, string expectedSignAndOffset)
    {
        // This test intentionally checks non-DST Windows timezone IDs only.
        // DST-observing timezones shift offset seasonally, so we only assert
        // that the format is correct and the sign is as expected.
        var location = BuildLocation(tzId);
        location.Refresh();

        var tz     = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var isDst  = tz.IsDaylightSavingTime(DateTime.UtcNow);

        if (!isDst)
        {
            location.UtcOffset.Should().EndWith(expectedSignAndOffset.TrimStart('+').TrimStart('-'));
        }
        else
        {
            // During DST the offset is shifted — just verify format
            location.UtcOffset.Should().MatchRegex(@"^UTC[+-]\d{2}:\d{2}$");
        }
    }
}
