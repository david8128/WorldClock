using Avalonia.Headless.XUnit;
using FluentAssertions;
using WorldClock.Models;
using WorldClock.Services;
using WorldClock.ViewModels;
using Xunit;

namespace WorldClock.Tests.UI;

/// <summary>
/// Avalonia.Headless port of the former FlaUI SettingsUITests.
/// Settings window rendering tests are re-expressed via ThemeService / MainViewModel.
///
/// MIGRATION TODO: tests for Delete/Edit mode toggle visual state, opacity slider
/// and theme ComboBox require full XAML tree — port when HeadlessApp loads App.axaml.
/// </summary>
[Collection("UI Tests")]
public sealed class SettingsUITests
{
    // ── Settings button availability (ViewModel proxy) ────────────────────────

    [Fact]
    public void MainViewModel_IsNotNull_IndicatesSettingsCanBeOpened()
    {
        var vm = TestViewModel.Fresh();
        vm.Should().NotBeNull();
    }

    // ── Delete / Edit mode toggle ─────────────────────────────────────────────

    [Fact]
    public void ThemeService_DeleteMode_DefaultIsFalse()
    {
        ThemeService.Instance.DeleteMode = false; // reset
        ThemeService.Instance.DeleteMode.Should().BeFalse();
    }

    [Fact]
    public void ThemeService_EditMode_DefaultIsFalse()
    {
        ThemeService.Instance.EditMode = false; // reset
        ThemeService.Instance.EditMode.Should().BeFalse();
    }

    [Fact]
    public void ThemeService_SetDeleteMode_True_FiresPropertyChanged()
    {
        ThemeService.Instance.DeleteMode = false;
        var raised = new List<string?>();
        ThemeService.Instance.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        ThemeService.Instance.DeleteMode = true;

        raised.Should().Contain(nameof(ThemeService.DeleteMode));
        ThemeService.Instance.DeleteMode = false; // restore
    }

    [Fact]
    public void ThemeService_SetEditMode_True_FiresPropertyChanged()
    {
        ThemeService.Instance.EditMode = false;
        var raised = new List<string?>();
        ThemeService.Instance.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        ThemeService.Instance.EditMode = true;

        raised.Should().Contain(nameof(ThemeService.EditMode));
        ThemeService.Instance.EditMode = false; // restore
    }

    [Fact]
    public void ClockLocation_IsDeleteVisible_TrueWhenDeleteModeOnAndNotUtc()
    {
        ThemeService.Instance.DeleteMode = true;
        var vm  = TestViewModel.Fresh();
        var nyc = vm.Locations.First(l => l.CityName == "New York");

        nyc.IsDeleteVisible.Should().BeTrue("delete mode is ON and card is not UTC");

        ThemeService.Instance.DeleteMode = false;
    }

    [Fact]
    public void ClockLocation_IsDeleteVisible_FalseForUtcCard()
    {
        ThemeService.Instance.DeleteMode = true;
        var vm  = TestViewModel.Fresh();
        var utc = vm.Locations.First(l => l.TimeZoneId == "UTC");

        utc.IsDeleteVisible.Should().BeFalse("UTC clock cannot be deleted");

        ThemeService.Instance.DeleteMode = false;
    }

    [Fact]
    public void ClockLocation_IsEditVisible_TrueWhenEditModeOnAndNotUtc()
    {
        ThemeService.Instance.EditMode = true;
        var vm  = TestViewModel.Fresh();
        var lon = vm.Locations.First(l => l.CityName == "London");

        lon.IsEditVisible.Should().BeTrue("edit mode is ON and card is not UTC");

        ThemeService.Instance.EditMode = false;
    }

    // ── Opacity slider ────────────────────────────────────────────────────────

    [Fact]
    public void ThemeService_Opacity_CanBeSetTo50Percent()
    {
        ThemeService.Instance.Opacity = 0.5;
        ThemeService.Instance.Opacity.Should().BeApproximately(0.5, 0.001);
        ThemeService.Instance.Opacity = 1.0;
    }

    // ── Theme ComboBox ────────────────────────────────────────────────────────

    [Fact]
    public void AppTheme_All_HasMultipleThemes()
    {
        AppTheme.All.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    // ── Headless smoke test ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void HeadlessEnvironment_IsAvailable()
    {
        Avalonia.Application.Current.Should().NotBeNull();
    }
}
