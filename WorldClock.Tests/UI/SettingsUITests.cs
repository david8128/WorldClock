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
/// UI automation tests for the Settings window, city management, and transparency.
/// Requires WorldClock.exe to be built first (dotnet build WorldClock).
/// </summary>
[Collection("UI Tests")]
public sealed class SettingsUITests : IDisposable
{
    private static readonly string AppExePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WorldClock", "bin", "Debug", "net8.0-windows", "WorldClock.exe"));

    private static readonly TimeSpan StartupDelay  = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ActionDelay   = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan SettingsDelay = TimeSpan.FromSeconds(1);

    private readonly Application?   _app;
    private readonly UIA3Automation _automation;
    private readonly bool           _appAvailable;

    public SettingsUITests()
    {
        _automation   = new UIA3Automation();
        _appAvailable = File.Exists(AppExePath);

        if (_appAvailable)
        {
            _app = Application.Launch(AppExePath);
            Thread.Sleep(StartupDelay);
        }
    }

    public void Dispose()
    {
        try { _app?.Close(); } catch { }
        _automation.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Window GetWindow() =>
        _app!.GetMainWindow(_automation, TimeSpan.FromSeconds(5));

    private void SkipIfNoApp()
    {
        if (!_appAvailable)
            throw new InvalidOperationException(
                $"[SKIP] WorldClock.exe not found — build the main project first. Path: {AppExePath}");
    }

    private static AutomationElement? FindByName(AutomationElement root, string name) =>
        root.FindFirstDescendant(x => x.ByName(name));

    private static List<string> AllTexts(AutomationElement root) =>
        root.FindAllDescendants(x => x.ByControlType(ControlType.Text))
            .Select(e => e.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

    // ── Settings button presence ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void MainWindow_SettingsButton_IsPresent()
    {
        SkipIfNoApp();
        var window = GetWindow();
        var btn    = FindByName(window, "SettingsButton");
        btn.Should().NotBeNull("the ⚙ settings button must be present in the header");
    }

    // ── Open settings window ──────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsButton_Click_OpensSettingsWindow()
    {
        SkipIfNoApp();
        var window = GetWindow();

        var settingsBtn = FindByName(window, "SettingsButton");
        settingsBtn.Should().NotBeNull();
        settingsBtn!.AsButton().Invoke();

        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .FirstOrDefault(w => w.Title == "Settings");

        settingsWin.Should().NotBeNull("clicking the gear icon must open the Settings window");
        settingsWin?.Close();
    }

    // ── Add city textboxes ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_HasCityNameTextBox()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var cityBox = FindByName(settingsWin, "CityName");
        cityBox.Should().NotBeNull("the settings window must have a CityName text box");
        settingsWin.Close();
    }

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_HasTimezoneComboBox()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var combo = FindByName(settingsWin, "TimezoneSelector");
        combo.Should().NotBeNull("the settings window must have a timezone combo box");
        settingsWin.Close();
    }

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_HasTeamLabelTextBox()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var teamBox = FindByName(settingsWin, "TeamLabel");
        teamBox.Should().NotBeNull("the settings window must have a TeamLabel text box");
        settingsWin.Close();
    }

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_HasAddCityButton()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var addBtn = FindByName(settingsWin, "AddCityButton");
        addBtn.Should().NotBeNull("the settings window must have an Add City button");
        settingsWin.Close();
    }

    // ── Delete-mode toggle ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_HasDeleteModeToggle()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var toggle = FindByName(settingsWin, "DeleteModeToggle");
        toggle.Should().NotBeNull("the settings window must have a Delete Mode toggle button");
        settingsWin.Close();
    }

    [Fact]
    [Trait("Category", "UI")]
    public void DeleteModeToggle_WhenChecked_ShowsDeleteButtonsOnCards()
    {
        SkipIfNoApp();
        var window = GetWindow();

        // Verify no delete buttons initially
        var deleteBtns = window.FindAllDescendants(x => x.ByName("DeleteCityButton"));
        deleteBtns.Should().BeEmpty("delete buttons must not show before delete mode is on");

        // Open settings and enable delete mode
        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        FindByName(settingsWin, "DeleteModeToggle")!.AsToggleButton().Toggle();
        Thread.Sleep(ActionDelay);
        settingsWin.Close();
        Thread.Sleep(ActionDelay);

        // Delete buttons should now be visible
        window = GetWindow();
        var deleteButtonsAfter = window.FindAllDescendants(x => x.ByName("DeleteCityButton"));
        deleteButtonsAfter.Should().NotBeEmpty("delete buttons must appear after enabling delete mode");

        // Clean up: turn off delete mode
        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);
        var sw2 = _app!.GetAllTopLevelWindows(_automation).First(w => w.Title == "Settings");
        FindByName(sw2, "DeleteModeToggle")!.AsToggleButton().Toggle();
        Thread.Sleep(ActionDelay);
        sw2.Close();
    }

    // ── Transparency slider ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_HasOpacitySlider()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var sliders = settingsWin.FindAllDescendants(x => x.ByControlType(ControlType.Slider));
        sliders.Should().NotBeEmpty("the settings window must contain an opacity slider");
        settingsWin.Close();
    }

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_OpacitySlider_ShowsPercentageLabel()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var texts = AllTexts(settingsWin);
        texts.Should().Contain(t => t.EndsWith("%"),
            "the settings window must show a transparency percentage label");

        settingsWin.Close();
    }

    // ── Theme selector ────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void SettingsWindow_HasThemeComboBox_WithMultipleItems()
    {
        SkipIfNoApp();
        var window = GetWindow();

        FindByName(window, "SettingsButton")!.AsButton().Invoke();
        Thread.Sleep(SettingsDelay);

        var settingsWin = _app!.GetAllTopLevelWindows(_automation)
            .First(w => w.Title == "Settings");

        var combos = settingsWin.FindAllDescendants(x => x.ByControlType(ControlType.ComboBox));
        combos.Should().NotBeEmpty("the settings window must contain at least one ComboBox");

        // The first combo is the theme selector
        var themeCombo = combos[0].AsComboBox();
        themeCombo.Expand();
        Thread.Sleep(ActionDelay);

        var items = themeCombo.Items;
        items.Should().HaveCountGreaterThanOrEqualTo(5,
            "the theme selector should have at least 5 themes");

        settingsWin.Close();
    }
}
