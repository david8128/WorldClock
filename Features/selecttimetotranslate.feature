Feature: Create a time picker to view exact time in different timezones and places

    Scenario:
        Given a time + date picker after choosing the date and time to address what time in local or in UTC it translates
        And use the wtb.png layout for the Time Visualizer
        Then Test this feature for both daylight saving time mode or normal mode.
