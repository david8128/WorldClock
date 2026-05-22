using System.Diagnostics;
using System.IO;

namespace WorldClock.Helpers;

internal static class StartupHelper
{
    // ── Linux XDG autostart ───────────────────────────────────────────────────
    private static string XdgAutostartDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart");

    private static string DesktopFilePath =>
        Path.Combine(XdgAutostartDir, "worldclock.desktop");

    // ── Windows Registry ──────────────────────────────────────────────────────
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WorldClock";

    public static bool IsRegistered
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return IsRegisteredWindows();
            return File.Exists(DesktopFilePath);
        }
    }

    public static void SetStartup(bool enable)
    {
        if (OperatingSystem.IsWindows())
            SetStartupWindows(enable);
        else
            SetStartupXdg(enable);
    }

    // ── Windows implementation ────────────────────────────────────────────────

    private static bool IsRegisteredWindows()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunSubKey, false);
            var raw = key?.GetValue(ValueName) as string;
            if (string.IsNullOrEmpty(raw)) return false;
            return File.Exists(raw.Trim('"'));
        }
        catch { return false; }
    }

    private static void SetStartupWindows(bool enable)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunSubKey, true);
            if (key is null) return;
            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch { }
    }

    // ── Linux XDG autostart implementation ───────────────────────────────────

    private static void SetStartupXdg(bool enable)
    {
        try
        {
            if (!enable)
            {
                if (File.Exists(DesktopFilePath)) File.Delete(DesktopFilePath);
                return;
            }

            Directory.CreateDirectory(XdgAutostartDir);
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "worldclock";
            File.WriteAllText(DesktopFilePath,
                $"[Desktop Entry]\n" +
                $"Type=Application\n" +
                $"Name=WorldClock\n" +
                $"Exec={exePath}\n" +
                $"Hidden=false\n" +
                $"NoDisplay=false\n" +
                $"X-GNOME-Autostart-enabled=true\n");
        }
        catch { }
    }
}

