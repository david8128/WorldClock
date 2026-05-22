using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WorldClock.Helpers;

/// <summary>
/// Converts a slot index (0–47) to the pixel X position inside a Canvas
/// used as the ItemsPanel for timeline cells and column-header slots.
/// Each slot is <see cref="SlotWidthPx"/> pixels wide (default 20).
/// </summary>
public sealed class SlotIndexToCanvasLeftConverter : IValueConverter
{
    public const double SlotWidthPx = 24.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int slot ? slot * SlotWidthPx : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double x ? (int)(x / SlotWidthPx) : 0;
}
