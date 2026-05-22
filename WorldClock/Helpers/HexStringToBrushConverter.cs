using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WorldClock.Helpers;

/// <summary>
/// Converts a hex colour string (e.g. "#00E5FF") to a frozen <see cref="SolidColorBrush"/>.
/// Returns <see cref="Brushes.Transparent"/> for null, empty, or invalid values.
/// </summary>
public sealed class HexStringToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.Transparent;
        try
        {
            var color = Color.Parse(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
