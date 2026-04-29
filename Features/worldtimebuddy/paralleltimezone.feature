Feature: Parallel Timezone Comparison

    Scenario:
        Given multiple timezones are added to the main view
        And each timezone is displayed as a vertical column
        When the user scrolls horizontally or vertically
        Then all timezone columns should remain synchronized in time alignment
        And each row should represent the same exact moment across all zones
        And visual separators should distinguish working hours vs night hours