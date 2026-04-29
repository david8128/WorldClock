namespace WorldClock.Models;

/// <summary>Time-of-day band used to colour-code cells in the Time Translator grid.</summary>
public enum TimeBand
{
    Night,       // 22:00 – 05:59  deep dark blue
    Morning,     // 06:00 – 08:59  soft blue
    WorkHours,   // 09:00 – 17:59  muted green
    Evening,     // 18:00 – 21:59  warm amber
}
