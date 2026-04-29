using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WorldClock.Models;

public sealed class ClockLocation : INotifyPropertyChanged
{
    // ── Live clock fields ─────────────────────────────────────────────────────
    private string _currentTime = string.Empty;
    private string _currentDate = string.Empty;
    private string _utcOffset   = string.Empty;
    private string _dstLabel    = string.Empty;
    private bool   _isDst;

    // ── Editable metadata ─────────────────────────────────────────────────────
    private string _cityName    = string.Empty;
    private string _countryFlag = string.Empty;
    private string _teamLabel   = string.Empty;

    // ── Edit-mode state ───────────────────────────────────────────────────────
    private bool   _isEditing;
    private string _editingCityName    = string.Empty;
    private string _editingCountryFlag = string.Empty;
    private string _editingTeamLabel   = string.Empty;

    // ── Immutable identity ────────────────────────────────────────────────────
    public required string          TimeZoneId  { get; init; }
    public required SolidColorBrush AccentBrush { get; init; }

    // ── Mutable display properties ────────────────────────────────────────────
    public required string CityName
    {
        get => _cityName;
        set { _cityName = value; OnPropertyChanged(); }
    }

    public required string CountryFlag
    {
        get => _countryFlag;
        set { _countryFlag = value; OnPropertyChanged(); }
    }

    public required string TeamLabel
    {
        get => _teamLabel;
        set { _teamLabel = value; OnPropertyChanged(); }
    }

    /// <summary>True when this is the UTC clock shown in the dedicated banner (cannot be moved or edited).</summary>
    public bool IsUtc => TimeZoneId == "UTC";

    // ── Inline-edit state ─────────────────────────────────────────────────────

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>Scratch city name used while editing. Bound to the card TextBox.</summary>
    public string EditingCityName
    {
        get => _editingCityName;
        set { _editingCityName = value; OnPropertyChanged(); }
    }

    /// <summary>Scratch country flag emoji used while editing.</summary>
    public string EditingCountryFlag
    {
        get => _editingCountryFlag;
        set { _editingCountryFlag = value; OnPropertyChanged(); }
    }

    /// <summary>Scratch team label used while editing.</summary>
    public string EditingTeamLabel
    {
        get => _editingTeamLabel;
        set { _editingTeamLabel = value; OnPropertyChanged(); }
    }

    /// <summary>Copies current values to scratch fields and enters edit mode.</summary>
    public void BeginEdit()
    {
        EditingCityName    = CityName;
        EditingCountryFlag = CountryFlag;
        EditingTeamLabel   = TeamLabel;
        IsEditing          = true;
    }

    /// <summary>Applies scratch fields back to the model and exits edit mode.</summary>
    public void CommitEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditingCityName))    CityName    = EditingCityName.Trim();
        if (!string.IsNullOrWhiteSpace(EditingCountryFlag)) CountryFlag = EditingCountryFlag.Trim();
        TeamLabel = EditingTeamLabel.Trim();   // allow empty team label
        IsEditing = false;
    }

    /// <summary>Discards scratch fields and exits edit mode without changing data.</summary>
    public void CancelEdit() => IsEditing = false;

    // ── Live-clock properties ─────────────────────────────────────────────────

    public string CurrentTime
    {
        get => _currentTime;
        set { _currentTime = value; OnPropertyChanged(); }
    }

    public string CurrentDate
    {
        get => _currentDate;
        set { _currentDate = value; OnPropertyChanged(); }
    }

    public string UtcOffset
    {
        get => _utcOffset;
        set { _utcOffset = value; OnPropertyChanged(); }
    }

    /// <summary>"☀ DST" or "— STD" (or empty for fixed-offset zones)</summary>
    public string DstLabel
    {
        get => _dstLabel;
        set { _dstLabel = value; OnPropertyChanged(); }
    }

    public bool IsDst
    {
        get => _isDst;
        set { _isDst = value; OnPropertyChanged(); }
    }

    public void Refresh()
    {
        var tz     = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var now    = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var offset = tz.GetUtcOffset(now);
        var sign   = offset >= TimeSpan.Zero ? "+" : "-";

        CurrentTime = now.ToString("HH:mm:ss");
        CurrentDate = now.ToString("ddd, dd MMM yyyy");
        UtcOffset   = $"UTC{sign}{offset:hh\\:mm}";

        if (tz.SupportsDaylightSavingTime)
        {
            IsDst    = tz.IsDaylightSavingTime(now);
            DstLabel = IsDst ? "☀ DST" : "— STD";
        }
        else
        {
            IsDst    = false;
            DstLabel = "— STD";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
