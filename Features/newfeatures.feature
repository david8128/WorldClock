Feature: New features and fixes added during the current development session

  # ── DOCUMENTATION ──────────────────────────────────────────────────────────

  Scenario: Project documentation and getting-started guide
    Given the project lacked user-facing documentation
    When the docs/ folder is created
    Then a README.md is generated with a getting-started guide
    And it includes WorldClock.png as the main application screenshot
    And it references selectionairports.png to explain that the city database originates from airport data
    And individual markdown dump files in docs/ cover each major feature area

  # ── TIME VISUALIZER ────────────────────────────────────────────────────────

  Scenario: Real-time "now" needle across the 24-hour visualizer
    Given the Time Visualizer is open and the current day is selected
    When time passes
    Then a single vertical line marks the exact current time across all city rows
    And the needle position is computed as: 160px (row header) + (hour * 2 + minute / 30.0) * 14px
    And each clock card shows the current time and current date as a single-line label
    And the needle updates automatically as time progresses

  Scenario: Teams meeting deep link from a selected time window
    [Solved] Given the user has selected a time window in the Time Visualizer
    When the user clicks the "Create Teams Meeting" button
    Then a Microsoft Teams deep link is generated using the selected start and end times
    And the link is opened in the default browser
    And the button is visible directly in the Time Visualizer toolbar

  # ── EDIT MODE ──────────────────────────────────────────────────────────────

  Scenario: Edit and delete mode toggle buttons
    [Solved] Given the main window toolbar
    When the user clicks the Edit Mode button
    Then all city cards enter edit mode simultaneously
    And when the user clicks the Delete Mode button
    Then all city cards show delete controls
    And both modes are mutually exclusive

  Scenario: Per-card accent colour picker in edit mode
    Given a city card is in edit mode
    When the user clicks the colour swatch on the card
    Then a colour picker appears with the 12-colour accent palette
    And selecting a colour immediately updates the card's accent colour
    And the chosen colour is persisted to settings so it survives app restart
    And the palette colours are: #00E5FF, #FFD600, #00E676, #FF9100, #CE93D8, #FF4081, #69F0AE, #F48FB1, #80DEEA, #FFCC02, #B39DDB, #4DD0E1

  # ── CITY MANAGEMENT & SEARCH ───────────────────────────────────────────────

  Scenario: Cities sharing the same Windows timezone ID can all be added
    [Solved] Given that Rome, Berlin, Vienna, Amsterdam and Paris all map to "W. Europe Standard Time"
    When the user searches for and adds Rome
    Then Rome is added successfully even when other CET cities are already present
    And the deduplication guard is based on city name only, not on timezone ID
    And any two cities with different names but the same timezone ID can coexist in the list

  Scenario: City names with "City, State" format are normalised on add
    [Solved] Given that the airport CSV may produce names like "New York, New York" or "Kansas City, Missouri"
    When such a name is added via search or manually
    Then the city is stored using only the city portion before the comma (e.g. "New York")
    And attempting to add "New York, New York" when "New York" already exists is rejected as a duplicate
    And the normalisation is applied both at CSV load time (WorldCitySearchService) and at AddLocation time (MainViewModel)
