Feature: Main Clock and Global Header
  Display a high-level global time summary

  Scenario: Verify header and primary digital clock
    Given the application is launched
    Then I should see a central digital clock displaying time in "HH:mm:ss"
    And the clock should have a cyan glow effect
    And below the main clock, the date should be displayed

Feature: Search Location Relocation
  Relocate City Search Bar

  Scenario: Search bar placement
    Given the Time Visualizer is active
    Then the "Add a city to the timeline..." search bar should be positioned directly above the timeline control bar
    And it should be removed from any top-right or sidebar-only positions

Feature: Timeline Control Consolidation
  Consolidate Timeline Controls

  Scenario: Verify date picker and teams button relocation
    Given the timeline is visible
    Then I should see a control bar containing the Date Navigator
    And the "Teams" button should be placed immediately to the right of the date navigator
    And this entire control row should be positioned below the search bar

Feature: Layout Alignment (Parallel Selection)
  Align Selection Controls with Text

  Scenario: Fix horizontal alignment of timeline labels
    Given the timeline controls are displayed
    Then the "New York" location text should be on the exact same horizontal baseline as the Date Navigator
    And there should be no vertical offset between the search bar and the selection controls

Feature: Time Visualizer General Behavior
  Synchronized Time Visualization Grid

  Scenario: Display time bars and vertical synchronization
    Given multiple cities are added to the list
    Then each city should show a horizontal 24-hour bar
    And a vertical cyan line should indicate the "Current Time" across all city bars simultaneously
    And the vertical line must span the entire height of the timeline grid