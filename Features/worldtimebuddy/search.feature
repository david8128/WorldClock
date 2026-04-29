Feature: Timezone Search and Selection

    Scenario:
        Given the user is on the main interface with an empty or partially filled timezone list
        And the search input is focused
        When the user types a city, country, or timezone keyword
        Then matching locations should appear instantly in a dropdown
        And selecting a result should add a new column to the comparison view
        And the newly added timezone should align with the existing timeline
        