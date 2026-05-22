using Avalonia.Media;
using WorldClock.Services;

namespace WorldClock.Helpers;

/// <summary>
/// Shared HSV-based colour helpers for producing theme-aware accent brushes.
/// Used by both <see cref="WorldClock.Models.ClockLocation"/> and
/// <see cref="WorldClock.Models.TimeGridRow"/> so the same darkening logic applies
/// everywhere accent colours appear.
/// </summary>
public static class ThemeColorHelper
{
    /// <summary>
    /// Returns <paramref name="accent"/> unchanged on dark themes.
    /// On light themes, keeps the exact hue and saturation but reduces the HSV Value
    /// to 70 % of the original so every colour stays distinctly itself while being
    /// readable on a light background and never going completely black.
    /// </summary>
    public static SolidColorBrush ThemedBrush(SolidColorBrush accent)
    {
        if (ThemeService.Instance.ActiveTheme.IsDark) return accent;
        RgbToHsv(accent.Color.R, accent.Color.G, accent.Color.B,
                 out double h, out double s, out double v);
        // 70 % of the original Value — distinct, visible on white, never fully black
        var light = HsvToRgbColor(accent.Color.A, h, s, v * 0.70);
        var b = new SolidColorBrush(light);
        return b;
    }

    /// <summary>
    /// Reduces the HSV Value component (keeping hue and saturation constant) until the
    /// contrast ratio against white reaches <paramref name="targetContrast"/> (WCAG AA = 4.5:1).
    /// Binary-searches the Value axis so the result is the minimum darkening needed.
    /// </summary>
    public static Color DarkenValueForContrast(Color c, double targetContrast = 4.5)
    {
        if (RelativeLuminance(c) <= (1.05 / targetContrast) - 0.05)
            return c;   // already dark enough, no change needed

        RgbToHsv(c.R, c.G, c.B, out double h, out double s, out double v);

        double lo = 0.0, hi = v;
        for (int i = 0; i < 24; i++)
        {
            double mid  = (lo + hi) * 0.5;
            var    test = HsvToRgbColor(c.A, h, s, mid);
            double cr   = 1.05 / (RelativeLuminance(test) + 0.05);
            if (cr >= targetContrast) hi = mid;
            else                      lo = mid;
        }
        return HsvToRgbColor(c.A, h, s, hi);
    }

    public static void RgbToHsv(byte r, byte g, byte b,
                                 out double h, out double s, out double v)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max   = Math.Max(rf, Math.Max(gf, bf));
        double min   = Math.Min(rf, Math.Min(gf, bf));
        double delta = max - min;

        v = max;
        s = max < 1e-10 ? 0 : delta / max;

        if (delta < 1e-10) { h = 0; return; }
        if      (max == rf) h = 60 * (((gf - bf) / delta) % 6);
        else if (max == gf) h = 60 * (((bf - rf) / delta) + 2);
        else                h = 60 * (((rf - gf) / delta) + 4);
        if (h < 0) h += 360;
    }

    public static Color HsvToRgbColor(byte a, double h, double s, double v)
    {
        if (s < 1e-10) { byte grey = (byte)(v * 255); return Color.FromArgb(a, grey, grey, grey); }
        double c1 = v * s;
        double x  = c1 * (1 - Math.Abs((h / 60) % 2 - 1));
        double m  = v - c1;
        double r1, g1, b1;
        switch ((int)(h / 60) % 6)
        {
            case 0:  r1 = c1; g1 = x;  b1 = 0;  break;
            case 1:  r1 = x;  g1 = c1; b1 = 0;  break;
            case 2:  r1 = 0;  g1 = c1; b1 = x;  break;
            case 3:  r1 = 0;  g1 = x;  b1 = c1; break;
            case 4:  r1 = x;  g1 = 0;  b1 = c1; break;
            default: r1 = c1; g1 = 0;  b1 = x;  break;
        }
        return Color.FromArgb(a,
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    public static double RelativeLuminance(Color c)
    {
        static double Lin(byte ch)
        {
            double v = ch / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }
}
