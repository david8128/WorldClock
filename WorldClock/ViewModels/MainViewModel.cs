using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using WorldClock.Models;
using WorldClock.Services;

namespace WorldClock.ViewModels;

public sealed class MainViewModel
{
    // ── Accent palette (cycles for dynamically added cities) ─────────────────
    private static readonly string[] AccentCycle =
    [
        "#00E5FF","#FFD600","#00E676","#FF9100",
        "#CE93D8","#FF4081","#69F0AE","#F48FB1",
        "#80DEEA","#FFCC02","#B39DDB","#4DD0E1"
    ];

    private int _accentIndex;

    private readonly SettingsService _store;

    public ObservableCollection<ClockLocation> Locations { get; } = new();

    /// <summary>All Windows system timezone IDs (sorted) for the settings picker.</summary>
    public IReadOnlyList<TimeZoneInfo> AllTimeZones { get; } =
        TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.BaseUtcOffset).ToList();

    /// <summary>Time Translator panel view-model.</summary>
    public TimeTranslatorViewModel Translator { get; }

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
            // UTC is always first and implicit — add it unconditionally.
            Add("UTC / GMT", "🌐", "UTC", "Universal Time");
            foreach (var city in saved.Cities)
            {
                // Skip UTC (already added) and skip cities with invalid timezone IDs.
                if (city.TimeZoneId == "UTC") continue;
                try   { TimeZoneInfo.FindSystemTimeZoneById(city.TimeZoneId); }
                catch { continue; }   // timezone removed from OS — skip gracefully
                Add(city.CityName, city.CountryFlag, city.TimeZoneId, city.TeamLabel);
            }
        }

        Translator = new TimeTranslatorViewModel(Locations);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => { foreach (var l in Locations) l.Refresh(); };
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
        if (Locations.Any(l => string.Equals(l.CityName, cityName.Trim(),
                                             StringComparison.OrdinalIgnoreCase))) return false;
        try   { TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { return false; }
        Add(cityName.Trim(), flag, tzId, teamLabel);
        PersistCities();
        return true;
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
            .Where(l => l.TimeZoneId != "UTC")
            .Select(l => new SavedCity
            {
                CityName    = l.CityName,
                CountryFlag = l.CountryFlag,
                TimeZoneId  = l.TimeZoneId,
                TeamLabel   = l.TeamLabel,
            })
            .ToList();
        _store.Save(saved);
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

    private void Add(string city, string flag, string tzId, string team)
    {
        var hex    = AccentCycle[_accentIndex % AccentCycle.Length];
        _accentIndex++;
        var color  = (Color)ColorConverter.ConvertFromString(hex);
        var brush  = new SolidColorBrush(color);
        brush.Freeze();
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
