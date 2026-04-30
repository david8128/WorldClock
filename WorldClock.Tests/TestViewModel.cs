using System.IO;
using WorldClock.Services;
using WorldClock.ViewModels;

namespace WorldClock.Tests;

/// <summary>
/// Test helper that provides a <see cref="MainViewModel"/> isolated from any
/// real AppData settings file. Each call returns a brand-new instance backed
/// by an empty in-memory (temp-dir) <see cref="SettingsService"/> so tests are
/// never affected by previously saved user settings.
/// </summary>
internal static class TestViewModel
{
    /// <summary>
    /// Creates a <see cref="MainViewModel"/> with first-run defaults (7 cities)
    /// regardless of whether a real settings file exists on the machine.
    /// </summary>
    internal static MainViewModel Fresh() => new(EmptyStore());

    /// <summary>A SettingsService backed by a unique temp directory with no file.</summary>
    internal static SettingsService EmptyStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WorldClockTests", Guid.NewGuid().ToString());
        return new TempSettingsService(dir);
    }

    private sealed class TempSettingsService : SettingsService
    {
        public TempSettingsService(string dir) : base(Path.Combine(dir, "settings.json")) { }
    }
}
