Feature: Selection Area Alignment in the Time Visualizer Grid

  Background:
    Given the World Clock application is running
    And the home location is set to "Bogota" (UTC-05:00)
    And the time visualizer grid is visible with at least one data row

  # ── Bug: click area, colored box, and text label are not co-located ─────────

  Scenario: Clicking on a visible time label selects the correct hour slot
    Given the Bogota data row shows the label "12am" at a certain horizontal position X
    When the user clicks on that "12am" label
    Then the debug status bar should report "sel=0-0"
    And the debug status bar should report "lastClick=(160.0px → slot=0)"
    And "IHS[0]=True" should appear in the DataRow debug line
    And "col[0].IHS=True" should appear in the ColHdr debug line

  Scenario: The cyan selection box must cover the same horizontal span as the time label
    Given the user clicks on the "12am" label in the Bogota data row
    When the selection is applied
    Then the cyan highlight box (Layer 1b) for slot 0 must be visible
    And the left edge of the cyan box must coincide with the left edge of the "12am" text
    And no horizontal gap or overlap greater than 1px exists between the box and the label

  Scenario: The column header highlight aligns with the data row highlight and text
    Given the user clicks on the "5am" label in the Bogota data row at slot 10
    When the selection is applied
    Then the column header highlight at slot 10 must be at the same horizontal position
    And the data row cyan box at slot 10 must be at the same horizontal position
    And the "5am" text label must be visible inside or immediately above that box
    And the debug status bar must report "sel=10-10" or "sel=11-11"
    And "IHS[10]=True" must appear in both the DataRow and ColHdr debug lines

  Scenario: The reported click position matches the visual position of the label
    Given the time visualizer is displayed with no active horizontal scroll
    And the row header (left panel) is exactly 160 px wide
    When the user clicks at the "12am" text in any data row
    Then the debug status bar "Click" line must show a pos.X value between 160 and 180
    And the calculated slot must be 0

  Scenario: Clicking exactly on the row header boundary does not trigger a selection
    Given the row header is 160 px wide
    When the user clicks at pos.X = 159 (inside the row header)
    Then no selection change should occur
    And the debug status bar should retain the previous "sel=" value

  # ── Root-cause evidence captured during investigation ────────────────────────

  Scenario: DiagStatus exposes misalignment — sel=11 when clicking on 12am
    Given the user is running the current build
    And the needle is at approximately 486 px (≈ 4:16 pm Colombia time)
    When the user clicks on the "12am" text visible in the Bogota row
    Then the debug status bar shows "sel=11-11 | 5:30am"
    And the debug status bar shows "IHS[0]=False IHS[10]=False"
    And the "Click" line shows pos.X ≈ 270 instead of 160
    # This proves Layer 2 text renders at x = 270 while GetSlotFromPosition
    # calculates the slot using RowHeaderWidth=160, creating a 110 px / 11-slot offset.

  # ── Acceptance criteria for the fix ─────────────────────────────────────────

  Scenario: After the fix — all four alignment axes must agree for every hour slot
    Given the time visualizer grid is rendered with srcZone = "SA Pacific Standard Time"
    When the user clicks on any visible hour label in any data row at visual position X
    Then the click area (GetSlotFromPosition result) resolves to slot N = (X - 160) / 10
    And the colored box (Layer 1b, IsHourSelected) is rendered at visual x = 160 + N * 10
    And the time text label (Layer 2) is rendered at visual x = 160 + N * 10
    And the column header highlight is rendered at visual x = 160 + N * 10
    And the row header width remains at least 160 px to keep all city info readable
