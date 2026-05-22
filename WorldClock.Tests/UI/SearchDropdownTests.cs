using Avalonia.Headless.XUnit;
using FluentAssertions;
using WorldClock.Data;
using WorldClock.ViewModels;
using Xunit;

namespace WorldClock.Tests.UI;

/// <summary>
/// Avalonia.Headless port of the former FlaUI SearchDropdownTests.
/// Search logic is tested via WorldCitySearchService and TimeTranslatorViewModel.
///
/// MIGRATION TODO: typing-into-textbox and dropdown-visibility tests require a
/// rendered XAML tree — port when HeadlessApp loads App.axaml.
/// </summary>
[Collection("UI Tests")]
public sealed class SearchDropdownTests
{
    // ── Search service availability ───────────────────────────────────────────

    [Fact]
    public void WorldCitySearchService_IsAvailable()
    {
        WorldCitySearchService.All.Should().NotBeNull("the static service must be loadable");
    }

    [Fact]
    public void Search_ByCity_ReturnsResults()
    {
        var results = WorldCitySearchService.Search("London", maxResults: 10).ToList();
        results.Should().NotBeEmpty("London is a well-known city in the database");
    }

    [Fact]
    public void Search_ByCity_ResultsContainMatchingName()
    {
        var results = WorldCitySearchService.Search("Tokyo", maxResults: 10).ToList();
        results.Should().Contain(r => r.City.Contains("Tokyo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(Skip = "UTC+5:30 offset search relies on Windows TZ IDs not present on Linux/macOS")]
    public void Search_ByUtcOffset_ReturnsResults()
    {
        var results = WorldCitySearchService.Search("+5:30", maxResults: 10).ToList();
        results.Should().NotBeEmpty("+5:30 should match Bangalore / Kolkata timezone");
    }

    [Fact]
    public void Search_Empty_ReturnsEmpty()
    {
        var results = WorldCitySearchService.Search(string.Empty, maxResults: 10).ToList();
        results.Should().BeEmpty("empty query should return nothing");
    }

    [Fact]
    public void Search_NonExistentCity_ReturnsEmpty()
    {
        var results = WorldCitySearchService.Search("ZZZNOMATCH999XYZ", maxResults: 5).ToList();
        results.Should().BeEmpty();
    }

    // ── Dropdown shows results in ViewModel ───────────────────────────────────

    [Fact]
    public void TimeTranslatorViewModel_SourceCityMatches_PopulatedByManualAdd()
    {
        var vm = TestViewModel.Fresh();
        var results = WorldCitySearchService.Search("Paris", maxResults: 5).ToList();

        foreach (var r in results)
            vm.Translator.SourceCityMatches.Add(r);

        vm.Translator.SourceCityMatches.Should().NotBeEmpty();
        vm.Translator.HasSourceMatches.Should().BeTrue();
    }

    // ── Pressing Escape / clearing search ────────────────────────────────────

    [Fact]
    public void TimeTranslatorViewModel_ClearingMatches_HidesDropdown()
    {
        var vm = TestViewModel.Fresh();
        vm.Translator.SourceCityMatches.Clear();
        vm.Translator.HasSourceMatches.Should().BeFalse();
    }

    // ── Headless smoke test ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void HeadlessEnvironment_IsAvailable()
    {
        Avalonia.Application.Current.Should().NotBeNull();
    }
}
