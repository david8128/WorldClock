using System.IO;
using System.Collections.ObjectModel;

namespace WorldClock.Data;

/// <summary>
/// Unified city search service used by both the Time Translator and Settings window.
/// Searches the curated <see cref="CityDatabase.All"/> collection at startup,
/// and also loads the generated GeoNames CSV (<c>world_cities_timezones.csv</c>)
/// when available, merging the two sources.
///
/// Run <c>WorldClock/citiesdb/generate_world_cities_timezones.py</c> to produce the CSV.
/// </summary>
public static class WorldCitySearchService
{
    private static IReadOnlyList<CityEntry>? _all;
    private static readonly object _loadLock = new();

    /// <summary>All searchable cities (curated + CSV if available). Thread-safe lazy load.</summary>
    public static IReadOnlyList<CityEntry> All
    {
        get
        {
            if (_all is not null) return _all;
            lock (_loadLock)
            {
                _all ??= BuildDatabase();
                return _all;
            }
        }
    }

    // ── Public search API ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns up to <paramref name="maxResults"/> cities matching <paramref name="query"/>,
    /// ordered by relevance. Supports:
    /// <list type="bullet">
    ///   <item>IATA / airport codes — "NYC", "BOG", "LAX"</item>
    ///   <item>City name prefix / contains — "Lon", "New"</item>
    ///   <item>Country name — "Germany", "Colombia"</item>
    ///   <item>City initials — "NY" → New York, "NOLA" → New Orleans</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<CityEntry> Search(string? query, int maxResults = 14)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var qUpper = query.Trim().ToUpperInvariant();

        return All
            .Select(c => (city: c, score: Score(c, qUpper)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.city.City)
            .Take(maxResults)
            .Select(x => x.city)
            .ToList();
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    private static int Score(CityEntry c, string qUpper)
    {
        int score = 0;
        var city    = c.City.ToUpperInvariant();
        var country = c.Country.ToUpperInvariant();
        var tzId    = c.TimeZoneId.ToUpperInvariant();

        // Exact code match (highest): BOG→Bogotá, NYC→New York, LAX→Los Angeles
        if (c.Codes is { Length: > 0 } codes &&
            codes.Any(code => code.Equals(qUpper, StringComparison.Ordinal)))
            score += 200;

        // Exact initials: "NY"→New York, "SFO"→San Francisco
        if (Initials(c.City)    == qUpper) score += 100;
        if (Initials(c.Country) == qUpper) score += 80;

        // Code starts-with (partial entry): "JF"→JFK
        if (c.Codes is { Length: > 0 } partialCodes &&
            partialCodes.Any(code => code.StartsWith(qUpper, StringComparison.Ordinal)))
            score += 60;

        // Starts-with name
        if (city.StartsWith(qUpper,    StringComparison.Ordinal)) score += 70;
        if (country.StartsWith(qUpper, StringComparison.Ordinal)) score += 50;

        // Contains name
        if (city.Contains(qUpper,    StringComparison.Ordinal)) score += 30;
        if (country.Contains(qUpper, StringComparison.Ordinal)) score += 20;
        if (tzId.Contains(qUpper,    StringComparison.Ordinal)) score += 10;

        return score;
    }

    private static string Initials(string s) =>
        new(s.Split(' ', '-', '/', '_')
             .Where(w => w.Length > 0)
             .Select(w => char.ToUpper(w[0]))
             .ToArray());

    // ── Database building ─────────────────────────────────────────────────────

    private static IReadOnlyList<CityEntry> BuildDatabase()
    {
        var csvPath = FindCsvPath();
        if (csvPath is null || !CsvHasData(csvPath))
            return CityDatabase.All;   // CSV not ready → curated list only

        // Build country-code → (name, flag) from curated data
        var countryLookup = BuildCountryLookup();

        var csvEntries = LoadCsv(csvPath, countryLookup);

        // Merge: curated first, then CSV cities not already present
        var existingKeys = new HashSet<string>(
            CityDatabase.All.Select(c => NKey(c.Country, c.City)),
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<CityEntry>(CityDatabase.All.Count + csvEntries.Count);
        merged.AddRange(CityDatabase.All);
        foreach (var e in csvEntries)
            if (!existingKeys.Contains(NKey(e.Country, e.City)))
                merged.Add(e);

        return merged;
    }

    private static string NKey(string country, string city) => $"{country}|{city}";

    // ── CSV loading ───────────────────────────────────────────────────────────

    private static string? FindCsvPath()
    {
        // 1. Next to the running executable (production / publish layout)
        var next = Path.Combine(AppContext.BaseDirectory, "world_cities_timezones.csv");
        if (File.Exists(next)) return next;

        // 2. Development layout: <repo>/citiesdb/world_cities_timezones.csv
        //    AppContext.BaseDirectory is e.g. <repo>/WorldClock/bin/Release/net8.0-windows/
        var dev = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "citiesdb", "world_cities_timezones.csv"));
        if (File.Exists(dev)) return dev;

        return null;
    }

    private static bool CsvHasData(string path)
    {
        using var r = File.OpenText(path);
        r.ReadLine();            // skip header
        return r.ReadLine() is not null;   // at least one data row?
    }

    private static List<CityEntry> LoadCsv(string path,
        Dictionary<string, (string Name, string Flag)> countryLookup)
    {
        var result = new List<CityEntry>(4096);
        using var reader = new StreamReader(path);
        reader.ReadLine();   // skip header: country_code,city_name,ascii_name,alt_names,lat,lon,iana_timezone,...

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var p = SplitCsv(line);
            if (p.Length < 7) continue;

            var code   = p[0];   // country_code
            var city   = p[1];   // city_name
            var iana   = p[6];   // iana_timezone

            if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(iana)) continue;

            // IANA → Windows TZ (requires .NET 6+, available on .NET 8)
            if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(iana, out var winId) ||
                string.IsNullOrEmpty(winId))
                continue;

            var (countryName, flag) = countryLookup.TryGetValue(code, out var ci)
                ? ci
                : (code, CodeToFlag(code));

            // alt_names field contains pipe-separated names; short alphanumeric ones become codes
            var altNames = p.Length > 3 ? p[3] : "";
            var codes = altNames.Split('|')
                .Where(n => n.Length is >= 2 and <= 5 && n.All(char.IsLetterOrDigit))
                .Select(n => n.ToUpperInvariant())
                .Distinct()
                .ToArray();

            result.Add(new CityEntry(
                Country:     countryName,
                CountryFlag: flag,
                City:        city,
                TimeZoneId:  winId,
                Codes:       codes.Length > 0 ? codes : null));
        }

        return result;
    }

    // Minimal CSV splitter (handles RFC-4180 quoted fields)
    private static string[] SplitCsv(string line)
    {
        var fields  = new List<string>(10);
        var current = new System.Text.StringBuilder();
        bool inQ    = false;

        foreach (char c in line)
        {
            if (c == '"') { inQ = !inQ; continue; }
            if (c == ',' && !inQ) { fields.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        fields.Add(current.ToString());
        return [.. fields];
    }

    // ── Country helpers ───────────────────────────────────────────────────────

    private static Dictionary<string, (string Name, string Flag)> BuildCountryLookup()
    {
        var dict = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name) in CountryCodes)
        {
            var flag = CodeToFlag(code);
            // Find matching flag from curated DB if available
            var curated = CityDatabase.All.FirstOrDefault(c =>
                c.Country.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (curated is not null) flag = curated.CountryFlag;
            dict[code] = (name, flag);
        }
        return dict;
    }

    /// <summary>Derives a flag emoji from a 2-letter ISO 3166 country code.</summary>
    private static string CodeToFlag(string code) =>
        code.Length == 2
            ? string.Concat(code.ToUpperInvariant().Select(c => char.ConvertFromUtf32(0x1F1E6 + (c - 'A'))))
            : "";

    // ISO 3166 alpha-2 → name for the most common timezones
    private static readonly IReadOnlyList<(string Code, string Name)> CountryCodes =
    [
        ("AE", "United Arab Emirates"), ("AR", "Argentina"),  ("AT", "Austria"),
        ("AU", "Australia"),            ("BE", "Belgium"),     ("BR", "Brazil"),
        ("CA", "Canada"),               ("CH", "Switzerland"), ("CL", "Chile"),
        ("CN", "China"),                ("CO", "Colombia"),    ("CZ", "Czech Republic"),
        ("DE", "Germany"),              ("DK", "Denmark"),     ("EG", "Egypt"),
        ("ES", "Spain"),                ("FI", "Finland"),     ("FR", "France"),
        ("GB", "United Kingdom"),       ("GR", "Greece"),      ("HK", "Hong Kong"),
        ("HU", "Hungary"),              ("ID", "Indonesia"),   ("IE", "Ireland"),
        ("IL", "Israel"),               ("IN", "India"),       ("JP", "Japan"),
        ("KE", "Kenya"),                ("KR", "South Korea"), ("MX", "Mexico"),
        ("MY", "Malaysia"),             ("NG", "Nigeria"),     ("NL", "Netherlands"),
        ("NO", "Norway"),               ("NZ", "New Zealand"), ("PH", "Philippines"),
        ("PK", "Pakistan"),             ("PL", "Poland"),      ("PE", "Peru"),
        ("PT", "Portugal"),             ("RO", "Romania"),     ("RU", "Russia"),
        ("SA", "Saudi Arabia"),         ("SE", "Sweden"),      ("SG", "Singapore"),
        ("TH", "Thailand"),             ("TR", "Turkey"),      ("TW", "Taiwan"),
        ("UA", "Ukraine"),              ("US", "United States"),("VE", "Venezuela"),
        ("VN", "Vietnam"),              ("ZA", "South Africa"),
    ];
}
