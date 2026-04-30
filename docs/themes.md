# Themes

WorldClock ships with **11 built-in colour themes** inspired by popular developer editors. Themes apply live — no restart required. Open **Settings → Appearance** to switch.

Each theme defines six semantic colour slots:

| Slot | Role |
|---|---|
| `BackgroundDark` | Outermost window background |
| `BackgroundMid` | Secondary panels, separators |
| `BackgroundCard` | Individual clock card background |
| `TextPrimary` | Main labels, city names, times |
| `TextDim` | Secondary labels, offsets, DST badge |
| `AccentPrimary` | Highlights, selection markers, home badge |

---

## Available themes

### Dark themes

| Theme | Background | Accent | Inspired by |
|---|---|---|---|
| **Dark Default** | `#0D0D1A` deep navy | `#00E5FF` cyan | WorldClock custom |
| **One Dark** | `#21252B` charcoal | `#61AFEF` sky blue | Atom / VS Code One Dark |
| **Monokai** | `#272822` dark olive | `#A6E22E` lime green | Sublime Text Monokai |
| **Solarized Dark** | `#002B36` deep teal | `#268BD2` blue | Solarized by Ethan Schoonover |
| **Nord Dark** | `#2E3440` slate | `#88C0D0` frost blue | Nord by Arctic Ice Studio |
| **Tokyo Night** | `#1A1B26` midnight | `#7AA2F7` purple-blue | Tokyo Night VS Code |
| **Catppuccin Mocha** | `#1E1E2E` dark mauve | `#CBA6F7` lavender | Catppuccin Mocha |
| **Ariake Dark** | `#0F1117` near-black | `#539BF5` blue | Ariake for VS Code |

### Light themes

| Theme | Background | Accent | Inspired by |
|---|---|---|---|
| **Light Default** | `#F5F5F5` near-white | `#0078D4` Microsoft blue | WorldClock custom |
| **Solarized Light** | `#FDF6E3` cream | `#268BD2` blue | Solarized Light |
| **Catppuccin Latte** | `#EFF1F5` soft white | `#8839EF` mauve | Catppuccin Latte |

---

## Transparency / acrylic

The **Opacity** slider (0 % → 100 %) controls window transparency using Windows acrylic composition. At values below 100 % the desktop behind the window is blurred and blended with the active theme's background colour.

- Changes apply **live** — drag the slider and see the effect immediately.
- The setting persists across restarts.
- On systems where acrylic is not available (some virtual machines, Remote Desktop sessions) the window falls back to a standard transparent background.

---

## Automatic contrast adjustment

On **light themes**, each city's per-card accent colour (used for the left border stripe, accent badge, and IATA code chip) is automatically darkened until it achieves WCAG AA contrast (4.5:1) against the white card background. Dark themes always display the original vivid accent colour unchanged.

This is handled by `ThemeColorHelper.ThemedBrush()` which shifts the HSV Value channel down until the contrast target is met, preserving hue and saturation.

---

## Adding a custom theme

Themes are defined as `AppTheme` records in [`WorldClock/Models/AppTheme.cs`](../WorldClock/Models/AppTheme.cs). To add one, append a new entry to the `All` array:

```csharp
new AppTheme
{
    Name           = "My Theme",
    BackgroundDark = C("#1C1C2E"),
    BackgroundMid  = C("#2A2A40"),
    BackgroundCard = C("#33334D"),
    TextPrimary    = C("#E0E0FF"),
    TextDim        = C("#6060A0"),
    AccentPrimary  = C("#FF79C6"),
    Separator      = C("#44445A"),
},
```

The new theme will appear in the Settings dropdown immediately without any further plumbing.
