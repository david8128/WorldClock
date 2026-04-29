using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WorldClock.Models;

/// <summary>One city/timezone row in the transposed Time Translator grid.
/// Contains 48 half-hour cells and a date label that reflects the selected time window.</summary>
public sealed class TimeGridRow : INotifyPropertyChanged
{
    // ── Location identity (shown in the frozen row header) ───────────────────
    public required string          CityName    { get; init; }
    public required string          CountryFlag { get; init; }
    public required string          UtcOffset   { get; init; }
    public required SolidColorBrush AccentBrush { get; init; }
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
