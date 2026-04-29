using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WorldClock.Models;

/// <summary>Half-hour column header in the transposed Time Translator grid.
/// SlotIndex 0 = 00:00, 1 = 00:30, 2 = 01:00 … 47 = 23:30.</summary>
public sealed class TimeGridColumn : INotifyPropertyChanged
{
    public required int    SlotIndex   { get; init; }  // 0-47
    /// <summary>"00"…"23" on hour-start slots; empty string on :30 slots.</summary>
    public required string SlotLabel   { get; init; }
    public required bool   IsHourStart { get; init; }  // true for :00 slots

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

    public event PropertyChangedEventHandler? PropertyChanged;
}
