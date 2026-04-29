using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using FluentAssertions;
using Xunit;

namespace WorldClock.Tests.UI;

/// <summary>
/// UI automation tests that verify the global city search bar behaviour:
/// — typing text triggers the dropdown after the 400 ms idle timer fires
/// — the dropdown ListBox becomes visible and contains matching city rows
/// — selecting a result adds a new clock card and clears the search box
/// — pressing Escape hides the dropdown
/// — pressing Enter triggers the search immediately (without waiting for the timer)
///
/// Root cause this test catches:
///   The search dropdown is a Canvas overlay (not a Popup).
///   If positioning fails (TransformToAncestor returns wrong coords) or the
///   ListBox background is transparent, the Border renders at 0,0 or is invisible.
///   These tests fail deterministically in that case, pinning the exact behaviour
///   expected by search.feature.
/// </summary>
[Collection("UI Tests")]
public sealed class SearchDropdownTests : IDisposable
{
    private static readonly string AppExePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WorldClock", "bin", "Release", "net8.0-windows", "WorldClock.exe"));

    private static readonly TimeSpan StartupDelay    = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TypeDelay        = TimeSpan.FromMilliseconds(100);
    // Must exceed the 400 ms DispatcherTimer idle interval
    private static readonly TimeSpan SearchIdleDelay  = TimeSpan.FromMilliseconds(700);

    private readonly Application?   _app;
    private readonly UIA3Automation _automation;
    private readonly bool           _appAvailable;

    public SearchDropdownTests()
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
        try { _app?.Close(); } catch { /* ignore on teardown */ }
        _automation.Dispose();
    }

    // ── Scenario: search box is present and focusable ─────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Search_TextBox_IsPresentAndEnabled()
    {
        SkipIfNoApp();
        var searchBox = FindSearchBox();
        searchBox.Should().NotBeNull("the add-city search TextBox must be in the UI tree");
        searchBox!.IsEnabled.Should().BeTrue("the search box must be interactable");
    }

    // ── Scenario: typing shows the dropdown after idle timer ──────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Search_TypingCityName_ShowsDropdownAfterIdleDelay()
    {
        SkipIfNoApp();
        var window    = GetWindow();
        var searchBox = FindSearchBox();
        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        // Type a well-known city that exists in every data set
        searchBox.AsTextBox().Enter("London");
        Thread.Sleep(TypeDelay);

        // Wait for the 400 ms idle timer to fire + a small buffer
        Thread.Sleep(SearchIdleDelay);

        // The Canvas-hosted dropdown (GlobalCitySearchList) must now be visible.
        // FindDropdownBorder returns non-null only when the list is on-screen AND
        // has items — a null result means the TransformToAncestor bug is still present.
        var dropdown = FindDropdownBorder(window);
        dropdown.Should().NotBeNull(
            "the search dropdown must be on-screen with results after the idle timer fires — " +
            "null means the Canvas overlay is mispositioned (TransformToAncestor bug) " +
            "or the ListBox background is still transparent");
    }

    // ── Scenario: dropdown contains matching city rows ────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Search_TypingCityName_DropdownContainsMatchingRows()
    {
        SkipIfNoApp();
        var window    = GetWindow();
        var searchBox = FindSearchBox();
        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        searchBox.AsTextBox().Enter("Tokyo");
        Thread.Sleep(SearchIdleDelay);

        // At least one ListBoxItem with "Tokyo" in its text must be visible
        var allTexts = GetVisibleTextValues(window);
        allTexts.Should().Contain(t => t.Contains("Tokyo"),
            "the dropdown must show a result row containing 'Tokyo'");
    }

    // ── Scenario: pressing Enter triggers search immediately ──────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Search_PressEnter_ShowsDropdownWithoutWaitingForTimer()
    {
        SkipIfNoApp();
        var window    = GetWindow();
        var searchBox = FindSearchBox();
        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        searchBox.AsTextBox().Enter("Paris");
        Thread.Sleep(TypeDelay); // much less than the 400 ms timer

        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Thread.Sleep(300); // allow UI to update

        var allTexts = GetVisibleTextValues(window);
        allTexts.Should().Contain(t => t.Contains("Paris"),
            "pressing Enter must trigger the search immediately without waiting for the idle timer");
    }

    // ── Scenario: pressing Escape hides the dropdown ──────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Search_PressEscape_HidesDropdown()
    {
        SkipIfNoApp();
        var window    = GetWindow();
        var searchBox = FindSearchBox();
        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        searchBox.AsTextBox().Enter("Berlin");
        Thread.Sleep(SearchIdleDelay);

        // Confirm it was visible first
        var dropdown = FindDropdownBorder(window);
        dropdown.Should().NotBeNull("dropdown must be visible before pressing Escape");

        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(200);

        // After Escape the Border should either be gone from the tree or off-screen
        var afterEscape = FindDropdownBorder(window);
        var isHidden = afterEscape == null || afterEscape.IsOffscreen;
        isHidden.Should().BeTrue("pressing Escape must hide the search dropdown");
    }

    // ── Scenario: selecting a result adds a clock card ────────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Search_SelectingResult_AddsClockCard()
    {
        SkipIfNoApp();
        var window    = GetWindow();
        var searchBox = FindSearchBox();
        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        // Search for a city that is NOT already in the default list
        const string testCity = "Bogota";
        searchBox.AsTextBox().Enter(testCity);
        Thread.Sleep(SearchIdleDelay);

        // Find and click the first matching ListBoxItem
        var listItems = window.FindAllDescendants(x =>
            x.ByControlType(ControlType.ListItem));

        var match = listItems.FirstOrDefault(i =>
            (i.Name ?? string.Empty).Contains(testCity, StringComparison.OrdinalIgnoreCase));

        match.Should().NotBeNull($"a list item containing '{testCity}' must be in the dropdown");
        match!.Click();
        Thread.Sleep(500); // allow AddLocation + UI refresh

        // The clock card should now be on screen
        var allTexts = GetVisibleTextValues(window);
        allTexts.Should().Contain(t => t.Contains("Bogot"),
            $"a clock card for '{testCity}' must appear after selecting it from the dropdown");

        // Search box should be cleared
        var boxText = searchBox.AsTextBox().Text;
        boxText.Should().BeNullOrEmpty("the search box must be cleared after a selection");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Window GetWindow() =>
        _app!.GetMainWindow(_automation, TimeSpan.FromSeconds(5));

    /// <summary>Finds the GlobalCitySearchBox by its AutomationProperties.Name.</summary>
    private AutomationElement? FindSearchBox()
    {
        var window = GetWindow();
        return window.FindFirstDescendant(x =>
            x.ByControlType(ControlType.Edit)
             .And(x.ByName("AddCitySearch")));
    }

    /// <summary>
    /// Finds the GlobalSearchDropdown by locating the GlobalCitySearchList ListBox
    /// via its AutomationProperties.Name. Returns null when the list is not in the tree
    /// or has no items (i.e. the dropdown is hidden).
    /// </summary>
    private static AutomationElement? FindDropdownBorder(AutomationElement root)
    {
        // The ListBox is named "GlobalCitySearchList" via AutomationProperties.Name.
        // When the dropdown is hidden (Visibility=Collapsed) it is still in the UIA
        // tree but IsOffscreen=true. We only return it when it is actually on-screen
        // AND has at least one child item (results loaded).
        var list = root.FindFirstDescendant(x =>
            x.ByControlType(ControlType.List)
             .And(x.ByName("GlobalCitySearchList")));

        if (list == null || list.IsOffscreen) return null;

        return list.FindFirstChild(x => x.ByControlType(ControlType.ListItem)) != null
            ? list
            : null;
    }

    private static List<string> GetVisibleTextValues(AutomationElement root) =>
        root.FindAllDescendants(x => x.ByControlType(ControlType.Text))
            .Select(e => e.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

    private void SkipIfNoApp()
    {
        if (!_appAvailable)
            throw new InvalidOperationException(
                $"[SKIP] WorldClock.exe not found at: {AppExePath}\n" +
                "Run: dotnet build WorldClock/WorldClock.csproj -c Release");
    }
}
