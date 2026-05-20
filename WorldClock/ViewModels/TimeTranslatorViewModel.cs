using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorldClock.Data;
using WorldClock.Helpers;
using WorldClock.Models;

namespace WorldClock.ViewModels;

/// <summary>
/// Drives the Time Visualizer panel: a WTB-style 24-hour grid across all configured
/// timezones, with city-search source selection, date navigation, and point-in-time
/// result cards.  All legacy properties are preserved for backward test compatibility.
/// </summary>
public sealed class TimeTranslatorViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<ClockLocation> _locations;

    // ── Legacy picker data (kept for tests + still usable in XAML) ────────────
    public IReadOnlyList<string>       Hours       { get; } =
        Enumerable.Range(0, 24).Select(i => i.ToString("D2")).ToList();
    public IReadOnlyList<string>       Minutes     { get; } =
        Enumerable.Range(0, 60).Select(i => i.ToString("D2")).ToList();
    public IReadOnlyList<TimeZoneInfo> SourceZones { get; } =
        TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.BaseUtcOffset).ToList();

    // ── Core inputs ───────────────────────────────────────────────────────────
    private DateTime?    _date       = DateTime.Today;
    // _hour/_minute represent the START of the selected slot in _sourceZone (or _homeZone).
    // They are overridden in the constructor via SnapSelectionToNow before first BuildGrid.
    private string       _hour       = DateTime.UtcNow.Hour.ToString("D2");
    private string       _minute     = (DateTime.UtcNow.Minute >= 30 ? 30 : 0).ToString("D2");
    private TimeZoneInfo _sourceZone = TimeZoneInfo.Local;
    private TimeZoneInfo? _homeZone;          // home location's timezone (null = not set)
    private bool         _isOpen = true;   // expanded by default
    private bool         _hasExplicitSelection;
    // Slot fields are set correctly by SnapSelectionToNow in the constructor.
    private int    _selectionStart    = 0;
    private int    _selectionEnd      = 0;
    private double _currentTimeLeft   = -1.0;

    public DateTime? Date
    {
        get => _date;
        set
        {
            if (!SetProperty(ref _date, value)) return;
            OnPropertyChanged(nameof(DateLabel));
            BuildGrid();
            Translate();
        }
    }

    public string Hour
    {
        get => _hour;
        set { if (SetProperty(ref _hour, value)) Translate(); }
    }

    public string Minute
    {
        get => _minute;
        set { if (SetProperty(ref _minute, value)) Translate(); }
    }

    /// <summary>The timezone that drives the column headers: home location tz, or source tz if no home is set.</summary>
    public TimeZoneInfo EffectiveZone => _homeZone ?? _sourceZone;

    public TimeZoneInfo SourceZone
    {
        get => _sourceZone;
        set
        {
            if (!SetProperty(ref _sourceZone, value ?? TimeZoneInfo.Local)) return;
            BuildGrid();
            Translate();
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    // ── Click diagnostics (last raw mouse-X received by the hit-test border) ─────
    private double _lastClickPosX      = -1;
    private double _diagScrollViewerX  = -1;  // GridScrollViewer origin-X in TranslatorResults coords
    private double _diagOrigSrcX       = -1;  // clicked element origin-X in TranslatorResults coords
    private double _diagCpCanvasLeft   = -1;  // Canvas.Left of the hit ContentPresenter (= slot * 10)
    private double _diagCanvas1aX      = -1;  // Layer-1a parent Canvas origin-X in TranslatorResults coords

    /// <summary>Stores the raw pos.X from the last PreviewMouseDown on the grid border.</summary>
    public double LastClickPosX
    {
        get => _lastClickPosX;
        set { _lastClickPosX = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiagStatus)); }
    }

    /// <summary>Actual x-origin of GridScrollViewer within the TranslatorResults Border (measured via TransformToAncestor).
    /// Should be 0 if layout is correct. Non-zero means the scrollviewer is offset.</summary>
    public double DiagScrollViewerX
    {
        get => _diagScrollViewerX;
        set { _diagScrollViewerX = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiagStatus)); }
    }

    /// <summary>Actual x-origin of the clicked element within the TranslatorResults Border.</summary>
    public double DiagOrigSrcX
    {
        get => _diagOrigSrcX;
        set { _diagOrigSrcX = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiagStatus)); }
    }

    /// <summary>Canvas.Left of the hit ContentPresenter (= its SlotIndex * 10). Negative = not measured.</summary>
    public double DiagCpCanvasLeft
    {
        get => _diagCpCanvasLeft;
        set { _diagCpCanvasLeft = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiagStatus)); }
    }

    /// <summary>X-origin of the Layer-1a parent Canvas in TranslatorResults coords. Should be 160.</summary>
    public double DiagCanvas1aX
    {
        get => _diagCanvas1aX;
        set { _diagCanvas1aX = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiagStatus)); }
    }

    // ── Current-time needle (today-only vertical marker) ─────────────────────

    /// <summary>Pixel offset from the left edge of the visualizer to the current-time needle.
    /// Includes the 160 px row-header.  Negative when the viewed date is not today.</summary>
    public double CurrentTimeLeft
    {
        get => _currentTimeLeft;
        private set
        {
            if (Math.Abs(_currentTimeLeft - value) < 0.01) return;
            _currentTimeLeft = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowCurrentTime));
        }
    }

    /// <summary>True when the current-time needle should be visible in the header.</summary>
    public bool ShowCurrentTime => _currentTimeLeft >= 0;

    /// <summary>
    /// Real-time debug status string — visible in the UI debug overlay when enabled.
    /// Format: "srcZone | slot=N | label=Nh:mm | needle=Npx"
    /// </summary>
    public string DiagStatus
    {
        get
        {
            var srcZone = _homeZone ?? _sourceZone;
            var slotHour  = _selectionStart / 2;
            var slotMin   = (_selectionStart % 2) * 30;
            bool pm       = slotHour >= 12;
            int  h12      = slotHour % 12 == 0 ? 12 : slotHour % 12;
            var slotLabel = $"{h12}:{slotMin:D2}{(pm ? "pm" : "am")}";

            // Per-cell IsHourSelected for first row — confirms binding is live.
            // Slots 0,2,4 (even = hour-starts) should show true when selection
            // overlaps that hour. All odd slots are always false.
            // Per-cell IsHourSelected around the active selection — shows slots 0,2,8,10,12
            string cellDbg = "(no rows)";
            if (Rows.Count > 0 && Rows[0].Cells.Count >= 14)
            {
                var c = Rows[0].Cells;
                cellDbg = $"IHS[0]={c[0].IsHourSelected} IHS[2]={c[2].IsHourSelected} IHS[8]={c[8].IsHourSelected} IHS[10]={c[10].IsHourSelected} IHS[12]={c[12].IsHourSelected}";
            }

            // Column-header IsHourSelected for slots 0,2,8,10,12
            string colDbg = "(no cols)";
            if (Columns.Count >= 14)
                colDbg = $"col[0].IHS={Columns[0].IsHourSelected} col[2].IHS={Columns[2].IsHourSelected} col[10].IHS={Columns[10].IsHourSelected}";

            // ── Compact first line: the numbers most likely to be truncated last ──
            string svX    = _diagScrollViewerX >= 0 ? $"{_diagScrollViewerX:F0}" : "?";
            string srcX   = _diagOrigSrcX      >= 0 ? $"{_diagOrigSrcX:F0}"      : "?";
            string clkSlot= _lastClickPosX     >= 0 ? $"{(int)((_lastClickPosX - 345) / 24)}" : "?";
            string clkX   = _lastClickPosX     >= 0 ? $"{_lastClickPosX:F0}"     : "?";

            string cpLeft  = _diagCpCanvasLeft >= 0 ? $"{_diagCpCanvasLeft:F0}" : "?";
            string canvX   = _diagCanvas1aX    >= 0 ? $"{_diagCanvas1aX:F0}"    : "?";
            int    hitSlot = _diagCpCanvasLeft >= 0 ? (int)(_diagCpCanvasLeft / 10) : -1;

            return $"sel={_selectionStart}-{_selectionEnd} | {slotLabel} | clk.X={clkX}\u2192slot={clkSlot} | SV.x={svX} | src.x={srcX}\n" +
                   $"  CP.L={cpLeft}\u2192hitSlot={hitSlot} | canv.X={canvX} | needle={_currentTimeLeft:F0}px\n" +
                   $"  DataRow: {cellDbg}\n" +
                   $"  ColHdr : {colDbg}";
        }
    }

    // ── Selection window (half-hour slot indices 0-47) ───────────────────────
    // Legacy int getter: hour of the selection start (for test compat)
    public int SelectedHour
    {
        get => _selectionStart / 2;
        set => SetSelectionRange(Math.Clamp(value, 0, 23) * 2,
                                 Math.Clamp(value, 0, 23) * 2);
    }

    public int SelectionStart => _selectionStart;
    public int SelectionEnd   => _selectionEnd;

    /// <summary>True when the user has explicitly clicked/dragged a slot on the timeline.</summary>
    public bool HasSelection  => _hasExplicitSelection && Results.Count > 0;

    /// <summary>e.g. "09:00 – 10:30" shown in the toggle bar digital display.</summary>
    public string SelectionWindowLabel
    {
        get
        {
            static string SlotTime(int slot)
            {
                int h = (slot / 2) % 24;
                int m = (slot % 2) * 30;
                return $"{h:D2}:{m:D2}";
            }
            return _selectionStart == _selectionEnd
                ? SlotTime(_selectionStart)
                : $"{SlotTime(_selectionStart)} – {SlotTime(_selectionEnd + 1)}";
        }
    }

    /// <summary>
    /// Sets the selected time window. Both ends inclusive (slot 0 = 00:00, 47 = 23:30).
    /// Updates column/cell highlights, row date labels, and result cards.
    /// </summary>
    public void SetSelectionRange(int start, int end)
    {
        if (start > end) (start, end) = (end, start);
        start = Math.Clamp(start, 0, 47);
        end   = Math.Clamp(end,   0, 47);
        _hasExplicitSelection = true;

        // Clear old highlight
        foreach (var col in Columns) col.IsSelected = col.IsHourSelected = false;
        foreach (var row in Rows)
            foreach (var cell in row.Cells) cell.IsSelected = cell.IsHourSelected = false;

        _selectionStart = start;
        _selectionEnd   = end;

        // Apply slot-level highlight (drives day-change bar and needle logic)
        for (int i = start; i <= end; i++)
        {
            if (i < Columns.Count) Columns[i].IsSelected = true;
            foreach (var row in Rows)
                if (i < row.Cells.Count) row.Cells[i].IsSelected = true;
        }

        // IsHourSelected: an even slot S is hour-selected when the hour it spans
        // [S, S+1] overlaps the selection [start, end].  Overlap ↔ S+1>=start && S<=end.
        // Drives the 20px selection highlight (header + data rows) to match the 20px text label.
        foreach (var col in Columns)
        {
            bool hourSel = col.SlotIndex % 2 == 0
                           && col.SlotIndex + 1 >= start
                           && col.SlotIndex     <= end;
            col.IsHourSelected = hourSel;
            // Mirror to the corresponding cell in every row
            foreach (var row in Rows)
                if (col.SlotIndex < row.Cells.Count)
                    row.Cells[col.SlotIndex].IsHourSelected = hourSel;
        }

        UpdateRowDateLabels();

        OnPropertyChanged(nameof(SelectionStart));
        OnPropertyChanged(nameof(SelectionEnd));
        OnPropertyChanged(nameof(SelectedHour));
        OnPropertyChanged(nameof(SelectionWindowLabel));

        _hour   = (start / 2).ToString("D2");
        _minute = (start % 2 == 0) ? "00" : "30";
        OnPropertyChanged(nameof(Hour));
        OnPropertyChanged(nameof(Minute));
        OnPropertyChanged(nameof(DiagStatus));
        Translate();
    }

    private void UpdateRowDateLabels()
    {
        var date = _date ?? DateTime.Today;
        foreach (var row in Rows)
        {
            if (_selectionStart >= row.Cells.Count) continue;
            var cell    = row.Cells[_selectionStart];
            var dayDiff = string.IsNullOrEmpty(cell.DayDiff) ? 0
                          : int.Parse(cell.DayDiff.TrimStart('+'));
            row.DateLabel   = date.AddDays(dayDiff).ToString("ddd, MMM d");
            row.DateDayDiff = dayDiff;
        }
    }

    // ── Source city search ────────────────────────────────────────────────────
    private string     _sourceCitySearchText = string.Empty;
    private CityEntry? _selectedSourceCity;

    public string SourceCitySearchText
    {
        get => _sourceCitySearchText;
        set => SetProperty(ref _sourceCitySearchText, value);
        // Search + popup are fully managed by MainWindow code-behind (async + debounced).
        // UpdateSourceCityMatches() is no longer called here to avoid a duplicate
        // synchronous search on the UI thread for every keystroke.
    }

    public ObservableCollection<CityEntry> SourceCityMatches { get; } = new();

    public bool HasSourceMatches => SourceCityMatches.Count > 0;

    public CityEntry? SelectedSourceCity
    {
        get => _selectedSourceCity;
        set
        {
            if (!SetProperty(ref _selectedSourceCity, value)) return;
            if (value is null) return;

            SourceCitySearchText = $"{value.CountryFlag} {value.City}";

            SourceCityMatches.Clear();

            try   { _sourceZone = TimeZoneInfo.FindSystemTimeZoneById(value.TimeZoneId); }
            catch { _sourceZone = TimeZoneInfo.Utc; }
            OnPropertyChanged(nameof(SourceZone));

            BuildGrid();
            Translate();
        }
    }

    // UpdateSourceCityMatches is intentionally not called from SourceCitySearchText.
    // The MainWindow code-behind owns the popup lifecycle (async + debounced).

    // ── Grid data ─────────────────────────────────────────────────────────────
    public ObservableCollection<TimeGridColumn> Columns { get; } = new();
    public ObservableCollection<TimeGridRow>    Rows    { get; } = new();

    // ── Results (point-in-time translation, kept for test compat + cards) ─────
    public ObservableCollection<TranslatedTime> Results { get; } = new();

    // ── Date helpers ──────────────────────────────────────────────────────────
    public string DateLabel => (_date ?? DateTime.Today).ToString("ddd, MMM d");

    public void PrevDay() => Date = (_date ?? DateTime.Today).AddDays(-1);
    public void NextDay() => Date = (_date ?? DateTime.Today).AddDays(1);
    public void GoToday()
    {
        _date = DateTime.Today;
        SnapSelectionToNow(EffectiveZone);
        OnPropertyChanged(nameof(Date));
        OnPropertyChanged(nameof(DateLabel));
        OnPropertyChanged(nameof(SelectedHour));
        BuildGrid();
        Translate();
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public TimeTranslatorViewModel(ObservableCollection<ClockLocation> locations)
    {
        _locations = locations;
        _locations.CollectionChanged += (_, _) => { BuildGrid(); Translate(); };

        // Keep HasSourceMatches in sync with the collection
        SourceCityMatches.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSourceMatches));

        // Snap selection to now in local timezone before the first render so that
        // column headers always show local (not UTC) times even before home is set.
        SnapSelectionToNow(_sourceZone);
        BuildGrid();
        Translate();
    }

    /// <summary>
    /// Called by MainViewModel when the home location changes.
    /// Sets the home timezone used as the grid's reference point (column headers + day-diff anchor).
    /// Pass null to clear the home zone and fall back to the user-selected source zone.
    /// </summary>
    public void SetHomeZone(TimeZoneInfo? tz)
    {
        _homeZone = tz;
        DiagLog.Info($"SetHomeZone: tz={(tz?.Id ?? "<null>")}, _hasExplicitSelection={_hasExplicitSelection}, effectiveZone={EffectiveZone.Id}");
        // Snap the default selection to "now" in the new reference zone so the
        // initial highlight aligns with the needle (both use the same zone).
        if (!_hasExplicitSelection)
            SnapSelectionToNow(tz ?? _sourceZone);
        BuildGrid();
        Translate();
    }

    /// <summary>
    /// Snaps _selectionStart/_selectionEnd and _hour/_minute to the current time
    /// in <paramref name="tz"/>, without marking an explicit user selection.
    /// </summary>
    private void SnapSelectionToNow(TimeZoneInfo tz)
    {
        var now  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        // Snap to the current 30-minute slot (half-hour granularity preserved).
        var slot = now.Hour * 2 + (now.Minute >= 30 ? 1 : 0);
        _selectionStart = _selectionEnd = slot;
        _hour   = (slot / 2).ToString("D2");
        _minute = slot % 2 == 0 ? "00" : "30";
        DiagLog.Info($"SnapSelectionToNow: tz={tz.Id}, utcNow={DateTime.UtcNow:HH:mm:ss}, localNow={now:HH:mm:ss}, slot={slot}, _hour={_hour}, _minute={_minute}");
    }

    // ── Build transposed grid: columns = 48 half-hour slots, rows = cities ─────
    public void BuildGrid()
    {
        var date    = _date ?? DateTime.Today;
        // When a home location is set, its timezone drives the column headers so the
        // home row always "coincides" with (shows the same times as) the header.
        var srcZone = _homeZone ?? _sourceZone;
        DiagLog.Info($"BuildGrid: date={date:yyyy-MM-dd}, srcZone={srcZone.Id}, _selectionStart={_selectionStart}, _selectionEnd={_selectionEnd}, _hour={_hour}, _minute={_minute}");

        // ── Columns: 48 half-hour slots (0 = 00:00 … 47 = 23:30) ────────────
        Columns.Clear();
        for (int s = 0; s < 48; s++)
        {
            Columns.Add(new TimeGridColumn
            {
                SlotIndex      = s,
                SlotLabel      = s % 2 == 0 ? ToAmPmHour(s / 2) : "",
                IsHourStart    = s % 2 == 0,
                IsSelected     = s >= _selectionStart && s <= _selectionEnd,
                IsHourSelected = s % 2 == 0
                                 && s + 1 >= _selectionStart
                                 && s     <= _selectionEnd,
                IsMidnight     = s == 0,
                DayOfWeekLabel = s == 0 ? date.ToString("ddd") : "",
                DateShortLabel = s == 0 ? date.ToString("MMM d") : "",
            });
        }

        // ── Rows: one per configured city + UTC row at the top (only if not already in locations) ───
        Rows.Clear();
        bool hasUtcLocation = _locations.Any(l =>
            string.Equals(l.TimeZoneId, TimeZoneInfo.Utc.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.TimeZoneId, "UTC", StringComparison.OrdinalIgnoreCase));

        // UTC row always first (unless user already added a UTC location manually)
        if (!hasUtcLocation)
        {
            var utcCells = new List<TimeGridCell>(48);
            for (int s = 0; s < 48; s++)
            {
                var h   = s / 2;
                var min = (s % 2) * 30;
                var srcTime = new DateTime(date.Year, date.Month, date.Day, h, min, 0,
                                           DateTimeKind.Unspecified);
                DateTime utcSlot;
                try   { utcSlot = TimeZoneInfo.ConvertTimeToUtc(srcTime, srcZone); }
                catch { utcSlot = TimeZoneInfo.ConvertTimeToUtc(srcTime.AddHours(1), srcZone); }
                // Day diff relative to home timezone (or source zone if no home set)
                var anchorDate = _homeZone != null
                    ? TimeZoneInfo.ConvertTimeFromUtc(utcSlot, _homeZone).Date
                    : date.Date;
                var dayDiff = (utcSlot.Date - anchorDate).Days;
                var diffStr = dayDiff > 0 ? $"+{dayDiff}" : "";  // suppress negative offsets
                utcCells.Add(new TimeGridCell
                {
                    SlotIndex      = s,
                    TimeStr        = utcSlot.ToString("HH:mm"),
                    DayDiff        = diffStr,
                    Band           = GetTimeBand(utcSlot.Hour),
                    IsSelected     = s >= _selectionStart && s <= _selectionEnd,
                    IsHourSelected = s % 2 == 0
                                     && s + 1 >= _selectionStart
                                     && s     <= _selectionEnd,
                });
            }
            var utcSelCell = utcCells[Math.Min(_selectionStart, 47)];
            var utcSdiff   = string.IsNullOrEmpty(utcSelCell.DayDiff) ? 0
                             : int.Parse(utcSelCell.DayDiff.TrimStart('+'));
            var utcRow = new TimeGridRow
            {
                CityName    = "UTC",
                CountryFlag = "🌐",
                Country     = "Universal Time",
                UtcOffset   = "UTC+00:00",
                AccentBrush = new System.Windows.Media.SolidColorBrush(
                                  System.Windows.Media.Color.FromRgb(0x88, 0x88, 0xAA)),
                IsSource    = _sourceZone.Id == TimeZoneInfo.Utc.Id,
                Cells       = utcCells,
            };
            utcRow.DateLabel   = date.AddDays(utcSdiff).ToString("ddd, MMM d");
            utcRow.DateDayDiff = utcSdiff;
            Rows.Add(utcRow);
        } // end if (!hasUtcLocation)

        // Home location row first, then others — preserves original insertion order for non-home rows
        foreach (var loc in _locations.OrderByDescending(l => l.IsHome))
        {
            TimeZoneInfo locTz;
            try   { locTz = TimeZoneInfo.FindSystemTimeZoneById(loc.TimeZoneId); }
            catch { continue; }

            var offset = locTz.GetUtcOffset(DateTime.UtcNow);
            var sign   = offset >= TimeSpan.Zero ? "+" : "-";
            var absMin = (int)Math.Abs(offset.TotalMinutes);
            var offStr = $"UTC{sign}{absMin / 60:D2}:{absMin % 60:D2}";

            var cells = new List<TimeGridCell>(48);
            for (int s = 0; s < 48; s++)
            {
                var h   = s / 2;
                var min = (s % 2) * 30;
                var srcTime = new DateTime(date.Year, date.Month, date.Day, h, min, 0,
                                           DateTimeKind.Unspecified);
                DateTime utc;
                try   { utc = TimeZoneInfo.ConvertTimeToUtc(srcTime, srcZone); }
                catch { utc = TimeZoneInfo.ConvertTimeToUtc(srcTime.AddHours(1), srcZone); }

                var local = TimeZoneInfo.ConvertTimeFromUtc(utc, locTz);
                // Day diff relative to home timezone so we only ever show "+1d" (next day),
                // never "-1d". If home is not set, fall back to source date as anchor.
                var anchorDate = _homeZone != null
                    ? TimeZoneInfo.ConvertTimeFromUtc(utc, _homeZone).Date
                    : date.Date;
                var dayDiff = (local.Date - anchorDate).Days;
                var diffStr = dayDiff > 0 ? $"+{dayDiff}" : "";  // suppress negatives

                cells.Add(new TimeGridCell
                {
                    SlotIndex      = s,
                    TimeStr        = local.ToString("HH:mm"),
                    DayDiff        = diffStr,
                    Band           = GetTimeBand(local.Hour),
                    IsSelected     = s >= _selectionStart && s <= _selectionEnd,
                    IsHourSelected = s % 2 == 0
                                     && s + 1 >= _selectionStart
                                     && s     <= _selectionEnd,
                });
            }

            // Date label from the cell at selection start
            var selCell = cells[Math.Min(_selectionStart, 47)];
            var sdiff   = string.IsNullOrEmpty(selCell.DayDiff) ? 0
                          : int.Parse(selCell.DayDiff.TrimStart('+'));

            var row = new TimeGridRow
            {
                CityName    = loc.CityName,
                CountryFlag = loc.CountryFlag,
                Country     = string.IsNullOrWhiteSpace(loc.TeamLabel) ? string.Empty : loc.TeamLabel,
                UtcOffset   = offStr,
                AccentBrush = loc.AccentBrush,
                IsSource    = loc.TimeZoneId == srcZone.Id,
                Cells       = cells,
            };
            row.DateLabel   = date.AddDays(sdiff).ToString("ddd, MMM d");
            row.DateDayDiff = sdiff;
            Rows.Add(row);
        }

        // Stamp the current-time needle position on every row
        RefreshCurrentTime();
        // Log cell text at slots 0 and 10 for the first row to verify alignment
        if (Rows.Count > 0)
        {
            var r0 = Rows[0];
            var c0  = r0.Cells[0].TimeStr;
            var c10 = r0.Cells.Count > 10 ? r0.Cells[10].TimeStr : "n/a";
            var p0  = r0.Cells[0].TimeAmPm;
            var p10 = r0.Cells.Count > 10 ? r0.Cells[10].TimeAmPm : "n/a";
            DiagLog.Info($"BuildGrid done: Row[0]={r0.CityName}, cell[0]={c0}/{p0}, cell[10]={c10}/{p10}");
        }
    }

    // ── Point-in-time translation (populates Results) ─────────────────────────
    public void Translate()
    {
        var date    = _date ?? DateTime.Today;
        var srcZone = _homeZone ?? _sourceZone;

        if (!int.TryParse(_hour,   out var h)) h = 0;
        if (!int.TryParse(_minute, out var m)) m = 0;
        h = Math.Clamp(h, 0, 23);
        m = Math.Clamp(m, 0, 59);

        var input = new DateTime(date.Year, date.Month, date.Day, h, m, 0,
                                 DateTimeKind.Unspecified);
        DateTime utc;
        try   { utc = TimeZoneInfo.ConvertTimeToUtc(input, srcZone); }
        catch { utc = TimeZoneInfo.ConvertTimeToUtc(input.AddHours(1), srcZone); }

        DiagLog.Info($"Translate: srcZone={srcZone.Id}, _hour={_hour}, _minute={_minute}, input={input:HH:mm}, utc={utc:HH:mm}, _selectionStart={_selectionStart}, SelectionWindowLabel={SelectionWindowLabel}");
        // The end of the selection is the START of slot (selectionEnd + 1).
        bool hasRange   = _selectionEnd > _selectionStart;
        int  endSlot    = _selectionEnd + 1;
        int  endH       = (endSlot / 2) % 24;
        int  endMin     = (endSlot % 2) * 30;
        bool endNextDay = endSlot >= 48;
        var  endBase    = new DateTime(date.Year, date.Month, date.Day, endH, endMin, 0,
                                       DateTimeKind.Unspecified);
        var  endInput   = endNextDay ? endBase.AddDays(1) : endBase;
        DateTime utcEnd;
        try   { utcEnd = TimeZoneInfo.ConvertTimeToUtc(endInput, srcZone); }
        catch { utcEnd = TimeZoneInfo.ConvertTimeToUtc(endInput.AddHours(1), srcZone); }

        var utcDto = new DateTimeOffset(utc, TimeSpan.Zero);

        // ── Populate row-level translated times ───────────────────────────────
        // Rows may be reordered (home first) so we match by CityName, not by index.
        // The implicit UTC row (added when no UTC city in _locations) has CityName="UTC".
        bool hasUtcRow = Rows.Count > 0 && Rows[0].CityName == "UTC"
                         && !_locations.Any(l =>
                                string.Equals(l.TimeZoneId, "UTC", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(l.TimeZoneId, TimeZoneInfo.Utc.Id, StringComparison.OrdinalIgnoreCase));
        if (hasUtcRow)
        {
            var utcRow = Rows[0];
            utcRow.SelectedTimeStr    = utc.ToString("HH:mm");
            utcRow.SelectedEndTimeStr = hasRange ? utcEnd.ToString("HH:mm") : null;
            utcRow.HasRange           = hasRange;
            utcRow.IsDst              = false;
            utcRow.ShowTranslatedTime = _hasExplicitSelection;
        }
        // Match each location to its row by CityName (order-independent, handles home-first reorder)
        foreach (var loc in _locations)
        {
            var row = Rows.FirstOrDefault(r => r.CityName == loc.CityName);
            if (row == null) continue;
            TimeZoneInfo tz;
            try   { tz = TimeZoneInfo.FindSystemTimeZoneById(loc.TimeZoneId); }
            catch { continue; }
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            row.SelectedTimeStr    = local.ToString("HH:mm");
            row.SelectedEndTimeStr = hasRange
                ? TimeZoneInfo.ConvertTimeFromUtc(utcEnd, tz).ToString("HH:mm")
                : null;
            row.HasRange           = hasRange;
            row.IsDst              = tz.IsDaylightSavingTime(utcDto);
            row.ShowTranslatedTime = _hasExplicitSelection;
            DiagLog.Debug($"  Row[{loc.CityName}] tz={tz.Id}, utc={utc:HH:mm} → local={local:HH:mm}, SelectedTimeStr={row.SelectedTimeStr}");
        }

        Results.Clear();
        // Results contains location entries only (UTC is already shown via Rows[0])
        foreach (var loc in _locations)
        {
            TimeZoneInfo tz;
            try   { tz = TimeZoneInfo.FindSystemTimeZoneById(loc.TimeZoneId); }
            catch { continue; }

            var local  = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            var isDst  = tz.IsDaylightSavingTime(utcDto);
            var offset = tz.GetUtcOffset(utcDto);

            var absMin    = (int)Math.Abs(offset.TotalMinutes);
            var sign      = offset >= TimeSpan.Zero ? "+" : "-";
            var offsetStr = $"UTC{sign}{absMin / 60:D2}:{absMin % 60:D2}";

            string? endTimeStr = hasRange
                ? TimeZoneInfo.ConvertTimeFromUtc(utcEnd, tz).ToString("HH:mm")
                : null;

            Results.Add(new TranslatedTime
            {
                CityName    = loc.CityName,
                CountryFlag = loc.CountryFlag,
                TimeStr     = local.ToString("HH:mm"),
                EndTimeStr  = endTimeStr,
                DateStr     = local.ToString("ddd, dd MMM"),
                UtcOffset   = offsetStr,
                IsDst       = isDst,
                AccentBrush = loc.AccentBrush,
            });
        }
        OnPropertyChanged(nameof(HasSelection));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static TimeBand GetTimeBand(int hour) => hour switch
    {
        >= 6  and <= 8  => TimeBand.Morning,
        >= 9  and <= 17 => TimeBand.WorkHours,
        >= 18 and <= 21 => TimeBand.Evening,
        _               => TimeBand.Night,
    };

    // ── Current-time needle helpers ────────────────────────────────────────

    // ── Teams meeting deep link ───────────────────────────────────────────────

    /// <summary>
    /// Builds the msteams:// and https:// fallback URIs for the current selection,
    /// using per-row translated times from the grid.
    /// </summary>
    public (string TeamsUri, string BrowserUri) BuildTeamsDeepLink()
    {
        var date = _date ?? DateTime.Today;

        // Collect city times from every row that has a translated time
        var cityTimes = Rows
            .Where(r => r.SelectedTimeStr != null)
            .Select(r =>
            (
                r.CityName,
                r.SelectedTimeStr ?? "",
                r.SelectedEndTimeStr ?? r.SelectedTimeStr ?? ""
            ))
            .ToList();

        return Helpers.TeamsDeepLinkBuilder.Build(date, _selectionStart, _selectionEnd, cityTimes);
    }

    // ── Current-time needle helpers ───────────────────────────────────────────

    /// <summary>
    /// Computes the pixel offset from the visualizer's left edge to "now" in the
    /// reference timezone (home tz, or source tz if no home is set).
    /// Returns -1 when the viewed date is not today.
    /// Row header = 160 px, each 30-min slot = 10 px.
    /// </summary>
    private double ComputeCurrentTimeLeft()
    {
        if ((_date ?? DateTime.Today).Date != DateTime.Today) return -1.0;
        var refZone  = _homeZone ?? _sourceZone;
        var now      = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, refZone);
        double slots = now.Hour * 2.0 + now.Minute / 30.0;  // fractional slot index
        double left  = 345.0 + slots * 24.0;                 // 345 = row-header, 24.0 = slot width px
        DiagLog.Debug($"ComputeCurrentTimeLeft: refZone={refZone.Id}, now={now:HH:mm:ss}, slots={slots:F2}, left={left:F1}px");
        return left;
    }

    /// <summary>
    /// Updates the needle position on the view-model and on every row.
    /// Called by the main 1-second timer tick and at the end of BuildGrid().
    /// </summary>
    public void RefreshCurrentTime()
    {
        double left = ComputeCurrentTimeLeft();
        CurrentTimeLeft = left;
        foreach (var row in Rows)
            row.CurrentTimeLeft = left;
    }

    private static string ToAmPmHour(int hour24)
    {
        bool pm  = hour24 >= 12;
        int  h12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
        return $"{h12}{(pm ? "p" : "a")}";
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
