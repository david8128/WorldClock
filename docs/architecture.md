# Architecture

WorldClock is a **.NET 10 + Avalonia 11.2.3** application following the MVVM pattern with a few deliberate design choices that make it feel more like a design-canvas app than a traditional UI form.

---

## Design canvas approach

The entire UI lives inside a single `Grid` named **`DesignRoot`** with a fixed design size of **1100 × 760 px**. This `DesignRoot` is wrapped in a `Viewbox` with `Stretch="Uniform"` (inside a custom `ScaleScrollViewer`), so the app scales like a vector graphic rather than reflowing like a traditional window.

```
Window
└── ScaleScrollViewer
    └── Viewbox (Stretch=Uniform)
        └── DesignRoot (1100 × 760, grows vertically as cities are added)
            ├── Left panel  — clock cards (ObservableCollection<ClockLocation>)
            └── Right panel — Time Visualizer (TimeTranslatorViewModel)
```

### Why a fixed design canvas?

- Pixel-perfect layouts without fighting Avalonia’s auto-layout in complex grid/canvas combos.
- Uniform scaling means the UI looks identical at any monitor DPI or window size.
- Adding a clock row grows `DesignRoot.Height` and `Window.Height` proportionally; the `Viewbox` re-scales automatically.

### Scale modes

Two modes are available (toggled in Settings → Appearance):

| Mode | Behaviour |
|---|---|
| **Proportion Scale** | The `Viewbox` shrinks/grows the entire canvas uniformly. Window can be resized freely. |
| **Min Limit** | Same uniform scale, but `DesignRoot` has minimum pixel dimensions (550 × 380) so the window never becomes unreadably small. |

---

## MVVM wiring

```
MainWindow.axaml.cs
   └── DataContext = MainViewModel
         ├── ObservableCollection<ClockLocation>  ← clock panel ItemsSource
         └── TimeTranslatorViewModel  (Translator)
               ├── ObservableCollection<TimeGridColumn>  ← header row
               └── ObservableCollection<TimeGridRow>     ← one row per location
                     └── ObservableCollection<TimeGridCell>  ← 48 cells (30-min slots)
```

`MainViewModel` owns the `DispatcherTimer` that fires every second and calls `Tick()` on each `ClockLocation` to update live time strings.

`TimeTranslatorViewModel` rebuilds its grid (`BuildGrid()`) whenever the location list or home timezone changes, and translates selected times (`Translate()`) whenever the selection slider moves.

---

## Key services

### `ThemeService` (singleton)

Holds the active `AppTheme` and transparency opacity. Calling `Apply()` pushes colour values into Avalonia application resources so that all `DynamicResource` bindings update without re-rendering the full tree. Also exposes `EditMode` and `DeleteMode` booleans that drive the per-card overlay buttons.

### `SettingsService` (singleton)

Persists `UserSettings` as indented JSON to `%APPDATA%\WorldClock\settings.json` (Windows) or `~/.config/WorldClock/settings.json` (Linux). Loaded once at startup; saved on every meaningful change (city add/remove, theme change, opacity change, home location change).

### `WorldCitySearchService` (static)

Thread-safe lazy loader that merges the curated `CityDatabase.All` list (which includes a built-in UTC entry searchable as `UTC`, `Z`, or `UCT`) with the generated airport CSV (`world_cities_timezones.csv`) if present. Exposes a `Search(query, maxResults)` method with prefix, substring, and IATA-code matching.

---

## Clock card data flow

```
DispatcherTimer (1 s)
   └── MainViewModel.Tick()
         └── foreach ClockLocation → loc.UpdateTime(DateTime.Now)
               ├── CurrentTime  ← formatted local time string
               ├── CurrentDate  ← formatted date string
               ├── UtcOffset    ← e.g. "UTC+5:30"
               └── DstLabel     ← "DST" / "STD"
```

Each `ClockLocation` raises `INotifyPropertyChanged` on every update so Avalonia bindings refresh automatically.

### UTC card

The UTC clock location is always present (added at startup if absent from saved settings). Unlike other cards:

- It **cannot be deleted** (`IsDeleteVisible` always returns `false` for UTC).
- It **can be edited** in edit mode: the label, flag emoji, team label, and accent colour are all customisable and persist across restarts.
- It is **searchable** in the city picker via the query strings `UTC`, `Z`, and `UCT`.

---

## Two-layer Canvas pattern (Time Visualizer)

The visualizer uses an **ItemsControl with a Canvas `ItemsPanel`** for both the column headers and the cell rows. Two independent layers sit on top of each other in a Grid:

- **Layer 1** (StackPanel-within-Canvas): cell background `Border` elements + selection highlight boxes. These are hit-test-visible and respond to mouse clicks.
- **Layer 2** (`IsHitTestVisible="False"` Canvas overlay): label text roots sit here so they float above the backgrounds without stealing mouse events.

Horizontal positioning uses `SlotIndexToCanvasLeftConverter` which multiplies `SlotIndex × 14.0 px` (48 slots × 14 px = 672 px, exactly filling the available space after the 160 px row header and margins).

---

## Mode proxies

`EditModeProxy.Instance` and `DeleteModeProxy.Instance` are thin `INotifyPropertyChanged` singletons that forward `ThemeService.Instance.EditMode` / `.DeleteMode`. They exist because DataTemplates for clock cards have their own `DataContext` (`ClockLocation`), making it impossible to bind directly to `ThemeService` without a proxy or ancestor lookup.

---

## Cross-platform notes

- UI markup uses **`.axaml`** files (Avalonia XAML). Legacy `.xaml` files from the WPF era remain in the repository but are excluded from the build via `<Compile Remove>` directives in the `.csproj`.
- Avalonia resolves boolean negation in bindings with `{Binding !BoolProp}` syntax (no `BooleanToVisibility` converter needed).
- Settings are stored in `%APPDATA%\WorldClock\` on Windows and `~/.config/WorldClock/` on Linux; the path is resolved via `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`.
- The `.csproj` targets `net10.0` (no `-windows` suffix), making the build portable across platforms without conditional logic.

---

## Frameless window

`WindowChrome` with `AllowsTransparency="True"` removes the OS chrome. The `DragHandle` strip at the top handles window dragging. Acrylic composition is applied via P/Invoke to `DwmExtendFrameIntoClientArea` and `SetWindowCompositionAttribute`, with a graceful fallback on systems where acrylic is unavailable.
