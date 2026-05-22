using Avalonia.Media;

namespace WorldClock.Models;

/// <summary>
/// Represents a single translated time result for one configured location.
/// Produced by <see cref="ViewModels.TimeTranslatorViewModel"/>.
/// </summary>
public sealed class TranslatedTime
{
    private static readonly SolidColorBrush DstActiveBrush;
    private static readonly SolidColorBrush StdBrush;

    static TranslatedTime()
    {
        DstActiveBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));
        StdBrush = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C));
    }

    public required string          CityName    { get; init; }
    public required string          CountryFlag { get; init; }
    /// <summary>Translated local start time in HH:mm format.</summary>
    public required string          TimeStr     { get; init; }
    /// <summary>Translated local end time in HH:mm format (null when selection is a single slot).</summary>
    public          string?         EndTimeStr  { get; init; }
    /// <summary>Translated local date in "ddd, dd MMM" format.</summary>
    public required string          DateStr     { get; init; }
    /// <summary>UTC offset string, e.g. "UTC-04:00" or "UTC+05:30".</summary>
    public required string          UtcOffset   { get; init; }
    /// <summary>True when daylight saving time is in effect at the translated moment.</summary>
    public required bool            IsDst       { get; init; }
    public required SolidColorBrush AccentBrush { get; init; }

    /// <summary>"09:00 – 10:30" for a range, or just "09:00" for a single slot.</summary>
    public string WindowLabel => EndTimeStr is null ? TimeStr : $"{TimeStr} – {EndTimeStr}";

    /// <summary>True when a selection window (not just a single slot) is active.</summary>
    public bool HasRange => EndTimeStr is not null;

    /// <summary>"☀ DST" when daylight saving is active, "— STD" otherwise.</summary>
    public string          DstLabel => IsDst ? "☀ DST" : "— STD";
    public SolidColorBrush DstBrush => IsDst ? DstActiveBrush : StdBrush;
}
