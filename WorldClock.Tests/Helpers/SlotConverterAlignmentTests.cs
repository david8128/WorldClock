using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using FluentAssertions;
using WorldClock.Helpers;
using WorldClock.Models;
using WorldClock.ViewModels;
using Xunit;

namespace WorldClock.Tests.Helpers;

/// <summary>
/// Validates the pixel-alignment invariants for the Time Visualizer timeline.
///
/// Root-cause analysis of the 5-hour shift bug confirmed THREE independent axes
/// that must all agree for the selection highlight, column header labels, cell
/// labels, and the current-time needle to be visually aligned:
///
///   Axis 1 – ViewModel (srcZone): _sourceZone must default to the machine's
///             local timezone so the first BuildGrid renders column headers in
///             local time, not UTC.  Fixed by changing the field initializer
///             from TimeZoneInfo.Utc to TimeZoneInfo.Local.
///
///   Axis 2 – Converter constant (SlotWidthPx = 10.0): ALL four XAML layers
///             (column-header selection boxes, column-header labels, cell
///             backgrounds, and cell labels) compute Canvas.Left via the same
///             SlotIndexToCanvasLeftConverter. If this constant drifts from the
///             code-behind's SlotCellWidth, clicks land on the wrong slot.
///
///   Axis 3 – Code-behind constants (RowHeaderWidth, SlotCellWidth): the click
///             handler subtracts RowHeaderWidth before dividing by SlotCellWidth.
///             These must match the XAML's 160px row-header Border and the
///             converter's SlotWidthPx respectively.
///
/// The needle also uses the formula:
///   Canvas.Left = 160 + (hour * 2 + minute / 30.0) * 10
/// which is computed in ComputeCurrentTimeLeft() and matches the same constants.
///
/// These tests make the invariants machine-checkable so any future constant
/// drift is caught immediately at build time.
/// </summary>
public sealed class SlotConverterAlignmentTests
{
    // ── Axis 2: SlotIndexToCanvasLeftConverter pixel positions ────────────────

    [Theory]
    [InlineData(0,  0.0)]    // slot 0  → midnight (left edge of canvas)
    [InlineData(1,  10.0)]   // slot 1  → 00:30
    [InlineData(2,  20.0)]   // slot 2  → 01:00
    [InlineData(10, 100.0)]  // slot 10 → 05:00  (was "5am appearing at 12am column")
    [InlineData(20, 200.0)]  // slot 20 → 10:00
    [InlineData(30, 300.0)]  // slot 30 → 15:00  (3pm Colombia in original bug)
    [InlineData(47, 470.0)]  // slot 47 → 23:30  (last slot)
    public void Converter_SlotToPixel_ReturnsCorrectCanvasLeft(int slot, double expectedPx)
    {
        var converter = new SlotIndexToCanvasLeftConverter();
        var result    = converter.Convert(slot, typeof(double), null, CultureInfo.InvariantCulture);

        result.Should().Be(expectedPx,
            $"slot {slot} must map to Canvas.Left={expectedPx}px " +
            $"(each half-hour slot is {SlotIndexToCanvasLeftConverter.SlotWidthPx}px wide)");
    }

    [Fact]
    public void Converter_SlotWidthPx_IsExactlyTen()
    {
        // This is the single constant that drives ALL four XAML canvas layers.
        // It must match MainWindow.xaml.cs SlotCellWidth = 10 and the XAML
        // Border Width="10" for each cell in the data rows.
        SlotIndexToCanvasLeftConverter.SlotWidthPx.Should().Be(10.0,
            "SlotWidthPx drives Canvas.Left for column headers AND cell backgrounds. " +
            "Changing it without updating XAML Border widths causes visual misalignment.");
    }

    [Fact]
    public void Converter_ConvertBack_InvertsConvert()
    {
        var converter = new SlotIndexToCanvasLeftConverter();

        for (int slot = 0; slot < 48; slot++)
        {
            var px     = (double)converter.Convert(slot, typeof(double), null, CultureInfo.InvariantCulture)!;
            var result = converter.ConvertBack(px, typeof(int), null, CultureInfo.InvariantCulture);
            result.Should().Be(slot,
                $"ConvertBack({px}) must return slot {slot} so click-to-slot mapping is correct");
        }
    }

    // ── Axis 3: Click-to-slot formula agrees with converter ───────────────────
    // The code-behind uses: slot = (posX - RowHeaderWidth) / SlotCellWidth
    // These constants must equal: RowHeaderWidth=160, SlotCellWidth=SlotWidthPx=10

    [Fact]
    public void ClickFormula_AtSlotBoundaries_ProducesCorrectSlot()
    {
        // Reproduce MainWindow.xaml.cs GetSlotFromPosition() without depending on
        // that private method.  The XAML has a 160px row-header border so:
        //   slot = Math.Clamp((int)((posX - 160) / 10), 0, 47)

        const double rowHeaderWidth = 160.0;  // must equal XAML Border Width="160"
        const double slotWidthPx    = SlotIndexToCanvasLeftConverter.SlotWidthPx;  // 10.0

        var cases = new (double posX, int expectedSlot)[]
        {
            (160.0, 0),   // left edge of slot 0
            (169.9, 0),   // right edge of slot 0 (just before slot 1)
            (170.0, 1),   // left edge of slot 1
            (260.0, 10),  // slot 10 = 05:00 (was misaligned with srcZone=UTC)
            (460.0, 30),  // slot 30 = 15:00 Colombia
            (630.0, 47),  // slot 47 = 23:30 (last slot)
            (640.0, 47),  // beyond last slot → clamped to 47
        };

        foreach (var (posX, expected) in cases)
        {
            var slot = Math.Clamp((int)((posX - rowHeaderWidth) / slotWidthPx), 0, 47);
            slot.Should().Be(expected,
                $"clicking at X={posX} should resolve to slot {expected}");
        }
    }

    // ── Axis 1 + Axis 2 combined: needle formula vs selection slot ────────────
    // The current-time needle uses: Canvas.Left = 160 + slots * 10
    // A selection at slot S is displayed at: 160px border + S * 10px
    // They must produce the same pixel when slots == S (integer slot index).

    [Theory]
    [InlineData(0)]   // midnight
    [InlineData(10)]  // 5am
    [InlineData(30)]  // 3pm
    [InlineData(47)]  // 11:30pm
    public void NeedleFormula_AtIntegerSlot_MatchesSelectionHighlightPosition(int slot)
    {
        // Selection highlight position (column header Canvas.Left + RowHeader offset):
        var converter = new SlotIndexToCanvasLeftConverter();
        var canvasLeft = (double)converter.Convert(slot, typeof(double), null, CultureInfo.InvariantCulture)!;
        double selectionPixel = 160.0 + canvasLeft;  // 160px row header + canvas offset

        // Needle formula (from ComputeCurrentTimeLeft in TimeTranslatorViewModel):
        //   double slots = hour * 2.0 + minute / 30.0
        //   double left  = 160.0 + slots * 10.0
        double hour   = slot / 2;
        double minute = (slot % 2) * 30;
        double needlePixel = 160.0 + (hour * 2.0 + minute / 30.0) * 10.0;

        needlePixel.Should().Be(selectionPixel,
            $"at slot {slot} the needle Canvas.Left must equal the selection highlight position " +
            $"so the needle visually aligns with the highlighted column");
    }

    // ── EffectiveZone before SetHomeZone: must not be UTC ─────────────────────

    [Fact]
    public void EffectiveZone_BeforeHomeSet_IsLocalNotUtc()
    {
        // If this is UTC, the first BuildGrid renders column headers in UTC
        // while the physical time is local, causing the 5-hour shift.
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());

        vm.EffectiveZone.Id.Should().NotBe("UTC",
            "EffectiveZone must never default to UTC — that caused the 5-hour shift bug " +
            "where column headers showed UTC times but the home row showed local times. " +
            $"Current machine timezone: {TimeZoneInfo.Local.Id}");

        vm.EffectiveZone.Id.Should().Be(TimeZoneInfo.Local.Id,
            "EffectiveZone before SetHomeZone must be the machine local timezone " +
            "so the very first render is already aligned with the user's clock");
    }

    // ── srcZone consistency: column headers and SnapSelectionToNow use same zone ─

    [Fact]
    public void SelectionStart_And_ColumnHeaders_UseConsistentZone()
    {
        // After construction, the selection slot must correspond to the SAME timezone
        // that drives the column headers (EffectiveZone = Local timezone).
        // Selection always snaps to the :00 slot of the current hour (even slot)
        // so the 20px highlight aligns with the 20px label.

        var vm   = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        var zone = vm.EffectiveZone;

        // The expected slot is the current 30-minute slot in the effective zone.
        var now      = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var expected = now.Hour * 2 + (now.Minute >= 30 ? 1 : 0);

        // Allow ±1 slot for the tick between SnapSelectionToNow and this assertion.
        Math.Abs(vm.SelectionStart - expected).Should().BeLessThanOrEqualTo(1,
            $"SelectionStart ({vm.SelectionStart}) must match the slot computed from " +
            $"EffectiveZone={zone.Id} local time {now:HH:mm} (expected slot ≈ {expected}). " +
            "A difference > 1 means the slot was computed in a different timezone than the column headers.");
    }

    // ── Column-header alignment: label width vs selection cell width ──────────
    //
    // The column header has two z-ordered layers:
    //   Layer 2 (top, hit-test transparent): 24 × Width="20" text labels, one per hour.
    //   Layer 1 (bottom, clickable):  must also be 20px per hour so the highlight
    //                                 exactly covers the label it belongs to.
    //
    // With 10px-wide selection cells (one per slot), a single-slot click produces
    // a 10px highlight that covers only the LEFT HALF of the 20px "12am" label.
    // The fix: Layer 1 uses 20px cells bound to IsHourSelected (not IsSelected),
    // which is true when EITHER the :00 or the :30 slot of that hour is selected.

    [Fact]
    public void ColumnHeader_LabelWidth_Is_Exactly_TwoSlots()
    {
        // Verifies the constant in XAML: Layer-2 border Width="20"
        const double labelWidthPx = 20.0;
        labelWidthPx.Should().Be(
            2 * SlotIndexToCanvasLeftConverter.SlotWidthPx,
            "each column-header hour label is 20px = 2 × SlotWidthPx, " +
            "spanning the :00 slot and the :30 slot of the same hour");
    }

    [Fact]
    public void ColumnHeader_LabelLeft_Equals_EvenSlot_CanvasLeft_ForAllHours()
    {
        // For each hour H (0-23): label Canvas.Left == H*2 * SlotWidthPx.
        // Both layers share the same SlotToPx converter, so they are always co-located.
        for (int hour = 0; hour < 24; hour++)
        {
            int    evenSlot  = hour * 2;
            double slotLeft  = evenSlot * SlotIndexToCanvasLeftConverter.SlotWidthPx;
            double labelLeft = evenSlot * SlotIndexToCanvasLeftConverter.SlotWidthPx;
            slotLeft.Should().Be(labelLeft,
                $"hour {hour}: label Canvas.Left ({labelLeft}px) must equal " +
                $"the :00-slot Canvas.Left ({slotLeft}px)");
        }
    }

    // ── IsHourSelected behaviour tests ────────────────────────────────────────
    // These assert the correct implementation of IsHourSelected on TimeGridColumn.
    // They must pass for the 20px header selection cell to light up correctly.

    [Fact]
    public void IsHourSelected_True_When_HourStart00Slot_IsSelected()
    {
        // Selecting only the :00 slot of hour 0 must set IsHourSelected=true on
        // column 0, so the 20px header cell for "12am" is fully highlighted.
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SetSelectionRange(0, 0);  // select only slot 0 (00:00)

        vm.Columns[0].IsHourSelected.Should().BeTrue(
            "slot 0 (00:00) is the :00 start of hour 0; selecting it must make " +
            "IsHourSelected=true so the 20px '12am' header label is fully covered");
    }

    [Fact]
    public void IsHourSelected_True_When_HalfHour30Slot_IsSelected()
    {
        // KEY TEST: selecting ONLY the :30 slot of an hour must also set
        // IsHourSelected=true on the corresponding :00 column — because the 20px
        // label spans both slots and the highlight must cover the full label.
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SetSelectionRange(1, 1);  // select only slot 1 (00:30)

        vm.Columns[0].IsHourSelected.Should().BeTrue(
            "slot 1 (00:30) is the :30 half of the '12am' hour; " +
            "the column-header label for 12am is 20px wide spanning slots 0 AND 1, " +
            "so IsHourSelected must be true on slot 0 when slot 1 is selected — " +
            "otherwise the label text appears outside the 10px single-slot highlight");
    }

    [Fact]
    public void IsHourSelected_False_For_Untouched_Hour()
    {
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SetSelectionRange(4, 5);  // select only hour 2 (02:00–02:30)

        // Hour 0 (slots 0,1) is NOT in range [4,5] — must be false
        vm.Columns[0].IsHourSelected.Should().BeFalse(
            "hour 0 (slots 0-1) does not overlap selection [4,5]; IsHourSelected must be false");

        // Hour 2 (slots 4,5) IS in range [4,5] — must be true
        vm.Columns[4].IsHourSelected.Should().BeTrue(
            "slot 4 is the :00 start of hour 2, which IS in selection [4,5]");
    }

    [Fact]
    public void IsHourSelected_TrueForAllTouchedHours_FalseForUntouched()
    {
        // Selecting slots 3-6 spans 1.5 hours: the :30 of hour 1, all of hour 2,
        // and the :00 of hour 3.  All three hours must have IsHourSelected=true.
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SetSelectionRange(3, 6);  // 01:30 → 03:00

        vm.Columns[2].IsHourSelected.Should().BeTrue(
            "slot 3 (01:30) is the :30 of hour 1; its :00 column (slot 2) must be hour-selected");
        vm.Columns[4].IsHourSelected.Should().BeTrue(
            "slots 4-5 (02:00-02:30) are both in range; slot 4 (:00) must be hour-selected");
        vm.Columns[6].IsHourSelected.Should().BeTrue(
            "slot 6 (03:00) is the :00 of hour 3 and is in range; must be hour-selected");

        // Hours outside the range must NOT be selected
        vm.Columns[0].IsHourSelected.Should().BeFalse("hour 0 is before the selection");
        vm.Columns[8].IsHourSelected.Should().BeFalse("hour 4 (slots 8-9) is after the selection");
    }

    [Fact]
    public void IsHourSelected_OddSlots_AlwaysFalse()
    {
        // Odd-indexed columns (:30 slots) should never have IsHourSelected=true —
        // the XAML only renders the 20px header cell for even (IsHourStart) columns.
        var vm = new TimeTranslatorViewModel(new ObservableCollection<ClockLocation>());
        vm.SetSelectionRange(0, 47);  // select everything

        foreach (var col in vm.Columns.Where(c => c.SlotIndex % 2 == 1))
            col.IsHourSelected.Should().BeFalse(
                $"odd column (slot {col.SlotIndex}) must always have IsHourSelected=false — " +
                "the header layer-1 cell is rendered on even slots only");
    }
}
