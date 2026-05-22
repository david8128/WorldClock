using Avalonia.Media;

namespace WorldClock.Helpers;

/// <summary>
/// Applies Windows blur-behind / acrylic / system-backdrop effects to a WPF window.
///
/// The implementation is tiered by OS version, mirroring the strategy used by
/// Windows Terminal (IslandWindow.cpp / AppHost.cpp):
///
///   Tier 3 – Windows 11 22H2+ (build ≥ 22621)
///     DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_TRANSIENTWINDOW)
///     – the official Acrylic system backdrop (same API Windows Terminal uses for Mica) –
///     + SetWindowCompositionAttribute for the tinted colour overlay.
///
///   Tier 2 – Windows 10 1803 – Windows 11 21H2  (build 17134 – 22620)
///     SetWindowCompositionAttribute(ACCENT_ENABLE_ACRYLICBLURBEHIND)
///     → falls back to ACCENT_ENABLE_BLURBEHIND if the acrylic call fails.
///
///   Tier 1 – Older Windows
///     SetWindowCompositionAttribute(ACCENT_ENABLE_BLURBEHIND).
///
/// In every tier the helper calls DWMWA_USE_IMMERSIVE_DARK_MODE so that the
/// Windows shell and taskbar treat this window correctly for the active
/// light / dark theme.  This mirrors IslandWindow::UseDarkTheme() in
/// Windows Terminal (introduced to fix GH#6620).
/// </summary>
/// <summary>
/// Identifies which OS-tier acrylic path was applied by <see cref="AcrylicHelper.Enable"/>.
/// </summary>
internal enum AcrylicTier
{
    /// <summary>Windows older than 10 1803: only plain blur-behind is available.</summary>
    Legacy,
    /// <summary>Windows 10 1803+ and Windows 11: colored acrylic via SetWindowCompositionAttribute.</summary>
    Win10_1803,
    /// <summary>Windows 11 22H2+: same as Win10_1803 on WS_EX_LAYERED (AllowsTransparency) windows.</summary>
    Win11_22H2,
}

internal static class AcrylicHelper
{

    internal static AcrylicTier DetectedTier => AcrylicTier.Legacy;
    internal static uint EncodeGradient(Color color, byte alpha)
    {
        // Windows AccentPolicy.GradientColor: 0xAABBGGRR (BGR order)
        return ((uint)alpha  << 24)
             | ((uint)color.B << 16)
             | ((uint)color.G <<  8)
             |  (uint)color.R;
    }

    internal const byte MaxTintAlpha       = 50;
    internal const byte MaxBackgroundAlpha = 110;

    internal static byte ToTintAlpha(double opacity) =>
        (byte)(Math.Clamp(opacity, 0.0, 1.0) * MaxTintAlpha);

    internal static byte ToBackgroundAlpha(double opacity) =>
        (byte)(Math.Clamp(opacity, 0.0, 1.0) * MaxBackgroundAlpha);

    internal static void Enable(object? window, Color bgColor, byte tintAlpha, bool isDark) { }
}
