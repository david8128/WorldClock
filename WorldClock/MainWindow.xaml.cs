using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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

    private const int RowHeaderWidth = 160;
    private const int SlotCellWidth   = 10;  // must match SlotIndexToCanvasLeftConverter.SlotWidthPx
    private bool _isDragging;
    private int  _dragStartSlot;

    // ── Auto-resize constants ─────────────────────────────────────────────────
    // DesignRoot is 1100×760. The Viewbox scales it uniformly to fill the window.
    // We grow DesignRoot.Height beyond the 760 baseline when clock count exceeds
    // the threshold that comfortably fits within the baseline height, then resize
    // the window proportionally so the scale factor stays the same.
    private const double DesignWidth         = 1100.0;  // fixed — never changes
    private const double BaseDesignHeight    =  760.0;  // baseline for ≤ threshold clocks
    private const double ClockCardDesignH    =   72.0;  // one clock card in design units
    private const int    ClockCountThreshold =    8;    // clocks that fit in the baseline

    // ── Card drag-and-drop state ──────────────────────────────────────────────
    private ClockLocation? _draggedItem;
    private Border?        _dragOverBorder;
    private const string   DragFormat = "WorldClockLocation";

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        // Apply initial theme resources
        ThemeService.Instance.Apply();

        // Pre-warm the city database on a background thread so the first search
        // is instant (avoids blocking the UI thread on CSV load).
        Task.Run(() => { _ = WorldCitySearchService.All; });

        // Use a live CollectionView so newly-added cities appear immediately.
        // Filter keeps the UTC entry out (it has its own dedicated banner).
        var locView = new ListCollectionView(_vm.Locations);
        locView.Filter = o => o is ClockLocation loc && loc.TimeZoneId != "UTC";
        LocationsPanel.ItemsSource = locView;

        _headerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _headerTimer.Tick += (_, _) => UpdateBanner();
        _headerTimer.Start();

        InitGlobalSearch();

        UpdateBanner();

        // Once the window handle exists, apply acrylic and subscribe to changes
        Loaded += OnLoaded;

        // Auto-resize the window whenever the clock list changes
        _vm.Locations.CollectionChanged += (_, _) => UpdateWindowSizeForClocks();
    }

    // ── Auto window sizing ────────────────────────────────────────────────────
    /// <summary>
    /// Grows (never shrinks) DesignRoot.Height and Window.Height when the
    /// number of user clocks exceeds <see cref="ClockCountThreshold"/>.
    /// The Viewbox (Stretch=Uniform) scales the design canvas proportionally to
    /// the window, so growing both by the same ratio keeps text at the same
    /// apparent size while giving all cards natural (uncompressed) height.
    /// </summary>
    private void UpdateWindowSizeForClocks()
    {
        // Count user clocks — exclude the always-present UTC sentinel (index 0)
        int userClocks = Math.Max(0, _vm.Locations.Count - 1);

        double extraCards   = Math.Max(0, userClocks - ClockCountThreshold);
        double newDesignH   = BaseDesignHeight + extraCards * ClockCardDesignH;

        // Only grow — never shrink back (avoids jarring resize when deleting one clock)
        if (newDesignH <= DesignRoot.Height) return;

        DesignRoot.Height = newDesignH;

        // Grow the window proportionally so the scale factor stays the same.
        // scale = ActualWidth / DesignWidth  →  newWindowH = newDesignH * scale
        if (ActualWidth <= 0) return;
        double scale       = ActualWidth / DesignWidth;
        double newWindowH  = newDesignH * scale;

        MinHeight = newWindowH;
        if (Height < newWindowH)
            Height = newWindowH;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
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
        };
        UpdateModeButtonStates();
    }

    // ── Scale mode ────────────────────────────────────────────────────────────
    // ProportionScale (default): Viewbox.Stretch=Uniform scales the fixed 1100×760 design
    //   to fill the window at any size — no scrollbars needed.
    // MinLimit: same Uniform scale but DesignRoot has minimum pixel dimensions;
    //   scrollbars appear only when the window is shrunk below those minimums.

    private void ApplyScaleMode()
    {
        var mode = ThemeService.Instance.ScaleMode;

        if (mode == ScaleMode.MinLimit)
        {
            // Scale proportionally but stop at half the design size; scrollbars below that.
            ScaleViewbox.Stretch = Stretch.Uniform;
            DesignRoot.MinWidth  = 550;   // half of 1100
            DesignRoot.MinHeight = 380;   // half of 760
            ScaleScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            ScaleScrollViewer.VerticalScrollBarVisibility   = ScrollBarVisibility.Auto;
        }
        else // ProportionScale — always fits, no scrollbars
        {
            ScaleViewbox.Stretch = Stretch.Uniform;
            DesignRoot.MinWidth  = 0;
            DesignRoot.MinHeight = 0;
            ScaleScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ScaleScrollViewer.VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled;
        }
    }

    // ── Acrylic ───────────────────────────────────────────────────────────────

    private void ApplyAcrylic()
    {
        var svc   = ThemeService.Instance;
        var alpha = AcrylicHelper.ToTintAlpha(svc.Opacity);
        AcrylicHelper.Enable(this, svc.ActiveTheme.BackgroundDark, alpha, svc.ActiveTheme.IsDark);
    }

    private void UpdateBanner()
    {
        var utcNow  = DateTime.UtcNow;
        HeaderDate.Text = utcNow.ToString("dddd, MMMM dd yyyy");
        var utcClock = _vm.Locations[0];
        UtcTime.Text = utcClock.CurrentTime;
        UtcDate.Text = utcClock.CurrentDate;
    }

    // ── Card inline-edit handlers ─────────────────────────────────────────────

    private void EditCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
            loc.BeginEdit();
    }

    private void SaveCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
        {
            loc.CommitEdit();
            // Rebuild the Time Visualizer so TimeGridRow picks up the updated AccentBrush.
            _vm.Translator.BuildGrid();
            _vm.Translator.Translate();
            _vm.PersistCities();
        }
    }

    private void CancelCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
            loc.CancelEdit();
    }

    // ── Drag-and-drop reordering ──────────────────────────────────────────────

    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ClockLocation loc) return;
        if (loc.IsUtc || loc.IsEditing) return;

        _draggedItem = loc;
        var data = new DataObject(DragFormat, loc);
        DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);

        // Clean up highlight if drop landed outside a valid card
        _draggedItem = null;
        if (_dragOverBorder is not null)
        {
            _dragOverBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
            _dragOverBorder = null;
        }
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragFormat) || _draggedItem is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var target = (sender as FrameworkElement)?.DataContext as ClockLocation;
        if (target is null || target == _draggedItem || target.IsUtc)
        {
            e.Effects = DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.Move;

            // Highlight the drop target
            if (sender is Border border && !ReferenceEquals(border, _dragOverBorder))
            {
                if (_dragOverBorder is not null)
                    _dragOverBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
                _dragOverBorder = border;
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
            }
        }
        e.Handled = true;
    }

    private void Card_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border && ReferenceEquals(border, _dragOverBorder))
        {
            border.BorderBrush = new SolidColorBrush(Colors.Transparent);
            _dragOverBorder = null;
        }
    }

    private void Card_Drop(object sender, DragEventArgs e)
    {
        if (_dragOverBorder is not null)
        {
            _dragOverBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
            _dragOverBorder = null;
        }

        if (!e.Data.GetDataPresent(DragFormat) || _draggedItem is null) return;

        var target = (sender as FrameworkElement)?.DataContext as ClockLocation;
        if (target is null || target == _draggedItem || target.IsUtc) return;

        int fromIndex = _vm.Locations.IndexOf(_draggedItem);
        int toIndex   = _vm.Locations.IndexOf(target);
        if (fromIndex > 0 && toIndex > 0)
            _vm.MoveLocation(fromIndex, toIndex);

        e.Handled = true;
    }

    // ── Window controls ───────────────────────────────────────────────────────

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    // ── Feature handlers ──────────────────────────────────────────────────────

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_vm) { Owner = this };
        win.ShowDialog();
    }

    private void ToggleTranslator_Click(object sender, RoutedEventArgs e)
    {
        _vm.Translator.IsOpen = !_vm.Translator.IsOpen;
        ApplyVisualizerLayout(animated: true);
        if (_vm.Translator.IsOpen)
            Dispatcher.InvokeAsync(SelectCurrentSlot,
                                   System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Expands/collapses the clocks panel. When <paramref name="animated"/> is true a
    /// smooth 300 ms ease-out-cubic animation expands/contracts the column. After a
    /// collapse animation the card list fades in as a WrapPanel grid; expanding always
    /// switches back to a StackPanel immediately so cards stack vertically during the
    /// shrink-back animation.
    /// </summary>
    private DispatcherTimer? _layoutAnimTimer;

    private void ApplyVisualizerLayout(bool animated = false)
    {
        _layoutAnimTimer?.Stop();

        if (_vm.Translator.IsOpen)
        {
            // ── Visualizer OPEN: switch to stack immediately, then animate to 220 px ──
            SwitchToStackLayout();
            VisualizerPanelBorder.Visibility = Visibility.Visible;
            VisualizerColumnDef.Width        = new GridLength(1, GridUnitType.Star);
            ClocksPanelBorder.CornerRadius   = new CornerRadius(10, 0, 0, 10);
            ClocksPanelHeader.CornerRadius   = new CornerRadius(10, 0, 0, 0);

            if (animated)
                AnimateLayout(ClocksColumnDef.ActualWidth, 220, finalColStar: true);
            else
                ClocksColumnDef.Width = new GridLength(220);
        }
        else
        {
            // ── Visualizer CLOSED: full-width clocks, grid layout after animation ──
            VisualizerPanelBorder.Visibility = Visibility.Collapsed;
            VisualizerColumnDef.Width        = new GridLength(0);
            ClocksPanelBorder.CornerRadius   = new CornerRadius(10);
            ClocksPanelHeader.CornerRadius   = new CornerRadius(10, 10, 0, 0);

            if (animated)
                AnimateLayout(220, ActualWidth - 32, finalColStar: false);
            else
            {
                ClocksColumnDef.Width = new GridLength(1, GridUnitType.Star);
                SwitchToGridLayout();
            }
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
            double eased = 1 - Math.Pow(1 - t, 3);   // ease-out cubic

            ClocksColumnDef.Width = new GridLength(
                fromColPx + (toColPx - fromColPx) * eased, GridUnitType.Pixel);

            if (t >= 1.0)
            {
                _layoutAnimTimer!.Stop();
                _layoutAnimTimer = null;
                if (finalColStar)
                    ClocksColumnDef.Width = new GridLength(220);
                else
                {
                    ClocksColumnDef.Width = new GridLength(1, GridUnitType.Star);
                    SwitchToGridLayoutAnimated();  // fade grid in after column fully expanded
                }
            }
        };
        _layoutAnimTimer.Start();
    }

    /// <summary>Switches the card list back to a vertical StackPanel (normal mode).</summary>
    private void SwitchToStackLayout()
    {
        LocationsPanel.Opacity              = 1;
        LocationsPanel.MaxWidth             = 220;
        LocationsPanel.HorizontalAlignment  = HorizontalAlignment.Center;
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        LocationsPanel.ItemsPanel = new ItemsPanelTemplate { VisualTree = factory };
    }

    /// <summary>Switches the card list to a wrapping grid (focus mode, no animation).</summary>
    private void SwitchToGridLayout()
    {
        LocationsPanel.MaxWidth            = double.PositiveInfinity;
        LocationsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        var factory = new FrameworkElementFactory(typeof(WrapPanel));
        factory.SetValue(WrapPanel.ItemWidthProperty, 220.0);
        LocationsPanel.ItemsPanel = new ItemsPanelTemplate { VisualTree = factory };
        LocationsPanel.Opacity = 1;
    }

    /// <summary>Switches to grid layout then fades the panel in over 200 ms.</summary>
    private void SwitchToGridLayoutAnimated()
    {
        LocationsPanel.Opacity = 0;
        SwitchToGridLayout();

        double elapsed = 0;
        var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(14) };
        fadeTimer.Tick += (_, _) =>
        {
            elapsed += 14;
            double t = Math.Min(elapsed / 200.0, 1.0);
            LocationsPanel.Opacity = t;
            if (t >= 1.0) fadeTimer.Stop();
        };
        fadeTimer.Start();
    }

    // ── Time Translator — date navigation ─────────────────────────────────────

    private void TranslatorPrevDay_Click(object sender, RoutedEventArgs e) =>
        _vm.Translator.PrevDay();

    private void TranslatorNextDay_Click(object sender, RoutedEventArgs e) =>
        _vm.Translator.NextDay();

    private void TranslatorToday_Click(object sender, RoutedEventArgs e)
    {
        _vm.Translator.GoToday();
        Dispatcher.InvokeAsync(SelectCurrentSlot,
                               System.Windows.Threading.DispatcherPriority.Background);
    }

    private void CreateTeamsMeeting_Click(object sender, RoutedEventArgs e)
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

    // ── Time Visualizer — grid drag-select (transposed: click/drag across slots) ──

    private int GetSlotFromPosition(FrameworkElement container, Point pos)
    {
        var xInCells = pos.X - RowHeaderWidth;
        if (xInCells < 0) xInCells = 0;
        return Math.Clamp((int)(xInCells / SlotCellWidth), 0, 47);
    }

    private void GridArea_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var pos = e.GetPosition(fe);

        // Publish raw pos.X to DiagStatus immediately
        _vm.Translator.LastClickPosX = pos.X;

        // ── Alignment diagnostics: measure actual element positions ──────────
        // GridScrollViewer origin tells us where the data-row area ACTUALLY starts
        // inside the TranslatorResults Border (x should be 0 if layout is correct).
        // OriginalSource origin confirms where the clicked element starts.
        try
        {
            var svOrigin = GridScrollViewer.TransformToAncestor(fe).Transform(new Point(0, 0));
            _vm.Translator.DiagScrollViewerX = svOrigin.X;
            DiagLog.Info($"GridScrollViewer in Border: x={svOrigin.X:F1}, y={svOrigin.Y:F1}");

            if (e.OriginalSource is UIElement origSrc)
            {
                var srcOrigin = origSrc.TransformToAncestor(fe).Transform(new Point(0, 0));
                _vm.Translator.DiagOrigSrcX = srcOrigin.X;
                DiagLog.Info($"OrigSrc={origSrc.GetType().Name}, in Border: x={srcOrigin.X:F1}");

                // Walk the visual tree up from origSrc to find the parent ContentPresenter
                // and the Canvas (ItemsPanel) that contains it.  This tells us:
                //   CP.CanvasLeft = slot * 10  →  hitSlot = CP.CanvasLeft / 10
                //   Canvas.x      = actual x-start of the Layer-1a Canvas in TranslatorResults coords
                //                   (expected: 160).  If != 160 the header spacer is wider than 160 px.
                DependencyObject? cursor2 = origSrc;
                ContentPresenter? hitCp   = null;
                Canvas? hitCanvas         = null;
                while (cursor2 is not null && !ReferenceEquals(cursor2, fe))
                {
                    cursor2 = VisualTreeHelper.GetParent(cursor2);
                    if (cursor2 is ContentPresenter cp2 && hitCp     is null) hitCp     = cp2;
                    if (cursor2 is Canvas          cv  && hitCanvas  is null) hitCanvas = cv;
                    if (hitCp is not null && hitCanvas is not null) break;
                }
                if (hitCp is not null && hitCanvas is not null)
                {
                    double cpLeft   = Canvas.GetLeft(hitCp);
                    var    canvPos  = hitCanvas.TransformToAncestor(fe).Transform(new Point(0, 0));
                    _vm.Translator.DiagCpCanvasLeft = cpLeft;
                    _vm.Translator.DiagCanvas1aX    = canvPos.X;
                    DiagLog.Info($"  CP.CanvasLeft={cpLeft:F0}, Canvas(L1a).x={canvPos.X:F1}");
                }
            }
        }
        catch (Exception ex) { DiagLog.Info($"TransformToAncestor error: {ex.Message}"); }

        // Log raw coordinates
        var feW     = fe.ActualWidth;
        var rawSlot = GetSlotFromPosition(fe, pos);
        DiagLog.Info($"GridArea_MouseDown: pos.X={pos.X:F1}, rawSlot={rawSlot}, " +
                     $"fe.ActualWidth={feW:F1}, xInCells={pos.X - RowHeaderWidth:F1}");

        if (pos.X < RowHeaderWidth) return;  // clicked in row-header zone

        _isDragging    = true;
        _dragStartSlot = rawSlot;
        _vm.Translator.SetSelectionRange(rawSlot, rawSlot);
        fe.CaptureMouse();
        e.Handled = true;
    }

    private void GridArea_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement fe) return;

        var slot  = GetSlotFromPosition(fe, e.GetPosition(fe));
        var start = Math.Min(_dragStartSlot, slot);
        var end   = Math.Max(_dragStartSlot, slot);
        _vm.Translator.SetSelectionRange(start, end);
    }

    private void GridArea_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        (sender as UIElement)?.ReleaseMouseCapture();
    }

    // ── Global add-city search bar ────────────────────────────────────────────
    // Dropdown is an in-tree Canvas overlay (not a Popup) — avoids the WPF
    // AllowsTransparency + Popup HWND Z-order bug on layered windows.
    //
    // Visibility strategy:
    //   • DispatcherTimer fires 400 ms after the user stops typing → triggers search
    //   • Enter key fires immediately without waiting for the timer
    //   • TextChanged restarts the timer on every keystroke
    //   • LostFocus hides the dropdown unless focus moved into it

    private CancellationTokenSource _globalSearchCts = new();
    private int _dropdownHighlight = -1;  // currently highlighted row index (-1 = none)

    // Called once from constructor to wire up the idle timer
    private void InitGlobalSearch()
    {
        _globalSearchTimer.Interval = TimeSpan.FromMilliseconds(400);
        _globalSearchTimer.Tick += GlobalSearchTimer_Tick;
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    private void PositionAndShowDropdown()
    {
        if (!GlobalCitySearchList.HasItems) return;

        GlobalSearchDropdown.Visibility = Visibility.Visible;
    }

    private void HideGlobalDropdown()
    {
        _globalSearchTimer.Stop();
        GlobalSearchDropdown.Visibility = Visibility.Collapsed;
        SetDropdownHighlight(-1);
    }

    /// <summary>
    /// Highlights the item at <paramref name="index"/> by setting the Background local value
    /// directly on the inner Border (Bd) of each ListBoxItem's visual tree.
    /// <para>
    /// Why VisualTreeHelper instead of item.Background:
    /// The ControlTemplate's Border has Background="{DynamicResource ...}" which reads
    /// directly from the resource dictionary — it is NOT bound to ListBoxItem.Background.
    /// Setting item.Background has zero visible effect.  Setting Bd.Background as a local
    /// value (DP precedence 3) wins over DynamicResource (precedence 9) and template
    /// triggers (precedence 7), so the cyan highlight is always visible.
    /// ClearValue() removes the local value so triggers (hover, etc.) resume normally.
    /// </para>
    /// </summary>
    private void SetDropdownHighlight(int index)
    {
        _dropdownHighlight = index;
        var cyanBrush = TryFindResource("BrushAccentCyan") as Brush ?? Brushes.Cyan;

        for (int i = 0; i < GlobalCitySearchList.Items.Count; i++)
        {
            if (GlobalCitySearchList.ItemContainerGenerator.ContainerFromIndex(i)
                    is not ListBoxItem item) continue;

            // The ListBoxItem’s visual tree is: ListBoxItem → Border (Bd) → ContentPresenter
            if (VisualTreeHelper.GetChildrenCount(item) == 0) continue;
            if (VisualTreeHelper.GetChild(item, 0) is not Border bd) continue;

            if (i == index)
                bd.Background = cyanBrush;   // local value (level 3): beats DynamicResource + triggers
            else
                bd.ClearValue(Border.BackgroundProperty);  // removes local value → triggers resume
        }
    }

    // ── Timer: fires when the user stops typing ───────────────────────────────

    private async void GlobalSearchTimer_Tick(object? sender, EventArgs e)
    {
        _globalSearchTimer.Stop();
        var text = GlobalCitySearchBox.Text.Trim();
        if (!string.IsNullOrEmpty(text))
            await CommitSearchAsync(text);
    }

    // ── TextBox events ────────────────────────────────────────────────────────

    private void GlobalCitySearch_GotFocus(object sender, RoutedEventArgs e)
    {
        var text = GlobalCitySearchBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (GlobalCitySearchList.HasItems)
            PositionAndShowDropdown();
        else
            _ = CommitSearchAsync(text);
    }

    private void GlobalCitySearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = GlobalCitySearchBox.Text;
        GlobalCitySearchPlaceholder.Visibility =
            string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(text))
        {
            HideGlobalDropdown();
            return;
        }

        // Restart the idle timer on every keystroke — fires 400 ms after last key
        _globalSearchTimer.Stop();
        _globalSearchTimer.Start();
    }

    private async void GlobalCitySearch_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                if (GlobalSearchDropdown.Visibility == Visibility.Visible && GlobalCitySearchList.HasItems)
                {
                    // Commit highlighted row, or fall back to first item
                    int idx  = _dropdownHighlight >= 0 ? _dropdownHighlight : 0;
                    var pick = GlobalCitySearchList.Items[idx] as CityEntry;
                    if (pick != null) CommitGlobalCity(pick);
                }
                else
                {
                    _globalSearchTimer.Stop();
                    var text = GlobalCitySearchBox.Text.Trim();
                    if (!string.IsNullOrEmpty(text))
                        await CommitSearchAsync(text);
                }
                break;

            case Key.Down:
                if (GlobalSearchDropdown.Visibility != Visibility.Visible || !GlobalCitySearchList.HasItems)
                    break;
                {
                    int count = GlobalCitySearchList.Items.Count;
                    int next  = _dropdownHighlight < count - 1 ? _dropdownHighlight + 1 : 0;
                    _dropdownHighlight = next;  // advance immediately so rapid presses are correct
                    GlobalCitySearchList.SelectedIndex = next;
                    GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.Items[next]);
                    // Use BeginInvoke so containers are fully laid out before we paint them
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => SetDropdownHighlight(next));
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (GlobalSearchDropdown.Visibility != Visibility.Visible || !GlobalCitySearchList.HasItems)
                    break;
                {
                    if (_dropdownHighlight <= 0)
                    {
                        _dropdownHighlight = -1;
                        GlobalCitySearchList.SelectedIndex = -1;
                        _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => SetDropdownHighlight(-1));
                    }
                    else
                    {
                        int prev = _dropdownHighlight - 1;
                        _dropdownHighlight = prev;
                        GlobalCitySearchList.SelectedIndex = prev;
                        GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.Items[prev]);
                        _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => SetDropdownHighlight(prev));
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

    private void GlobalCitySearch_LostFocus(object sender, RoutedEventArgs e)
    {
        // LostFocus fires even when focus moves to a ListBoxItem inside the dropdown
        // (e.g. on mouse-hover).  Use a deferred check at Input priority — by then
        // focus is stable and IsKeyboardFocusWithin is reliable.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            bool within = GlobalSearchDropdown.IsKeyboardFocusWithin
                       || GlobalCitySearchBox.IsKeyboardFocused;
            if (!within) HideGlobalDropdown();
        });
    }

    // ── Search execution ──────────────────────────────────────────────────────

    private async Task CommitSearchAsync(string text)
    {
        _globalSearchCts.Cancel();
        _globalSearchCts = new CancellationTokenSource();
        var cts = _globalSearchCts;

        IReadOnlyList<CityEntry> results;
        try
        {
            results = await Task.Run(
                () => WorldCitySearchService.Search(text),
                cts.Token);
        }
        catch (OperationCanceledException) { return; }

        if (cts.IsCancellationRequested) return;

        GlobalCitySearchList.ItemsSource = results;
        _dropdownHighlight = -1;  // reset highlight when new results arrive
        PositionAndShowDropdown();
    }

    private void GlobalCitySearchList_MouseSelect(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src)
        {
            var item = ItemsControl.ContainerFromElement(GlobalCitySearchList, src) as ListBoxItem;
            if (item?.DataContext is CityEntry city)
            {
                CommitGlobalCity(city);
                return;
            }
        }
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
        GlobalCitySearchPlaceholder.Visibility = Visibility.Visible;
        GlobalCitySearchBox.Focus();
    }

    /// <summary>
    /// Keyboard navigation inside GlobalCitySearchList when it somehow receives focus
    /// (e.g. mouse click moved focus there). Delegates back to the same logic.
    /// </summary>
    private void GlobalCitySearchList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                {
                    int idx  = _dropdownHighlight >= 0 ? _dropdownHighlight : 0;
                    var city = GlobalCitySearchList.Items.Count > idx
                        ? GlobalCitySearchList.Items[idx] as CityEntry : null;
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
                    _dropdownHighlight = next;
                    GlobalCitySearchList.SelectedIndex = next;
                    GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.Items[next]);
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => SetDropdownHighlight(next));
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (_dropdownHighlight <= 0)
                {
                    _dropdownHighlight = -1;
                    GlobalCitySearchList.SelectedIndex = -1;
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => SetDropdownHighlight(-1));
                    GlobalCitySearchBox.Focus();
                }
                else
                {
                    int prev = _dropdownHighlight - 1;
                    _dropdownHighlight = prev;
                    GlobalCitySearchList.SelectedIndex = prev;
                    GlobalCitySearchList.ScrollIntoView(GlobalCitySearchList.Items[prev]);
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => SetDropdownHighlight(prev));
                }
                e.Handled = true;
                break;
        }
    }

    // ── Source city is now set via CommitGlobalCity (unified search bar at top) ──

    private void SelectCurrentSlot()
    {
        // Use the same reference timezone as the grid columns (home zone, or source zone).
        var zone = _vm.Translator.EffectiveZone;
        var now  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var slot = now.Hour * 2 + (now.Minute >= 30 ? 1 : 0);
        DiagLog.Info($"SelectCurrentSlot: zone={zone.Id}, utcNow={DateTime.UtcNow:HH:mm:ss}, localNow={now:HH:mm:ss}, slot={slot}");
        _vm.Translator.SetSelectionRange(slot, slot);
    }

    private void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
            _vm.RemoveLocation(loc);
    }

    private void SetHomeLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ClockLocation loc }) return;
        if (loc.IsHome)
            _vm.ClearHomeLocation();
        else
            _vm.SetHomeLocation(loc);
    }

    private void ToggleEditMode_Click(object sender, MouseButtonEventArgs e)
    {
        var ts = ThemeService.Instance;
        bool next = !ts.EditMode;
        ts.EditMode   = next;
        ts.DeleteMode = false;   // mutually exclusive
    }

    private void ToggleDeleteMode_Click(object sender, MouseButtonEventArgs e)
    {
        var ts = ThemeService.Instance;
        bool next = !ts.DeleteMode;
        ts.DeleteMode = next;
        ts.EditMode   = false;   // mutually exclusive
    }

    /// <summary>Visually highlights the active mode button with a coloured background.</summary>
    private void UpdateModeButtonStates()
    {
        var ts = ThemeService.Instance;

        // Edit border: cyan tint when active
        EditModeBtnBorder.Background = ts.EditMode
            ? new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0xE5, 0xFF))
            : Brushes.Transparent;

        // Delete border: red tint when active
        DeleteModeBtnBorder.Background = ts.DeleteMode
            ? new SolidColorBrush(Color.FromArgb(0x55, 0xC6, 0x28, 0x28))
            : Brushes.Transparent;
    }
}

