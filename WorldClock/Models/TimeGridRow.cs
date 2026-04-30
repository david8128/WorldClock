using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WorldClock.Helpers;

namespace WorldClock.Models;

/// <summary>One city/timezone row in the transposed Time Translator grid.
/// Contains 48 half-hour cells and a date label that reflects the selected time window.</summary>
public sealed class TimeGridRow : INotifyPropertyChanged
{
    // ── Location identity (shown in the frozen row header) ───────────────────
    public required string          CityName    { get; init; }
    public required string          CountryFlag { get; init; }
    /// <summary>Country name for the second line of the row header card.</summary>
    public required string          Country     { get; init; }
    public required string          UtcOffset   { get; init; }
    public required SolidColorBrush AccentBrush { get; init; }
    /// <summary>Theme-aware accent brush: darkened on light themes for WCAG AA contrast,
    /// vivid on dark themes. Computed live — rows are recreated on theme switch.</summary>
    public SolidColorBrush ThemedAccentBrush => ThemeColorHelper.ThemedBrush(AccentBrush);
    /// <summary>True when this row represents the current source timezone.</summary>
    public required bool            IsSource    { get; init; }

    /// <summary>48 half-hour cells (slot 0 = 00:00, slot 47 = 23:30 in source time).</summary>
    public required IReadOnlyList<TimeGridCell> Cells { get; init; }

    // ── Date label for the selected slot (INPC so XAML updates live) ─────────
    private string _dateLabel = "";
    public string DateLabel
    {
        get => _dateLabel;
        set { if (_dateLabel == value) return; _dateLabel = value; OnPropertyChanged(); }
    }

    /// <summary>Day offset vs source date for the selected slot (0=same, +1=next day, -1=prev).</summary>
    private int _dateDayDiff;
    public int DateDayDiff
    {
        get => _dateDayDiff;
        set { if (_dateDayDiff == value) return; _dateDayDiff = value; OnPropertyChanged(); }
    }

    // ── Translated time for the selected slot (populated by Translate()) ─────
    private string? _selectedTimeStr;
    public string? SelectedTimeStr
    {
        get => _selectedTimeStr;
        set { if (_selectedTimeStr == value) return; _selectedTimeStr = value; OnPropertyChanged(); }
    }

    private string? _selectedEndTimeStr;
    public string? SelectedEndTimeStr
    {
        get => _selectedEndTimeStr;
        set { if (_selectedEndTimeStr == value) return; _selectedEndTimeStr = value; OnPropertyChanged(); }
    }

    private bool _hasRange;
    public bool HasRange
    {
        get => _hasRange;
        set { if (_hasRange == value) return; _hasRange = value; OnPropertyChanged(); }
    }

    private bool _isDst;
    public bool IsDst
    {
        get => _isDst;
        set { if (_isDst == value) return; _isDst = value; OnPropertyChanged(); OnPropertyChanged(nameof(DstLabel)); }
    }

    public string DstLabel => _isDst ? "☀ DST" : "— STD";

    /// <summary>True after the user has explicitly clicked/dragged a slot; controls time visibility in the row card.</summary>
    private bool _showTranslatedTime;
    public bool ShowTranslatedTime
    {
        get => _showTranslatedTime;
        set { if (_showTranslatedTime == value) return; _showTranslatedTime = value; OnPropertyChanged(); }
    }

    // ── Current-time needle ───────────────────────────────────────────────────
    private double _currentTimeLeft = -1;

    /// <summary>Pixel offset from the row's left edge to the current-time needle.
    /// Negative when the viewed date is not today (needle hidden).
    /// 160 px row-header + slot * 14 px + fractional offset within the slot.</summary>
    public double CurrentTimeLeft
    {
        get => _currentTimeLeft;
        set
        {
            if (Math.Abs(_currentTimeLeft - value) < 0.01) return;
            _currentTimeLeft = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowCurrentTime));
        }
    }

    /// <summary>True when the current-time needle should be visible (today only).</summary>
    public bool ShowCurrentTime => _currentTimeLeft >= 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
