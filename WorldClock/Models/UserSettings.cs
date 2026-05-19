namespace WorldClock.Models;

/// <summary>
/// JSON-serializable DTO that represents the full persisted state of the app.
/// Stored in %APPDATA%\WorldClock\settings.json.
/// </summary>
public sealed class UserSettings
{
    /// <summary>Name of the active <see cref="AppTheme"/> (matched by <see cref="AppTheme.Name"/>).</summary>
    public string ThemeName { get; set; } = "Dark Default";

    /// <summary>
    /// Opacity level (0.1–1.0 where 0.1 = maximum transparency, 1.0 = most opaque).
    /// Default is 0.5 (50% opacity — visually semi-transparent).
    /// </summary>
    public double Opacity { get; set; } = 0.5;

    /// <summary>Ordered list of city clocks to restore (UTC is always first and implicit).</summary>
    public List<SavedCity> Cities { get; set; } = [];

    /// <summary>TimeZoneId of the user's home city (empty = none set).</summary>
    public string HomeLocationId { get; set; } = string.Empty;

    /// <summary>Window scale mode. Defaults to ProportionScale (elements shrink with window).</summary>
    public ScaleMode ScaleMode { get; set; } = ScaleMode.ProportionScale;
}

/// <summary>Minimal data needed to reconstruct a <see cref="ClockLocation"/>.</summary>
public sealed class SavedCity
{
    public string CityName    { get; set; } = string.Empty;
    public string CountryFlag { get; set; } = string.Empty;
    public string TimeZoneId  { get; set; } = string.Empty;
    public string TeamLabel   { get; set; } = string.Empty;
    /// <summary>Hex accent colour, e.g. "#00E5FF". Empty = use auto-assigned palette colour.</summary>
    public string AccentHex   { get; set; } = string.Empty;
}
