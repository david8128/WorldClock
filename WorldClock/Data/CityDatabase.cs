namespace WorldClock.Data;

/// <summary>
/// A single entry in the city catalogue.
/// </summary>
public sealed record CityEntry(
    string Country,
    string CountryFlag,
    string City,
    string TimeZoneId,
    string DefaultTeamLabel = "Custom",
    /// <summary>
    /// Short codes for this city (IATA airport codes, UN/LOCODE, common abbreviations).
    /// Used for fast acronym search: typing "NYC", "BOG", "LAX" returns the matching city.
    /// Codes[0] is always the City_IATA from the CSV (the primary city-level code).
    /// </summary>
    string[]? Codes = null)
{
    /// <summary>
    /// All codes joined with " | ", or empty string if none.
    /// Bindable in XAML to show all IATA/airport codes for this city in search results.
    /// </summary>
    public string PrimaryCode => Codes is { Length: > 0 } ? string.Join(" | ", Codes) : string.Empty;

    /// <summary>
    /// Display text for the city search dropdown.
    /// Format: "City | Flag | Code1 | Code2 | …" (all separated with | signs).
    /// </summary>
    public string SearchLabel
    {
        get
        {
            var parts = new System.Collections.Generic.List<string> { City, CountryFlag };
            if (Codes is { Length: > 0 })
                parts.AddRange(Codes);
            return string.Join(" | ", parts);
        }
    }
}

/// <summary>
/// Catalogue of world cities with their country, flag, and Windows timezone ID.
/// Used to drive the cascading Country → City → Timezone selectors in Settings.
/// </summary>
public static class CityDatabase
{
    public static readonly IReadOnlyList<CityEntry> All = new List<CityEntry>
    {
        // ── Argentina ────────────────────────────────────────────────────────
        new("Argentina",    "🇦🇷", "Buenos Aires",     "Argentina Standard Time",     Codes: new[]{"BUE","EZE","AEP","BA"}),
        new("Argentina",    "🇦🇷", "Córdoba",          "Argentina Standard Time",     Codes: new[]{"COR"}),
        new("Argentina",    "🇦🇷", "Rosario",          "Argentina Standard Time",     Codes: new[]{"ROS"}),

        // ── Australia ────────────────────────────────────────────────────────
        new("Australia",    "🇦🇺", "Adelaide",         "Cen. Australia Standard Time",Codes: new[]{"ADL"}),
        new("Australia",    "🇦🇺", "Brisbane",         "E. Australia Standard Time",  Codes: new[]{"BNE"}),
        new("Australia",    "🇦🇺", "Darwin",           "AUS Central Standard Time",   Codes: new[]{"DRW"}),
        new("Australia",    "🇦🇺", "Melbourne",        "AUS Eastern Standard Time",   Codes: new[]{"MEL"}),
        new("Australia",    "🇦🇺", "Perth",            "W. Australia Standard Time",  Codes: new[]{"PER","PTA"}),
        new("Australia",    "🇦🇺", "Sydney",           "AUS Eastern Standard Time",   Codes: new[]{"SYD"}),

        // ── Austria ──────────────────────────────────────────────────────────
        new("Austria",      "🇦🇹", "Vienna",           "W. Europe Standard Time",     Codes: new[]{"VIE","VNA"}),

        // ── Belgium ──────────────────────────────────────────────────────────
        new("Belgium",      "🇧🇪", "Brussels",         "Romance Standard Time",       Codes: new[]{"BRU","BXL"}),

        // ── Brazil ───────────────────────────────────────────────────────────
        new("Brazil",       "🇧🇷", "Brasília",         "E. South America Standard Time",Codes: new[]{"BSB","BRS"}),
        new("Brazil",       "🇧🇷", "Manaus",           "SA Western Standard Time",    Codes: new[]{"MAO"}),
        new("Brazil",       "🇧🇷", "Rio de Janeiro",   "E. South America Standard Time",Codes: new[]{"RIO","GIG","SDU"}),
        new("Brazil",       "🇧🇷", "São Paulo",        "E. South America Standard Time",Codes: new[]{"SAO","GRU","CGH","SP"}),

        // ── Canada ───────────────────────────────────────────────────────────
        new("Canada",       "🇨🇦", "Calgary",          "Mountain Standard Time",      Codes: new[]{"YYC","CGY"}),
        new("Canada",       "🇨🇦", "Halifax",          "Atlantic Standard Time",      Codes: new[]{"YHZ","HAL"}),
        new("Canada",       "🇨🇦", "Montreal",         "Eastern Standard Time",       Codes: new[]{"YUL","MTL"}),
        new("Canada",       "🇨🇦", "Ottawa",           "Eastern Standard Time",       Codes: new[]{"YOW","OTT"}),
        new("Canada",       "🇨🇦", "Toronto",          "Eastern Standard Time",       Codes: new[]{"YYZ","TOR","YTO"}),
        new("Canada",       "🇨🇦", "Vancouver",        "Pacific Standard Time",       Codes: new[]{"YVR","VAN"}),
        new("Canada",       "🇨🇦", "Winnipeg",         "Central Standard Time",       Codes: new[]{"YWG","WPG"}),

        // ── Chile ────────────────────────────────────────────────────────────
        new("Chile",        "🇨🇱", "Santiago",         "Pacific SA Standard Time",    Codes: new[]{"SCL","STG"}),

        // ── China ────────────────────────────────────────────────────────────
        new("China",        "🇨🇳", "Beijing",          "China Standard Time",         Codes: new[]{"PEK","BJS","BJI"}),
        new("China",        "🇨🇳", "Chengdu",          "China Standard Time",         Codes: new[]{"CTU","CDU"}),
        new("China",        "🇨🇳", "Shanghai",         "China Standard Time",         Codes: new[]{"SHA","PVG","SHG"}),
        new("China",        "🇨🇳", "Shenzhen",         "China Standard Time",         Codes: new[]{"SZX","SZN"}),

        // ── Colombia ─────────────────────────────────────────────────────────
        new("Colombia",     "🇨🇴", "Bogotá",           "SA Pacific Standard Time",    Codes: new[]{"BOG"}),

        // ── Czech Republic ───────────────────────────────────────────────────
        new("Czech Republic","🇨🇿","Prague",           "Central Europe Standard Time",Codes: new[]{"PRG","PRA"}),

        // ── Denmark ──────────────────────────────────────────────────────────
        new("Denmark",      "🇩🇰", "Copenhagen",       "Romance Standard Time",       Codes: new[]{"CPH","CSN"}),

        // ── Egypt ────────────────────────────────────────────────────────────
        new("Egypt",        "🇪🇬", "Cairo",            "Egypt Standard Time",         Codes: new[]{"CAI","CAR"}),

        // ── Finland ──────────────────────────────────────────────────────────
        new("Finland",      "🇫🇮", "Helsinki",         "FLE Standard Time",           Codes: new[]{"HEL","HKI"}),

        // ── France ───────────────────────────────────────────────────────────
        new("France",       "🇫🇷", "Lyon",             "Romance Standard Time",       Codes: new[]{"LYN","LYS"}),
        new("France",       "🇫🇷", "Paris",            "Romance Standard Time",       Codes: new[]{"PAR","CDG","ORY"}),

        // ── Germany ──────────────────────────────────────────────────────────
        new("Germany",      "🇩🇪", "Berlin",           "W. Europe Standard Time",     Codes: new[]{"BER","TXL","SXF"}),
        new("Germany",      "🇩🇪", "Frankfurt",        "W. Europe Standard Time",     Codes: new[]{"FRA","FKB"}),
        new("Germany",      "🇩🇪", "Munich",           "W. Europe Standard Time",     Codes: new[]{"MUC","MUN"}),

        // ── Greece ───────────────────────────────────────────────────────────
        new("Greece",       "🇬🇷", "Athens",           "GTB Standard Time",           Codes: new[]{"ATH","ATN"}),

        // ── Hong Kong ────────────────────────────────────────────────────────
        new("Hong Kong",    "🇭🇰", "Hong Kong",        "China Standard Time",         Codes: new[]{"HKG","HK"}),

        // ── Hungary ──────────────────────────────────────────────────────────
        new("Hungary",      "🇭🇺", "Budapest",         "Central Europe Standard Time",Codes: new[]{"BUD","BPE"}),

        // ── India ────────────────────────────────────────────────────────────
        new("India",        "🇮🇳", "Bangalore",        "India Standard Time",         Codes: new[]{"BLR","BNG","BAN"}),
        new("India",        "🇮🇳", "Chennai",          "India Standard Time",         Codes: new[]{"MAA","CHN","MDS"}),
        new("India",        "🇮🇳", "Hyderabad",        "India Standard Time",         Codes: new[]{"HYD"}),
        new("India",        "🇮🇳", "Mumbai",           "India Standard Time",         Codes: new[]{"BOM","MUM","BBI"}),
        new("India",        "🇮🇳", "New Delhi",        "India Standard Time",         Codes: new[]{"DEL","NDL","INDEL"}),
        new("India",        "🇮🇳", "Pune",             "India Standard Time",         Codes: new[]{"PNQ","PUN"}),

        // ── Indonesia ────────────────────────────────────────────────────────
        new("Indonesia",    "🇮🇩", "Jakarta",          "SE Asia Standard Time",       Codes: new[]{"JKT","CGK"}),
        new("Indonesia",    "🇮🇩", "Surabaya",         "SE Asia Standard Time",       Codes: new[]{"SUB"}),

        // ── Ireland ──────────────────────────────────────────────────────────
        new("Ireland",      "🇮🇪", "Dublin",           "GMT Standard Time",           Codes: new[]{"DUB","DUB"}),

        // ── Israel ───────────────────────────────────────────────────────────
        new("Israel",       "🇮🇱", "Tel Aviv",         "Israel Standard Time",        Codes: new[]{"TLV","TA"}),

        // ── Italy ────────────────────────────────────────────────────────────
        new("Italy",        "🇮🇹", "Milan",            "W. Europe Standard Time",     Codes: new[]{"MIL","MXP","LIN"}),
        new("Italy",        "🇮🇹", "Rome",             "W. Europe Standard Time",     Codes: new[]{"ROM","FCO","CIA"}),

        // ── Japan ────────────────────────────────────────────────────────────
        new("Japan",        "🇯🇵", "Osaka",            "Tokyo Standard Time",         Codes: new[]{"OSA","KIX","ITM"}),
        new("Japan",        "🇯🇵", "Tokyo",            "Tokyo Standard Time",         Codes: new[]{"TYO","NRT","HND"}),
        new("Japan",        "🇯🇵", "Yokohama",         "Tokyo Standard Time",         Codes: new[]{"YOK"}),

        // ── Kenya ────────────────────────────────────────────────────────────
        new("Kenya",        "🇰🇪", "Nairobi",          "E. Africa Standard Time",     Codes: new[]{"NBI","NRB"}),

        // ── Malaysia ─────────────────────────────────────────────────────────
        new("Malaysia",     "🇲🇾", "Kuala Lumpur",     "Singapore Standard Time",     Codes: new[]{"KUL","KLU"}),

        // ── Mexico ───────────────────────────────────────────────────────────
        new("Mexico",       "🇲🇽", "Guadalajara",      "Central Standard Time (Mexico)",Codes: new[]{"GDL"}),
        new("Mexico",       "🇲🇽", "Mexico City",      "Central Standard Time (Mexico)",Codes: new[]{"MEX","MEXDF"}),
        new("Mexico",       "🇲🇽", "Monterrey",        "Central Standard Time (Mexico)",Codes: new[]{"MTY"}),
        new("Mexico",       "🇲🇽", "Tijuana",          "Pacific Standard Time (Mexico)",Codes: new[]{"TIJ","TJN"}),

        // ── Netherlands ──────────────────────────────────────────────────────
        new("Netherlands",  "🇳🇱", "Amsterdam",        "W. Europe Standard Time",     Codes: new[]{"AMS","AMS"}),

        // ── New Zealand ──────────────────────────────────────────────────────
        new("New Zealand",  "🇳🇿", "Auckland",         "New Zealand Standard Time",   Codes: new[]{"AKL"}),
        new("New Zealand",  "🇳🇿", "Wellington",       "New Zealand Standard Time",   Codes: new[]{"WLG","WEL"}),

        // ── Nigeria ──────────────────────────────────────────────────────────
        new("Nigeria",      "🇳🇬", "Lagos",            "W. Central Africa Standard Time",Codes: new[]{"LOS","LGS"}),

        // ── Norway ───────────────────────────────────────────────────────────
        new("Norway",       "🇳🇴", "Oslo",             "W. Europe Standard Time",     Codes: new[]{"OSL","OEN"}),

        // ── Pakistan ─────────────────────────────────────────────────────────
        new("Pakistan",     "🇵🇰", "Karachi",          "Pakistan Standard Time",      Codes: new[]{"KHI","KCT"}),
        new("Pakistan",     "🇵🇰", "Lahore",           "Pakistan Standard Time",      Codes: new[]{"LHE","LHR"}),

        // ── Peru ─────────────────────────────────────────────────────────────
        new("Peru",         "🇵🇪", "Lima",             "SA Pacific Standard Time",    Codes: new[]{"LIM","LMI"}),

        // ── Philippines ──────────────────────────────────────────────────────
        new("Philippines",  "🇵🇭", "Manila",           "Singapore Standard Time",     Codes: new[]{"MNL","MLA"}),

        // ── Poland ───────────────────────────────────────────────────────────
        new("Poland",       "🇵🇱", "Warsaw",           "Central European Standard Time",Codes: new[]{"WAW","WSW"}),

        // ── Portugal ─────────────────────────────────────────────────────────
        new("Portugal",     "🇵🇹", "Lisbon",           "GMT Standard Time",           Codes: new[]{"LIS","LIX"}),

        // ── Romania ──────────────────────────────────────────────────────────
        new("Romania",      "🇷🇴", "Bucharest",        "GTB Standard Time",           Codes: new[]{"OTP","BUH"}),

        // ── Russia ───────────────────────────────────────────────────────────
        new("Russia",       "🇷🇺", "Ekaterinburg",     "Ekaterinburg Standard Time",  Codes: new[]{"SVX","EKB"}),
        new("Russia",       "🇷🇺", "Moscow",           "Russian Standard Time",       Codes: new[]{"MOW","SVO","DME","VKO"}),
        new("Russia",       "🇷🇺", "Novosibirsk",      "N. Central Asia Standard Time",Codes: new[]{"OVB","NSK"}),
        new("Russia",       "🇷🇺", "Vladivostok",      "Vladivostok Standard Time",   Codes: new[]{"VVO","VDK"}),

        // ── Saudi Arabia ─────────────────────────────────────────────────────
        new("Saudi Arabia", "🇸🇦", "Riyadh",           "Arab Standard Time",          Codes: new[]{"RUH","RYD"}),

        // ── Singapore ────────────────────────────────────────────────────────
        new("Singapore",    "🇸🇬", "Singapore",        "Singapore Standard Time",     Codes: new[]{"SIN","SPN"}),

        // ── South Africa ─────────────────────────────────────────────────────
        new("South Africa", "🇿🇦", "Cape Town",        "South Africa Standard Time",  Codes: new[]{"CPT","CTN"}),
        new("South Africa", "🇿🇦", "Johannesburg",     "South Africa Standard Time",  Codes: new[]{"JNB","JHB"}),

        // ── South Korea ──────────────────────────────────────────────────────
        new("South Korea",  "🇰🇷", "Seoul",            "Korea Standard Time",         Codes: new[]{"SEL","ICN","GMP"}),
        new("South Korea",  "🇰🇷", "Busan",            "Korea Standard Time",         Codes: new[]{"PUS","BUS"}),

        // ── Spain ────────────────────────────────────────────────────────────
        new("Spain",        "🇪🇸", "Barcelona",        "Romance Standard Time",       Codes: new[]{"BCN","BAR"}),
        new("Spain",        "🇪🇸", "Madrid",           "Romance Standard Time",       Codes: new[]{"MAD","MDD"}),
        new("Spain",        "🇪🇸", "Seville",          "Romance Standard Time",       Codes: new[]{"SVQ","SVL"}),

        // ── Sweden ───────────────────────────────────────────────────────────
        new("Sweden",       "🇸🇪", "Stockholm",        "W. Europe Standard Time",     Codes: new[]{"STO","ARN","NYO"}),

        // ── Switzerland ──────────────────────────────────────────────────────
        new("Switzerland",  "🇨🇭", "Geneva",           "W. Europe Standard Time",     Codes: new[]{"GVA","GEN"}),
        new("Switzerland",  "🇨🇭", "Zurich",           "W. Europe Standard Time",     Codes: new[]{"ZRH","ZHR"}),

        // ── Taiwan ───────────────────────────────────────────────────────────
        new("Taiwan",       "🇹🇼", "Taipei",           "Taipei Standard Time",        Codes: new[]{"TPE","TAP"}),

        // ── Thailand ─────────────────────────────────────────────────────────
        new("Thailand",     "🇹🇭", "Bangkok",          "SE Asia Standard Time",       Codes: new[]{"BKK","DMK"}),

        // ── Turkey ───────────────────────────────────────────────────────────
        new("Turkey",       "🇹🇷", "Istanbul",         "Turkey Standard Time",        Codes: new[]{"IST","SAW"}),
        new("Turkey",       "🇹🇷", "Ankara",           "Turkey Standard Time",        Codes: new[]{"ANK","ESB"}),

        // ── Ukraine ──────────────────────────────────────────────────────────
        new("Ukraine",      "🇺🇦", "Kyiv",             "FLE Standard Time",           Codes: new[]{"KBP","IEV","KIV"}),

        // ── United Arab Emirates ─────────────────────────────────────────────
        new("United Arab Emirates","🇦🇪","Abu Dhabi",  "Arabian Standard Time",       Codes: new[]{"AUH","ABU"}),
        new("United Arab Emirates","🇦🇪","Dubai",      "Arabian Standard Time",       Codes: new[]{"DXB","DBX"}),

        // ── United Kingdom ───────────────────────────────────────────────────
        new("United Kingdom","🇬🇧", "Birmingham",      "GMT Standard Time",           Codes: new[]{"BHX","BHM"}),
        new("United Kingdom","🇬🇧", "Edinburgh",       "GMT Standard Time",           Codes: new[]{"EDI"}),
        new("United Kingdom","🇬🇧", "London",          "GMT Standard Time",           Codes: new[]{"LON","LHR","LGW","LCY","LHN"}),
        new("United Kingdom","🇬🇧", "Manchester",      "GMT Standard Time",           Codes: new[]{"MAN"}),

        // ── United States ────────────────────────────────────────────────────
        new("United States","🇺🇸",  "Atlanta",         "Eastern Standard Time",       Codes: new[]{"ATL"}),
        new("United States","🇺🇸",  "Austin",          "Central Standard Time",       Codes: new[]{"AUS","ATX"}),
        new("United States","🇺🇸",  "Boston",          "Eastern Standard Time",       Codes: new[]{"BOS"}),
        new("United States","🇺🇸",  "Charlotte",       "Eastern Standard Time",       Codes: new[]{"CLT","CRQ"}),
        new("United States","🇺🇸",  "Chicago",         "Central Standard Time",       Codes: new[]{"CHI","ORD","MDW"}),
        new("United States","🇺🇸",  "Dallas",          "Central Standard Time",       Codes: new[]{"DAL","DFW","DLF"}),
        new("United States","🇺🇸",  "Denver",          "Mountain Standard Time",      Codes: new[]{"DEN","DVN"}),
        new("United States","🇺🇸",  "Detroit",         "Eastern Standard Time",       Codes: new[]{"DTW","DET"}),
        new("United States","🇺🇸",  "Houston",         "Central Standard Time",       Codes: new[]{"HOU","IAH","HUS"}),
        new("United States","🇺🇸",  "Las Vegas",       "Pacific Standard Time",       Codes: new[]{"LAS","LV","LVS"}),
        new("United States","🇺🇸",  "Los Angeles",     "Pacific Standard Time",       Codes: new[]{"LAX","LA","LAS"}),
        new("United States","🇺🇸",  "Miami",           "Eastern Standard Time",       Codes: new[]{"MIA","MFL"}),
        new("United States","🇺🇸",  "Minneapolis",     "Central Standard Time",       Codes: new[]{"MSP","MIN","MPS"}),
        new("United States","🇺🇸",  "New York",        "Eastern Standard Time",       Codes: new[]{"NYC","JFK","LGA","EWR","NY"}),
        new("United States","🇺🇸",  "Phoenix",         "US Mountain Standard Time",   Codes: new[]{"PHX","PHO"}),
        new("United States","🇺🇸",  "Portland",        "Pacific Standard Time",       Codes: new[]{"PDX","POR"}),
        new("United States","🇺🇸",  "San Francisco",   "Pacific Standard Time",       Codes: new[]{"SFO","SF","SFN"}),
        new("United States","🇺🇸",  "Seattle",         "Pacific Standard Time",       Codes: new[]{"SEA","SEW"}),
        new("United States","🇺🇸",  "Washington D.C.", "Eastern Standard Time",       Codes: new[]{"DCA","IAD","DC","WAS","DWC"}),

        // ── Venezuela ────────────────────────────────────────────────────────
        new("Venezuela",    "🇻🇪", "Caracas",          "Venezuela Standard Time",     Codes: new[]{"CCS","CAR"}),

        // ── Vietnam ──────────────────────────────────────────────────────────
        new("Vietnam",      "🇻🇳", "Hanoi",            "SE Asia Standard Time",       Codes: new[]{"HAN","HNI"}),
        new("Vietnam",      "🇻🇳", "Ho Chi Minh City", "SE Asia Standard Time",       Codes: new[]{"SGN","HCM","TSN"}),

        // ── Timezone Catalog ─────────────────────────────────────────────────
        // Searchable by standard abbreviation: "GMT", "BST", "EDT", "JST", etc.
        // These also appear as a "Time Zones" group in the country / city dropdowns.

        // Universal
        new("Time Zones", "🌐", "UTC — Coordinated Universal Time", "UTC",                              Codes: new[]{"UTC","Z","UCT"}),
        new("Time Zones", "🌐", "GMT — Greenwich Mean Time",       "GMT Standard Time",                Codes: new[]{"GMT","WET"}),

        // Europe
        new("Time Zones", "🇬🇧", "BST — British Summer Time",       "GMT Standard Time",               Codes: new[]{"BST"}),
        new("Time Zones", "🇪🇺", "CET — Central European Time",     "Central Europe Standard Time",    Codes: new[]{"CET"}),
        new("Time Zones", "🇪🇺", "CEST — Central European Summer",  "Central Europe Standard Time",    Codes: new[]{"CEST","CEDT"}),
        new("Time Zones", "🇪🇺", "EET — Eastern European Time",     "E. Europe Standard Time",         Codes: new[]{"EET"}),
        new("Time Zones", "🇪🇺", "EEST — Eastern European Summer",  "E. Europe Standard Time",         Codes: new[]{"EEST","EEDT"}),
        new("Time Zones", "🇷🇺", "MSK — Moscow Standard Time",      "Russian Standard Time",           Codes: new[]{"MSK","MSD"}),
        new("Time Zones", "🇹🇷", "TRT — Turkey Time",               "Turkey Standard Time",            Codes: new[]{"TRT"}),
        new("Time Zones", "🇮🇷", "IRST — Iran Standard Time",       "Iran Standard Time",              Codes: new[]{"IRST","IRDT"}),

        // North America
        new("Time Zones", "🇺🇸", "ET — Eastern Time (US)",          "Eastern Standard Time",           Codes: new[]{"ET","EST","EDT"}),
        new("Time Zones", "🇺🇸", "CT — Central Time (US)",          "Central Standard Time",           Codes: new[]{"CT","CST","CDT"}),
        new("Time Zones", "🇺🇸", "MT — Mountain Time (US)",         "Mountain Standard Time",          Codes: new[]{"MT","MST","MDT"}),
        new("Time Zones", "🇺🇸", "PT — Pacific Time (US)",          "Pacific Standard Time",           Codes: new[]{"PT","PST","PDT"}),
        new("Time Zones", "🇺🇸", "AKT — Alaska Time",               "Alaskan Standard Time",           Codes: new[]{"AKT","AKST","AKDT"}),
        new("Time Zones", "🇺🇸", "HST — Hawaii Standard Time",      "Hawaiian Standard Time",          Codes: new[]{"HST","HAT"}),
        new("Time Zones", "🇨🇦", "AT — Atlantic Time (Canada)",     "Atlantic Standard Time",          Codes: new[]{"AT","ADT"}),
        new("Time Zones", "🇨🇦", "NST — Newfoundland Time",         "Newfoundland Standard Time",      Codes: new[]{"NST","NDT"}),

        // South America
        new("Time Zones", "🇧🇷", "BRT — Brasília Time",             "E. South America Standard Time",  Codes: new[]{"BRT","BRST"}),
        new("Time Zones", "🇦🇷", "ART — Argentina Time",            "Argentina Standard Time",         Codes: new[]{"ART"}),
        new("Time Zones", "🇨🇴", "COT — Colombia Time",             "SA Pacific Standard Time",        Codes: new[]{"COT"}),
        new("Time Zones", "🇻🇪", "VET — Venezuela Time",            "Venezuela Standard Time",         Codes: new[]{"VET"}),
        new("Time Zones", "🇨🇱", "CLT — Chile Standard Time",       "Pacific SA Standard Time",        Codes: new[]{"CLT","CLST"}),

        // Africa
        new("Time Zones", "🇿🇦", "SAST — South Africa Standard",    "South Africa Standard Time",      Codes: new[]{"SAST","CAT"}),
        new("Time Zones", "🌍",  "EAT — East Africa Time",           "E. Africa Standard Time",         Codes: new[]{"EAT"}),
        new("Time Zones", "🌍",  "WAT — West Africa Time",           "W. Central Africa Standard Time", Codes: new[]{"WAT"}),

        // Middle East
        new("Time Zones", "🇸🇦", "AST — Arabia Standard Time",       "Arab Standard Time",              Codes: new[]{"AST"}),
        new("Time Zones", "🇦🇪", "GST — Gulf Standard Time",         "Arabian Standard Time",           Codes: new[]{"GST"}),
        new("Time Zones", "🇮🇱", "IDT — Israel Time",                "Israel Standard Time",            Codes: new[]{"IDT","IST IL"}),
        new("Time Zones", "🇮🇷", "AFT — Afghanistan Time",           "Afghanistan Standard Time",       Codes: new[]{"AFT"}),

        // South Asia
        new("Time Zones", "🇵🇰", "PKT — Pakistan Standard Time",     "Pakistan Standard Time",          Codes: new[]{"PKT"}),
        new("Time Zones", "🇮🇳", "IST — India Standard Time",        "India Standard Time",             Codes: new[]{"IST"}),
        new("Time Zones", "🇧🇩", "BDT — Bangladesh Standard Time",   "Bangladesh Standard Time",        Codes: new[]{"BDT"}),
        new("Time Zones", "🇳🇵", "NPT — Nepal Time",                 "Nepal Standard Time",             Codes: new[]{"NPT"}),
        new("Time Zones", "🇲🇲", "MMT — Myanmar Time",               "Myanmar Standard Time",           Codes: new[]{"MMT"}),

        // Southeast & East Asia
        new("Time Zones", "🇹🇭", "ICT — Indochina Time",             "SE Asia Standard Time",           Codes: new[]{"ICT","WIB"}),
        new("Time Zones", "🇨🇳", "CST — China Standard Time",        "China Standard Time",             Codes: new[]{"CST CN","HKT"}),
        new("Time Zones", "🇸🇬", "SGT — Singapore Time",             "Singapore Standard Time",         Codes: new[]{"SGT","MYT","PHT","SST"}),
        new("Time Zones", "🇯🇵", "JST — Japan Standard Time",        "Tokyo Standard Time",             Codes: new[]{"JST","JT"}),
        new("Time Zones", "🇰🇷", "KST — Korea Standard Time",        "Korea Standard Time",             Codes: new[]{"KST","KT"}),

        // Australia & Pacific
        new("Time Zones", "🇦🇺", "AWST — Australian Western Time",   "W. Australia Standard Time",      Codes: new[]{"AWST"}),
        new("Time Zones", "🇦🇺", "ACST — Australian Central Time",   "Cen. Australia Standard Time",    Codes: new[]{"ACST","ACDT"}),
        new("Time Zones", "🇦🇺", "AEST — Australian Eastern Time",   "AUS Eastern Standard Time",       Codes: new[]{"AEST","AEDT","AET"}),
        new("Time Zones", "🇳🇿", "NZST — New Zealand Time",          "New Zealand Standard Time",       Codes: new[]{"NZST","NZDT"}),
    };

    /// <summary>Returns a sorted, distinct list of country names.</summary>
    public static IReadOnlyList<string> Countries { get; } =
        All.Select(c => c.Country).Distinct().OrderBy(c => c).ToList();

    /// <summary>Returns all cities for the given country, ordered by city name.</summary>
    public static IReadOnlyList<CityEntry> CitiesForCountry(string country) =>
        All.Where(c => c.Country == country).OrderBy(c => c.City).ToList();
}
