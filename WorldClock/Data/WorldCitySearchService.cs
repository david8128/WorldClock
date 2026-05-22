using System.IO;
using System.Collections.ObjectModel;

namespace WorldClock.Data;

/// <summary>
/// Unified city search service used by both the Time Visualizer and Settings window.
/// Searches the curated <see cref="CityDatabase.All"/> collection at startup,
/// and also loads the generated GeoNames CSV (<c>world_cities_timezones.csv</c>)
/// when available, merging the two sources.
///
/// CSV schema (header row, columns 0-based):
///   0  AirportName        — airport display name
///   1  IATA               — airport IATA code (e.g. LAX, BOG)
///   2  ICAO               — airport ICAO code
///   3  TimeZone           — IANA timezone id (e.g. America/Bogota)
///   4  City_Name          — city display name
///   5  City_IATA          — city-level IATA code (used as the primary city code)
///   6  UTC_Offset_Hours   — UTC offset in hours (float)
///   7  UTC_Offset_Seconds — UTC offset in seconds (float)
///   8  Country_CodeA2     — ISO 3166-1 alpha-2 country code (e.g. US, CO)
///   9  Country_CodeA3     — ISO 3166-1 alpha-3 country code
///  10  Country_Name       — full country name
///  11  GeoPointLat        — latitude
///  12  GeoPointLong       — longitude
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
    ///   <item>UTC offset — "+5:30" → Bangalore/Mumbai, "-3" → Buenos Aires, "UTC+1" → London</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<CityEntry> Search(string? query, int maxResults = 14)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var qUpper       = query.Trim().ToUpperInvariant();
        var parsedOffset = TryParseUtcOffset(query.Trim());

        return All
            .Select((c, rank) => (city: c, score: Score(c, qUpper, parsedOffset), rank))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.rank)        // curated cities (low index) win ties over CSV-only cities
            .ThenBy(x => x.city.City)   // alphabetical within the same rank tier
            .Take(maxResults)
            .Select(x => x.city)
            .ToList();
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    private static int Score(CityEntry c, string qUpper, TimeSpan? parsedOffset)
    {
        int score = 0;
        var city    = c.City.ToUpperInvariant();
        var country = c.Country.ToUpperInvariant();
        var tzId    = c.TimeZoneId.ToUpperInvariant();

        // UTC offset match: "+5:30"→India, "-3"→Argentina, "UTC+1"→UK winter
        if (parsedOffset.HasValue)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(c.TimeZoneId);
                if (tz.BaseUtcOffset == parsedOffset.Value) score += 150;
            }
            catch { /* unknown timezone id — skip */ }
        }

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

        // Code contains (covers queries that are a substring of a code)
        if (c.Codes is { Length: > 0 } containsCodes &&
            containsCodes.Any(code => code.Contains(qUpper, StringComparison.Ordinal)))
            score += 40;

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

    /// <summary>
    /// Parses a UTC offset query into a <see cref="TimeSpan"/>.
    /// Accepts: "+5:30", "-3", "+5.5", "UTC+5:30", "UTC-8", "+00:00", etc.
    /// Returns <see langword="null"/> when the string is not an offset expression.
    /// </summary>
    private static TimeSpan? TryParseUtcOffset(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return null;

        // Strip optional leading "UTC"
        var s = q.StartsWith("UTC", StringComparison.OrdinalIgnoreCase) ? q[3..] : q;

        int sign = 1;
        if      (s.StartsWith('+')) { sign =  1; s = s[1..]; }
        else if (s.StartsWith('-')) { sign = -1; s = s[1..]; }
        else return null;  // require explicit sign so plain city names don't accidentally parse

        if (string.IsNullOrEmpty(s)) return null;

        // "H:MM" or "HH:MM"
        if (s.Contains(':')
            && s.Split(':') is [var hPart, var mPart]
            && int.TryParse(hPart, out int hColon)
            && int.TryParse(mPart, out int mColon)
            && hColon is >= 0 and <= 14
            && mColon is >= 0 and < 60)
            return TimeSpan.FromMinutes(sign * (hColon * 60 + mColon));

        // "H.D" decimal (e.g. "+5.5" → +5h30m)
        if (s.Contains('.')
            && double.TryParse(s,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double dec)
            && dec is >= 0 and <= 14)
            return TimeSpan.FromHours(sign * dec);

        // Plain integer hours
        if (int.TryParse(s, out int h) && h is >= 0 and <= 14)
            return TimeSpan.FromHours(sign * h);

        return null;
    }

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

    /// <summary>
    /// Strips a trailing ", Region" or ", State" suffix from airport city names
    /// (e.g. "New York, New York" → "New York", "Kansas City, Missouri" → "Kansas City").
    /// </summary>
    private static string NormalizeCityName(string name)
    {
        var comma = name.IndexOf(',');
        return comma > 0 ? name[..comma].Trim() : name;
    }

    // ── CSV loading ───────────────────────────────────────────────────────────

    private static string? FindCsvPath()
    {
        // 1. Next to the running executable (production / publish layout)
        var next = Path.Combine(AppContext.BaseDirectory, "world_cities_timezones.csv");
        if (File.Exists(next)) return next;

        // 2. Development layout: <project>/citiesdb/world_cities_timezones.csv
        //    AppContext.BaseDirectory is e.g. <project>/WorldClock/bin/Release/net8.0-windows/
        //    Going up 3 levels reaches the WorldClock project folder; citiesdb lives there.
        var dev3 = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "citiesdb", "world_cities_timezones.csv"));
        if (File.Exists(dev3)) return dev3;

        // 3. Alternative dev layout: <repo>/citiesdb/world_cities_timezones.csv
        //    Going up 4 levels reaches the repository root.
        var dev4 = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "citiesdb", "world_cities_timezones.csv"));
        if (File.Exists(dev4)) return dev4;

        return null;
    }

    private static bool CsvHasData(string path)
    {
        using var r = File.OpenText(path);
        r.ReadLine();            // skip header row
        return r.ReadLine() is not null;   // at least one data row?
    }

    private static List<CityEntry> LoadCsv(string path,
        Dictionary<string, (string Name, string Flag)> countryLookup)
    {
        var result = new List<CityEntry>(4096);
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(path);
        var header = reader.ReadLine();
        if (header is null) return result;

        // Auto-detect schema from the header row.
        // Old schema:  country_code,city_name,ascii_name,alt_names,latitude,longitude,iana_timezone,...
        // New schema:  AirportName,IATA,ICAO,TimeZone,City_Name,City_IATA,UTC_Offset_Hours,...
        bool isNewSchema = header.StartsWith("AirportName", StringComparison.OrdinalIgnoreCase);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var p = SplitCsv(line);

            if (isNewSchema)
            {
                // New schema column indices:
                //  0=AirportName  1=IATA  2=ICAO  3=TimeZone  4=City_Name  5=City_IATA
                //  6=UTC_Offset_Hours  7=UTC_Offset_Seconds  8=Country_CodeA2
                //  9=Country_CodeA3  10=Country_Name  11=GeoPointLat  12=GeoPointLong
                if (p.Length < 11) continue;

                var iana        = p[3].Trim();
                var cityName    = NormalizeCityName(p[4].Trim());
                var cityCode    = p[5].Trim().ToUpperInvariant();
                var airportCode = p[1].Trim().ToUpperInvariant();
                var countryA2   = p[8].Trim();
                var countryRaw  = p[10].Trim();

                if (string.IsNullOrWhiteSpace(cityName) || string.IsNullOrWhiteSpace(iana)) continue;

                var key = $"{countryA2}|{cityName}";
                if (!seen.Add(key)) continue;

                if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(iana, out var winId) ||
                    string.IsNullOrEmpty(winId)) continue;

                var (cName, flag) = countryLookup.TryGetValue(countryA2, out var ci)
                    ? ci
                    : (!string.IsNullOrWhiteSpace(countryRaw)
                        ? (countryRaw, CodeToFlag(countryA2))
                        : (countryA2,  CodeToFlag(countryA2)));

                var codes = new List<string>();
                if (!string.IsNullOrWhiteSpace(cityCode))   codes.Add(cityCode);
                if (!string.IsNullOrWhiteSpace(airportCode) &&
                    !airportCode.Equals(cityCode, StringComparison.OrdinalIgnoreCase))
                    codes.Add(airportCode);

                result.Add(new CityEntry(Country: cName, CountryFlag: flag, City: cityName,
                    TimeZoneId: winId, Codes: codes.Count > 0 ? [.. codes] : null));
            }
            else
            {
                // Old schema column indices:
                //  0=country_code  1=city_name  2=ascii_name  3=alt_names(pipe-sep)
                //  4=latitude  5=longitude  6=iana_timezone  7=utc_offset_minutes  8=utc_offset_label
                if (p.Length < 7) continue;

                var countryCode = p[0].Trim();
                var cityName    = NormalizeCityName(p[1].Trim());
                var altNames    = p.Length > 3 ? p[3] : "";
                var iana        = p[6].Trim();

                if (string.IsNullOrWhiteSpace(cityName) || string.IsNullOrWhiteSpace(iana)) continue;

                var key = $"{countryCode}|{cityName}";
                if (!seen.Add(key)) continue;

                if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(iana, out var winId) ||
                    string.IsNullOrEmpty(winId)) continue;

                var (cName, flag) = countryLookup.TryGetValue(countryCode, out var ci)
                    ? ci
                    : (countryCode, CodeToFlag(countryCode));

                // alt_names is pipe-separated; keep short alphanumeric tokens as searchable codes
                var codes = altNames.Split('|')
                    .Select(n => n.Trim())
                    .Where(n => n.Length is >= 2 and <= 6 && n.All(char.IsLetterOrDigit))
                    .Select(n => n.ToUpperInvariant())
                    .Distinct()
                    .ToArray();

                result.Add(new CityEntry(Country: cName, CountryFlag: flag, City: cityName,
                    TimeZoneId: winId, Codes: codes.Length > 0 ? codes : null));
            }
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
