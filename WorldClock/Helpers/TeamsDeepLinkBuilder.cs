using System.Text;
using System.Web;

namespace WorldClock.Helpers;

/// <summary>
/// Builds Teams deep-link URIs that open the "New Meeting" dialog pre-filled
/// with a subject, start/end times, and per-city time breakdown.
///
/// Two URIs are returned:
///   • <c>msteams://</c>   — opens the Teams desktop app directly.
///   • <c>https://</c>     — browser fallback if Teams is not installed.
///
/// Neither URI creates the meeting silently; the user still clicks Send in Teams.
/// </summary>
public static class TeamsDeepLinkBuilder
{
    private const string TeamsBase   = "msteams://teams.microsoft.com/l/meeting/new";
    private const string BrowserBase = "https://teams.microsoft.com/l/meeting/new";

    /// <summary>
    /// Builds both URIs.
    /// </summary>
    /// <param name="date">The calendar date the visualizer is showing.</param>
    /// <param name="startSlot">Selection start (0 = 00:00, 47 = 23:30).</param>
    /// <param name="endSlot">Selection end (inclusive).</param>
    /// <param name="cityTimes">
    ///   Sequence of (cityName, startTimeStr, endTimeStr) from the translated rows.
    ///   May be empty.
    /// </param>
    /// <returns>(teamsUri, browserFallbackUri)</returns>
    public static (string TeamsUri, string BrowserUri) Build(
        DateTime date,
        int startSlot,
        int endSlot,
        IEnumerable<(string City, string StartTime, string EndTime)> cityTimes)
    {
        var start   = SlotToDateTime(date, startSlot);
        var end     = SlotToDateTime(date, endSlot).AddMinutes(30);   // exclusive upper bound

        var subject = "World Sync";
        var content = BuildContent(start, end, cityTimes);

        var query = BuildQueryString(subject, start, end, content);

        return (TeamsBase + "?" + query, BrowserBase + "?" + query);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static DateTime SlotToDateTime(DateTime date, int slot)
    {
        var hour   = (slot / 2) % 24;
        var minute = (slot % 2) * 30;
        return new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Local);
    }

    private static string BuildContent(
        DateTime start,
        DateTime end,
        IEnumerable<(string City, string StartTime, string EndTime)> cityTimes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Meeting window: {start:HH:mm} – {end:HH:mm}");
        sb.AppendLine();

        var rows = cityTimes.ToList();
        if (rows.Count > 0)
        {
            sb.AppendLine("Local times:");
            foreach (var (city, s, e) in rows)
                sb.AppendLine($"  {city}: {s} – {e}");
        }

        sb.AppendLine();
        sb.AppendLine("Created with WorldClock");
        return sb.ToString().TrimEnd();
    }

    private static string BuildQueryString(
        string subject, DateTime start, DateTime end, string content)
    {
        // Format: yyyy-MM-ddTHH:mm:ss (Teams accepts local ISO-8601 without Z)
        static string Iso(DateTime dt) => dt.ToString("yyyy-MM-ddTHH:mm:ss");

        return
            "subject=" + Uri.EscapeDataString(subject)   +
            "&startTime=" + Uri.EscapeDataString(Iso(start)) +
            "&endTime="   + Uri.EscapeDataString(Iso(end))   +
            "&content="   + Uri.EscapeDataString(content);
    }
}
