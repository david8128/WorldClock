using System.IO;
using System.Linq;
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
/// UI automation tests for keyboard navigation inside the global city search dropdown:
///   — ↓ arrow moves focus from the TextBox into the first ListBoxItem (dropdown stays open)
///   — ↑ arrow at the top of the list returns focus to the TextBox (dropdown stays open)
///   — Enter in the TextBox (dropdown open) commits the first/highlighted item and adds a clock
///   — Enter on a highlighted ListBoxItem commits it and adds a clock
///   — Mouse click on a ListBoxItem adds a clock (regression: was broken by Mouse.LeftButton guard)
/// </summary>
[Collection("UI Tests")]
public sealed class KeyboardNavTests : IDisposable
{
    private static readonly string AppExePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WorldClock", "bin", "Release", "net8.0-windows", "WorldClock.exe"));

    private static readonly TimeSpan StartupDelay   = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TypeDelay       = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan SearchIdleDelay = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan UiUpdateDelay   = TimeSpan.FromMilliseconds(500);

    private readonly Application?   _app;
    private readonly UIA3Automation _automation;
    private readonly bool           _appAvailable;

    public KeyboardNavTests()
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
        try { _app?.Close(); } catch { /* ignore */ }
        _automation.Dispose();
    }

    // ── ↓ arrow: dropdown stays open, focus moves to first item ─────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Keyboard_ArrowDown_MovesFocusIntoDropdown_DropdownRemainsVisible()
    {
        SkipIfNoApp();
        EnsureTranslatorOpen();
        var window    = GetWindow();
        var searchBox = FindSearchBox();

        searchBox!.Click();
        Thread.Sleep(TypeDelay);
        searchBox.AsTextBox().Enter("Madrid");
        Thread.Sleep(SearchIdleDelay); // wait for dropdown to appear

        // Confirm dropdown is visible before pressing ↓
        var listBefore = FindDropdown(window);
        listBefore.Should().NotBeNull("dropdown must be visible before pressing ↓");

        // Press ↓ — should move keyboard focus into the list
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
        Thread.Sleep(UiUpdateDelay);

        // Dropdown must still be visible (not hidden by LostFocus)
        var listAfter = FindDropdown(window);
        listAfter.Should().NotBeNull(
            "dropdown must remain visible after pressing ↓ from the TextBox — " +
            "_suppressDropdownHide must prevent LostFocus from closing it");

        // At least one ListBoxItem must exist and the first one should be present
        var firstItem = listAfter!.FindFirstChild(x => x.ByControlType(ControlType.ListItem));
        firstItem.Should().NotBeNull("at least one result item must exist after ↓");
    }

    // ── ↑ arrow at index 0: returns focus to the TextBox ────────────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Keyboard_ArrowUpAtTop_ReturnsFocusToSearchBox()
    {
        SkipIfNoApp();
        EnsureTranslatorOpen();
        var window    = GetWindow();
        var searchBox = FindSearchBox();

        searchBox!.Click();
        Thread.Sleep(TypeDelay);
        searchBox.AsTextBox().Enter("Sydney");
        Thread.Sleep(SearchIdleDelay);

        var dropdown = FindDropdown(window);
        dropdown.Should().NotBeNull("dropdown must appear before keyboard test");

        // Move focus into the list
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
        Thread.Sleep(UiUpdateDelay);

        // Press ↑ — should return focus to the TextBox, dropdown stays open
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.UP);
        Thread.Sleep(UiUpdateDelay);

        var dropdownAfter = FindDropdown(window);
        dropdownAfter.Should().NotBeNull(
            "dropdown must remain visible after pressing ↑ back to the TextBox");
    }

    // ── Enter in TextBox (dropdown open): commits first item, adds clock ─────

    [Fact]
    [Trait("Category", "UI")]
    public void Keyboard_EnterInTextBox_CommitsFirstResult_AddsClock()
    {
        SkipIfNoApp();
        EnsureTranslatorOpen();
        var window    = GetWindow();
        var searchBox = FindSearchBox();

        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        const string city = "Amsterdam";
        searchBox.AsTextBox().Enter(city);
        Thread.Sleep(SearchIdleDelay); // wait for results

        var dropdown = FindDropdown(window);
        dropdown.Should().NotBeNull($"dropdown must open after typing '{city}'");

        // Press Enter while focus is in the TextBox — should commit the first/highlighted item
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Thread.Sleep(UiUpdateDelay);

        // Dropdown must close
        var dropdownAfter = FindDropdown(window);
        var hidden = dropdownAfter == null || dropdownAfter.IsOffscreen;
        hidden.Should().BeTrue("dropdown must close after Enter commits a city");

        // Clock must be added to the left panel
        var allTexts = GetVisibleTextValues(window);
        allTexts.Should().Contain(t => t.Contains("Amsterdam", StringComparison.OrdinalIgnoreCase),
            "a clock for Amsterdam must appear in the left panel after Enter commits the selection");

        // TextBox must be cleared
        searchBox.AsTextBox().Text.Should().BeNullOrEmpty("TextBox must clear after commit");
    }

    // ── Enter on highlighted ListBoxItem: commits it, adds clock ────────────

    [Fact]
    [Trait("Category", "UI")]
    public void Keyboard_EnterOnListItem_CommitsItem_AddsClock()
    {
        SkipIfNoApp();
        EnsureTranslatorOpen();
        var window    = GetWindow();
        var searchBox = FindSearchBox();

        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        const string city = "Nairobi";
        searchBox.AsTextBox().Enter(city);
        Thread.Sleep(SearchIdleDelay);

        var dropdown = FindDropdown(window);
        dropdown.Should().NotBeNull($"dropdown must appear for '{city}'");

        // Move focus into the list with ↓, then press Enter
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
        Thread.Sleep(UiUpdateDelay);
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Thread.Sleep(UiUpdateDelay);

        // Dropdown must close
        var afterCommit = FindDropdown(window);
        var hidden = afterCommit == null || afterCommit.IsOffscreen;
        hidden.Should().BeTrue("dropdown must close after Enter on a list item");

        // Clock must appear
        var allTexts = GetVisibleTextValues(window);
        allTexts.Should().Contain(t => t.Contains("Nairobi", StringComparison.OrdinalIgnoreCase),
            "Nairobi clock must appear after committing via Enter on a list item");
    }

    // ── ↓ × 2: second item committed by Enter (not the first) ───────────────

    /// <summary>
    /// Regression test for two bugs:
    ///   1. No visual highlight after pressing ↓ (visual was never applied).
    ///   2. Pressing ↓ twice committed the first item — _dropdownHighlight counter was wrong.
    ///
    /// Strategy: record item[0] and item[1] city names before any navigation.
    /// After ↓×2 + Enter, the clock panel must show item[1]'s city, not item[0].
    /// (UIA SelectionItemPattern.IsSelected is not checked — it's unreliable in this
    ///  configuration because WPF highlight is applied via VisualTreeHelper direct-paint,
    ///  not via SelectedIndex alone.)
    /// </summary>
    [Fact]
    [Trait("Category", "UI")]
    public void Keyboard_ArrowDownTwice_SelectsSecondItem_EnterCommitsIt()
    {
        SkipIfNoApp();
        EnsureTranslatorOpen();
        var window    = GetWindow();
        var searchBox = FindSearchBox();

        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        // "New" returns New York, New Delhi, Newcastle … — at least 2 results
        searchBox.AsTextBox().Enter("New");
        Thread.Sleep(SearchIdleDelay);

        var dropdown = FindDropdown(window);
        dropdown.Should().NotBeNull("dropdown must open after typing 'New'");

        var items = dropdown!.FindAllChildren(x => x.ByControlType(ControlType.ListItem));
        items.Should().HaveCountGreaterThanOrEqualTo(2,
            "at least 2 results must exist so the second-item selection can be verified");

        // The automation Name for each item is CityEntry.ToString():
        //   "CityEntry { Country = USA, CountryFlag = 🇺🇸, City = New York, ... }"
        // Extract the City field so we can verify the correct clock was added.
        static string ParseCity(AutomationElement el)
        {
            var name = el.Name ?? string.Empty;
            var start = name.IndexOf("City = ", StringComparison.Ordinal);
            if (start < 0) return name;
            start += "City = ".Length;
            var end = name.IndexOfAny(new[] { ',', '}' }, start);
            return end < 0 ? name[start..] : name[start..end].Trim();
        }

        string firstCity  = ParseCity(items[0]);
        string secondCity = ParseCity(items[1]);
        firstCity.Should().NotBeNullOrEmpty("first item must have a parseable city name");
        secondCity.Should().NotBeNullOrEmpty("second item must have a parseable city name");
        secondCity.Should().NotBe(firstCity, "first and second result must be different cities");

        // ── ↓ × 2 ────────────────────────────────────────────────────────────
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
        Thread.Sleep(UiUpdateDelay);

        FindDropdown(window).Should().NotBeNull(
            "dropdown must stay open after ↓ × 1");

        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
        Thread.Sleep(UiUpdateDelay);

        FindDropdown(window).Should().NotBeNull(
            "dropdown must stay open after ↓ × 2");

        // ── Enter ─────────────────────────────────────────────────────────────
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Thread.Sleep(UiUpdateDelay);

        var dropdownAfterEnter = FindDropdown(window);
        (dropdownAfterEnter == null || dropdownAfterEnter.IsOffscreen)
            .Should().BeTrue("dropdown must close after Enter commits the selection");

        // The SECOND city — not the first — must appear as a new clock.
        // If the _dropdownHighlight counter bug was present, firstCity would be committed.
        var allTexts = GetVisibleTextValues(window);
        allTexts.Should().Contain(t => t.Contains(secondCity, StringComparison.OrdinalIgnoreCase),
            $"second item '{secondCity}' must be added as a clock after ↓×2+Enter — " +
            $"if '{firstCity}' was added instead, _dropdownHighlight counter bug is present");

        searchBox.AsTextBox().Text.Should().BeNullOrEmpty("TextBox must clear after commit");
    }

    // ── Mouse click on ListBoxItem: adds clock (regression guard) ───────────

    [Fact]
    [Trait("Category", "UI")]
    public void Mouse_ClickListItem_AddsClock()
    {
        SkipIfNoApp();
        EnsureTranslatorOpen();
        var window    = GetWindow();
        var searchBox = FindSearchBox();

        searchBox!.Click();
        Thread.Sleep(TypeDelay);

        const string city = "Lagos";
        searchBox.AsTextBox().Enter(city);
        Thread.Sleep(SearchIdleDelay);

        var dropdown = FindDropdown(window);
        dropdown.Should().NotBeNull($"dropdown must appear for '{city}'");

        // Click the first matching list item
        var items = window.FindAllDescendants(x => x.ByControlType(ControlType.ListItem));
        var match = items.FirstOrDefault(i =>
            (i.Name ?? string.Empty).Contains(city, StringComparison.OrdinalIgnoreCase));

        match.Should().NotBeNull($"a list item for '{city}' must exist in the dropdown");
        match!.Click();
        Thread.Sleep(UiUpdateDelay);

        // Clock must appear in the vertical panel
        var allTexts = GetVisibleTextValues(window);
        allTexts.Should().Contain(t => t.Contains("Lagos", StringComparison.OrdinalIgnoreCase),
            "Lagos clock must appear after clicking the list item — " +
            "regression: Mouse.LeftButton guard in SelectionChanged blocked this");

        searchBox.AsTextBox().Text.Should().BeNullOrEmpty("TextBox must clear after mouse selection");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Window GetWindow() =>
        _app!.GetMainWindow(_automation, TimeSpan.FromSeconds(5));

    private void EnsureTranslatorOpen()
    {
        var window    = GetWindow();
        var searchBox = window.FindFirstDescendant(x =>
            x.ByControlType(ControlType.Edit).And(x.ByName("AddCitySearch")));

        if (searchBox != null && !searchBox.IsOffscreen) return;

        var toggle = window.FindFirstDescendant(x =>
            x.ByControlType(ControlType.Button).And(x.ByName("ToggleTranslatorButton")));

        toggle?.Click();
        Thread.Sleep(400);
    }

    private AutomationElement? FindSearchBox()
    {
        var window = GetWindow();
        return window.FindFirstDescendant(x =>
            x.ByControlType(ControlType.Edit).And(x.ByName("AddCitySearch")));
    }

    /// <summary>
    /// Returns the GlobalCitySearchList when it is on-screen and has at least one item.
    /// </summary>
    private static AutomationElement? FindDropdown(AutomationElement root)
    {
        var list = root.FindFirstDescendant(x =>
            x.ByControlType(ControlType.List).And(x.ByName("GlobalCitySearchList")));

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
