Feature: Changes in Layout for Time Visualizer

    Scenario:
        1. Remove the old horizontal bottom cards with a Vertical Clocks Panel in the left side of the timeline (old bottom cards should be removed now).
            timeline and vertical clocks panel should be vertically scrollable
            This vertical panel Acts like a persistent timezone list.
            Keeps the clocks in a font a little bigger for all text than previous timezone with little fonts, on the bottom the intent is to free horizontal space
        2. Removal of Bottom Sections
            The following are removed entirely:
            Bottom timezone-clocks on time visualizer cards row
            Bottom “Add a city” input is moved to replaced the FROM search
            This reduces duplication and clutter.
        3. Expanded Timeline Area
            The main grid becomes wider and more dominant.
            More room for visual comparison across timezones.
        4. Where does the FROM search move?
            The "Add a city…" (FROM search) moves from the bottom area into the top of the Time Visualizer section.
            It becomes a prominent horizontal input (listbox-style) just under the "Time Visualizer" header.
            This replaces the older, less visible bottom textbox.
        5. Is the timeline grid gone?
            No, the timeline grid is NOT gone.
            It is still present but:
            It now occupies the central, wider area of the screen.
            It becomes the primary interaction surface.
            It appears visually emphasized with more space due to removal of bottom elements.
        6. Is the date navigation repositioned?
            Yes.
            The date navigation (e.g., “Tue, Apr 28”, “Today”) is:
            Positioned above the timeline grid, near the search bar.
            Integrated into the top control strip, instead of being more loosely placed before.
        7. Are search results shown differently?
            Yes, significantly:
            Results now behave like a dropdown/listbox tied to the top search field.
            This replaces the older inline textbox + bottom interaction pattern.
            The UX is more consistent with modern autocomplete:
            Type → see results → select → instantly added to timeline.
        