using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WorldClock.Data;
using WorldClock.Helpers;
using WorldClock.Models;
using WorldClock.Services;
using WorldClock.ViewModels;

namespace WorldClock;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ThemeService  _theme = ThemeService.Instance;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        // Theme picker
        ThemeComboBox.ItemsSource       = AppTheme.All;
        ThemeComboBox.DisplayMemberPath = "Name";
        ThemeComboBox.SelectedItem      = _theme.ActiveTheme;

        // Opacity slider — wire AFTER setting Value to prevent the BAML coercion
        // (Minimum=0.10 coerces default Value=0.0 → 0.10 and fires ValueChanged before
        //  InitializeComponent returns, which would overwrite _theme.Opacity with 0.10).
        OpacitySlider.Value = _theme.Opacity;
        OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;
        UpdateOpacityLabel();

        // Country / City / Timezone cascade
        CountryComboBox.ItemsSource = CityDatabase.Countries;

        // Delete-mode toggle sync
        DeleteModeToggle.IsChecked = _theme.DeleteMode;
        UpdateDeleteToggleContent();

        // Edit-mode toggle sync
        EditModeToggle.IsChecked = _theme.EditMode;
        UpdateEditToggleContent();

        // Scale mode sync
        ProportionScaleRadio.IsChecked = _theme.ScaleMode == ScaleMode.ProportionScale;
        MinLimitRadio.IsChecked        = _theme.ScaleMode == ScaleMode.MinLimit;

        // Diagnostics toggle sync
        DiagnosticsToggle.IsChecked = _theme.ShowDiagnostics;
        UpdateDiagnosticsToggleContent();

        // Startup toggle sync — read live registry state so external changes are reflected
        StartupToggle.IsChecked = _theme.RunOnStartup;
        UpdateStartupToggleContent();

        // Placeholder visibility
        CityNameBox.TextChanged  += (_, _) => CityNamePlaceholder.Visibility =
            string.IsNullOrEmpty(CityNameBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        TeamLabelBox.TextChanged += (_, _) => TeamLabelPlaceholder.Visibility =
            string.IsNullOrEmpty(TeamLabelBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        // Apply AddCity button color from theme
        UpdateAddCityButtonColor();

        // Pre-warm city database on a background thread so the first search is instant
        Task.Run(() => { _ = WorldCitySearchService.All; });

        Loaded += (_, _) =>
        {
            var alpha = AcrylicHelper.ToTintAlpha(_theme.Opacity);
            AcrylicHelper.Enable(this, _theme.ActiveTheme.BackgroundDark, alpha, _theme.ActiveTheme.IsDark);
        };
    }

    // Theme selection
    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is AppTheme selected)
        {
            _theme.ActiveTheme = selected;
            UpdateAddCityButtonColor();
            // Re-apply acrylic with the new theme's background colour
            if (IsLoaded)
            {
                var alpha = AcrylicHelper.ToTintAlpha(_theme.Opacity);
                AcrylicHelper.Enable(this, selected.BackgroundDark, alpha, selected.IsDark);
            }
        }
    }

    private void UpdateAddCityButtonColor()
    {
        AddCityButton.Background = _theme.ActiveTheme.BrushAccentPrimary;
    }

    // Opacity slider
    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _theme.Opacity = e.NewValue;
        UpdateOpacityLabel();
        // Live update acrylic on this window too
        if (IsLoaded)
        {
            var alpha = AcrylicHelper.ToTintAlpha(_theme.Opacity);
            AcrylicHelper.Enable(this, _theme.ActiveTheme.BackgroundDark, alpha, _theme.ActiveTheme.IsDark);
        }
    }

    private void UpdateOpacityLabel()
    {
        if (OpacityLabel != null)
            OpacityLabel.Text = $"{_theme.Opacity * 100:0}%";
    }

    // ── Quick city search ─────────────────────────────────────────────────────

    /// <summary>
    /// City selected via the quick-search box.
    /// Used by <see cref="AddCityButton_Click"/> when the cascade has no selection.
    /// </summary>
    private CityEntry? _lastQuickSearchEntry;

    private CancellationTokenSource _settingsSearchCts = new();

    private async void CitySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = CitySearchBox.Text;
        CitySearchPlaceholder.Visibility = string.IsNullOrEmpty(q)
            ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(q))
        {
            CitySearchPopup.IsOpen = false;
            return;
        }

        // Debounce + background thread search
        _settingsSearchCts.Cancel();
        _settingsSearchCts = new CancellationTokenSource();
        var cts = _settingsSearchCts;

        try { await Task.Delay(150, cts.Token); }
        catch (OperationCanceledException) { return; }

        IReadOnlyList<CityEntry> matches;
        try { matches = await Task.Run(() => WorldCitySearchService.Search(q), cts.Token); }
        catch (OperationCanceledException) { return; }

        if (cts.IsCancellationRequested) return;
        CitySearchList.ItemsSource = matches;
        CitySearchPopup.IsOpen     = matches.Count > 0;
    }

    private void CitySearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) tb.SelectAll();
        if (!string.IsNullOrEmpty(CitySearchBox.Text))
        {
            var q = CitySearchBox.Text;
            _ = Task.Run(() => WorldCitySearchService.Search(q))
                    .ContinueWith(t => Dispatcher.Invoke(() =>
                    {
                        CitySearchList.ItemsSource = t.Result;
                        CitySearchPopup.IsOpen     = t.Result.Count > 0;
                    }));
        }
    }

    private void CitySearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CitySearchPopup.IsOpen = false;
            CitySearchBox.Clear();
            _lastQuickSearchEntry = null;
        }
        else if (e.Key == Key.Enter && CitySearchList.Items.Count > 0)
        {
            // Select first result on Enter
            if (CitySearchList.Items[0] is CityEntry entry)
            {
                CitySearchPopup.IsOpen = false;
                ApplySelectedCity(entry);
            }
        }
    }

    private void CitySearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is CityEntry entry)
        {
            lb.SelectedItem = null;
            CitySearchPopup.IsOpen = false;
            ApplySelectedCity(entry);
        }
    }

    /// <summary>
    /// "+" button on a search result — adds directly to the clock list without going through the form.
    /// </summary>
    private void AddCityFromSearch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CityEntry entry })
        {
            e.Handled = true;  // don't also trigger SelectionChanged on the ListBox
            CitySearchPopup.IsOpen = false;
            CitySearchBox.Clear();
            CitySearchPlaceholder.Visibility = Visibility.Visible;
            _lastQuickSearchEntry = null;

            if (!_vm.AddLocation(entry.City, entry.TimeZoneId, "Custom", entry.CountryFlag))
                ShowError($"'{entry.City}' is already in your clock list.");
        }
    }

    /// <summary>Fills the cascade (Country → City) and form fields from a search result.</summary>
    private void ApplySelectedCity(CityEntry entry)
    {
        _lastQuickSearchEntry = entry;

        CitySearchBox.Text = $"{entry.CountryFlag} {entry.City}";
        CitySearchPlaceholder.Visibility = Visibility.Collapsed;

        // Select matching country in the cascade
        CountryComboBox.SelectedItem = CountryComboBox.Items
            .Cast<string>()
            .FirstOrDefault(c => c.Equals(entry.Country, StringComparison.OrdinalIgnoreCase));

        // Select matching city — cascade populates CityComboBox after country selection
        if (CityComboBox.ItemsSource is IEnumerable<CityEntry> cities)
        {
            CityComboBox.SelectedItem = cities.FirstOrDefault(c =>
                c.City.Equals(entry.City, StringComparison.OrdinalIgnoreCase));
        }

        // Always fill display name and timezone from the entry (cascade may not have it)
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(entry.TimeZoneId);
            TimezoneBox.Text = tz.DisplayName;
        }
        catch { TimezoneBox.Text = entry.TimeZoneId; }

        CityNameBox.Text = entry.City;
        CityNamePlaceholder.Visibility = Visibility.Collapsed;
    }

    // Cascading country → city → timezone selectors
    private void CountryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CountryComboBox.SelectedItem is not string country) return;

        // User switched to cascade — discard any quick-search pre-fill
        _lastQuickSearchEntry = null;
        CitySearchBox.Clear();
        CitySearchPlaceholder.Visibility = Visibility.Visible;

        var cities = CityDatabase.CitiesForCountry(country);
        CityComboBox.ItemsSource       = cities;
        CityComboBox.DisplayMemberPath = "City";
        CityComboBox.IsEnabled         = cities.Count > 0;
        CityComboBox.SelectedIndex     = -1;
        TimezoneBox.Text               = string.Empty;
        CityNameBox.Clear();
        CityNamePlaceholder.Visibility = Visibility.Visible;
    }

    private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CityComboBox.SelectedItem is not CityEntry entry) return;

        // Auto-fill timezone (read-only display)
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(entry.TimeZoneId);
            TimezoneBox.Text = tz.DisplayName;
        }
        catch
        {
            TimezoneBox.Text = entry.TimeZoneId;
        }

        // Auto-fill city name (user can still edit)
        CityNameBox.Text = entry.City;
        CityNamePlaceholder.Visibility = Visibility.Collapsed;
    }

    // Add city
    private void AddCityButton_Click(object sender, RoutedEventArgs e)
    {
        AddCityError.Visibility = Visibility.Collapsed;

        // Accept a city from either the cascade selector OR the quick-search box
        CityEntry? entry = CityComboBox.SelectedItem as CityEntry ?? _lastQuickSearchEntry;
        if (entry is null)
        {
            ShowError("Please search for or select a city above.");
            return;
        }

        var cityName  = CityNameBox.Text.Trim();
        var teamLabel = string.IsNullOrWhiteSpace(TeamLabelBox.Text)
                            ? "Custom"
                            : TeamLabelBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(cityName))
        {
            ShowError("Please enter a display name.");
            return;
        }

        if (!_vm.AddLocation(cityName, entry.TimeZoneId, teamLabel, entry.CountryFlag))
        {
            ShowError($"A city named '{cityName}' already exists.");
            return;
        }

        // Reset form
        _lastQuickSearchEntry         = null;
        CountryComboBox.SelectedIndex = -1;
        CityComboBox.ItemsSource      = null;
        CityComboBox.IsEnabled        = false;
        TimezoneBox.Text              = string.Empty;
        CityNameBox.Clear();
        TeamLabelBox.Clear();
        CitySearchBox.Clear();
        CitySearchPlaceholder.Visibility = Visibility.Visible;
        CityNamePlaceholder.Visibility   = Visibility.Visible;
    }

    private void ShowError(string message)
    {
        AddCityError.Text       = message;
        AddCityError.Visibility = Visibility.Visible;
    }

    // Delete mode
    private void DeleteModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _theme.DeleteMode = true;
        UpdateDeleteToggleContent();
    }

    private void DeleteModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _theme.DeleteMode = false;
        UpdateDeleteToggleContent();
    }

    private void UpdateDeleteToggleContent()
    {
        if (DeleteModeToggle == null) return;
        DeleteModeToggle.Content = _theme.DeleteMode ? "Delete Mode: ON" : "Delete Mode: OFF";
    }

    // Scale mode
    private void ScaleMode_Changed(object sender, RoutedEventArgs e)
    {
        if (ProportionScaleRadio is null || MinLimitRadio is null) return;
        _theme.ScaleMode = ProportionScaleRadio.IsChecked == true
            ? ScaleMode.ProportionScale
            : ScaleMode.MinLimit;
    }

    // Edit mode
    private void EditModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _theme.EditMode = true;
        UpdateEditToggleContent();
    }

    private void EditModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _theme.EditMode = false;
        UpdateEditToggleContent();
    }

    private void UpdateEditToggleContent()
    {
        if (EditModeToggle == null) return;
        EditModeToggle.Content = _theme.EditMode ? "Edit Mode: ON" : "Edit Mode: OFF";
    }

    // Diagnostics window toggle
    private void DiagnosticsToggle_Checked(object sender, RoutedEventArgs e)
    {
        _theme.ShowDiagnostics = true;
        UpdateDiagnosticsToggleContent();
    }

    private void DiagnosticsToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _theme.ShowDiagnostics = false;
        UpdateDiagnosticsToggleContent();
    }

    private void UpdateDiagnosticsToggleContent()
    {
        if (DiagnosticsToggle == null) return;
        DiagnosticsToggle.Content = _theme.ShowDiagnostics
            ? "Diagnostics Window: ON"
            : "Diagnostics Window: OFF";
    }

    // Startup toggle
    private void StartupToggle_Checked(object sender, RoutedEventArgs e)
    {
        _theme.RunOnStartup = true;
        UpdateStartupToggleContent();
    }

    private void StartupToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _theme.RunOnStartup = false;
        UpdateStartupToggleContent();
    }

    private void UpdateStartupToggleContent()
    {
        if (StartupToggle == null) return;
        StartupToggle.Content = _theme.RunOnStartup ? "Start on Login: ON" : "Start on Login: OFF";

        if (StartupStatusLabel == null) return;

        bool liveActive = StartupHelper.IsRegistered;
        bool intended   = _theme.RunOnStartup;

        if (intended && liveActive)
        {
            StartupStatusLabel.Text       = "✓ Active — WorldClock will launch at Windows login";
            StartupStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
        }
        else if (!intended && !liveActive)
        {
            StartupStatusLabel.Text       = "✗ Inactive — WorldClock will not launch at login";
            StartupStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
        }
        else if (intended && !liveActive)
        {
            // Toggle ON but registry entry is missing (e.g. removed externally or first write failed)
            StartupStatusLabel.Text       = "⚠ Registry entry missing — try toggling off then on again";
            StartupStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));
        }
        else
        {
            // Toggle OFF but registry entry still exists (e.g. added externally)
            StartupStatusLabel.Text       = "⚠ Registry entry exists externally — toggle ON then OFF to remove";
            StartupStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26));
        }
    }

    // Close
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
