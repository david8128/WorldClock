using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

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
    // ── OS version thresholds ─────────────────────────────────────────────────

    private static readonly bool IsAtLeastWin10_1803 =
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134);

    private static readonly bool IsAtLeastWin11_22H2 =
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    // ── Tier detection and gradient encoding ──────────────────────────────────

    /// <summary>The acrylic tier available on the current OS build.</summary>
    internal static AcrylicTier DetectedTier =>
        IsAtLeastWin11_22H2 ? AcrylicTier.Win11_22H2 :
        IsAtLeastWin10_1803 ? AcrylicTier.Win10_1803 :
        AcrylicTier.Legacy;

    /// <summary>
    /// Encodes a colour and alpha into the Windows BGR gradient format used by
    /// <see cref="AccentPolicy.GradientColor"/>: <c>0xAABBGGRR</c>.
    /// </summary>
    internal static uint EncodeGradient(Color color, byte alpha) =>
        ((uint)alpha   << 24) |
        ((uint)color.B << 16) |
        ((uint)color.G <<  8) |
        color.R;

    // ── Opacity → alpha mapping ───────────────────────────────────────────────
    //
    // Two separate alpha scales are needed for layered transparency to work:
    //
    //   DWM tint alpha  – applied to the blurred-desktop colour by the compositor.
    //     Capped at 50 (~20 %) so the Gaussian blur dominates and the tint is just
    //     a colour hint, not a coloured wash.
    //
    //   WPF panel alpha – the SolidColorBrush alpha on root/mid/card Borders.
    //     Capped at 110 (~43 %) so even at slider=100 % the window is at least
    //     57 % transparent and the DWM blur-behind shows through clearly.
    //
    // Reference: Windows 10/11 Acrylic material uses a similar two-layer model
    // (tint layer + noise layer on top of Gaussian-blurred exclusion layer).
    // https://learn.microsoft.com/windows/apps/design/style/acrylic

    /// <summary>Maximum DWM tint alpha (0xAABBGGRR alpha byte).</summary>
    internal const byte MaxTintAlpha       = 50;    // ~20 % — colour hint, barely visible tint

    /// <summary>Maximum WPF background-panel alpha.</summary>
    internal const byte MaxBackgroundAlpha = 110;   // ~43 % — min 57 % transparency at slider max

    /// <summary>
    /// Converts the opacity slider value (0.0–1.0) to the DWM tint alpha byte.
    /// Capped at <see cref="MaxTintAlpha"/> so the blur layer is always partially visible.
    /// </summary>
    internal static byte ToTintAlpha(double opacity) =>
        (byte)(Math.Clamp(opacity, 0.0, 1.0) * MaxTintAlpha);

    /// <summary>
    /// Converts the opacity slider value (0.0–1.0) to the WPF background-panel alpha byte.
    /// Capped at <see cref="MaxBackgroundAlpha"/> so DWM blur-behind shows through at all
    /// slider positions.
    /// </summary>
    internal static byte ToBackgroundAlpha(double opacity) =>
        (byte)(Math.Clamp(opacity, 0.0, 1.0) * MaxBackgroundAlpha);

    // ── DWM attribute constants ───────────────────────────────────────────────

    /// <summary>
    /// Tells the Windows shell whether this window uses a dark colour scheme.
    /// Works on Win10 1809+ including WS_EX_LAYERED (AllowsTransparency="True") windows.
    /// Mirrors IslandWindow::UseDarkTheme() in Windows Terminal (GH#6620).
    /// </summary>
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    // Note: DWMWA_SYSTEMBACKDROP_TYPE (38) is intentionally omitted.
    // DWM silently ignores it on WS_EX_LAYERED windows (AllowsTransparency="True"),
    // so SetWindowCompositionAttribute is used for all supported OS tiers.

    // ── SetWindowCompositionAttribute types ───────────────────────────────────

    private enum AccentState
    {
        Disabled            = 0,
        Gradient            = 1,
        TransparentGradient = 2,
        BlurBehind          = 3,
        AcrylicBlurBehind   = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int         AccentFlags;
        public uint        GradientColor;   // 0xAABBGGRR  (Windows BGR byte order)
        public int         AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int    Attribute;    // WCA_ACCENT_POLICY = 19
        public IntPtr Data;
        public int    SizeOfData;
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables the best available backdrop effect for the running OS version.
    /// </summary>
    /// <param name="window">Target WPF window.</param>
    /// <param name="tintColor">Base colour for the blur tint.</param>
    /// <param name="tintAlpha">Opacity of the colour tint (0 = pure blur, 220 = nearly opaque).</param>
    /// <param name="isDarkTheme">
    ///   Whether the active colour scheme is dark.  Passed to DWMWA_USE_IMMERSIVE_DARK_MODE
    ///   exactly as Windows Terminal does via IslandWindow::UseDarkTheme().
    /// </param>
    /// <returns>The <see cref="AcrylicTier"/> that was actually applied.</returns>
    public static AcrylicTier Enable(Window window, Color tintColor, byte tintAlpha, bool isDarkTheme)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return DetectedTier;

            // Dark-mode shell flag — works on all tiers including WS_EX_LAYERED windows.
            // Mirrors IslandWindow::UseDarkTheme() from Windows Terminal (GH#6620).
            SetDwmAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, isDarkTheme ? 1 : 0);

            uint gradient = EncodeGradient(tintColor, tintAlpha);

            var tier = DetectedTier;
            if (tier >= AcrylicTier.Win10_1803)
            {
                // Windows 10 1803+ and all Windows 11 builds:
                // AcrylicBlurBehind provides a Gaussian-blurred, tinted compositor layer.
                // Falls back to plain BlurBehind only if the Acrylic call fails.
                bool ok = ApplyLegacyAccent(hwnd, AccentState.AcrylicBlurBehind, gradient);
                if (!ok) ApplyLegacyAccent(hwnd, AccentState.BlurBehind, gradient);
            }
            else
            {
                // Older Windows: plain blur is the best available option.
                ApplyLegacyAccent(hwnd, AccentState.BlurBehind, gradient);
            }
            return tier;
        }
        catch { return DetectedTier; /* unsupported platform — fail silently */ }
    }

    /// <summary>Removes all backdrop effects from the window.</summary>
    public static void Disable(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;
            ApplyLegacyAccent(hwnd, AccentState.Disabled, 0);
        }
        catch { }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Calls DwmSetWindowAttribute with an int value.
    /// Failures are swallowed so that unsupported attributes on older OS versions
    /// do not surface as exceptions (same pattern used in Windows Terminal).
    /// </summary>
    private static void SetDwmAttribute(IntPtr hwnd, int attribute, int value)
    {
        try { DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)); }
        catch { /* attribute not supported on this OS version */ }
    }

    /// <summary>
    /// Applies an accent / composition effect via SetWindowCompositionAttribute.
    /// This is the "legacy" acrylic path used for tinted blur on Windows 10/11,
    /// and for tint overlays on Win11 22H2+ in combination with DWM backdrops.
    /// </summary>
    private static bool ApplyLegacyAccent(IntPtr hwnd, AccentState state, uint gradient)
    {
        var accent = new AccentPolicy
        {
            AccentState   = state,
            AccentFlags   = 2,
            GradientColor = gradient,
        };
        int  size = Marshal.SizeOf<AccentPolicy>();
        var  ptr  = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute  = 19,  // WCA_ACCENT_POLICY
                Data       = ptr,
                SizeOfData = size,
            };
            // SetWindowCompositionAttribute returns BOOL: nonzero = success, 0 = failure.
            return SetWindowCompositionAttribute(hwnd, ref data) != 0;
        }
        catch { return false; }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}
