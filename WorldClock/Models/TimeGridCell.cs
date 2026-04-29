using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace WorldClock.Models;

/// <summary>One half-hour cell in the transposed Time Translator grid (city row × time slot).</summary>
public sealed class TimeGridCell : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<TimeBand, SolidColorBrush> Backgrounds;
    private static readonly IReadOnlyDictionary<TimeBand, SolidColorBrush> Foregrounds;

    static TimeGridCell()
    {
        static SolidColorBrush Mk(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        Backgrounds = new Dictionary<TimeBand, SolidColorBrush>
        {
            [TimeBand.Night]     = Mk("#0B1525"),
            [TimeBand.Morning]   = Mk("#0E1E30"),
            [TimeBand.WorkHours] = Mk("#0B1E10"),
            [TimeBand.Evening]   = Mk("#1E1408"),
        };

        Foregrounds = new Dictionary<TimeBand, SolidColorBrush>
        {
            [TimeBand.Night]     = Mk("#3A6080"),
            [TimeBand.Morning]   = Mk("#5A9CB5"),
            [TimeBand.WorkHours] = Mk("#5A9470"),
            [TimeBand.Evening]   = Mk("#9A7840"),
        };
    }

    public required int      SlotIndex { get; init; }  // 0-47 (slot = hour*2 + half)
    public required string   TimeStr   { get; init; }  // "14:30"
    public required string   DayDiff   { get; init; }  // "" / "+1" / "-1"
    public required TimeBand Band      { get; init; }

    public SolidColorBrush Background => Backgrounds[Band];
    public SolidColorBrush Foreground => Foregrounds[Band];

    // INPC IsSelected so XAML DataTrigger highlights the selection window
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
