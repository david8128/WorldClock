Feature: Interactive Date Picker Navigation

    Scenario:
        Given the user is viewing the current day timeline
        When the user interacts with the date picker control
        Then they should be able to switch to past or future dates
        And the timeline should update instantly to reflect the selected date
        And all timezone columns should maintain correct offsets for that date
        And daylight saving differences should be handled automatically