using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorldClock.Data;
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
    private string       _hour       = DateTime.Now.Hour.ToString("D2");
    private string       _minute     = DateTime.Now.Minute.ToString("D2");
    private TimeZoneInfo _sourceZone = TimeZoneInfo.Utc;
    private TimeZoneInfo? _homeZone;          // home location's timezone (null = not set)
    private bool         _isOpen = true;   // expanded by default
    private bool         _hasExplicitSelection;
    private int _selectionStart = DateTime.Now.Hour * 2;
    private int _selectionEnd   = DateTime.Now.Hour * 2;

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

    public TimeZoneInfo SourceZone
    {
        get => _sourceZone;
        set
        {
            if (!SetProperty(ref _sourceZone, value ?? TimeZoneInfo.Utc)) return;
            BuildGrid();
            Translate();
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
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
        foreach (var col in Columns)        col.IsSelected = false;
        foreach (var row in Rows)
            foreach (var cell in row.Cells) cell.IsSelected = false;

        _selectionStart = start;
        _selectionEnd   = end;

        // Apply new highlight
        for (int i = start; i <= end; i++)
        {
            if (i < Columns.Count) Columns[i].IsSelected = true;
            foreach (var row in Rows)
                if (i < row.Cells.Count) row.Cells[i].IsSelected = true;
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
        _date           = DateTime.Today;
        _selectionStart = DateTime.Now.Hour * 2;
        _selectionEnd   = DateTime.Now.Hour * 2;
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
        BuildGrid();
        Translate();
    }

    // ── Build transposed grid: columns = 48 half-hour slots, rows = cities ─────
    public void BuildGrid()
    {
        var date    = _date ?? DateTime.Today;
        // When a home location is set, its timezone drives the column headers so the
        // home row always "coincides" with (shows the same times as) the header.
        var srcZone = _homeZone ?? _sourceZone;

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
                    SlotIndex  = s,
                    TimeStr    = utcSlot.ToString("HH:mm"),
                    DayDiff    = diffStr,
                    Band       = GetTimeBand(utcSlot.Hour),
                    IsSelected = s >= _selectionStart && s <= _selectionEnd,
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
                    SlotIndex  = s,
                    TimeStr    = local.ToString("HH:mm"),
                    DayDiff    = diffStr,
                    Band       = GetTimeBand(local.Hour),
                    IsSelected = s >= _selectionStart && s <= _selectionEnd,
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

        // Compute selection-end UTC for the window end time shown in cards.
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
