using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using WorldClock.Data;
using WorldClock.Helpers;
using WorldClock.Models;
using WorldClock.Services;
using WorldClock.ViewModels;

namespace WorldClock;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _headerTimer;
    private readonly DispatcherTimer _globalSearchTimer = new();

    private const int RowHeaderWidth = 345;
    private const int SlotCellWidth  = 24;
    private bool _isDragging;
    private int  _dragStartSlot;
    private IPointer? _capturedPointer;
    private DiagnosticsWindow? _diagWindow;

    private const double DesignWidth         = 1860.0;
    private const double BaseDesignHeight    =  796.0;
    private const double ClockCardDesignH    =   72.0;
    private const int    ClockCountThreshold =    8;

    private ClockLocation? _draggedItem;
    private Border?        _dragOverBorder;
    private const string   DragFormat = "WorldClockLocation";

    // Column definition helpers (ColumnDefinition has no code-behind field generation in Avalonia)
    private ColumnDefinition ClocksColumnDef     => ContentColumnsGrid.ColumnDefinitions[0];
    private ColumnDefinition VisualizerColumnDef => ContentColumnsGrid.ColumnDefinitions[1];

    private DispatcherTimer? _layoutAnimTimer;

    private CancellationTokenSource _globalSearchCts = new();
    private int _dropdownHighlight = -1;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        ThemeService.Instance.Apply();

        Task.Run(() => { _ = WorldCitySearchService.All; });

        LocationsPanel.ItemsSource = _vm.Locations;

        _headerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _headerTimer.Tick += (_, _) => UpdateBanner();
        _headerTimer.Start();

        InitGlobalSearch();
        UpdateBanner();

        Opened += OnOpened;
        _vm.Locations.CollectionChanged += (_, _) => UpdateWindowSizeForClocks();
    }

    private void UpdateWindowSizeForClocks()
    {
        int userClocks = Math.Max(0, _vm.Locations.Count - 1);
        double extraCards = Math.Max(0, userClocks - ClockCountThreshold);
        double newDesignH = BaseDesignHeight + extraCards * ClockCardDesignH;

        if (newDesignH <= DesignRoot.Height) return;
        DesignRoot.Height = newDesignH;

        if (Bounds.Width <= 0) return;
        double scale     = Bounds.Width / DesignWidth;
        double newWindowH = newDesignH * scale;

        MinHeight = newWindowH;
        if (Height < newWindowH)
            Height = newWindowH;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyAcrylic();
        ApplyScaleMode();
        ApplyVisualizerLayout();
        UpdateWindowSizeForClocks();

        ThemeService.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ThemeService.Opacity)
                                  or nameof(ThemeService.ActiveTheme))
                ApplyAcrylic();

            if (args.PropertyName == nameof(ThemeService.ScaleMode))
                ApplyScaleMode();

            if (args.PropertyName is nameof(ThemeService.EditMode)
                                  or nameof(ThemeService.DeleteMode))
                UpdateModeButtonStates();

            if (args.PropertyName == nameof(ThemeService.ShowDiagnostics))
                ApplyDiagnosticsWindow();
        };
        UpdateModeButtonStates();
        ApplyDiagnosticsWindow();
    }

    private void ApplyDiagnosticsWindow()
    {
        if (ThemeService.Instance.ShowDiagnostics)
        {
            if (_diagWindow is null || !_diagWindow.IsVisible)
            {
                _diagWindow = new DiagnosticsWindow(_vm);
                _diagWindow.Position = new PixelPoint(
                    (int)(Position.X + Bounds.Width - 660),
                    (int)(Position.Y + Bounds.Height + 4));
                _diagWindow.Show();
            }
        }
        else
        {
            _diagWindow?.Close();
            _diagWindow = null;
        }
    }

    private void ApplyScaleMode()
    {
        var mode = ThemeService.Instance.ScaleMode;
        if (mode == ScaleMode.MinLimit)
        {
            ScaleViewbox.Stretch = Stretch.Uniform;
            DesignRoot.MinWidth  = 550;
            DesignRoot.MinHeight = 398;
            ScaleScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            ScaleScrollViewer.VerticalScrollBarVisibility   = ScrollBarVisibility.Auto;
        }
        else
        {
            ScaleViewbox.Stretch = Stretch.Uniform;
            DesignRoot.MinWidth  = 0;
            DesignRoot.MinHeight = 0;
            ScaleScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ScaleScrollViewer.VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled;
        }
    }

    private void ApplyAcrylic()
    {
        var svc   = ThemeService.Instance;
        var alpha = AcrylicHelper.ToTintAlpha(svc.Opacity);
        AcrylicHelper.Enable(this, svc.ActiveTheme.BackgroundDark, alpha, svc.ActiveTheme.IsDark);
    }

    private void UpdateBanner()
    {
        var utcNow = DateTime.UtcNow;
        HeaderDate.Text = utcNow.ToString("dddd, MMMM dd yyyy");
        if (_vm.Locations.Count > 0)
        {
            var utcClock = _vm.Locations[0];
            UtcTime.Text = utcClock.CurrentTime;
            UtcDate.Text = utcClock.CurrentDate;
        }
    }

    // ── Card edit handlers ────────────────────────────────────────────────────

    private void EditCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
            loc.BeginEdit();
    }

    private void SaveCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
        {
            loc.CommitEdit();
            _vm.Translator.BuildGrid();
            _vm.Translator.Translate();
            _vm.PersistCities();
        }
    }

    private void CancelCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
            loc.CancelEdit();
    }

    /// <summary>
    /// Handles a swatch click from the 12-colour accent palette in edit mode.
    /// Updates EditingAccentHex and immediately previews the new colour on the card;
    /// CancelEdit() restores the original brush if the user discards.
    /// </summary>
    private void PaletteColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string hex } btn) return;
        if (btn.DataContext is not ClockLocation loc) return;
        loc.EditingAccentHex = hex;
        loc.AccentBrush = new SolidColorBrush(Color.Parse(hex));
    }

    // ── Drag-and-drop reordering ──────────────────────────────────────────────

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control fe || fe.DataContext is not ClockLocation loc) return;
        if (loc.IsUtc || loc.IsEditing) return;

        _draggedItem = loc;
        var dataObj = new DataObject();
        dataObj.Set(DragFormat, loc);
        _ = DragDrop.DoDragDrop(e, dataObj, DragDropEffects.Move).ContinueWith(_ =>
        {
            _draggedItem = null;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_dragOverBorder is not null)
                {
                    _dragOverBorder.BorderBrush = Brushes.Transparent;
                    _dragOverBorder = null;
                }
            });
        });
    }

    private void Card_DragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DragFormat) || _draggedItem is null)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var target = (sender as Control)?.DataContext as ClockLocation;
        if (target is null || target == _draggedItem || target.IsUtc)
        {
            e.DragEffects = DragDropEffects.None;
        }
        else
        {
            e.DragEffects = DragDropEffects.Move;
            if (sender is Border border && !ReferenceEquals(border, _dragOverBorder))
            {
                if (_dragOverBorder is not null)
                    _dragOverBorder.BorderBrush = Brushes.Transparent;
                _dragOverBorder = border;
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
            }
        }
        e.Handled = true;
    }

    private void Card_DragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border && ReferenceEquals(border, _dragOverBorder))
        {
            border.BorderBrush = Brushes.Transparent;
            _dragOverBorder = null;
        }
    }

    private void Card_Drop(object? sender, DragEventArgs e)
    {
        if (_dragOverBorder is not null)
        {
            _dragOverBorder.BorderBrush = Brushes.Transparent;
            _dragOverBorder = null;
        }

        if (!e.Data.Contains(DragFormat) || _draggedItem is null) return;

        var target = (sender as Control)?.DataContext as ClockLocation;
        if (target is null || target == _draggedItem || target.IsUtc) return;

        int fromIndex = _vm.Locations.IndexOf(_draggedItem);
        int toIndex   = _vm.Locations.IndexOf(target);
        if (fromIndex > 0 && toIndex > 0)
            _vm.MoveLocation(fromIndex, toIndex);

        e.Handled = true;
    }

    // ── Window controls ───────────────────────────────────────────────────────

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_vm);
        await win.ShowDialog(this);
    }

    private void ToggleTranslator_Click(object? sender, PointerPressedEventArgs e)
    {
        _vm.Translator.IsOpen = !_vm.Translator.IsOpen;
        ApplyVisualizerLayout(animated: true);
        if (_vm.Translator.IsOpen)
            Dispatcher.UIThread.InvokeAsync(SelectCurrentSlot, DispatcherPriority.Background);
    }

    // ── Layout animation ──────────────────────────────────────────────────────

    private void ApplyVisualizerLayout(bool animated = false)
    {
        _layoutAnimTimer?.Stop();

        if (_vm.Translator.IsOpen)
        {
            VisualizerPanelBorder.IsVisible   = true;
            VisualizerColumnDef.Width         = new GridLength(1, GridUnitType.Star);
            ClocksPanelBorder.CornerRadius    = new CornerRadius(10, 0, 0, 10);
            ClocksPanelHeader.CornerRadius    = new CornerRadius(10, 0, 0, 0);

            if (animated)
                AnimateLayout(ClocksColumnDef.ActualWidth, 275, finalColStar: true);
            else
                ClocksColumnDef.Width = new GridLength(275);
        }
        else
        {
            VisualizerPanelBorder.IsVisible   = false;
            VisualizerColumnDef.Width         = new GridLength(0);
            ClocksPanelBorder.CornerRadius    = new CornerRadius(10);
            ClocksPanelHeader.CornerRadius    = new CornerRadius(10, 10, 0, 0);

            if (animated)
                AnimateLayout(275, Bounds.Width - 32, finalColStar: false);
            else
                ClocksColumnDef.Width = new GridLength(1, GridUnitType.Star);
        }
    }

    private void AnimateLayout(double fromColPx, double toColPx, bool finalColStar)
    {
        const double DurationMs = 300;
        double elapsed = 0;

        ClocksColumnDef.Width = new GridLength(fromColPx, GridUnitType.Pixel);

        _layoutAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(14) };
        _layoutAnimTimer.Tick += (_, _) =>
        {
            elapsed += 14;
            double t     = Math.Min(elapsed / DurationMs, 1.0);
            double eased = 1 - Math.Pow(1 - t, 3);

            ClocksColumnDef.Width = new GridLength(
                fromColPx + (toColPx - fromColPx) * eased, GridUnitType.Pixel);

            if (t >= 1.0)
            {
                _layoutAnimTimer!.Stop();
                _layoutAnimTimer = null;
                ClocksColumnDef.Width = finalColStar
                    ? new GridLength(275)
                    : new GridLength(1, GridUnitType.Star);
            }
        };
        _layoutAnimTimer.Start();
    }

    // ── Time Translator — date navigation ─────────────────────────────────────

    private void TranslatorPrevDay_Click(object? sender, RoutedEventArgs e) =>
        _vm.Translator.PrevDay();

    private void TranslatorNextDay_Click(object? sender, RoutedEventArgs e) =>
        _vm.Translator.NextDay();

    private void TranslatorToday_Click(object? sender, RoutedEventArgs e)
    {
        _vm.Translator.GoToday();
        Dispatcher.UIThread.InvokeAsync(SelectCurrentSlot, DispatcherPriority.Background);
    }

    private void CreateTeamsMeeting_Click(object? sender, RoutedEventArgs e)
    {
        var (teamsUri, browserUri) = _vm.Translator.BuildTeamsDeepLink();
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(teamsUri) { UseShellExecute = true });
        }
        catch
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(browserUri) { UseShellExecute = true });
        }
    }

    // ── Time Visualizer — grid pointer-select ─────────────────────────────────

    private int GetSlotFromPosition(Control container, Point pos)
    {
        var xInCells = pos.X - RowHeaderWidth;
        if (xInCells < 0) xInCells = 0;
        return Math.Clamp((int)(xInCells / SlotCellWidth), 0, 47);
    }

    private void GridArea_PreviewMouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control fe) return;
        var pos = e.GetPosition(fe);

        _vm.Translator.LastClickPosX = pos.X;

        if (pos.X < RowHeaderWidth) return;

        _isDragging    = true;
        _dragStartSlot = GetSlotFromPosition(fe, pos);
        _vm.Translator.SetSelectionRange(_dragStartSlot, _dragStartSlot);
        _capturedPointer = e.Pointer;
        e.Pointer.Capture(fe);
        e.Handled = true;
    }

    private void GridArea_PreviewMouseMove(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        if (sender is not Control fe) return;

        var slot  = GetSlotFromPosition(fe, e.GetPosition(fe));
        var start = Math.Min(_dragStartSlot, slot);
        var end   = Math.Max(_dragStartSlot, slot);
        _vm.Translator.SetSelectionRange(start, end);
    }

    private void GridArea_PreviewMouseUp(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;
    }

    // ── Global add-city search bar ────────────────────────────────────────────

    private void InitGlobalSearch()
    {
        _globalSearchTimer.Interval = TimeSpan.FromMilliseconds(400);
        _globalSearchTimer.Tick += GlobalSearchTimer_Tick;
    }

    private void PositionAndShowDropdown()
    {
        if (!GlobalCitySearchList.Items.Any()) return;
        GlobalSearchDropdown.IsVisible = true;
    }

    private void HideGlobalDropdown()
    {
        _globalSearchTimer.Stop();
        GlobalSearchDropdown.IsVisible = false;
        _dropdownHighlight = -1;
    }

    private void SetDropdownHighlight(int index)
    {
        _dropdownHighlight = index;
        GlobalCitySearchList.SelectedIndex = index;
    }

    private async void GlobalSearchTimer_Tick(object? sender, EventArgs e)
    {
        _globalSearchTimer.Stop();
        var text = GlobalCitySearchBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(text))
            await CommitSearchAsync(text);
    }

    private void GlobalCitySearch_GotFocus(object? sender, GotFocusEventArgs e)
    {
        var text = GlobalCitySearchBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;

        if (GlobalCitySearchList.Items.Any())
            PositionAndShowDropdown();
        else
            _ = CommitSearchAsync(text);
    }

    private void GlobalCitySearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = GlobalCitySearchBox.Text ?? string.Empty;
        GlobalCitySearchPlaceholder.IsVisible = string.IsNullOrEmpty(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            HideGlobalDropdown();
            return;
        }

        _globalSearchTimer.Stop();
        _globalSearchTimer.Start();
    }

    private async void GlobalCitySearch_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Return:
                e.Handled = true;
                if (GlobalSearchDropdown.IsVisible && GlobalCitySearchList.Items.Any())
                {
                    int idx  = _dropdownHighlight >= 0 ? _dropdownHighlight : 0;
                    var pick = GlobalCitySearchList.Items.Cast<CityEntry>().ElementAtOrDefault(idx);
                    if (pick != null) CommitGlobalCity(pick);
                }
                else
                {
                    _globalSearchTimer.Stop();
                    var text = GlobalCitySearchBox.Text?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(text))
                        await CommitSearchAsync(text);
                }
                break;

            case Key.Down:
                if (!GlobalSearchDropdown.IsVisible || !GlobalCitySearchList.Items.Any())
                    break;
                {
                    int count = GlobalCitySearchList.Items.Count;
                    int next  = _dropdownHighlight < count - 1 ? _dropdownHighlight + 1 : 0;
                    SetDropdownHighlight(next);
                    if (GlobalCitySearchList.SelectedItem is not null)
                        GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (!GlobalSearchDropdown.IsVisible || !GlobalCitySearchList.Items.Any())
                    break;
                {
                    if (_dropdownHighlight <= 0)
                    {
                        SetDropdownHighlight(-1);
                    }
                    else
                    {
                        int prev = _dropdownHighlight - 1;
                        SetDropdownHighlight(prev);
                        if (GlobalCitySearchList.SelectedItem is not null)
                            GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.SelectedItem);
                    }
                }
                e.Handled = true;
                break;

            case Key.Escape:
                HideGlobalDropdown();
                e.Handled = true;
                break;
        }
    }

    private void GlobalCitySearch_LostFocus(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!GlobalCitySearchBox.IsFocused) HideGlobalDropdown();
        }, DispatcherPriority.Input);
    }

    private async Task CommitSearchAsync(string text)
    {
        _globalSearchCts.Cancel();
        _globalSearchCts = new CancellationTokenSource();
        var cts = _globalSearchCts;

        IReadOnlyList<CityEntry> results;
        try
        {
            results = await Task.Run(() => WorldCitySearchService.Search(text), cts.Token);
        }
        catch (OperationCanceledException) { return; }

        if (cts.IsCancellationRequested) return;

        GlobalCitySearchList.ItemsSource = results;
        _dropdownHighlight = -1;
        PositionAndShowDropdown();
    }

    private void GlobalCitySearchList_MouseSelect(object? sender, PointerReleasedEventArgs e)
    {
        if (GlobalCitySearchList.SelectedItem is CityEntry selected)
            CommitGlobalCity(selected);
    }

    private void CommitGlobalCity(CityEntry city)
    {
        GlobalCitySearchList.SelectedItem = null;
        HideGlobalDropdown();
        _vm.Translator.SelectedSourceCity = city;
        _vm.AddLocation(city.City, city.TimeZoneId, string.Empty, city.CountryFlag);
        GlobalCitySearchBox.Text = "";
        GlobalCitySearchPlaceholder.IsVisible = true;
        GlobalCitySearchBox.Focus();
    }

    private void GlobalCitySearchList_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Return:
                {
                    int idx  = _dropdownHighlight >= 0 ? _dropdownHighlight : 0;
                    var city = GlobalCitySearchList.Items.Cast<CityEntry>().ElementAtOrDefault(idx);
                    if (city != null) CommitGlobalCity(city);
                }
                e.Handled = true;
                break;

            case Key.Escape:
                HideGlobalDropdown();
                GlobalCitySearchBox.Focus();
                e.Handled = true;
                break;

            case Key.Down:
                {
                    int count = GlobalCitySearchList.Items.Count;
                    int next  = _dropdownHighlight < count - 1 ? _dropdownHighlight + 1 : 0;
                    SetDropdownHighlight(next);
                    if (GlobalCitySearchList.SelectedItem is not null)
                        GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (_dropdownHighlight <= 0)
                {
                    SetDropdownHighlight(-1);
                    GlobalCitySearchBox.Focus();
                }
                else
                {
                    SetDropdownHighlight(_dropdownHighlight - 1);
                    if (GlobalCitySearchList.SelectedItem is not null)
                        GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.SelectedItem);
                }
                e.Handled = true;
                break;
        }
    }

    private void SelectCurrentSlot()
    {
        var zone = _vm.Translator.EffectiveZone;
        var now  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var slot = now.Hour * 2 + (now.Minute >= 30 ? 1 : 0);
        _vm.Translator.SetSelectionRange(slot, slot);
    }

    private void DeleteCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
            _vm.RemoveLocation(loc);
    }

    private void SetHomeLocation_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ClockLocation loc }) return;
        if (loc.IsHome)
            _vm.ClearHomeLocation();
        else
            _vm.SetHomeLocation(loc);
    }

    private void SetUtcHome_Click(object? sender, RoutedEventArgs e)
    {
        var utcLoc = _vm.Locations.FirstOrDefault(l => l.IsUtc);
        if (utcLoc is null) return;
        if (utcLoc.IsHome)
            _vm.ClearHomeLocation();
        else
            _vm.SetHomeLocation(utcLoc);
    }

    private void ToggleEditMode_Click(object? sender, PointerPressedEventArgs e)
    {
        var ts = ThemeService.Instance;
        ts.EditMode   = !ts.EditMode;
        ts.DeleteMode = false;
    }

    private void ToggleDeleteMode_Click(object? sender, PointerPressedEventArgs e)
    {
        var ts = ThemeService.Instance;
        ts.DeleteMode = !ts.DeleteMode;
        ts.EditMode   = false;
    }

    private void UpdateModeButtonStates()
    {
        var ts = ThemeService.Instance;

        EditModeBtnBorder.Background = ts.EditMode
            ? new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0xE5, 0xFF))
            : Brushes.Transparent;

        DeleteModeBtnBorder.Background = ts.DeleteMode
            ? new SolidColorBrush(Color.FromArgb(0x55, 0xC6, 0x28, 0x28))
            : Brushes.Transparent;

        // Propagate to all card ViewModels so IsEditVisible/IsDeleteVisible update.
        _vm.NotifyModeChanged();
    }
}
