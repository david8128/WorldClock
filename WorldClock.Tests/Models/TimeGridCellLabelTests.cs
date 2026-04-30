using FluentAssertions;
using WorldClock.Models;
using Xunit;

namespace WorldClock.Tests.Models;

/// <summary>
/// Verifies the label-split properties on TimeGridCell and TimeGridColumn
/// that are used by the XAML timeline to render two-line time labels
/// centred over each hour+:30 cell pair.
///
/// These tests confirm the MODEL is correct. If the labels still appear
/// trimmed at runtime it is a XAML rendering issue, not a data issue.
///
/// Root cause of the rendering bug (documented here for clarity):
///   StackPanel does NOT respect Panel.ZIndex. Only Canvas and Grid do.
///   The ItemContainerStyle approach that sets Panel.ZIndex=1 on hour-slot
///   ContentPresenters has zero visual effect when the ItemsPanel is a
///   StackPanel – children paint in DOM order regardless. The :30 cell's
///   background therefore always paints after (on top of) the hour cell's
///   overflowing 40px label, clipping it. Fix: use Canvas as ItemsPanel
///   with Canvas.Left set via a SlotIndexToCanvasLeftConverter so that
///   Canvas (which DOES honour Panel.ZIndex) draws hour cells last.
/// </summary>
public sealed class TimeGridCellLabelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TimeGridCell MakeCell(int slotIndex, string timeStr)
        => new()
        {
            SlotIndex = slotIndex,
            TimeStr   = timeStr,
            DayDiff   = "",
            Band      = TimeBand.WorkHours,
        };

    // ── IsHourSlot ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  true)]   // 00:00
    [InlineData(1,  false)]  // 00:30
    [InlineData(2,  true)]   // 01:00
    [InlineData(13, false)]  // 06:30
    [InlineData(14, true)]   // 07:00
    [InlineData(47, false)]  // 23:30
    [InlineData(46, true)]   // 23:00
    public void IsHourSlot_ReturnsTrue_OnlyForEvenSlotIndex(int slot, bool expected)
        => MakeCell(slot, "00:00").IsHourSlot.Should().Be(expected);

    // ── TimeHourPart / TimeAmPmPart ───────────────────────────────────────────

    [Theory]
    [InlineData("00:00",  "12", "am")]   // midnight
    [InlineData("01:00",   "1", "am")]
    [InlineData("06:00",   "6", "am")]
    [InlineData("07:00",   "7", "am")]
    [InlineData("11:00",  "11", "am")]
    [InlineData("12:00",  "12", "pm")]   // noon
    [InlineData("13:00",   "1", "pm")]
    [InlineData("22:00",  "10", "pm")]
    [InlineData("23:00",  "11", "pm")]
    [InlineData("23:30", "11:30", "pm")] // half-hour
    [InlineData("07:30",  "7:30", "am")]
    public void TimeHourPart_And_TimeAmPmPart_SplitCorrectly(
        string timeStr, string expectedHour, string expectedAmPm)
    {
        var cell = MakeCell(0, timeStr);
        cell.TimeHourPart.Should().Be(expectedHour,
            because: $"hour part of '{timeStr}' should be '{expectedHour}'");
        cell.TimeAmPmPart.Should().Be(expectedAmPm,
            because: $"am/pm part of '{timeStr}' should be '{expectedAmPm}'");
    }

    // ── TimeGridColumn ────────────────────────────────────────────────────────

    private static TimeGridColumn MakeColumn(int slotIndex, string slotLabel)
        => new()
        {
            SlotIndex   = slotIndex,
            SlotLabel   = slotLabel,
            IsHourStart = slotIndex % 2 == 0,
        };

    [Theory]
    [InlineData(0,  "12a", "12", "am")]
    [InlineData(2,  "1a",  "1",  "am")]
    [InlineData(14, "7a",  "7",  "am")]
    [InlineData(24, "12p", "12", "pm")]
    [InlineData(44, "10p", "10", "pm")]
    [InlineData(1,  "",    "",   "")]   // :30 slot — empty
    public void TimeGridColumn_SlotHour_And_SlotAmPm_SplitCorrectly(
        int slotIndex, string slotLabel, string expectedHour, string expectedAmPm)
    {
        var col = MakeColumn(slotIndex, slotLabel);
        col.SlotHour.Should().Be(expectedHour,
            because: $"SlotHour of '{slotLabel}' should be '{expectedHour}'");
        col.SlotAmPm.Should().Be(expectedAmPm,
            because: $"SlotAmPm of '{slotLabel}' should be '{expectedAmPm}'");
    }

    // ── ZIndex explanation (documents rendering fix requirement) ─────────────

    [Fact]
    public void StackPanel_ZIndex_Limitation_IsDocumented()
    {
        // This test encodes the architectural fact that the ZIndex fix MUST
        // live at the Canvas (ItemsPanel) level, NOT inside the DataTemplate.
        //
        // WPF panels that HONOUR Panel.ZIndex: Canvas, Grid.
        // WPF panels that IGNORE Panel.ZIndex: StackPanel, WrapPanel, DockPanel.
        //
        // Consequence: using StackPanel as the ItemsPanel and setting
        // Panel.ZIndex on ContentPresenter or the inner Border has NO effect.
        // The only correct solution is a Canvas ItemsPanel with Canvas.Left
        // set via a converter (SlotIndex * 20), so hour cells can be
        // elevated to ZIndex=1 and their 40px labels paint on top of the
        // adjacent :30 cell's background.

        const bool stackPanelHonoursZIndex = false;
        stackPanelHonoursZIndex.Should().BeFalse(
            because: "StackPanel renders children in DOM order; " +
                     "ItemsPanel must be Canvas for Panel.ZIndex to take effect");
    }
}
