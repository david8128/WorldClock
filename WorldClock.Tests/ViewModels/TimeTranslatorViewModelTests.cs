using System.Collections.ObjectModel;
using Avalonia.Media;
using FluentAssertions;
using WorldClock.Models;
using WorldClock.ViewModels;
using Xunit;

namespace WorldClock.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="TimeTranslatorViewModel"/>.
/// Verifies correct time translation and DST detection for both
/// daylight saving time and standard time periods.
/// </summary>
public sealed class TimeTranslatorViewModelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ObservableCollection<ClockLocation> MakeLocations(
        params (string city, string flag, string tzId)[] entries)
    {
        var col = new ObservableCollection<ClockLocation>();
        var brush = new SolidColorBrush(Colors.White);
        foreach (var (city, flag, tzId) in entries)
        {
            var loc = new ClockLocation
            {
                CityName    = city,
                CountryFlag = flag,
                TimeZoneId  = tzId,
                TeamLabel   = "Test",
                AccentBrush = brush
            };
            loc.Refresh();
            col.Add(loc);
        }
        return col;
    }

    private static TimeTranslatorViewModel BuildVm(
        ObservableCollection<ClockLocation> locations,
        string tzId, DateTime date, int hour, int minute)
    {
        var vm = new TimeTranslatorViewModel(locations)
        {
            SourceZone = TimeZoneInfo.FindSystemTimeZoneById(tzId),
            Date       = date,
            Hour       = hour.ToString("D2"),
            Minute     = minute.ToString("D2"),
        };
        return vm;
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_PopulatesResults_ForEveryLocation()
    {
        var locs = MakeLocations(
            ("UTC / GMT", "🌐", "UTC"),
            ("New York",  "🇺🇸", "Eastern Standard Time"));

        var vm = new TimeTranslatorViewModel(locs);

        vm.Results.Should().HaveCount(2, "one result per configured location");
    }

    [Fact]
    public void Constructor_Hours_HasTwentyFourEntries()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.Hours.Should().HaveCount(24);
        vm.Hours.First().Should().Be("00");
        vm.Hours.Last().Should().Be("23");
    }

    [Fact]
    public void Constructor_Minutes_HasSixtyEntries()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.Minutes.Should().HaveCount(60);
        vm.Minutes.First().Should().Be("00");
        vm.Minutes.Last().Should().Be("59");
    }

    [Fact]
    public void Constructor_DefaultSourceZone_IsLocalMachineTimezone()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SourceZone.Id.Should().Be(TimeZoneInfo.Local.Id,
            "default source zone should be the machine local timezone so column headers show local time before a home location is set");
    }

    [Fact]
    public void Constructor_IsOpen_DefaultIsTrue()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.IsOpen.Should().BeTrue("Time Visualizer is expanded by default");
    }

    [Fact]
    public void SourceZones_ContainsUtc()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SourceZones.Should().Contain(z => z.Id == "UTC");
    }

    [Fact]
    public void SourceZones_AreOrderedByBaseOffset()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        var offsets = vm.SourceZones.Select(z => z.BaseUtcOffset).ToList();
        offsets.Should().BeInAscendingOrder();
    }

    // ── DST detection ─────────────────────────────────────────────────────────

    [Fact]
    public void Translate_SummerUtcInput_EasternTime_ShowsDstActive()
    {
        // 1 July 2026, 12:00 UTC → Eastern Daylight Time (UTC-4), DST = true
        var locs = MakeLocations(("New York", "🇺🇸", "Eastern Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 7, 1), 12, 0);

        var result = vm.Results.Single();
        result.IsDst.Should().BeTrue("Eastern Time observes DST in July 2026");
        result.DstLabel.Should().Be("☀ DST");
        result.TimeStr.Should().Be("08:00",    "UTC-4 offset in summer (EDT)");
        result.UtcOffset.Should().Be("UTC-04:00");
    }

    [Fact]
    public void Translate_WinterUtcInput_EasternTime_ShowsDstInactive()
    {
        // 1 Jan 2026, 12:00 UTC → Eastern Standard Time (UTC-5), DST = false
        var locs = MakeLocations(("New York", "🇺🇸", "Eastern Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 1, 1), 12, 0);

        var result = vm.Results.Single();
        result.IsDst.Should().BeFalse("Eastern Time does not observe DST in January");
        result.DstLabel.Should().Be("— STD");
        result.TimeStr.Should().Be("07:00",    "UTC-5 offset in winter (EST)");
        result.UtcOffset.Should().Be("UTC-05:00");
    }

    [Fact]
    public void Translate_SummerUtcInput_LondonTime_ShowsDstActive()
    {
        // 1 July 2026, 12:00 UTC → British Summer Time (UTC+1), DST = true
        var locs = MakeLocations(("London", "🇬🇧", "GMT Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 7, 1), 12, 0);

        var result = vm.Results.Single();
        result.IsDst.Should().BeTrue("London observes BST in July");
        result.TimeStr.Should().Be("13:00", "UTC+1 in summer");
        result.UtcOffset.Should().Be("UTC+01:00");
    }

    [Fact]
    public void Translate_WinterUtcInput_LondonTime_ShowsDstInactive()
    {
        var locs = MakeLocations(("London", "🇬🇧", "GMT Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 1, 1), 12, 0);

        var result = vm.Results.Single();
        result.IsDst.Should().BeFalse("London is on GMT (UTC+0) in January");
        result.TimeStr.Should().Be("12:00");
        result.UtcOffset.Should().Be("UTC+00:00");
    }

    [Fact(Skip = "India Standard Time is a Windows TZ ID not available on Linux/macOS")]
    public void Translate_IndiaTimezone_NeverDst()
    {
        // India Standard Time (UTC+5:30) does not observe DST at any time of year
        var locs = MakeLocations(("Mumbai", "🇮🇳", "India Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 7, 1), 0, 0);

        var result = vm.Results.Single();
        result.IsDst.Should().BeFalse("India does not observe DST");
        result.UtcOffset.Should().Be("UTC+05:30");
        result.TimeStr.Should().Be("05:30");
    }

    // ── Cross-timezone source ─────────────────────────────────────────────────

    [Fact]
    public void Translate_FromEasternSource_ToUtc_SummerConversion()
    {
        // Input: 08:00 EDT (Eastern DST, UTC-4) on 1 July 2026
        // Expected UTC result: 12:00
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        var vm   = BuildVm(locs, "Eastern Standard Time", new DateTime(2026, 7, 1), 8, 0);

        var result = vm.Results.Single();
        result.TimeStr.Should().Be("12:00", "08:00 EDT = 12:00 UTC in summer");
    }

    [Fact]
    public void Translate_FromEasternSource_ToUtc_WinterConversion()
    {
        // Input: 07:00 EST (UTC-5) on 1 Jan 2026
        // Expected UTC result: 12:00
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        var vm   = BuildVm(locs, "Eastern Standard Time", new DateTime(2026, 1, 1), 7, 0);

        var result = vm.Results.Single();
        result.TimeStr.Should().Be("12:00", "07:00 EST = 12:00 UTC in winter");
    }

    // ── Property change triggers retranslation ────────────────────────────────

    [Fact]
    public void SetDate_TriggersRetranslation()
    {
        var locs    = MakeLocations(("New York", "🇺🇸", "Eastern Standard Time"));
        var vm      = BuildVm(locs, "UTC", new DateTime(2026, 1, 1), 12, 0);
        var initial = vm.Results.Single().IsDst;  // should be false (winter)

        vm.Date = new DateTime(2026, 7, 1);         // switch to summer
        vm.Results.Single().IsDst.Should().BeTrue("switching to July should activate DST");
    }

    [Fact]
    public void SetHour_UpdatesTranslatedTime()
    {
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 1, 1), 10, 0);

        vm.Hour = "15";
        vm.Results.Single().TimeStr.Should().Be("15:00");
    }

    [Fact]
    public void SetMinute_UpdatesTranslatedTime()
    {
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 1, 1), 9, 0);

        vm.Minute = "45";
        vm.Results.Single().TimeStr.Should().Be("09:45");
    }

    [Fact]
    public void SetSourceZone_TriggersRetranslation()
    {
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        // Input 12:00 UTC → UTC result 12:00
        var vm = BuildVm(locs, "UTC", new DateTime(2026, 1, 1), 12, 0);
        vm.Results.Single().TimeStr.Should().Be("12:00");

        // Switch source to Eastern (UTC-5 in Jan) → 12:00 EST = 17:00 UTC
        vm.SourceZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        vm.Results.Single().TimeStr.Should().Be("17:00",
            "12:00 local Eastern Standard Time = 17:00 UTC in winter");
    }

    // ── IsOpen toggle ─────────────────────────────────────────────────────────

    [Fact]
    public void IsOpen_SetFalse_FiresPropertyChanged()
    {
        var vm      = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsOpen = false;   // starts true, toggling to false fires the event
        changed.Should().Contain(nameof(vm.IsOpen));
    }

    // ── Result content ────────────────────────────────────────────────────────

    [Fact]
    public void Results_ContainCityName_AndFlag()
    {
        var locs = MakeLocations(("Tokyo", "🇯🇵", "Tokyo Standard Time"));
        var vm   = new TimeTranslatorViewModel(locs);

        var result = vm.Results.Single();
        result.CityName.Should().Be("Tokyo");
        result.CountryFlag.Should().Be("🇯🇵");
    }

    [Fact]
    public void Results_DstBrush_IsNotNull()
    {
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        var vm   = new TimeTranslatorViewModel(locs);
        vm.Results.Single().DstBrush.Should().NotBeNull();
    }

    [Fact]
    public void Translate_EmptyLocations_ResultsIsEmpty()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.Results.Should().BeEmpty();
    }

    [Fact]
    public void Translate_MultipleLocations_AllTranslated()
    {
        var locs = MakeLocations(
            ("UTC / GMT", "🌐",  "UTC"),
            ("New York",  "🇺🇸", "Eastern Standard Time"),
            ("Tokyo",     "🇯🇵", "Tokyo Standard Time"));

        var vm = BuildVm(locs, "UTC", new DateTime(2026, 1, 1), 0, 0);

        vm.Results.Should().HaveCount(3);
        vm.Results.Should().Contain(r => r.CityName == "UTC / GMT");
        vm.Results.Should().Contain(r => r.CityName == "New York");
        vm.Results.Should().Contain(r => r.CityName == "Tokyo");
    }

    // ── DST boundary robustness ───────────────────────────────────────────────

    [Fact]
    public void Translate_ExactDstStartMoment_DoesNotThrow()
    {
        // 8 March 2026 02:00 local US Eastern is the clocks-spring-forward moment
        // (02:00 is skipped — non-existent). The VM must handle this gracefully.
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        var vm   = new TimeTranslatorViewModel(locs);

        var act = () =>
        {
            vm.SourceZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            vm.Date   = new DateTime(2026, 3, 8);
            vm.Hour   = "02";
            vm.Minute = "30";
        };

        act.Should().NotThrow("DST gap should be handled silently by advancing an hour");
    }

    [Fact]
    public void Translate_ExactDstEndMoment_DoesNotThrow()
    {
        // 1 Nov 2026 01:00 local US Eastern is the clocks-fall-back moment (ambiguous)
        var locs = MakeLocations(("UTC / GMT", "🌐", "UTC"));
        var vm   = new TimeTranslatorViewModel(locs);

        var act = () =>
        {
            vm.SourceZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            vm.Date   = new DateTime(2026, 11, 1);
            vm.Hour   = "01";
            vm.Minute = "30";
        };

        act.Should().NotThrow("ambiguous DST time should not throw");
    }

    // ── Selection / cell alignment ────────────────────────────────────────────
    // These tests verify that the selected slot index, the hour/minute properties,
    // and the cell IsSelected flags all point to the SAME slot so that the column
    // header highlight and every row's cell highlight are pixel-aligned.

    [Fact]
    public void SetSelectionRange_SingleSlot_ColumnAndAllCellsSelected()
    {
        // With srcZone = UTC and slot 10 (05:00 UTC), every row's cell[10]
        // must be IsSelected=true and cells outside [10] must be false.
        var locs = MakeLocations(
            ("New York", "🇺🇸", "Eastern Standard Time"),
            ("Tokyo",    "🇯🇵", "Tokyo Standard Time"));
        var vm = BuildVm(locs, "UTC", new DateTime(2026, 5, 7), 0, 0);

        vm.SetSelectionRange(10, 10);

        // Column highlight
        vm.Columns[10].IsSelected.Should().BeTrue("column header slot 10 must be selected");
        vm.Columns[9].IsSelected.Should().BeFalse("adjacent column 9 must not be selected");
        vm.Columns[11].IsSelected.Should().BeFalse("adjacent column 11 must not be selected");

        // Every data row
        foreach (var row in vm.Rows)
        {
            row.Cells[10].IsSelected.Should().BeTrue(
                $"row '{row.CityName}' cell[10] must be selected when selection=10");
            row.Cells[9].IsSelected.Should().BeFalse(
                $"row '{row.CityName}' cell[9] must not be selected");
            row.Cells[11].IsSelected.Should().BeFalse(
                $"row '{row.CityName}' cell[11] must not be selected");
        }
    }

    [Fact]
    public void SetSelectionRange_HourAndMinuteMatchSlot()
    {
        // Hour/Minute properties must reflect the START slot so the search bar
        // text field and the column highlight both show the same time.
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SetSelectionRange(20, 20);   // slot 20 = 10:00

        vm.Hour.Should().Be("10",  "slot 20 / 2 = hour 10");
        vm.Minute.Should().Be("00", "slot 20 % 2 = 0 → 00 minutes");
        vm.SelectionStart.Should().Be(20);

        vm.SetSelectionRange(21, 21);   // slot 21 = 10:30
        vm.Hour.Should().Be("10",  "slot 21 / 2 = hour 10");
        vm.Minute.Should().Be("30", "slot 21 % 2 = 1 → 30 minutes");
        vm.SelectionStart.Should().Be(21);
    }

    [Fact]
    public void SetSelectionRange_Range_AllSlotsInRangeSelected()
    {
        // Multi-slot selection: columns and cells in [start..end] must all be
        // selected; slots outside the range must not be.
        var locs = MakeLocations(("New York", "🇺🇸", "Eastern Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 5, 7), 0, 0);

        vm.SetSelectionRange(10, 13);  // slots 10–13 (05:00 – 06:30 UTC)

        for (int s = 10; s <= 13; s++)
        {
            vm.Columns[s].IsSelected.Should().BeTrue(
                $"column slot {s} must be selected in range [10,13]");
            foreach (var row in vm.Rows)
                row.Cells[s].IsSelected.Should().BeTrue(
                    $"row '{row.CityName}' cell[{s}] must be selected in range [10,13]");
        }

        vm.Columns[9].IsSelected.Should().BeFalse("slot 9 is before the range");
        vm.Columns[14].IsSelected.Should().BeFalse("slot 14 is after the range");
    }

    [Fact]
    public void SetSelectionRange_ClearsOldHighlight_BeforeApplyingNew()
    {
        // After moving selection from slot 5 to slot 30, slot 5 must be deselected.
        var locs = MakeLocations(("Bogota", "🇨🇴", "SA Pacific Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 5, 7), 0, 0);

        vm.SetSelectionRange(5, 5);
        vm.SetSelectionRange(30, 30);

        vm.Columns[5].IsSelected.Should().BeFalse("old slot 5 must be cleared");
        vm.Columns[30].IsSelected.Should().BeTrue("new slot 30 must be selected");
        foreach (var row in vm.Rows)
        {
            row.Cells[5].IsSelected.Should().BeFalse(
                $"row '{row.CityName}' old cell[5] must be cleared");
            row.Cells[30].IsSelected.Should().BeTrue(
                $"row '{row.CityName}' new cell[30] must be selected");
        }
    }

    [Fact]
    public void SetSelectionRange_ColumnSlotIndex_MatchesCellSlotIndex()
    {
        // The critical alignment invariant: Columns[S].SlotIndex == S must equal
        // every row's Cells[S].SlotIndex.  Canvas.Left = SlotIndex * 10px, so
        // if they match, header highlights and cell boxes are guaranteed pixel-aligned.
        var locs = MakeLocations(
            ("New York",  "🇺🇸", "Eastern Standard Time"),
            ("Bogota",    "🇨🇴", "SA Pacific Standard Time"),
            ("Tokyo",     "🇯🇵", "Tokyo Standard Time"));
        var vm = BuildVm(locs, "UTC", new DateTime(2026, 5, 7), 0, 0);

        for (int s = 0; s < 48; s++)
        {
            vm.Columns[s].SlotIndex.Should().Be(s,
                $"Columns[{s}].SlotIndex must equal {s} so Canvas.Left = {s * 10}px");

            foreach (var row in vm.Rows)
                row.Cells[s].SlotIndex.Should().Be(s,
                    $"Row '{row.CityName}' Cells[{s}].SlotIndex must equal {s}");
        }
    }

    [Fact]
    public void SetSelectionRange_ColumnAndCellCountIs48()
    {
        // Both Columns and every row's Cells must have exactly 48 entries.
        var locs = MakeLocations(("New York", "🇺🇸", "Eastern Standard Time"));
        var vm   = BuildVm(locs, "UTC", new DateTime(2026, 5, 7), 0, 0);

        vm.Columns.Should().HaveCount(48, "48 half-hour slots per day");
        foreach (var row in vm.Rows)
            row.Cells.Should().HaveCount(48,
                $"row '{row.CityName}' must have 48 cells");
    }

    [Fact]
    public void SetHomeZone_BeforeExplicitClick_SnapsToColombia_NotUtc()
    {
        // The initial selection must be at the START of the current hour in the home
        // timezone (Colombia = UTC-5) — always an even slot (:00, not :30).
        // Before the fix, the first render used srcZone=UTC causing a 5-hour shift.
        var locs = MakeLocations(("Bogota", "🇨🇴", "SA Pacific Standard Time"));
        var vm   = new TimeTranslatorViewModel(locs);

        var colombia = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        vm.SetHomeZone(colombia);

        // EffectiveZone must be Colombia
        vm.EffectiveZone.Id.Should().Be("SA Pacific Standard Time",
            "after SetHomeZone, EffectiveZone must be Colombia, not UTC");

        // SelectionStart must match the current 30-minute slot in Colombia time.
        var now      = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, colombia);
        var expected = now.Hour * 2 + (now.Minute >= 30 ? 1 : 0);
        vm.SelectionStart.Should().Be(expected,
            $"SelectionStart must be at slot {expected} (Colombia local {now:HH:mm}), not at the UTC-offset slot");
    }
}
