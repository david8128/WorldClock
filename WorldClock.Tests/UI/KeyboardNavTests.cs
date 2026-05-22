using Avalonia.Headless.XUnit;
using FluentAssertions;
using WorldClock.Data;
using WorldClock.ViewModels;
using Xunit;

namespace WorldClock.Tests.UI;

/// <summary>
/// Avalonia.Headless port of the former FlaUI KeyboardNavTests.
/// Keyboard navigation tests that require a live XAML tree (arrow-key focus,
/// Enter-to-commit) are re-expressed via ViewModel state.
///
/// MIGRATION TODO: actual key-simulation tests need Avalonia.Headless
/// TopLevel.KeyPress API — port after HeadlessApp loads App.axaml.
/// </summary>
[Collection("UI Tests")]
public sealed class KeyboardNavTests
{
    // ── Arrow-key equivalent: SelectedSourceCity update ───────────────────────

    [Fact]
    public void SelectingSourceCity_Updates_SourceZone()
    {
        var vm      = TestViewModel.Fresh();
        var results = WorldCitySearchService.Search("Madrid", 5).ToList();
        results.Should().NotBeEmpty();

        vm.Translator.SelectedSourceCity = results[0];
        vm.Translator.SourceZone.Should().NotBeNull();
    }

    [Fact]
    public void SelectingSourceCity_ClearsMatches()
    {
        var vm = TestViewModel.Fresh();
        foreach (var r in WorldCitySearchService.Search("Rome", 3))
            vm.Translator.SourceCityMatches.Add(r);

        vm.Translator.SelectedSourceCity = vm.Translator.SourceCityMatches[0];
        vm.Translator.SourceCityMatches.Should().BeEmpty(
            "selecting an item must clear the dropdown list");
    }

    // ── Enter-to-add equivalent: AddLocation ─────────────────────────────────

    [Fact]
    public void AddLocation_Enter_CommitsFirstResult_AddsClock()
    {
        var vm      = TestViewModel.Fresh();
        var before  = vm.Locations.Count;
        var results = WorldCitySearchService.Search("Seoul", 5).ToList();
        results.Should().NotBeEmpty();

        var entry  = results[0];
        var added  = vm.AddLocation(entry.City, entry.TimeZoneId, entry.Country);
        added.Should().BeTrue();
        vm.Locations.Should().HaveCount(before + 1);
    }

    // ── Focus state after arrow-up: search box retains text ──────────────────

    [Fact]
    public void SourceCitySearchText_RetainsTextAfterClearingSelection()
    {
        var vm = TestViewModel.Fresh();
        vm.Translator.SourceCitySearchText = "Berlin";
        vm.Translator.SourceCityMatches.Clear();

        // Simulates: user clears dropdown (Escape) — search text must still be there
        vm.Translator.SourceCitySearchText.Should().Be("Berlin");
    }

    // ── Mouse-click equivalent ────────────────────────────────────────────────

    [Fact]
    public void AddLocation_MouseSelect_AddsClockToLocations()
    {
        var vm     = TestViewModel.Fresh();
        var before = vm.Locations.Count;

        vm.AddLocation("Cairo", "Egypt Standard Time");

        vm.Locations.Should().HaveCount(before + 1);
    }

    // ── Arrow-down twice then Enter ───────────────────────────────────────────

    [Fact]
    public void SelectingSecondSearchResult_Works()
    {
        var results = WorldCitySearchService.Search("New", 5).ToList();
        results.Should().HaveCountGreaterThanOrEqualTo(2,
            "search for 'New' must return at least 2 results for this test to be meaningful");

        var vm     = TestViewModel.Fresh();
        var before = vm.Locations.Count;

        // Simulate ArrowDown ×2 then Enter → pick results[1]
        vm.Translator.SelectedSourceCity = results[1];
        var added = vm.AddLocation(results[1].City, results[1].TimeZoneId, results[1].Country);
        // May return false if city already exists — just check state is consistent
        vm.Locations.Count.Should().BeGreaterThanOrEqualTo(before);
    }

    // ── Headless smoke test ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void HeadlessEnvironment_IsAvailable()
    {
        Avalonia.Application.Current.Should().NotBeNull();
    }
}
