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

    private const int RowHeaderWidth = 90;
    private const int SlotCellWidth   = 20;
    private bool _isDragging;
    private int  _dragStartSlot;

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
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyAcrylic();
        ApplyScaleMode();

        ThemeService.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ThemeService.Opacity)
                                  or nameof(ThemeService.ActiveTheme))
                ApplyAcrylic();

            if (args.PropertyName == nameof(ThemeService.ScaleMode))
                ApplyScaleMode();
        };
    }

    // ── Scale mode ────────────────────────────────────────────────────────────
    // Layout is now always fluid (DesignRoot binds its Width to the ScrollViewer).
    // ScaleMode setting is kept for settings persistence but no longer controls a Viewbox.

    private void ApplyScaleMode()
    {
        // Both modes now get a fluid layout — scrollbars appear only when the window
        // is narrower than MinWidth (430px).
        ScaleViewbox.Stretch = Stretch.None;
        ScaleScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ScaleScrollViewer.VerticalScrollBarVisibility   = ScrollBarVisibility.Auto;
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
        if (_vm.Translator.IsOpen)
            Dispatcher.InvokeAsync(SelectCurrentSlot,
                                   System.Windows.Threading.DispatcherPriority.Background);
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

    // ── Time Translator — grid drag-select (transposed: click/drag across slots) ──

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
        if (pos.X < RowHeaderWidth) return;  // clicked in row-header zone

        _isDragging    = true;
        _dragStartSlot = GetSlotFromPosition(fe, pos);
        _vm.Translator.SetSelectionRange(_dragStartSlot, _dragStartSlot);
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

        // GlobalCitySearchBar and SearchOverlayCanvas are SIBLINGS inside LayoutRoot.
        // TransformToAncestor requires an actual ancestor — use LayoutRoot as the
        // common reference. The Canvas starts at LayoutRoot row 0 so its coordinate
        // origin is the same as LayoutRoot's, making Canvas.Left/Top values correct.
        var origin = GlobalCitySearchBar
            .TransformToAncestor(LayoutRoot)
            .Transform(new Point(0, GlobalCitySearchBar.ActualHeight));

        Canvas.SetLeft(GlobalSearchDropdown, origin.X);
        Canvas.SetTop(GlobalSearchDropdown,  origin.Y);
        GlobalSearchDropdown.Width = GlobalCitySearchBar.ActualWidth;
        GlobalSearchDropdown.Visibility = Visibility.Visible;
    }

    private void HideGlobalDropdown()
    {
        _globalSearchTimer.Stop();
        GlobalSearchDropdown.Visibility = Visibility.Collapsed;
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
                // Immediate search without waiting for the timer
                _globalSearchTimer.Stop();
                var text = GlobalCitySearchBox.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                    await CommitSearchAsync(text);
                e.Handled = true;
                break;

            case Key.Down:
                // Move focus into the list so arrow keys can navigate it
                if (GlobalCitySearchList.HasItems)
                {
                    GlobalCitySearchList.Focus();
                    GlobalCitySearchList.SelectedIndex = 0;
                    (GlobalCitySearchList.ItemContainerGenerator
                        .ContainerFromIndex(0) as ListBoxItem)?.Focus();
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
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!GlobalSearchDropdown.IsKeyboardFocusWithin)
                HideGlobalDropdown();
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
        PositionAndShowDropdown();
    }

    private void GlobalCitySearch_ResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is CityEntry city)
        {
            _vm.AddLocation(city.City, city.TimeZoneId, "", city.CountryFlag);
            lb.SelectedItem = null;
            HideGlobalDropdown();
            GlobalCitySearchBox.Text = "";
            GlobalCitySearchPlaceholder.Visibility = Visibility.Visible;
            GlobalCitySearchBox.Focus();
        }
    }

    // ── Time Translator — source city search ──────────────────────────────────

    private CancellationTokenSource _searchCts = new();

    private void SourceCityBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) tb.SelectAll();
        // Re-open popup if there is already text
        if (!string.IsNullOrEmpty(SourceCityBox.Text))
            _ = RunSearchAsync(SourceCityBox.Text);
    }

    private async void SourceCityBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SourceCityBox.Text;

        SourceCityPlaceholder.Visibility =
            string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(text))
        {
            SourceCityPopup.IsOpen = false;
            return;
        }

        // Cancel any pending search from previous keystrokes
        _searchCts.Cancel();
        _searchCts = new CancellationTokenSource();
        var cts = _searchCts;

        // Open immediately so popup is visible during the async wait
        SourceCityList.ItemsSource = null;
        SourceCityPopup.IsOpen = true;

        // Debounce: wait 150ms before searching so rapid typing doesn't pile up
        try { await Task.Delay(150, cts.Token); }
        catch (OperationCanceledException) { return; }

        await RunSearchAsync(text, cts);
    }

    private async Task RunSearchAsync(string text, CancellationTokenSource? cts = null)
    {
        // Run scoring on a background thread — keeps UI fully responsive
        IReadOnlyList<CityEntry> results;
        try
        {
            results = await Task.Run(
                () => WorldCitySearchService.Search(text),
                cts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException) { return; }

        if (cts?.IsCancellationRequested == true) return;

        SourceCityList.ItemsSource = results;
        SourceCityPopup.IsOpen     = results.Count > 0;
    }

    private void SourceCityBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Give the list a chance to receive the click before closing
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (!SourceCityList.IsKeyboardFocusWithin &&
                !SourceCityPopup.IsKeyboardFocusWithin)
                SourceCityPopup.IsOpen = false;
        });
    }

    private void SourceCityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is CityEntry city)
        {
            _vm.Translator.SelectedSourceCity = city;
            lb.SelectedItem = null;
            SourceCityPopup.IsOpen = false;
        }
    }

    private void AddCityFromTranslator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CityEntry city })
        {
            e.Handled = true;
            _vm.AddLocation(city.City, city.TimeZoneId, string.Empty, city.CountryFlag);
        }
    }

    private void SelectCurrentSlot()
    {
        var now  = DateTime.Now;
        var slot = now.Hour * 2 + (now.Minute >= 30 ? 1 : 0);
        _vm.Translator.SetSelectionRange(slot, slot);
    }

    private void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClockLocation loc })
            _vm.RemoveLocation(loc);
    }
}
