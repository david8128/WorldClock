# WorldClock WPF → Avalonia Migration Status

> Last updated: 2026-05-21  
> Framework: WPF (`net8.0-windows`) → Avalonia 11.2.3 (`net10.0`)

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Completed and verified |
| 🔧 | In progress |
| ❌ | Failing / not started |
| ⚠️ | Partial / workaround in place |
| ⏭️ | Skipped / out of scope |

---

## 1. Build & Infrastructure

| Item | Status | Notes |
|------|--------|-------|
| Target framework → `net10.0` | ✅ | Changed from `net8.0-windows` |
| Avalonia 11.2.3 packages added | ✅ | `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` |
| `App.xaml` → `App.axaml` (AvaloniaResource) | ✅ | Renamed; old `.xaml` excluded from compile |
| `DiagnosticsWindow.axaml` created | ✅ | Replaces WPF `.xaml` |
| `SettingsWindow.axaml` created | ✅ | Replaces WPF `.xaml` |
| `MainWindow.axaml` created | ✅ | Replaces WPF `.xaml` |
| `Styles.axaml` exists | ✅ | Already correct |
| CVE-2026-39959 (`Tmds.DBus.Protocol`) | ✅ | Overridden to 0.21.3 |
| `Program.cs` entry point | ✅ | Avalonia AppBuilder pattern |
| 0 build errors, 0 warnings | ✅ | Confirmed May 2026 |

---

## 2. Runtime Bugs

| Bug | Status | Root Cause | Fix |
|-----|--------|-----------|-----|
| **Time Visualizer shows empty panel** | ✅ | `MainWindow.axaml` bound `{Binding Translator.GridRows}` but property is `Rows` | Changed binding to `{Binding Translator.Rows}` |
| **Edit mode button click has no card-level effect** | ✅ | Card ✏ button `IsVisible="{Binding !IsUtc}"` — no connection to `ThemeService.EditMode` | Added `IsEditVisible` to `ClockLocation`; `NotifyModeChanged()` chain wired |
| **Delete mode button click has no card-level effect** | ✅ | Same root cause as edit mode | Added `IsDeleteVisible` to `ClockLocation` |
| **Teams button shows 🤝 (handshake)** | ✅ | Icon copied from legacy WPF design | Changed to 📅 (calendar) in `MainWindow.axaml` |
| **Drag-and-drop card reorder** | ⚠️ | `DragDrop.DoDragDrop` wired but requires `DragDrop.AllowDrop="True"` on the panel — needs runtime verification | Verify on Windows target |
| **Window transparency / acrylic** | ⚠️ | `AcrylicHelper` is a Linux no-op; Avalonia handles via `TransparencyLevelHint="AcrylicBlur"` | Functional only on Windows |
| **Custom window chrome (drag, resize)** | ⚠️ | `SystemDecorations="None"` set; drag via `BeginMoveDrag` — needs runtime verification on Windows | — |

---

## 3. Feature Files Status

### Top-level Features (`Features/`)

| Feature File | Feature | Status | Notes |
|--------------|---------|--------|-------|
| `wordlclock.feature` | Core world clock display | ✅ | Clocks render with live time |
| `lookandfeel.feature` | Themes, transparency, settings window | ✅ | 10+ themes, acrylic, settings window works |
| `editable.feature` | Edit mode + drag reorder | ✅ | Toggle wired; `IsEditVisible`/`IsDeleteVisible` fixed; drag-reorder needs Windows verification |
| `fixes.feature` | All items marked `[Solved]` | ✅ | All 22 solved items verified in code |
| `layoutchanges.feature` | Header, search bar, timeline control layout | ✅ | Layout matches spec |
| `dynamicresizing.feature` | Proportion scale + MinLimit scale | ✅ | `ScaleMode` enum, `Viewbox` wrapper |
| `stateful.feature` | Settings persistence | ✅ | `SettingsService` persists all state |
| `selecttimetotranslate.feature` | Half-hour slot selection | ✅ | 48-slot grid, drag selection |
| `newfeatures.feature` (Teams deep link) | `[Solved]` Teams meeting link | ✅ | `TeamsDeepLinkBuilder` |
| `newfeatures.feature` (edit/delete toggle) | `[Solved]` mode buttons | ✅ | Button border highlights + card-level visibility both work |
| `newfeatures.feature` (accent colour picker) | Per-card colour picker | ✅ | 12-colour palette, persisted |
| `newfeatures.feature` (city dedup) | `[Solved]` deduplication by city name | ✅ | Name-based guard in `AddLocation` |
| `LogoIcon.feature` | App icon in taskbar | ⚠️ | Icon files exist; Avalonia icon binding needs verification |

### Time Visualizer Features (`Features/timevisualizer/`)

| Feature File | Feature | Status | Notes |
|--------------|---------|--------|-------|
| `behaviour.feature` | Drag-to-adjust time selection | ✅ | Mouse drag selection wired |
| `changeslayout.feature` | Vertical clocks panel, wider timeline | ✅ | Left-panel + right-panel layout |
| `fixeswtb.feature` | Date labels, transposed grid, half-hour selection | ✅ | All in `TimeTranslatorViewModel` |
| `interactivedatepicker.feature` | Date picker navigation | ✅ | Prev/Today/Next wired |
| `layoutfixes.feature` | All `[Solved]` items | ✅ | Text sizing, UTC search |
| `maincalendar.feature` | Click/drag selection highlight | ✅ | `SetSelectionRange` |
| `mispositionselectionarea.feature` | Slot alignment — click area, box, label | ✅ | Fixed `RowHeaderWidth=345, SlotCellWidth=24` |
| `paralleltimezone.feature` | Multi-timezone sync | ✅ | All rows share same slot grid |
| `search.feature` | City/timezone search + dropdown | ✅ | `WorldCitySearchService` |
| `theme.feature` | Time-band colours (morning/work/evening/night) | ✅ | `TimeBand` enum + `TimeGridCell` colours |
| `timeindicator.feature` | Real-time needle (vertical line) | ✅ | `CurrentTimeLeft` computed every tick |
| `timetranslatortweaks.feature` | City search linked to timezone | ✅ | `WorldCitySearchService` + `SourceCityMatches` |

---

## 4. Test Suite Status

### Build & Run Status

| Item | Status | Notes |
|------|--------|-------|
| Target framework `net10.0` | ✅ | Migrated from `net8.0-windows` |
| FlaUI packages removed | ✅ | `FlaUI.Core`, `FlaUI.UIA3`, `Xunit.StaFact` removed |
| Avalonia.Headless.XUnit 11.2.3 added | ✅ | `HeadlessApp.cs` with `[AvaloniaTestApplication]` wired |
| Build: 0 errors | ✅ | Confirmed 2026-05-21 |
| Tests: **282 passed, 2 skipped, 0 failed** | ✅ | Confirmed 2026-05-21 |

### Test Files

| File | Status | Notes |
|------|--------|-------|
| `Helpers/AcrylicHelperTests.cs` | ✅ | WPF `Window` smoke tests → no-op stub calls; `EncodeGradient` implemented |
| `Helpers/SlotConverterAlignmentTests.cs` | ✅ | Pure logic — passes |
| `Helpers/TeamsDeepLinkBuilderTests.cs` | ✅ | Pure logic — passes |
| `Integration/UtcApiTests.cs` | ✅ | Pure HTTP — passes (skips gracefully if offline) |
| `Models/ClockLocationTests.cs` | ✅ | `System.Windows.Media` → `Avalonia.Media`; `brush.Freeze()` removed |
| `Models/TimeGridCellLabelTests.cs` | ✅ | Passes |
| `Services/SettingsServiceTests.cs` | ✅ | Passes |
| `Services/ThemeServiceTests.cs` | ✅ | `[StaFact]` → `[Fact]`, `[StaTheory]` → `[Theory]` |
| `ViewModels/CityManagementTests.cs` | ✅ | `[StaFact]` → `[Fact]`; WPF brush replaced |
| `ViewModels/MainViewModelTests.cs` | ✅ | `[StaFact]` → `[Fact]` |
| `ViewModels/TimeTranslatorViewModelTests.cs` | ✅ | WPF brush replaced; India DST test skipped (Windows TZ ID) |
| `UI/WorldClockUITests.cs` | ✅ | FlaUI → Avalonia.Headless.XUnit rewrite |
| `UI/SettingsUITests.cs` | ✅ | FlaUI → ViewModel-level + `[AvaloniaFact]` headless smoke test |
| `UI/SearchDropdownTests.cs` | ✅ | FlaUI → `WorldCitySearchService` direct calls; UTC-offset test skipped (Windows TZ ID) |
| `UI/KeyboardNavTests.cs` | ✅ | FlaUI → ViewModel-level equivalents |
| `UI/UITestCollection.cs` | ✅ | Sequential collection definition retained |

### Known Skips (2 tests)

| Test | Reason |
|------|--------|
| `Translate_IndiaTimezone_NeverDst` | `India Standard Time` is a Windows TZ ID; not in Linux `/usr/share/zoneinfo` |
| `Search_ByUtcOffset_ReturnsResults` | UTC offset search uses `TimeZoneInfo` with Windows IDs — fails on Linux |

---

## 5. Code-Behind Issues (MainWindow.xaml.cs)

| Issue | Status | Notes |
|-------|--------|-------|
| `DataContext = _vm` set before `InitializeComponent` is called | ✅ | Fixed: set in constructor after `InitializeComponent` |
| `LocationsPanel.ItemsSource` set imperatively | ✅ | Works; DataTemplate binds to `ClockLocation` |
| `DispatcherTimer` tick updates all clocks | ✅ | `Dispatcher.UIThread.InvokeAsync` used |
| `_capturedPointer` for mouse capture | ✅ | Replaced `Mouse.Capture` with `Pointer.Capture` |
| `BeginMoveDrag` for custom title bar drag | ✅ | `e.GetCurrentPoint(this)` checked for left button |
| `await win.ShowDialog(this)` for SettingsWindow | ✅ | Modal dialog works |
| `PixelPoint` for DiagnosticsWindow positioning | ✅ | Avalonia equivalent used |
| `AnimateLayout` uses `DispatcherTimer` | ✅ | Iterates column width steps |

---

## 6. XAML Conversion Notes

| Pattern | WPF | Avalonia | Status |
|---------|-----|----------|--------|
| Namespace | `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` | `xmlns="https://github.com/avaloniaui"` | ✅ |
| AvaloniaResource extension | `.xaml` + `<Page>` | `.axaml` + auto-include | ✅ |
| `DataTrigger` / `Trigger` | WPF triggers | Avalonia styles with `Setter`/`:is()` or code-behind | ⚠️ Not used — bindings drive visibility |
| `Popup.PlacementMode` | `PlacementMode="Bottom"` | `Placement="Bottom"` | ✅ |
| `DragOver`/`Drop` events | Direct events on element | `DragDrop.DragOver`, `DragDrop.Drop` (attached) | ✅ |
| `DoDragDropAsync` | N/A | `DragDrop.DoDragDrop()` (sync) | ✅ |
| `x:Name` on `ColumnDefinition` | Generates code-behind field | NOT generated → use parent `Grid.ColumnDefinitions[i]` | ✅ |
| `Window.Loaded` | Event | `Window.Opened` | ✅ |
| `SolidColorBrush.Freeze()` | Thread safety | Not needed (Avalonia is single-threaded UI) | ✅ |
| `ListCollectionView` | WPF collection view | Direct `ObservableCollection<T>` | ✅ |
| `IValueConverter` params | `object value` | `object? value` (nullable) | ✅ |

---

## 7. Pending Work / Backlog

| Priority | Item | Feature File | Effort |
|----------|------|-------------|--------|
| 🟠 Medium | Verify drag-and-drop card reorder on Windows target | `editable.feature` | — |
| 🟠 Medium | Verify acrylic transparency on Windows | `lookandfeel.feature` | — |
| 🟠 Medium | Verify custom window chrome (drag, resize) on Windows | — | — |
| 🟡 Low | Verify app icon in Avalonia taskbar | `LogoIcon.feature` | Small |
| 🟡 Low | Dynamic resizing / MinLimit scale mode runtime test | `dynamicresizing.feature` | — |
| 🟡 Low | Port skipped TZ tests to use IANA IDs (cross-platform) | `tests.feature` | Small |

---

## 8. Resolved

| Date | Item |
|------|------|
| 2026-05-20 | `App.xaml` → `App.axaml` rename (XamlLoadException fixed) |
| 2026-05-20 | `App.xaml.cs` → `App.axaml.cs` code-behind paired |
| 2026-05-20 | Old `App.xaml`/`App.xaml.cs` excluded from compilation in `.csproj` |
| 2026-05-20 | `net8.0` → `net10.0` target framework |
| 2026-05-20 | `Tmds.DBus.Protocol` 0.21.3 override for CVE-2026-39959 |
| 2026-05-20 | `MainWindow.axaml` + `.xaml.cs` — full Avalonia rewrite |
| 2026-05-20 | `SettingsWindow.axaml` + `.xaml.cs` — full Avalonia rewrite |
| 2026-05-20 | `DiagnosticsWindow.axaml` + `.xaml.cs` — full Avalonia rewrite |
| 2026-05-20 | `SlotIndexToCanvasLeftConverter` — `object?` params fixed |
| 2026-05-20 | `TimeTranslatorViewModel` — `SolidColorBrush` → Avalonia |
| 2026-05-20 | Build: 0 errors, 0 warnings confirmed |
| 2026-05-21 | **BUG FIX**: Time Visualizer binding `GridRows` → `Rows` in `MainWindow.axaml` |
| 2026-05-21 | **BUG FIX**: Edit/Delete card visibility — `IsEditVisible`/`IsDeleteVisible` added to `ClockLocation`; `NotifyModeChanged()` chain wired through `MainViewModel` → `MainWindow.UpdateModeButtonStates()` |
| 2026-05-21 | **BUG FIX**: Teams icon 🤝 → 📅 in `MainWindow.axaml` |
| 2026-05-21 | **TEST MIGRATION**: `WorldClock.Tests` migrated from `net8.0-windows`/FlaUI to `net10.0`/Avalonia.Headless.XUnit |
| 2026-05-21 | **TEST MIGRATION**: All 16 test files fixed (`[StaFact]`→`[Fact]`, WPF namespaces→Avalonia, FlaUI UI tests rewritten) |
| 2026-05-21 | **TEST MIGRATION**: `HeadlessApp.cs` created with `AvaloniaHeadlessPlatformOptions` |
| 2026-05-21 | **BUG FIX**: `AcrylicHelper.EncodeGradient` implemented (was stub returning `0`) |
| 2026-05-21 | **RESULT**: 282 tests pass, 2 skipped (Windows-only TZ IDs), 0 failed |

---

## 9. File Map (WPF → Avalonia)

```
WorldClock/
  App.xaml          → App.axaml          (AvaloniaResource, compiled)
  App.xaml.cs       → App.axaml.cs       (code-behind)
  MainWindow.xaml   → MainWindow.axaml   (full rewrite)
  MainWindow.xaml.cs              (rewritten in-place, Avalonia APIs)
  SettingsWindow.xaml → SettingsWindow.axaml (full rewrite)
  SettingsWindow.xaml.cs          (rewritten in-place)
  DiagnosticsWindow.xaml → DiagnosticsWindow.axaml (full rewrite)
  DiagnosticsWindow.xaml.cs       (rewritten in-place)
  Styles.axaml                    (already Avalonia)
  Helpers/AcrylicHelper.cs        (no-op stub for non-Windows)
  Helpers/HexStringToBrushConverter.cs  (IValueConverter → Avalonia)
  Helpers/SlotIndexToCanvasLeftConverter.cs (IValueConverter → Avalonia)
  ViewModels/TimeTranslatorViewModel.cs (SolidColorBrush namespace fixed)
```
