using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorldClock.Models;

namespace WorldClock.Services;

/// <summary>
/// Persists and restores user settings (theme, opacity, city list) to
/// <c>%APPDATA%\WorldClock\settings.json</c>.
/// </summary>
public class SettingsService
{
    // -- Singleton --
    public static SettingsService Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented            = true,
        DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;

    /// <summary>Absolute path to the settings file.</summary>
    public virtual string FilePath => _filePath;

    private SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorldClock",
            "settings.json")) { }

    /// <summary>Protected constructor for testable subclasses that need a custom file path.</summary>
    protected SettingsService(string filePath) => _filePath = filePath;

    // -- Public API --

    /// <summary>
    /// Loads persisted settings. Returns defaults if the file does not exist or is corrupt.
    /// </summary>
    public virtual UserSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new UserSettings();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    /// <summary>
    /// Persists <paramref name="settings"/> to disk. Never throws.
    /// </summary>
    public virtual void Save(UserSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch { /* disk full, permissions, etc. -- silently ignore */ }
    }
}
