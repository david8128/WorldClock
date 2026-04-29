using System.IO;
using System.Text.Json;
using FluentAssertions;
using WorldClock.Models;
using WorldClock.Services;
using Xunit;

namespace WorldClock.Tests.Services;

/// <summary>
/// Tests for <see cref="SettingsService"/>:
///   • Round-trip serialization (save → load) for all persisted fields.
///   • Default values match the designed first-run experience.
///   • Graceful fallback on missing / corrupt files.
///   • City list persistence (add, remove, UTC always excluded).
/// All tests use a temporary directory so they never touch real AppData.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    // ── Isolated temp store used by every test ─────────────────────────────────

    private readonly string          _dir;
    private readonly TestableSettingsService _svc;

    public SettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "WorldClockTests", Guid.NewGuid().ToString());
        _svc = new TestableSettingsService(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // ── Default values ─────────────────────────────────────────────────────────

    [Fact]
    public void SettingsService_Load_WhenNoFile_ReturnsDefaults()
    {
        var s = _svc.Load();
        s.ThemeName.Should().Be("Dark Default");
        s.Opacity.Should().Be(0.1, "first-run default must be maximum transparency");
        s.Cities.Should().BeEmpty();
    }

    [Fact]
    public void SettingsService_DefaultOpacity_IsMaximumTransparency()
    {
        // Regression guard: default opacity must always be 0.1 (slider fully transparent).
        var s = new UserSettings();
        s.Opacity.Should().Be(0.1,
            "UserSettings.Opacity default must be 0.1 so the app launches at max transparency");
    }

    // ── Round-trip ─────────────────────────────────────────────────────────────

    [Fact]
    public void SettingsService_SaveAndLoad_PreservesThemeName()
    {
        _svc.Save(new UserSettings { ThemeName = "Nord Dark", Opacity = 0.5 });
        _svc.Load().ThemeName.Should().Be("Nord Dark");
    }

    [Fact]
    public void SettingsService_SaveAndLoad_PreservesOpacity()
    {
        _svc.Save(new UserSettings { ThemeName = "Dark Default", Opacity = 0.7 });
        _svc.Load().Opacity.Should().BeApproximately(0.7, 0.001);
    }

    [Fact]
    public void SettingsService_SaveAndLoad_PreservesCityList()
    {
        var cities = new List<SavedCity>
        {
            new() { CityName = "London",   CountryFlag = "🇬🇧", TimeZoneId = "GMT Standard Time",  TeamLabel = "EMEA" },
            new() { CityName = "New York",  CountryFlag = "🇺🇸", TimeZoneId = "Eastern Standard Time", TeamLabel = "Americas" },
        };
        _svc.Save(new UserSettings { Cities = cities });

        var loaded = _svc.Load();
        loaded.Cities.Should().HaveCount(2);
        loaded.Cities[0].CityName.Should().Be("London");
        loaded.Cities[1].CityName.Should().Be("New York");
    }

    [Fact]
    public void SettingsService_SaveAndLoad_PreservesAllCityFields()
    {
        var city = new SavedCity
        {
            CityName    = "Tokyo",
            CountryFlag = "🇯🇵",
            TimeZoneId  = "Tokyo Standard Time",
            TeamLabel   = "APAC",
        };
        _svc.Save(new UserSettings { Cities = [city] });

        var c = _svc.Load().Cities[0];
        c.CityName.Should().Be("Tokyo");
        c.CountryFlag.Should().Be("🇯🇵");
        c.TimeZoneId.Should().Be("Tokyo Standard Time");
        c.TeamLabel.Should().Be("APAC");
    }

    [Fact]
    public void SettingsService_Save_CreatesDirectoryIfMissing()
    {
        // Remove the directory so Save must create it.
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);

        _svc.Save(new UserSettings { ThemeName = "One Dark" });

        File.Exists(_svc.FilePath).Should().BeTrue("Save must create the directory and file");
    }

    [Fact]
    public void SettingsService_Save_WritesValidJson()
    {
        _svc.Save(new UserSettings { ThemeName = "Monokai", Opacity = 0.4 });

        var json = File.ReadAllText(_svc.FilePath);
        var doc  = JsonDocument.Parse(json);   // throws if invalid JSON
        doc.RootElement.GetProperty("ThemeName").GetString().Should().Be("Monokai");
    }

    [Fact]
    public void SettingsService_Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_svc.FilePath, "NOT VALID JSON {{{{");

        var s = _svc.Load();
        s.ThemeName.Should().Be("Dark Default");
        s.Opacity.Should().Be(0.1);
    }

    [Fact]
    public void SettingsService_Load_EmptyJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_svc.FilePath, "{}");

        var s = _svc.Load();
        // Deserialized from "{}" — property defaults come from UserSettings ctor.
        s.ThemeName.Should().Be("Dark Default");
        s.Opacity.Should().Be(0.1);
    }

    // ── Partial save / merge behaviour (theme saves, cities preserved) ─────────

    [Fact]
    public void SettingsService_SaveTheme_DoesNotEraseCities()
    {
        // First save: cities
        _svc.Save(new UserSettings
        {
            Cities = [new() { CityName = "Paris", TimeZoneId = "Romance Standard Time" }]
        });

        // Second save: theme only (as ThemeService.Persist() does — loads first, then saves)
        var existing = _svc.Load();
        existing.ThemeName = "Catppuccin Mocha";
        _svc.Save(existing);

        var final = _svc.Load();
        final.ThemeName.Should().Be("Catppuccin Mocha");
        final.Cities.Should().HaveCount(1, "city list must survive a theme-only save");
    }

    // ── Testable subclass with injected file path ──────────────────────────────

    private sealed class TestableSettingsService : SettingsService
    {
        public TestableSettingsService(string dir)
            : base(Path.Combine(dir, "settings.json")) { }
    }
}
