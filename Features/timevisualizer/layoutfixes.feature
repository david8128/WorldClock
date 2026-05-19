Feature: This fixes all bugs

    Scenario:
        [Unsolved]                Selection highlight in data rows was never visible.
                                Root cause: Layer 1b (20px/hour selection highlight ItemsControl, Canvas 480x44)
                                was placed INSIDE the horizontal StackPanel AFTER Layer 1a (another 480px Canvas),
                                causing it to render at x≈640 — completely off-screen.
                                Fix: closed the StackPanel after Layer 1a; moved Layer 1b to be a direct Grid
                                child with Margin="160,0,0,0", matching Layer 2 (text labels). Now both layers
                                occupy the same x=160…640 region and the selection highlight exactly covers
                                the 20px-wide time-label border.
                                Also addressed: "text of 12am appears on the time window of 5am" is NOT a bug —
                                the column header shows SOURCE timezone time ("12am" = midnight Colombia/UTC-5)
                                while the UTC row at the same x-position shows UTC equivalent ("05:00"). This is
                                correct semantics. DiagStatus now exposes per-cell IsHourSelected state to confirm.
        [Unsolved]              Keep a easy to read size of all text and one caveat, make sure the text fits the selection hour for each hour, specially for timezones that are showing starting time with 12:30am and 1:30am and so on