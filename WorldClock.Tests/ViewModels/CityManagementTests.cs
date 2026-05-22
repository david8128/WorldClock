using FluentAssertions;
using WorldClock.Models;
using WorldClock.ViewModels;
using WorldClock.Tests;
using Xunit;

namespace WorldClock.Tests.ViewModels;

/// <summary>Tests for add-city and remove-city features in MainViewModel.</summary>
public class CityManagementTests
{
    // ── AddLocation ───────────────────────────────────────────────────────────

    [Fact]
    public void AddLocation_ValidCityAndTz_IncreasesLocationCount()
    {
        var vm     = TestViewModel.Fresh();
        var before = vm.Locations.Count;

        var result = vm.AddLocation("Berlin", "W. Europe Standard Time", "EU Team");

        result.Should().BeTrue();
        vm.Locations.Should().HaveCount(before + 1);
    }

    [Fact]
    public void AddLocation_ValidCity_AppearsInLocations()
    {
        var vm = TestViewModel.Fresh();
        vm.AddLocation("Berlin", "W. Europe Standard Time", "EU Team");

        vm.Locations.Should().Contain(l => l.CityName == "Berlin");
    }

    [Fact]
    public void AddLocation_EmptyCityName_ReturnsFalse()
    {
        var vm     = TestViewModel.Fresh();
        var before = vm.Locations.Count;

        var result = vm.AddLocation("", "W. Europe Standard Time");

        result.Should().BeFalse();
        vm.Locations.Should().HaveCount(before);
    }

    [Fact]
    public void AddLocation_WhitespaceCityName_ReturnsFalse()
    {
        var vm = TestViewModel.Fresh();
        vm.AddLocation("   ", "W. Europe Standard Time").Should().BeFalse();
    }

    [Fact]
    public void AddLocation_InvalidTimezoneId_ReturnsFalse()
    {
        var vm = TestViewModel.Fresh();
        vm.AddLocation("BadCity", "Totally/Invalid").Should().BeFalse();
    }

    [Fact]
    public void AddLocation_DuplicateCityName_ReturnsFalse()
    {
        var vm = TestViewModel.Fresh();
        // "London" city name is already in the default list
        vm.AddLocation("London", "GMT Standard Time").Should().BeFalse();
    }

    [Fact]
    public void AddLocation_CityStateFormat_NormalizesToCityName()
    {
        // "New York, New York" should be stored as "New York" and treated as duplicate of "New York".
        var vm = TestViewModel.Fresh();
        vm.AddLocation("New York, New York", "Eastern Standard Time").Should().BeFalse(); // duplicate of default "New York"
    }

    [Fact]
    public void AddLocation_CityStateFormat_StoredWithoutState()
    {
        // "Seattle, Washington" → stored as "Seattle", not "Seattle, Washington"
        var vm = TestViewModel.Fresh();
        vm.AddLocation("Seattle, Washington", "Pacific Standard Time").Should().BeTrue();
        vm.Locations.Should().Contain(l => l.CityName == "Seattle");
        vm.Locations.Should().NotContain(l => l.CityName == "Seattle, Washington");
    }

    [Fact]
    public void AddLocation_SameTimezone_DifferentCityName_Succeeds()
    {
        // Rome and Milan share "W. Europe Standard Time" — both must be addable.
        var vm = TestViewModel.Fresh();
        vm.AddLocation("Milan", "W. Europe Standard Time").Should().BeTrue();
        vm.AddLocation("Rome",  "W. Europe Standard Time").Should().BeTrue();
        vm.Locations.Should().Contain(l => l.CityName == "Milan");
        vm.Locations.Should().Contain(l => l.CityName == "Rome");
    }

    [Fact]
    public void AddLocation_SetsTeamLabel_Correctly()
    {
        var vm = TestViewModel.Fresh();
        vm.AddLocation("Auckland", "New Zealand Standard Time", "NZ Team");
        var loc = vm.Locations.FirstOrDefault(l => l.CityName == "Auckland");
        loc.Should().NotBeNull();
        loc!.TeamLabel.Should().Be("NZ Team");
    }

    [Fact]
    public void AddLocation_DefaultTeamLabel_IsCustom()
    {
        var vm = TestViewModel.Fresh();
        vm.AddLocation("Auckland", "New Zealand Standard Time");

        var loc = vm.Locations.First(l => l.CityName == "Auckland");
        loc.TeamLabel.Should().Be("Custom");
    }

    [Fact]
    public void AddLocation_NewLocation_HasValidCurrentTime()
    {
        var vm = TestViewModel.Fresh();
        vm.AddLocation("Auckland", "New Zealand Standard Time");

        var loc = vm.Locations.First(l => l.CityName == "Auckland");
        loc.CurrentTime.Should().MatchRegex(@"^\d{2}:\d{2}:\d{2}$");
    }

    [Fact]
    public void AddLocation_NewLocation_HasNonNullAccentBrush()
    {
        var vm = TestViewModel.Fresh();
        vm.AddLocation("Auckland", "New Zealand Standard Time");

        var loc = vm.Locations.First(l => l.CityName == "Auckland");
        loc.AccentBrush.Should().NotBeNull();
    }

    // ── RemoveLocation ────────────────────────────────────────────────────────

    [Fact]
    public void RemoveLocation_ExistingCity_DecreasesCount()
    {
        var vm  = TestViewModel.Fresh();
        var loc = vm.Locations.First(l => l.CityName == "London");
        var before = vm.Locations.Count;

        var result = vm.RemoveLocation(loc);

        result.Should().BeTrue();
        vm.Locations.Should().HaveCount(before - 1);
    }

    [Fact]
    public void RemoveLocation_ExistingCity_RemovedFromList()
    {
        var vm  = TestViewModel.Fresh();
        var loc = vm.Locations.First(l => l.CityName == "Tokyo");

        vm.RemoveLocation(loc);

        vm.Locations.Should().NotContain(l => l.CityName == "Tokyo");
    }

    [Fact]
    public void RemoveLocation_UtcEntry_ReturnsFalse()
    {
        var vm  = TestViewModel.Fresh();
        var utc = vm.Locations.First(l => l.TimeZoneId == "UTC");

        var result = vm.RemoveLocation(utc);

        result.Should().BeFalse("UTC entry must not be removable");
        vm.Locations.Should().Contain(l => l.TimeZoneId == "UTC");
    }

    [Fact]
    public void RemoveLocation_NonExistentLocation_ReturnsFalse()
    {
        var vm = TestViewModel.Fresh();
        var brush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Red);
        var ghost = new ClockLocation
        {
            CityName    = "Ghost",
            CountryFlag = "👻",
            TimeZoneId  = "Pacific Standard Time",
            TeamLabel   = "None",
            AccentBrush = brush
        };

        var result = vm.RemoveLocation(ghost);
        result.Should().BeFalse();
    }

    // ── AllTimeZones ──────────────────────────────────────────────────────────

    [Fact]
    public void AllTimeZones_IsNotEmpty()
    {
        var vm = TestViewModel.Fresh();
        vm.AllTimeZones.Should().NotBeEmpty();
    }

    [Fact]
    public void AllTimeZones_ContainsUtc()
    {
        var vm = TestViewModel.Fresh();
        vm.AllTimeZones.Should().Contain(z => z.Id == "UTC");
    }

    [Fact]
    public void AllTimeZones_AreOrderedByBaseOffset()
    {
        var vm      = TestViewModel.Fresh();
        var offsets = vm.AllTimeZones.Select(z => z.BaseUtcOffset).ToList();
        offsets.Should().BeInAscendingOrder();
    }
}
