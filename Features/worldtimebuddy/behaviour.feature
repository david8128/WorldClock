Feature: Drag-to-Adjust Time Selection

    Scenario:
        Given the user has selected a time range
        When the user drags the selection up or down
        Then the selected time window should shift accordingly
        And all timezones should update in sync
        And the duration of the selection should remain constant unless resized