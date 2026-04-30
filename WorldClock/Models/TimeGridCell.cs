using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using WorldClock.Services;

namespace WorldClock.Models;

/// <summary>One half-hour cell in the transposed Time Translator grid (city row × time slot).</summary>
public sealed class TimeGridCell : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<TimeBand, SolidColorBrush> BackgroundsDark;
    private static readonly IReadOnlyDictionary<TimeBand, SolidColorBrush> BackgroundsLight;
    private static readonly IReadOnlyDictionary<TimeBand, SolidColorBrush> ForegroundsDark;
    private static readonly IReadOnlyDictionary<TimeBand, SolidColorBrush> ForegroundsLight;

    static TimeGridCell()
    {
        static SolidColorBrush Mk(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        // Dark-theme cell fills — deep, saturated night/day colours
        BackgroundsDark = new Dictionary<TimeBand, SolidColorBrush>
        {
            [TimeBand.Night]     = Mk("#0B1525"),  // deep navy
            [TimeBand.Morning]   = Mk("#0E1E30"),  // dark steel blue
            [TimeBand.WorkHours] = Mk("#0B1E10"),  // dark forest green
            [TimeBand.Evening]   = Mk("#1E1408"),  // dark amber
        };

        // Light-theme cell fills — subtle, desaturated tints of the same hue families
        BackgroundsLight = new Dictionary<TimeBand, SolidColorBrush>
        {
            [TimeBand.Night]     = Mk("#D8E4F0"),  // pale blue-grey (night sky)
            [TimeBand.Morning]   = Mk("#FFF4CC"),  // soft golden (sunrise)
            [TimeBand.WorkHours] = Mk("#E6F4EA"),  // soft mint (daytime)
            [TimeBand.Evening]   = Mk("#FFF0E0"),  // soft peach (sunset)
        };

        // Dark-theme text — kept warm/neutral to contrast against the cyan selection overlay (#00E5FF)
        ForegroundsDark = new Dictionary<TimeBand, SolidColorBrush>
        {
            [TimeBand.Night]     = Mk("#B0A890"),  // warm sand — clearly distinct from cyan selection
            [TimeBand.Morning]   = Mk("#90C0A8"),  // soft teal-green
            [TimeBand.WorkHours] = Mk("#70B880"),  // soft green
            [TimeBand.Evening]   = Mk("#C8A060"),  // warm amber
        };

        // Light-theme text — darker, warmer tones for readability on pastel backgrounds
        ForegroundsLight = new Dictionary<TimeBand, SolidColorBrush>
        {
            [TimeBand.Night]     = Mk("#3A5068"),  // dark steel blue
            [TimeBand.Morning]   = Mk("#6A5010"),  // dark golden
            [TimeBand.WorkHours] = Mk("#2A6040"),  // dark forest green
            [TimeBand.Evening]   = Mk("#7A4818"),  // dark amber-brown
        };
    }

    public required int      SlotIndex  { get; init; }  // 0-47 (slot = hour*2 + half)
    public required string   TimeStr    { get; init; }  // "14:30"
    public required string   DayDiff    { get; init; }  // "" / "+1" / "-1"
    public required TimeBand Band       { get; init; }

    /// <summary>Local time in compact am/pm format shown inside each cell.
    /// Returns "2p", "2:30p", "12a", "12:30p" etc. Empty for :30 slots that aren't on the hour.</summary>
    public string TimeAmPm
    {
        get
        {
            if (!TimeSpan.TryParseExact(TimeStr, @"hh\:mm", null, out var ts)
                && !TimeSpan.TryParseExact(TimeStr, @"h\:mm", null, out ts))
                return string.Empty;
            bool pm   = ts.Hours >= 12;
            int  h12  = ts.Hours % 12 == 0 ? 12 : ts.Hours % 12;
            string suffix = pm ? "p" : "a";
            return ts.Minutes == 0
                ? $"{h12}{suffix}"
                : $"{h12}:{ts.Minutes:D2}{suffix}";
        }
    }

    /// <summary>Hour (+ optional minutes) part of the label, e.g. "6" or "2:30". Empty on :30 slots.</summary>
    public string TimeHourPart
    {
        get
        {
            var t = TimeAmPm;
            return t.Length > 0 ? t[..^1] : "";
        }
    }

    /// <summary>"am" or "pm" part of the label. Empty when TimeAmPm is empty.</summary>
    public string TimeAmPmPart
    {
        get
        {
            var t = TimeAmPm;
            return t.Length > 0 ? (t[^1] == 'a' ? "am" : "pm") : "";
        }
    }

    /// <summary>True for on-the-hour slots (SlotIndex even).</summary>
    public bool IsHourSlot => SlotIndex % 2 == 0;

    /// <summary>Cell fill: dark tint on dark themes, subtle pastel tint on light themes.</summary>
    public SolidColorBrush Background =>
        ThemeService.Instance.ActiveTheme.IsDark
            ? BackgroundsDark[Band]
            : BackgroundsLight[Band];

    public SolidColorBrush Foreground =>
        ThemeService.Instance.ActiveTheme.IsDark
            ? ForegroundsDark[Band]
            : ForegroundsLight[Band];

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
