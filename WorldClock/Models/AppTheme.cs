using Avalonia.Media;

namespace WorldClock.Models;

/// <summary>
/// Represents a complete colour theme for the app.
/// All colour properties are immutable (set at construction time).
/// </summary>
public sealed class AppTheme
{
    public required string Name { get; init; }

    // ── Backgrounds ───────────────────────────────────────────────────────────
    public required Color BackgroundDark { get; init; }
    public required Color BackgroundMid  { get; init; }
    public required Color BackgroundCard { get; init; }

    // ── Foreground / text ─────────────────────────────────────────────────────
    public required Color TextPrimary    { get; init; }
    public required Color TextDim        { get; init; }

    // ── Accent ────────────────────────────────────────────────────────────────
    public required Color AccentPrimary  { get; init; }

    // ── Card separator ────────────────────────────────────────────────────────
    public required Color Separator      { get; init; }

    // ── Pre-built brushes (derived) ───────────────────────────────────────────
    public SolidColorBrush BrushBackgroundDark => new SolidColorBrush(BackgroundDark);
    public SolidColorBrush BrushBackgroundMid  => new SolidColorBrush(BackgroundMid);
    public SolidColorBrush BrushBackgroundCard => new SolidColorBrush(BackgroundCard);
    public SolidColorBrush BrushTextPrimary    => new SolidColorBrush(TextPrimary);
    public SolidColorBrush BrushTextDim        => new SolidColorBrush(TextDim);
    public SolidColorBrush BrushAccentPrimary  => new SolidColorBrush(AccentPrimary);
    public SolidColorBrush BrushSeparator      => new SolidColorBrush(Separator);

    /// <summary>
    /// True when the theme background is perceived as dark.
    /// Computed from the ITU-R BT.709 relative luminance of <see cref="BackgroundDark"/>.
    /// Used to drive DWMWA_USE_IMMERSIVE_DARK_MODE (see AcrylicHelper), mirroring
    /// how Windows Terminal decides whether to call UseDarkTheme(true/false).
    /// </summary>
    public bool IsDark
    {
        get
        {
            var c   = BackgroundDark;
            var lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
            return lum < 0.5;   // below mid-luminance → dark theme
        }
    }

    public override string ToString() => Name;


    // ── Built-in themes ───────────────────────────────────────────────────────

    public static readonly AppTheme[] All =
    [
        new AppTheme
        {
            Name            = "Dark Default",
            BackgroundDark  = C("#0D0D1A"),
            BackgroundMid   = C("#12122A"),
            BackgroundCard  = C("#1A1A35"),
            TextPrimary     = C("#FFFFFF"),
            TextDim         = C("#607D8B"),
            AccentPrimary   = C("#00E5FF"),
            Separator       = C("#2A2A4A"),
        },
        new AppTheme
        {
            Name            = "Light Default",
            BackgroundDark  = C("#F5F5F5"),
            BackgroundMid   = C("#E8E8E8"),
            BackgroundCard  = C("#FFFFFF"),
            TextPrimary     = C("#1A1A1A"),
            TextDim         = C("#757575"),
            AccentPrimary   = C("#0078D4"),
            Separator       = C("#DCDCDC"),
        },
        new AppTheme
        {
            Name            = "One Dark",
            BackgroundDark  = C("#21252B"),
            BackgroundMid   = C("#282C34"),
            BackgroundCard  = C("#2C313A"),
            TextPrimary     = C("#ABB2BF"),
            TextDim         = C("#5C6370"),
            AccentPrimary   = C("#61AFEF"),
            Separator       = C("#3E4451"),
        },
        new AppTheme
        {
            Name            = "Monokai",
            BackgroundDark  = C("#272822"),
            BackgroundMid   = C("#2D2E27"),
            BackgroundCard  = C("#3E3D32"),
            TextPrimary     = C("#F8F8F2"),
            TextDim         = C("#75715E"),
            AccentPrimary   = C("#A6E22E"),
            Separator       = C("#49483E"),
        },
        new AppTheme
        {
            Name            = "Solarized Dark",
            BackgroundDark  = C("#002B36"),
            BackgroundMid   = C("#073642"),
            BackgroundCard  = C("#0D4B5E"),
            TextPrimary     = C("#839496"),
            TextDim         = C("#586E75"),
            AccentPrimary   = C("#268BD2"),
            Separator       = C("#164B5A"),
        },
        new AppTheme
        {
            Name            = "Solarized Light",
            BackgroundDark  = C("#FDF6E3"),
            BackgroundMid   = C("#EEE8D5"),
            BackgroundCard  = C("#FFFFFF"),
            TextPrimary     = C("#657B83"),
            TextDim         = C("#93A1A1"),
            AccentPrimary   = C("#268BD2"),
            Separator       = C("#D3CCB8"),
        },
        new AppTheme
        {
            Name            = "Nord Dark",
            BackgroundDark  = C("#2E3440"),
            BackgroundMid   = C("#3B4252"),
            BackgroundCard  = C("#434C5E"),
            TextPrimary     = C("#ECEFF4"),
            TextDim         = C("#4C566A"),
            AccentPrimary   = C("#88C0D0"),
            Separator       = C("#4C566A"),
        },
        new AppTheme
        {
            Name            = "Tokyo Night",
            BackgroundDark  = C("#1A1B26"),
            BackgroundMid   = C("#16161E"),
            BackgroundCard  = C("#1F2335"),
            TextPrimary     = C("#C0CAF5"),
            TextDim         = C("#565F89"),
            AccentPrimary   = C("#7AA2F7"),
            Separator       = C("#292E42"),
        },
        new AppTheme
        {
            Name            = "Catppuccin Mocha",
            BackgroundDark  = C("#1E1E2E"),
            BackgroundMid   = C("#181825"),
            BackgroundCard  = C("#313244"),
            TextPrimary     = C("#CDD6F4"),
            TextDim         = C("#585B70"),
            AccentPrimary   = C("#CBA6F7"),
            Separator       = C("#45475A"),
        },
        new AppTheme
        {
            Name            = "Catppuccin Latte",
            BackgroundDark  = C("#EFF1F5"),
            BackgroundMid   = C("#E6E9EF"),
            BackgroundCard  = C("#FFFFFF"),
            TextPrimary     = C("#4C4F69"),
            TextDim         = C("#9CA0B0"),
            AccentPrimary   = C("#8839EF"),
            Separator       = C("#CCD0DA"),
        },
        new AppTheme
        {
            Name            = "Ariake Dark",
            BackgroundDark  = C("#0F1117"),
            BackgroundMid   = C("#161B22"),
            BackgroundCard  = C("#1C2128"),
            TextPrimary     = C("#CDD9E5"),
            TextDim         = C("#545D68"),
            AccentPrimary   = C("#539BF5"),
            Separator       = C("#2D333B"),
        },
    ];

    private static Color C(string hex) => Color.Parse(hex);
}
