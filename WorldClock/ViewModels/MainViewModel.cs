using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Threading;
using WorldClock.Helpers;
using WorldClock.Models;
using WorldClock.Services;

namespace WorldClock.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    // ── Accent palette (cycles for dynamically added cities) ─────────────────
    /// <summary>Ordered preset accent colours available in the card colour picker.</summary>
    public static readonly IReadOnlyList<string> AccentPalette =
    [
        "#00E5FF","#FFD600","#00E676","#FF9100",
        "#CE93D8","#FF4081","#69F0AE","#F48FB1",
        "#80DEEA","#FFCC02","#B39DDB","#4DD0E1"
    ];

    /// <summary>Pairs an original palette hex (used for saving/selection) with the
    /// theme-adjusted brush used for display in the colour picker swatches.</summary>
    public sealed record AccentEntry(string Hex, SolidColorBrush Display);

    /// <summary>The accent palette entries pre-themed for the active theme.</summary>
    public IReadOnlyList<AccentEntry> ThemedAccentPalette =>
        AccentPalette.Select(hex =>
        {
            var color    = Color.Parse(hex);
            var rawBrush = new SolidColorBrush(color);
            var display  = ThemeColorHelper.ThemedBrush(rawBrush);
            return new AccentEntry(hex, display);
        }).ToList();

    private int _accentIndex;

    private readonly SettingsService _store;

    public ObservableCollection<ClockLocation> Locations { get; } = new();

    /// <summary>All Windows system timezone IDs (sorted) for the settings picker.</summary>
    public IReadOnlyList<TimeZoneInfo> AllTimeZones { get; } =
        TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.BaseUtcOffset).ToList();

    /// <summary>Time Translator panel view-model.</summary>
    public TimeTranslatorViewModel Translator { get; }

    // ── Home location ─────────────────────────────────────────────────────────

    private string _homeLocationId = string.Empty;

    /// <summary>TimeZoneId of the home location (empty = none set).</summary>
    public string HomeLocationId
    {
        get => _homeLocationId;
        private set { _homeLocationId = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsUtcHome)); }
    }

    /// <summary>True when UTC is the current home location. Bound by the UTC banner toggle button.</summary>
    public bool IsUtcHome => _homeLocationId == "UTC";

    /// <summary>Resolves a timezone ID to a <see cref="TimeZoneInfo"/>.
    /// Returns <see cref="TimeZoneInfo.Utc"/> directly for "UTC" so the static
    /// well-known instance is always used regardless of OS timezone database.
    /// </summary>
    private static TimeZoneInfo LookupTz(string id) =>
        id == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);

    // ── Clock panel layout (compact vertical / expanded wrap) ─────────────────

    private double _clocksCardWidth = double.NaN;

    /// <summary>Card width in the clock panel. NaN = stretch (compact mode). Fixed value = wrap-panel mode.</summary>
    public double ClocksCardWidth
    {
        get => _clocksCardWidth;
        internal set { _clocksCardWidth = value; OnPropertyChanged(); }
    }

    private readonly DispatcherTimer _timer;

    public MainViewModel() : this(SettingsService.Instance) { }

    /// <summary>Internal constructor for unit tests — accepts an isolated <see cref="SettingsService"/>.</summary>
    internal MainViewModel(SettingsService store)
    {
        _store = store;
        var saved = store.Load();

        if (saved.Cities.Count == 0)
        {
            // ── First-run defaults ─────────────────────────────────────────────
            Add("UTC / GMT", "🌐", "UTC",                        "Universal Time");
            Add("New York",  "🇺🇸", "Eastern Standard Time",     "Americas Team");
            Add("London",    "🇬🇧", "GMT Standard Time",         "EMEA Team");
            Add("Madrid",    "🇪🇸", "Romance Standard Time",     "EMEA Team");
            Add("Dubai",     "🇦🇪", "Arabian Standard Time",     "Middle East Team");
            Add("Tokyo",     "🇯🇵", "Tokyo Standard Time",       "APAC Team");
            Add("Sydney",    "🇦🇺", "AUS Eastern Standard Time", "APAC Team");
        }
        else
        {
            // ── Restore saved city list ────────────────────────────────────────
            // UTC is always first — restore any user-customized label, flag, or accent.
            var utcData = saved.Cities.FirstOrDefault(c => c.TimeZoneId == "UTC");
            Add(utcData?.CityName    ?? "UTC / GMT",
                utcData?.CountryFlag ?? "🌐",
                "UTC",
                utcData?.TeamLabel   ?? "Universal Time",
                string.IsNullOrEmpty(utcData?.AccentHex) ? null : utcData?.AccentHex);
            foreach (var city in saved.Cities)
            {
                // Skip UTC (already added) and skip cities with invalid timezone IDs.
                if (city.TimeZoneId == "UTC") continue;
                try   { TimeZoneInfo.FindSystemTimeZoneById(city.TimeZoneId); }
                catch { continue; }   // timezone removed from OS — skip gracefully
                Add(city.CityName, city.CountryFlag, city.TimeZoneId, city.TeamLabel,
                    string.IsNullOrEmpty(city.AccentHex) ? null : city.AccentHex);
            }
        }

        Translator = new TimeTranslatorViewModel(Locations);

        // ── Restore home location ──────────────────────────────────────────────
        _homeLocationId = saved.HomeLocationId ?? string.Empty;
        RestoreHomeFlags();
        RefreshHomeDiffs();
        if (!string.IsNullOrEmpty(_homeLocationId))
        {
            try   { Translator.SetHomeZone(LookupTz(_homeLocationId)); }
            catch { /* invalid saved ID — leave home zone unset */ }
        }

        // ── Notify all locations when theme changes (updates ThemedAccentBrush) ──
        // Also rebuild the visualizer grid so TimeGridRow/TimeGridCell get fresh theme-aware colours.
        ThemeService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeService.ActiveTheme))
            {
                foreach (var loc in Locations) loc.NotifyThemeChanged();
                OnPropertyChanged(nameof(ThemedAccentPalette));
                Translator.BuildGrid();
                Translator.Translate();
            }
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            foreach (var l in Locations) l.Refresh();
            RefreshHomeDiffs();
            Translator.RefreshCurrentTime();
        };
        _timer.Start();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new location. Returns false if:
    /// - city name is empty/whitespace
    /// - a location with the exact same city name already exists (case-insensitive)
    /// - tzId is not a valid Windows timezone ID
    /// </summary>
    public bool AddLocation(string cityName, string tzId, string teamLabel = "Custom",
                            string flag = "🏙️")
    {
        if (string.IsNullOrWhiteSpace(cityName)) return false;
        if (tzId == "UTC") return false;  // UTC is always the first fixed card; search sets visualizer source only
        // Normalise "City, State" → "City" (e.g. "New York, New York" → "New York")
        var comma = cityName.IndexOf(',');
        cityName = (comma > 0 ? cityName[..comma] : cityName).Trim();
        if (Locations.Any(l => string.Equals(l.CityName, cityName,
                                             StringComparison.OrdinalIgnoreCase))) return false;
        try   { TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { return false; }
        Add(cityName.Trim(), flag, tzId, teamLabel);
        PersistCities();
        return true;
    }

    // ── Mode change propagation ───────────────────────────────────────────────

    /// <summary>
    /// Called by MainWindow when Edit or Delete mode toggles.
    /// Notifies every <see cref="ClockLocation"/> so their <c>IsEditVisible</c> /
    /// <c>IsDeleteVisible</c> computed properties fire PropertyChanged and the
    /// card buttons update immediately without rebuilding the entire item list.
    /// </summary>
    public void NotifyModeChanged()
    {
        foreach (var loc in Locations)
            loc.NotifyModeChanged();
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the current (non-UTC) city list into the shared settings file.
    /// Theme and opacity are managed separately by <see cref="ThemeService"/>.
    /// </summary>
    internal void PersistCities()
    {
        var saved = _store.Load();
        saved.Cities = Locations
            .Select(l => new SavedCity
            {
                CityName    = l.CityName,
                CountryFlag = l.CountryFlag,
                TimeZoneId  = l.TimeZoneId,
                TeamLabel   = l.TeamLabel,
                AccentHex   = $"#{l.AccentBrush.Color.R:X2}{l.AccentBrush.Color.G:X2}{l.AccentBrush.Color.B:X2}",
            })
            .ToList();
        saved.HomeLocationId = _homeLocationId;
        _store.Save(saved);
    }

    // ── Home location ─────────────────────────────────────────────────────────

    /// <summary>Marks <paramref name="location"/> as the home city, clears the previous home, and refreshes diffs.</summary>
    public void SetHomeLocation(ClockLocation location)
    {
        HomeLocationId = location.TimeZoneId;
        foreach (var loc in Locations) loc.IsHome = loc.TimeZoneId == location.TimeZoneId;
        RefreshHomeDiffs();
        try   { Translator.SetHomeZone(LookupTz(location.TimeZoneId)); }
        catch { Translator.SetHomeZone(null); }
        PersistCities();
    }

    /// <summary>Clears the home location.</summary>
    public void ClearHomeLocation()
    {
        HomeLocationId = string.Empty;
        foreach (var loc in Locations) { loc.IsHome = false; loc.DiffFromHome = string.Empty; }
        Translator.SetHomeZone(null);
        PersistCities();
    }

    private void RestoreHomeFlags()
    {
        foreach (var loc in Locations) loc.IsHome = loc.TimeZoneId == _homeLocationId;
    }

    private void RefreshHomeDiffs()
    {
        if (string.IsNullOrEmpty(_homeLocationId)) return;
        try
        {
            var homeTz  = LookupTz(_homeLocationId);
            var nowUtc  = DateTime.UtcNow;
            var homeOff = homeTz.GetUtcOffset(nowUtc);

            foreach (var loc in Locations)
            {
                if (loc.IsHome) { loc.DiffFromHome = "HOME"; continue; }
                try
                {
                    var tz       = TimeZoneInfo.FindSystemTimeZoneById(loc.TimeZoneId);
                    var locOff   = tz.GetUtcOffset(nowUtc);
                    var diffMins = (int)(locOff - homeOff).TotalMinutes;
                    if (diffMins == 0) { loc.DiffFromHome = "="; continue; }
                    var sign = diffMins > 0 ? "+" : "-";
                    var abs  = Math.Abs(diffMins);
                    var h    = abs / 60;
                    var m    = abs % 60;
                    loc.DiffFromHome = m == 0 ? $"{sign}{h}h" : $"{sign}{h}h{m}m";
                }
                catch { loc.DiffFromHome = string.Empty; }
            }
        }
        catch { /* home timezone no longer valid — clear */ ClearHomeLocation(); }
    }

    /// <summary>Removes a location by reference. The UTC entry (index 0) cannot be removed.</summary>
    public bool RemoveLocation(ClockLocation location)
    {
        if (location.TimeZoneId == "UTC") return false;
        var removed = Locations.Remove(location);
        if (removed) PersistCities();
        return removed;
    }

    /// <summary>Moves the location at <paramref name="fromIndex"/> to <paramref name="toIndex"/> and persists the new order.
    /// UTC is always at index 0 and cannot be moved.</summary>
    public void MoveLocation(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex <= 0 || toIndex <= 0) return; // UTC at 0 is immovable
        if (fromIndex >= Locations.Count || toIndex >= Locations.Count) return;
        Locations.Move(fromIndex, toIndex);
        PersistCities();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void Add(string city, string flag, string tzId, string team, string? accentHex = null)
    {
        var hex = accentHex ?? AccentPalette[_accentIndex % AccentPalette.Count];
        if (accentHex is null) _accentIndex++;
        var color  = Color.Parse(hex);
        var brush  = new SolidColorBrush(color);
        var loc = new ClockLocation
        {
            CityName    = city,
            CountryFlag = flag,
            TimeZoneId  = tzId,
            TeamLabel   = team,
            AccentBrush = brush
        };
        loc.Refresh();
        Locations.Add(loc);
    }
}
