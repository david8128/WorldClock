using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WorldClock.Helpers;

/// <summary>
/// Manages the Windows "Run at login" registry entry for WorldClock.
/// Key: HKCU\Software\Microsoft\Windows\CurrentVersion\Run  value: WorldClock
/// </summary>
internal static class StartupHelper
{
    private const string RunSubKey   = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName   = "WorldClock";

    /// <summary>
    /// Returns the path stored in the startup registry entry, or <c>null</c> when absent.
    /// </summary>
    public static string? GetRegisteredPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
            return key?.GetValue(ValueName) as string;
        }
        catch { return null; }
    }

    /// <summary>
    /// <c>true</c> when the registry entry exists <em>and</em> the exe it points to exists on disk.
    /// This reflects the real live state, not just what settings.json says.
    /// </summary>
    public static bool IsRegistered
    {
        get
        {
            var raw = GetRegisteredPath();
            if (string.IsNullOrEmpty(raw)) return false;
            // Strip surrounding quotes that we add when writing
            var path = raw.Trim('"');
            return File.Exists(path);
        }
    }

    /// <summary>
    /// Adds or removes the startup registry entry.
    /// When <paramref name="enable"/> is <c>true</c> the entry is set to the current
    /// process executable path (quoted to handle paths with spaces).
    /// </summary>
    public static void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
            if (key is null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch { /* registry access denied, sandboxed environment, etc. — silently ignore */ }
    }
}
