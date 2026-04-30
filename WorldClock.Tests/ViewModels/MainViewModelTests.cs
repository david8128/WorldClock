using FluentAssertions;
using WorldClock.ViewModels;
using WorldClock.Tests;
using Xunit;

namespace WorldClock.Tests.ViewModels;

public class MainViewModelTests
{
    // ── Count & structure ─────────────────────────────────────────────────────

    [StaFact]
    public void Locations_HasSevenItems()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().HaveCount(7);
    }

    [StaFact]
    public void Locations_FirstItem_IsUtcGmt()
    {
        var vm = TestViewModel.Fresh();
        var utc = vm.Locations[0];
        utc.CityName.Should().Be("UTC / GMT");
        utc.TimeZoneId.Should().Be("UTC");
    }

    [StaFact]
    public void Locations_ContainsAllExpectedCities()
    {
        var vm    = TestViewModel.Fresh();
        var names = vm.Locations.Select(l => l.CityName).ToList();

        names.Should().Contain("UTC / GMT");
        names.Should().Contain("New York");
        names.Should().Contain("London");
        names.Should().Contain("Madrid");
        names.Should().Contain("Dubai");
        names.Should().Contain("Tokyo");
        names.Should().Contain("Sydney");
    }

    // ── Data integrity ────────────────────────────────────────────────────────

    [StaFact]
    public void Locations_AllHaveNonEmptyCityName()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.CityName.Should().NotBeNullOrWhiteSpace());
    }

    [StaFact]
    public void Locations_AllHaveNonEmptyCountryFlag()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.CountryFlag.Should().NotBeNullOrWhiteSpace());
    }

    [StaFact]
    public void Locations_AllHaveNonEmptyTeamLabel()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.TeamLabel.Should().NotBeNullOrWhiteSpace());
    }

    [StaFact]
    public void Locations_AllHaveNonNullAccentBrush()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.AccentBrush.Should().NotBeNull());
    }

    [StaFact]
    public void Locations_AllHaveValidTimeZoneIds()
    {
        var vm = TestViewModel.Fresh();
        foreach (var loc in vm.Locations)
        {
            var act = () => TimeZoneInfo.FindSystemTimeZoneById(loc.TimeZoneId);
            act.Should().NotThrow($"TimeZoneId '{loc.TimeZoneId}' must be valid on Windows");
        }
    }

    [StaFact]
    public void Locations_CityNames_AreUnique()
    {
        var vm    = TestViewModel.Fresh();
        var names = vm.Locations.Select(l => l.CityName).ToList();
        names.Should().OnlyHaveUniqueItems("each location must have a distinct city name");
    }

    // ── Initial clock state (Add() calls Refresh() internally) ───────────────

    [StaFact]
    public void Locations_AllHaveCurrentTime_AfterConstruction()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.CurrentTime.Should().NotBeNullOrWhiteSpace());
    }

    [StaFact]
    public void Locations_AllHaveCurrentDate_AfterConstruction()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.CurrentDate.Should().NotBeNullOrWhiteSpace());
    }

    [StaFact]
    public void Locations_AllHaveUtcOffset_AfterConstruction()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.UtcOffset.Should().StartWith("UTC"));
    }

    [StaFact]
    public void Locations_CurrentTime_MatchesHhMmSsFormat()
    {
        var vm = TestViewModel.Fresh();
        vm.Locations.Should().AllSatisfy(l =>
            l.CurrentTime.Should().MatchRegex(@"^\d{2}:\d{2}:\d{2}$"));
    }

    // ── Specific city/timezone mapping ────────────────────────────────────────

    [StaTheory]
    [InlineData("New York",  "Eastern Standard Time")]
    [InlineData("London",    "GMT Standard Time")]
    [InlineData("Madrid",    "Romance Standard Time")]
    [InlineData("Dubai",     "Arabian Standard Time")]
    [InlineData("Tokyo",     "Tokyo Standard Time")]
    [InlineData("Sydney",    "AUS Eastern Standard Time")]
    public void Location_TimeZoneId_MapsCorrectlyToCity(string city, string expectedTzId)
    {
        var vm  = TestViewModel.Fresh();
        var loc = vm.Locations.FirstOrDefault(l => l.CityName == city);

        loc.Should().NotBeNull($"city '{city}' should exist in locations");
        loc!.TimeZoneId.Should().Be(expectedTzId);
    }

    // ── UTC time accuracy ─────────────────────────────────────────────────────

    [StaFact]
    public void Locations_UtcEntry_CurrentTime_MatchesUtcNow_WithinTwoSeconds()
    {
        var vm      = TestViewModel.Fresh();
        var utcLoc  = vm.Locations.First(l => l.TimeZoneId == "UTC");
        var before  = DateTime.UtcNow;
        utcLoc.Refresh();
        var after   = DateTime.UtcNow;

        var parsed = TimeSpan.Parse(utcLoc.CurrentTime);
        parsed.Should().BeGreaterThanOrEqualTo(before.TimeOfDay - TimeSpan.FromSeconds(1));
        parsed.Should().BeLessThanOrEqualTo(after.TimeOfDay + TimeSpan.FromSeconds(1));
    }
}
