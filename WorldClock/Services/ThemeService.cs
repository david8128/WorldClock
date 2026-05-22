using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WorldClock.Helpers;
using WorldClock.Models;

namespace WorldClock.Services;

/// <summary>
/// Singleton service that holds the active theme and transparency level.
/// Calling <see cref="Apply"/> pushes colours into App.Current.Resources so all
/// static-resource bindings across the app update automatically.
/// Settings are persisted via <see cref="SettingsService"/> on every change.
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static ThemeService Instance { get; } = new();

    private ThemeService()
    {
        // Restore persisted theme and opacity on first access.
        // Guard: unit-test processes have no AppData path concerns, Load() returns defaults safely.
        var saved = SettingsService.Instance.Load();
        _theme           = AppTheme.All.FirstOrDefault(t => t.Name == saved.ThemeName) ?? AppTheme.All[0];
        _opacity         = Math.Clamp(saved.Opacity, 0.1, 1.0);  // always honour the last saved value
        _scaleMode       = saved.ScaleMode;
        _showDiagnostics = saved.ShowDiagnostics;
        // Use the live registry state as the source of truth so external changes are reflected.
        _runOnStartup    = StartupHelper.IsRegistered;
    }

    // ── Active theme ──────────────────────────────────────────────────────────

    private AppTheme _theme;

    public AppTheme ActiveTheme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            OnPropertyChanged();
            Apply();
            Persist();
        }
    }

    // ── Opacity / transparency (1.0 = opaque, 0.1 = maximum transparency) ───────────

    private double _opacity;   // initialised from saved settings in constructor

    public double Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0.1, 1.0);
            if (Math.Abs(_opacity - clamped) < 0.001) return;
            _opacity = clamped;
            OnPropertyChanged();
            ApplyOpacity();
            Persist();
        }
    }

    // ── Delete-mode toggle ────────────────────────────────────────────────────

    private bool _deleteMode;

    public bool DeleteMode
    {
        get => _deleteMode;
        set { _deleteMode = value; OnPropertyChanged(); }
    }

    // ── Edit-mode toggle (transient – not persisted) ──────────────────────────

    private bool _editMode;

    public bool EditMode
    {
        get => _editMode;
        set { _editMode = value; OnPropertyChanged(); }
    }
    // ── Run on startup toggle (persisted to registry + settings.json) ─────────

    private bool _runOnStartup;

    public bool RunOnStartup
    {
        get => _runOnStartup;
        set
        {
            if (_runOnStartup == value) return;
            _runOnStartup = value;
            OnPropertyChanged();
            StartupHelper.SetStartup(value);
            Persist();
        }
    }

    // ── Diagnostics window toggle ─────────────────────────────────────────────

    private bool _showDiagnostics;

    public bool ShowDiagnostics
    {
        get => _showDiagnostics;
        set
        {
            if (_showDiagnostics == value) return;
            _showDiagnostics = value;
            OnPropertyChanged();
            Persist();
        }
    }

    // ── Scale mode ────────────────────────────────────────────────────────────

    private ScaleMode _scaleMode;

    public ScaleMode ScaleMode
    {
        get => _scaleMode;
        set
        {
            if (_scaleMode == value) return;
            _scaleMode = value;
            OnPropertyChanged();
            Persist();
        }
    }
    // ── Apply theme to App.Resources ─────────────────────────────────────────

    public void Apply()
    {
        // Guard: in unit-test processes there is no running Avalonia Application
        if (Application.Current is null) return;

        var res = (Application.Current.Resources as ResourceDictionary)!;

        // Text and accent brushes are always fully opaque
        SetBrush(res, "BrushTextPrimary", _theme.TextPrimary);
        SetBrush(res, "BrushTextDim",     _theme.TextDim);
        SetBrush(res, "BrushAccentCyan",  _theme.AccentPrimary);

        SetColor(res, "AccentCyan", _theme.AccentPrimary);
        SetColor(res, "TextDim",    _theme.TextDim);

        // Glow effects look great on dark backgrounds but hurt readability on light ones.
        // Expose a double resource that XAML DropShadowEffects bind to.
        var glowOpacity = _theme.IsDark ? 0.7 : 0.0;
        res["GlowOpacity"]        = glowOpacity;
        res["GlowOpacitySubtle"]  = _theme.IsDark ? 0.35 : 0.0;
        // White stroke glow for emoji icons (country flag, UTC globe, world clock globe).
        // Higher opacity than GlowOpacitySubtle so the stroke is clearly visible on dark.
        res["IconGlowOpacity"]    = _theme.IsDark ? 0.9 : 0.0;
        res["CardShadowOpacity"]  = _theme.IsDark ? 0.55 : 0.20;
        // Card shadow color: dark for light themes, black for dark themes
        var shadowColor = _theme.IsDark
            ? Color.FromRgb(0, 0, 0)
            : Color.FromRgb(0, 0, 0);
        res["CardShadowColor"] = shadowColor;

        // DST active badge: bright yellow on dark backgrounds, readable dark-amber on light.
        var dstColor = _theme.IsDark
            ? Color.FromRgb(0xFF, 0xD6, 0x00)   // #FFD600
            : Color.FromRgb(0x92, 0x40, 0x00);  // #924000
        SetBrush(res, "BrushDstActive", dstColor);

        // UTC banner gradient: dark navy on dark themes, neutral card surface on light.
        if (_theme.IsDark)
        {
            res["BannerGradientStart"] = Color.FromArgb(0xBB, 0x0A, 0x16, 0x28);
            res["BannerGradientEnd"]   = Color.FromArgb(0xBB, 0x00, 0x33, 0x66);
        }
        else
        {
            var bm = _theme.BackgroundMid;
            var bd = _theme.BackgroundDark;
            res["BannerGradientStart"] = Color.FromArgb(0xFF, bm.R, bm.G, bm.B);
            res["BannerGradientEnd"]   = Color.FromArgb(0xFF, bd.R, bd.G, bd.B);
        }

        // Background brushes respect the current opacity setting
        ApplyOpacity();
    }

    private void ApplyOpacity()
    {
        if (Application.Current is null) return;

        var res   = (Application.Current.Resources as ResourceDictionary)!;
        // Cap at MaxBackgroundAlpha (~43 %) so the window is always at least 57 %
        // transparent. Using 255 (fully opaque) would cover the compositor blur completely.
        var alpha = AcrylicHelper.ToBackgroundAlpha(_opacity);

        // Background brushes carry the transparency alpha channel.
        // This works because AllowsTransparency="True" is set on the window.
        SetBrushAlpha(res, "BrushBackgroundDark", _theme.BackgroundDark, alpha);
        SetBrushAlpha(res, "BrushBackgroundMid",  _theme.BackgroundMid,  alpha);
        SetBrushAlpha(res, "BrushBackgroundCard", _theme.BackgroundCard, alpha);
        SetBrushAlpha(res, "BrushSeparator",      _theme.Separator,      alpha);

        SetColorAlpha(res, "BackgroundCard", _theme.BackgroundCard, alpha);
    }

    private static void SetBrush(ResourceDictionary res, string key, Color color)
    {
        res[key] = new SolidColorBrush(color);
    }

    private static void SetBrushAlpha(ResourceDictionary res, string key, Color baseColor, byte alpha)
    {
        res[key] = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
    }

    private static void SetColor(ResourceDictionary res, string key, Color color)
    {
        res[key] = color;
    }

    private static void SetColorAlpha(ResourceDictionary res, string key, Color baseColor, byte alpha)
    {
        res[key] = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Persistence ──────────────────────────────────────────────────────────

    /// <summary>
    /// Saves theme + opacity to disk.  City list is owned by MainViewModel,
    /// which calls <see cref="PersistWithCities"/> when the city list changes.
    /// </summary>
    internal void Persist()
    {
        var saved = SettingsService.Instance.Load();
        saved.ThemeName       = _theme.Name;
        saved.Opacity         = _opacity;
        saved.ScaleMode       = _scaleMode;
        saved.ShowDiagnostics = _showDiagnostics;
        saved.RunOnStartup    = _runOnStartup;
        SettingsService.Instance.Save(saved);
    }
}
