using Avalonia.Media;
using FluentAssertions;
using WorldClock.Helpers;
using Xunit;

namespace WorldClock.Tests.Helpers;

/// <summary>
/// Tests for <see cref="AcrylicHelper"/>:
///   • Tier detection — OS version mapping to <see cref="AcrylicTier"/> (no window needed).
///   • Gradient encoding — 0xAABBGGRR byte-order math (no window needed).
///   • Enable / Disable smoke tests — verify the P/Invoke path never throws on any OS version.
/// </summary>
public sealed class AcrylicHelperTests
{
    // ── Tier detection (pure computation, no window needed) ───────────────────

    [Fact]
    public void AcrylicHelper_DetectedTier_IsDefinedEnumValue()
    {
        var tier = AcrylicHelper.DetectedTier;
        Enum.IsDefined(typeof(AcrylicTier), tier).Should().BeTrue(
            "DetectedTier must always resolve to a valid AcrylicTier member");
    }

    [Fact]
    public void AcrylicHelper_DetectedTier_IsAtLeastWin10_1803_OnCurrentMachine()
    {
        // AcrylicHelper is a cross-platform no-op stub on non-Windows; Legacy is expected.
        // On Windows 10 1803+ it should be Win10_1803 or higher.
        if (!OperatingSystem.IsWindows())
        {
            AcrylicHelper.DetectedTier.Should().Be(AcrylicTier.Legacy,
                "non-Windows platforms always return Legacy tier");
            return;
        }
        AcrylicHelper.DetectedTier.Should().NotBe(AcrylicTier.Legacy,
            "the build machine must be running Windows 10 1803 or later");
    }

    [Fact]
    public void AcrylicHelper_DetectedTier_IsWin11_22H2_WhenOSIsWin11_22H2()
    {
        // Conditional: only assert when actually on Win11 22H2+.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return;
        AcrylicHelper.DetectedTier.Should().Be(AcrylicTier.Win11_22H2);
    }

    [Fact]
    public void AcrylicHelper_DetectedTier_IsWin10_1803_WhenOnWin10()
    {
        // Conditional: only assert when on Win10 1803+ but below Win11 22H2.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134)) return;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return;
        AcrylicHelper.DetectedTier.Should().Be(AcrylicTier.Win10_1803);
    }

    // ── Gradient encoding (pure math, no window needed) ───────────────────────
    //
    // The AccentPolicy.GradientColor field uses Windows BGR order: 0xAABBGGRR
    //   Bits 31–24: alpha    (most-significant byte)
    //   Bits 23–16: Blue
    //   Bits 15–8:  Green
    //   Bits 7–0:   Red      (least-significant byte)

    [Fact]
    public void AcrylicHelper_EncodeGradient_Red_HalfAlpha_Produces_0x800000FF()
    {
        // R=0xFF, G=0x00, B=0x00, alpha=0x80 → 0x80 | 0x00_00_FF
        var red = Color.FromRgb(0xFF, 0x00, 0x00);
        AcrylicHelper.EncodeGradient(red, 0x80).Should().Be(0x800000FFu,
            "Red channel maps to the least-significant byte (RR), alpha to the most-significant");
    }

    [Fact]
    public void AcrylicHelper_EncodeGradient_Blue_ZeroAlpha_Produces_0x00FF0000()
    {
        // B=0xFF → bits 23–16, alpha=0x00 → bits 31–24
        var blue = Color.FromRgb(0x00, 0x00, 0xFF);
        AcrylicHelper.EncodeGradient(blue, 0x00).Should().Be(0x00FF0000u,
            "Blue channel maps to bits 23–16 (BB) in Windows BGR format");
    }

    [Fact]
    public void AcrylicHelper_EncodeGradient_Green_ZeroAlpha_Produces_0x0000FF00()
    {
        // G=0xFF → bits 15–8, alpha=0x00
        var green = Color.FromRgb(0x00, 0xFF, 0x00);
        AcrylicHelper.EncodeGradient(green, 0x00).Should().Be(0x0000FF00u,
            "Green channel maps to bits 15–8 (GG) in Windows BGR format");
    }

    [Fact]
    public void AcrylicHelper_EncodeGradient_FullAlpha_MostSignificantByteIsFF()
    {
        var color = Color.FromRgb(0x1E, 0x1E, 0x2E);
        var result = AcrylicHelper.EncodeGradient(color, 0xFF);
        (result >> 24).Should().Be(0xFFu, "alpha=0xFF must occupy bits 31–24");
    }

    [Fact]
    public void AcrylicHelper_EncodeGradient_ZeroAlpha_MostSignificantByteIsZero()
    {
        var color = Color.FromRgb(0xFF, 0xFF, 0xFF);
        var result = AcrylicHelper.EncodeGradient(color, 0x00);
        (result >> 24).Should().Be(0x00u, "alpha=0x00 must produce zero in bits 31–24");
    }

    [Fact]
    public void AcrylicHelper_EncodeGradient_Black_FullAlpha_IsOnlyAlphaBits()
    {
        // All colour channels zero → only the alpha byte should be set.
        var black = Color.FromRgb(0x00, 0x00, 0x00);
        AcrylicHelper.EncodeGradient(black, 0xFF).Should().Be(0xFF000000u);
    }

    // ── Opacity → alpha mapping (pure math, no window needed) ─────────────────
    //
    // These tests lock down the opacity→alpha contract so that no one accidentally
    // raises the caps back to 255 and breaks the acrylic transparency.
    //
    // Design rationale (per AcrylicHelper XML-doc):
    //   MaxTintAlpha       = 50   (~20 %) — DWM blur tint stays barely-visible colour hint
    //   MaxBackgroundAlpha = 110  (~43 %) — WPF panels min 57 % transparent at slider max

    // ToTintAlpha ──────────────────────────────────────────────────────────────

    [Fact]
    public void AcrylicHelper_ToTintAlpha_ZeroOpacity_IsZero()
    {
        AcrylicHelper.ToTintAlpha(0.0).Should().Be(0,
            "zero opacity means fully transparent tint (pure blur visible)");
    }

    [Fact]
    public void AcrylicHelper_ToTintAlpha_MaxOpacity_IsMaxTintAlpha()
    {
        AcrylicHelper.ToTintAlpha(1.0).Should().Be(AcrylicHelper.MaxTintAlpha,
            "slider at 100 % must not exceed MaxTintAlpha so blur remains visible");
    }

    [Fact]
    public void AcrylicHelper_ToTintAlpha_MaxOpacity_IsNeverFullyOpaque()
    {
        AcrylicHelper.ToTintAlpha(1.0).Should().BeLessThan(255,
            "a tint alpha of 255 would turn the DWM blur into a solid colour");
    }

    [Fact]
    public void AcrylicHelper_ToTintAlpha_HalfOpacity_IsHalfOfMax()
    {
        var expected = (byte)(0.5 * AcrylicHelper.MaxTintAlpha);
        AcrylicHelper.ToTintAlpha(0.5).Should().Be(expected);
    }

    [Fact]
    public void AcrylicHelper_ToTintAlpha_OverOpacity_ClampsToMax()
    {
        // Values outside [0,1] must clamp, not overflow the byte.
        AcrylicHelper.ToTintAlpha(2.0).Should().Be(AcrylicHelper.MaxTintAlpha);
    }

    [Fact]
    public void AcrylicHelper_ToTintAlpha_NegativeOpacity_ClampsToZero()
    {
        AcrylicHelper.ToTintAlpha(-1.0).Should().Be(0);
    }

    // ToBackgroundAlpha ────────────────────────────────────────────────────────

    [Fact]
    public void AcrylicHelper_ToBackgroundAlpha_ZeroOpacity_IsZero()
    {
        AcrylicHelper.ToBackgroundAlpha(0.0).Should().Be(0,
            "zero opacity means fully transparent WPF panels (pure acrylic visible)");
    }

    [Fact]
    public void AcrylicHelper_ToBackgroundAlpha_MaxOpacity_IsMaxBackgroundAlpha()
    {
        AcrylicHelper.ToBackgroundAlpha(1.0).Should().Be(AcrylicHelper.MaxBackgroundAlpha,
            "slider at 100 % must not exceed MaxBackgroundAlpha so blur still shows");
    }

    [Fact]
    public void AcrylicHelper_ToBackgroundAlpha_MaxOpacity_IsNeverFullyOpaque()
    {
        AcrylicHelper.ToBackgroundAlpha(1.0).Should().BeLessThan(255,
            "alpha=255 would make WPF panels fully opaque and hide the DWM blur behind them");
    }

    [Fact]
    public void AcrylicHelper_ToBackgroundAlpha_HalfOpacity_IsHalfOfMax()
    {
        var expected = (byte)(0.5 * AcrylicHelper.MaxBackgroundAlpha);
        AcrylicHelper.ToBackgroundAlpha(0.5).Should().Be(expected);
    }

    [Fact]
    public void AcrylicHelper_ToBackgroundAlpha_OverOpacity_ClampsToMax()
    {
        AcrylicHelper.ToBackgroundAlpha(2.0).Should().Be(AcrylicHelper.MaxBackgroundAlpha);
    }

    [Fact]
    public void AcrylicHelper_ToBackgroundAlpha_NegativeOpacity_ClampsToZero()
    {
        AcrylicHelper.ToBackgroundAlpha(-0.5).Should().Be(0);
    }

    // Constants sanity ─────────────────────────────────────────────────────────

    [Fact]
    public void AcrylicHelper_MaxTintAlpha_IsLessThan255()
    {
        // Regression guard: must never be raised to 255.
        ((int)AcrylicHelper.MaxTintAlpha).Should().BeLessThan(255,
            "MaxTintAlpha=255 would make the DWM tint a solid colour with no blur visible");
    }

    [Fact]
    public void AcrylicHelper_MaxBackgroundAlpha_IsLessThan255()
    {
        // Regression guard: must never be raised to 255.
        ((int)AcrylicHelper.MaxBackgroundAlpha).Should().BeLessThan(255,
            "MaxBackgroundAlpha=255 would cover the DWM blur-behind completely");
    }

    [Fact]
    public void AcrylicHelper_MaxTintAlpha_IsLessThanMaxBackgroundAlpha()
    {
        // The tint (DWM layer) should be lighter than the WPF panel overlay so
        // the colour saturation comes from the panels, not the raw blur tint.
        ((int)AcrylicHelper.MaxTintAlpha).Should().BeLessThan(
            AcrylicHelper.MaxBackgroundAlpha,
            "tint alpha should be lighter than the WPF background alpha");
    }

    // ── Enable / Disable smoke tests (cross-platform stub) ────────────────────
    //
    // AcrylicHelper.Enable / Disable are no-op stubs on non-Windows (Avalonia
    // handles transparency via TransparencyLevelHint="AcrylicBlur").
    // These tests verify the stubs never throw regardless of arguments.

    [Fact]
    public void AcrylicHelper_Enable_DarkTheme_DoesNotThrow()
    {
        var act = () => AcrylicHelper.Enable(null, Color.FromRgb(0x1E, 0x1E, 0x2E), 180, true);
        act.Should().NotThrow("Enable must fail silently on all supported OS versions");
    }

    [Fact]
    public void AcrylicHelper_Enable_LightTheme_DoesNotThrow()
    {
        var act = () => AcrylicHelper.Enable(null, Color.FromRgb(0xF5, 0xF5, 0xF5), 220, false);
        act.Should().NotThrow();
    }

    [Fact]
    public void AcrylicHelper_Enable_ZeroAlpha_DoesNotThrow()
    {
        var act = () => AcrylicHelper.Enable(null, Color.FromRgb(0x1E, 0x1E, 0x2E), 0, true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AcrylicHelper_Enable_MaxAlpha_DoesNotThrow()
    {
        var act = () => AcrylicHelper.Enable(null, Color.FromRgb(0x1E, 0x1E, 0x2E), 255, true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AcrylicHelper_MultipleEnableCalls_DoNotThrow()
    {
        var act = () =>
        {
            AcrylicHelper.Enable(null, Color.FromRgb(0x1E, 0x1E, 0x2E), 150, true);
            AcrylicHelper.Enable(null, Color.FromRgb(0xF5, 0xF5, 0xF5), 200, false);
            AcrylicHelper.Enable(null, Color.FromRgb(0x24, 0x28, 0x3B), 100, true);
        };
        act.Should().NotThrow();
    }
}
