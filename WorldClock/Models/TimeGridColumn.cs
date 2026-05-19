using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WorldClock.Models;

/// <summary>Half-hour column header in the transposed Time Translator grid.
/// SlotIndex 0 = 00:00, 1 = 00:30, 2 = 01:00 … 47 = 23:30.</summary>
public sealed class TimeGridColumn : INotifyPropertyChanged
{
    public required int    SlotIndex   { get; init; }  // 0-47
    /// <summary>"12a", "9p" etc. on hour-start slots; empty string on :30 slots.</summary>
    public required string SlotLabel   { get; init; }
    public required bool   IsHourStart { get; init; }  // true for :00 slots

    /// <summary>Short weekday for the midnight slot, e.g. "Wed". Empty for all other slots.</summary>
    public required string DayOfWeekLabel { get; init; }
    /// <summary>Short month+day for the midnight slot, e.g. "May 1". Empty for all other slots.</summary>
    public required string DateShortLabel { get; init; }

    /// <summary>True when this column sits on the 00:00 boundary of a calendar day (may appear
    /// anywhere in the grid when the view is shifted to start at the current home time).</summary>
    public required bool   IsMidnight    { get; init; }

    /// <summary>Hour portion of the slot label (e.g. "6", "12"). Empty on :30 slots or midnight.</summary>
    public string SlotHour => (!IsMidnight && SlotLabel.Length > 0) ? SlotLabel[..^1] : "";
    /// <summary>"am" or "pm" portion of the slot label. Empty on :30 slots or midnight.</summary>
    public string SlotAmPm => (!IsMidnight && SlotLabel.Length > 0) ? (SlotLabel[^1] == 'a' ? "am" : "pm") : "";

    // INPC IsSelected so the column header highlights when in the selection window
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    /// <summary>
    /// True when this column's HOUR (either the :00 slot or its paired :30 slot)
    /// is touched by the current selection window.
    /// Only meaningful for even (IsHourStart) slots — always false for odd slots.
    /// Used by the column-header layer-1 XAML: each 20px-wide hour cell highlights
    /// via this property so the highlight width exactly matches the 20px label width.
    /// Overlap condition: this slot's hour spans [SlotIndex, SlotIndex+1], which
    /// overlaps selection [start, end] when (SlotIndex+1 >= start AND SlotIndex <= end).
    /// </summary>
    private bool _isHourSelected;
    public bool IsHourSelected
    {
        get => _isHourSelected;
        set
        {
            if (_isHourSelected == value) return;
            _isHourSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHourSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
