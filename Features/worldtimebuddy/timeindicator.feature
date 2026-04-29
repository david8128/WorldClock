Feature: Real-Time Time Indicator

    Scenario:
        Given the current day is selected
        When the current time is within the visible range
        Then a horizontal indicator line should show the exact current time
        And it should move automatically as time progresses
        And it should remain aligned across all timezone columns