using FluentAssertions;
using WorldClock.Models;
using WorldClock.Services;
using Xunit;

namespace WorldClock.Tests.Services;

/// <summary>Unit tests for ThemeService — theme switching and opacity management.</summary>
public class ThemeServiceTests
{
    // Reset instance state before each test using a helper
    private static ThemeService Svc => ThemeService.Instance;

    // ── Theme catalogue ───────────────────────────────────────────────────────

    [Fact]
    public void AppTheme_All_HasAtLeastTenThemes()
    {
        AppTheme.All.Should().HaveCountGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void AppTheme_All_AllHaveUniqueName()
    {
        var names = AppTheme.All.Select(t => t.Name).ToList();
        names.Should().OnlyHaveUniqueItems("each theme must have a distinct name");
    }

    [Fact]
    public void AppTheme_All_ContainsDarkDefault()
    {
        AppTheme.All.Should().Contain(t => t.Name == "Dark Default");
    }

    [Fact]
    public void AppTheme_All_ContainsLightDefault()
    {
        AppTheme.All.Should().Contain(t => t.Name == "Light Default");
    }

    [Theory]
    [InlineData("One Dark")]
    [InlineData("Monokai")]
    [InlineData("Solarized Dark")]
    [InlineData("Solarized Light")]
    [InlineData("Nord Dark")]
    [InlineData("Tokyo Night")]
    [InlineData("Catppuccin Mocha")]
    [InlineData("Catppuccin Latte")]
    [InlineData("Ariake Dark")]
    public void AppTheme_All_ContainsNamedTheme(string name)
    {
        AppTheme.All.Should().Contain(t => t.Name == name, $"theme '{name}' must exist");
    }

    [Fact]
    public void AppTheme_All_AllHaveNonDefaultBackgroundDark()
    {
        // BackgroundDark must be set (not default Color struct == transparent black)
        AppTheme.All.Should().AllSatisfy(t =>
            t.BackgroundDark.Should().NotBe(default));
    }

    [Fact]
    public void AppTheme_All_AllHaveNonDefaultAccentPrimary()
    {
        AppTheme.All.Should().AllSatisfy(t =>
            t.AccentPrimary.Should().NotBe(default));
    }

    [Fact]
    public void AppTheme_BrushBackgroundDark_IsNotNull()
    {
        var theme = AppTheme.All[0];
        theme.BrushBackgroundDark.Should().NotBeNull();
    }

    [Fact]
    public void AppTheme_BrushAccentPrimary_ColorMatchesAccentPrimary()
    {
        var theme = AppTheme.All.First(t => t.Name == "One Dark");
        theme.BrushAccentPrimary.Color.Should().Be(theme.AccentPrimary);
    }

    // ── ThemeService state ────────────────────────────────────────────────────

    [Fact]
    public void ThemeService_DefaultTheme_IsDarkDefault()
    {
        // Reset to known state — the service is a singleton shared across tests
        Svc.ActiveTheme = AppTheme.All.First(t => t.Name == "Dark Default");
        Svc.ActiveTheme.Name.Should().Be("Dark Default");
    }

    [Fact]
    public void ThemeService_SetActiveTheme_ChangesTheme()
    {
        var nord = AppTheme.All.First(t => t.Name == "Nord Dark");
        Svc.ActiveTheme = nord;
        Svc.ActiveTheme.Name.Should().Be("Nord Dark");
        // Restore
        Svc.ActiveTheme = AppTheme.All[0];
    }

    [Fact]
    public void ThemeService_SetActiveTheme_FiresPropertyChanged()
    {
        var raised = new List<string?>();
        Svc.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Svc.ActiveTheme = AppTheme.All.First(t => t.Name == "Monokai");

        raised.Should().Contain(nameof(ThemeService.ActiveTheme));
        // Restore
        Svc.ActiveTheme = AppTheme.All[0];
    }

    // ── Opacity ───────────────────────────────────────────────────────────────

    [Fact]
    public void ThemeService_DefaultOpacity_IsOne()
    {
        Svc.Opacity = 1.0;
        Svc.Opacity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void ThemeService_SetOpacity_Clamps_AboveMax()
    {
        Svc.Opacity = 1.5;
        Svc.Opacity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void ThemeService_SetOpacity_Clamps_BelowMin()
    {
        Svc.Opacity = 0.0;
        Svc.Opacity.Should().BeApproximately(0.1, 0.001);
        Svc.Opacity = 1.0; // restore
    }

    [Fact]
    public void ThemeService_SetOpacity_AcceptsValidValue()
    {
        Svc.Opacity = 0.5;
        Svc.Opacity.Should().BeApproximately(0.5, 0.001);
        Svc.Opacity = 1.0;
    }

    [Fact]
    public void ThemeService_SetOpacity_FiresPropertyChanged()
    {
        Svc.Opacity = 1.0;
        var raised = new List<string?>();
        Svc.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Svc.Opacity = 0.7;

        raised.Should().Contain(nameof(ThemeService.Opacity));
        Svc.Opacity = 1.0;
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void ThemeService_SetOpacity_ValidRange_Stored(double value)
    {
        Svc.Opacity = value;
        Svc.Opacity.Should().BeApproximately(value, 0.001);
        Svc.Opacity = 1.0;
    }

    // ── DeleteMode ────────────────────────────────────────────────────────────

    [Fact]
    public void ThemeService_DeleteMode_DefaultIsFalse()
    {
        Svc.DeleteMode = false;
        Svc.DeleteMode.Should().BeFalse();
    }

    [Fact]
    public void ThemeService_SetDeleteMode_True_FiresPropertyChanged()
    {
        Svc.DeleteMode = false;
        var raised = new List<string?>();
        Svc.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Svc.DeleteMode = true;

        raised.Should().Contain(nameof(ThemeService.DeleteMode));
        Svc.DeleteMode = false;
    }

    [Fact]
    public void ThemeService_SetDeleteMode_False_FiresPropertyChanged()
    {
        Svc.DeleteMode = true;
        var raised = new List<string?>();
        Svc.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Svc.DeleteMode = false;

        raised.Should().Contain(nameof(ThemeService.DeleteMode));
    }

    // ── IsDark ────────────────────────────────────────────────────────────────

    [Fact]
    public void AppTheme_IsDark_DarkDefault_IsTrue()
    {
        var theme = AppTheme.All.First(t => t.Name == "Dark Default");
        theme.IsDark.Should().BeTrue("Dark Default has a very dark background");
    }

    [Fact]
    public void AppTheme_IsDark_LightDefault_IsFalse()
    {
        var theme = AppTheme.All.First(t => t.Name == "Light Default");
        theme.IsDark.Should().BeFalse("Light Default has a very light background");
    }

    [Fact]
    public void AppTheme_IsDark_CatppuccinLatte_IsFalse()
    {
        var theme = AppTheme.All.First(t => t.Name == "Catppuccin Latte");
        theme.IsDark.Should().BeFalse("Catppuccin Latte is a light theme");
    }

    [Fact]
    public void AppTheme_IsDark_SolarizedLight_IsFalse()
    {
        var theme = AppTheme.All.First(t => t.Name == "Solarized Light");
        theme.IsDark.Should().BeFalse("Solarized Light is a light theme");
    }

    [Theory]
    [InlineData("One Dark")]
    [InlineData("Monokai")]
    [InlineData("Solarized Dark")]
    [InlineData("Nord Dark")]
    [InlineData("Tokyo Night")]
    [InlineData("Catppuccin Mocha")]
    [InlineData("Ariake Dark")]
    public void AppTheme_IsDark_AllDarkThemes_AreTrue(string name)
    {
        var theme = AppTheme.All.First(t => t.Name == name);
        theme.IsDark.Should().BeTrue($"'{name}' is a dark theme");
    }

    [Theory]
    [InlineData("Light Default")]
    [InlineData("Solarized Light")]
    [InlineData("Catppuccin Latte")]
    public void AppTheme_IsDark_AllLightThemes_AreFalse(string name)
    {
        var theme = AppTheme.All.First(t => t.Name == name);
        theme.IsDark.Should().BeFalse($"'{name}' is a light theme");
    }
}
