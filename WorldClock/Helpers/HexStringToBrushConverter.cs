using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WorldClock.Helpers;

/// <summary>
/// Converts a hex colour string (e.g. "#00E5FF") to a frozen <see cref="SolidColorBrush"/>.
/// Returns <see cref="Brushes.Transparent"/> for null, empty, or invalid values.
/// </summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public sealed class HexStringToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.Transparent;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
