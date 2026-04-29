using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using FluentAssertions;
using Xunit;

namespace WorldClock.Tests.UI;

/// <summary>
/// End-to-end UI automation tests using FlaUI + UIA3.
/// Tests are skipped gracefully when the compiled exe is not present.
/// Run "dotnet build" on the main project first, then execute these tests.
/// </summary>
[Collection("UI Tests")]
public sealed class WorldClockUITests : IDisposable
{
    // Resolved relative to the test binary output folder.
    // Uses the Release build so running a Debug instance never locks the exe during test builds.
    private static readonly string AppExePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WorldClock", "bin", "Release", "net8.0-windows", "WorldClock.exe"));

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan UpdateDelay  = TimeSpan.FromSeconds(2);

    private readonly Application?    _app;
    private readonly UIA3Automation  _automation;
    private readonly bool            _appAvailable;

    public WorldClockUITests()
    {
        _automation   = new UIA3Automation();
        _appAvailable = File.Exists(AppExePath);

        if (_appAvailable)
        {
            _app = Application.Launch(AppExePath);
            Thread.Sleep(StartupDelay); // allow WPF to initialise
        }
    }

    public void Dispose()
    {
        try { _app?.Close(); } catch { /* ignore on teardown */ }
        _automation.Dispose();
    }

    // ── Window ────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void App_MainWindow_HasCorrectTitle()
    {
        SkipIfNoApp();
        var window = GetWindow();
        window.Title.Should().Be("World Clock");
    }

    [Fact]
    [Trait("Category", "UI")]
    public void App_MainWindow_IsVisible()
    {
        SkipIfNoApp();
        var window = GetWindow();
        window.IsOffscreen.Should().BeFalse("main window must be visible");
    }

    // ── UTC banner ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void App_UtcBanner_ShowsUtcOffset()
    {
        SkipIfNoApp();
        var window   = GetWindow();
        var allTexts = GetAllTextValues(window);

        allTexts.Should().Contain(t => t.Contains("UTC+00:00"),
            "the UTC banner must display 'UTC+00:00'");
    }

    [Fact]
    [Trait("Category", "UI")]
    public void App_UtcBanner_ShowsTimeInHhMmSsFormat()
    {
        SkipIfNoApp();
        var window   = GetWindow();
        var allTexts = GetAllTextValues(window);

        allTexts.Should().Contain(t =>
            System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d{2}:\d{2}:\d{2}$"),
            "at least one element should display a time in HH:mm:ss format");
    }

    // ── Location cards ────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void App_Cards_ShowAllExpectedCities()
    {
        SkipIfNoApp();
        var window   = GetWindow();
        var allTexts = GetAllTextValues(window);

        var expectedCities = new[]
        {
            "New York", "London", "Madrid", "Dubai", "Tokyo", "Sydney"
        };

        foreach (var city in expectedCities)
        {
            allTexts.Should().Contain(t => t.Contains(city),
                $"a card for '{city}' must be present");
        }
    }

    [Fact]
    [Trait("Category", "UI")]
    public void App_Cards_ShowTeamLabels()
    {
        SkipIfNoApp();
        var window   = GetWindow();
        var allTexts = GetAllTextValues(window);

        var expectedLabels = new[]
        {
            "Americas Team", "EMEA Team", "APAC Team", "Middle East Team"
        };

        foreach (var label in expectedLabels)
        {
            allTexts.Should().Contain(t => t.Contains(label),
                $"team label '{label}' must be visible in the UI");
        }
    }

    [Fact]
    [Trait("Category", "UI")]
    public void App_Cards_ShowUtcOffsetForEachLocation()
    {
        SkipIfNoApp();
        var window   = GetWindow();
        var allTexts = GetAllTextValues(window);

        // At least 6 distinct UTC offset labels should be visible (one per card)
        var offsetMatches = allTexts
            .Where(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^UTC[+-]\d{2}:\d{2}$"))
            .ToList();

        offsetMatches.Should().HaveCountGreaterThanOrEqualTo(6,
            "each location card must show its UTC offset");
    }

    // ── Live update ───────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void App_Time_UpdatesAutomatically_AfterTwoSeconds()
    {
        SkipIfNoApp();
        var window = GetWindow();

        var timeBefore = GetFirstTimeString(window);
        timeBefore.Should().NotBeNull("a time in HH:mm:ss format must be visible before waiting");

        Thread.Sleep(UpdateDelay);

        var timeAfter = GetFirstTimeString(window);
        timeAfter.Should().NotBeNull("a time in HH:mm:ss format must still be visible after waiting");
        timeAfter.Should().NotBe(timeBefore,
            "the displayed time must tick forward after 2 seconds");
    }

    // ── Footer ────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void App_Footer_IsVisible()
    {
        SkipIfNoApp();
        var window   = GetWindow();
        var allTexts = GetAllTextValues(window);

        allTexts.Should().Contain(t => t.Contains("World Clock") && t.Contains("2026"),
            "the footer copyright line must be visible");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Window GetWindow() =>
        _app!.GetMainWindow(_automation, TimeSpan.FromSeconds(5));

    private static List<string> GetAllTextValues(AutomationElement root)
    {
        return root
            .FindAllDescendants(x => x.ByControlType(ControlType.Text))
            .Select(e => e.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    private static string? GetFirstTimeString(AutomationElement root)
    {
        return GetAllTextValues(root)
            .FirstOrDefault(t =>
                System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d{2}:\d{2}:\d{2}$"));
    }

    private void SkipIfNoApp()
    {
        if (!_appAvailable)
        {
            throw new InvalidOperationException(
                $"[SKIP] WorldClock.exe not found — build the main project first. " +
                $"Expected path: {AppExePath}");
        }
    }
}
