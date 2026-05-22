using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WorldClock.Helpers;

/// <summary>
/// Converts a hex colour string supplied via <see cref="ConverterParameter"/>
/// (e.g. "#00E5FF") to a <see cref="SolidColorBrush"/> with the active-theme
/// adjustment applied by <see cref="ThemeColorHelper.ThemedBrush"/>.
///
/// The binding <em>value</em> is intentionally unused.  Bind to a reactive
/// property such as <c>ThemedAccentBrush</c> so the palette swatches
/// automatically re-evaluate whenever the active theme changes.
/// </summary>
public sealed class HexToThemedBrushConverter : IValueConverter
{
    public static readonly HexToThemedBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string hex) return null;
        try
        {
            var brush = new SolidColorBrush(Color.Parse(hex));
            return ThemeColorHelper.ThemedBrush(brush);
        }
        catch { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
