Feature: Theme and Visual Customization (Fonts/Colors)

    Scenario:
        Given the default calendar view is displayed
        When the system applies visual styling
        Then different time ranges (morning, work hours, evening, night) should have distinct background colors
        And text should remain readable with sufficient contrast
        And selected or highlighted time blocks should use accent colors
        And fonts should be clean, consistent, and optimized for quick scanning