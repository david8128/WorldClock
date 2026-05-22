Feature: Main Calendar Grid Interaction

    Scenario:
        Given the timeline grid is visible with hours displayed vertically
        When the user clicks or drags across a time range
        Then a selection highlight should appear across all timezones
        And the selected range should represent equivalent times in each column
        And the UI should clearly show start and end times for each timezone
        And the interaction should feel smooth with no lag or misalignment