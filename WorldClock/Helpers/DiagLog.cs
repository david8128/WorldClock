using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace WorldClock.Helpers;

/// <summary>
/// Thin diagnostic logger that writes to <see cref="Trace"/> (visible in the
/// Visual Studio Output window under "Debug" channel), to the Windows
/// DebugView tool, AND to %TEMP%\worldclock_diag.log so output is available
/// without needing the VS debugger attached.
/// Call <see cref="Enable"/> / <see cref="Disable"/> at runtime;
/// disabled by default in release builds.
/// </summary>
internal static class DiagLog
{
#if DEBUG
    private static bool _enabled = true;   // on by default in Debug builds
#else
    private static bool _enabled = false;
#endif

    private static readonly string LogFilePath =
        Path.Combine(Path.GetTempPath(), $"worldclock_diag_{Environment.ProcessId}.log");

    /// <summary>Turn logging on or off at runtime.</summary>
    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    private static void WriteToFile(string line)
    {
        try { File.AppendAllText(LogFilePath, line + Environment.NewLine); }
        catch { /* never throw from a logger */ }
    }

    // ── Logging levels ────────────────────────────────────────────────────────

    public static void Info(string message,
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
    {
        if (!_enabled) return;
        var short_file = System.IO.Path.GetFileNameWithoutExtension(file);
        var line = $"[INFO ][{short_file}.{member}] {message}";
        Trace.WriteLine(line);
        WriteToFile($"{DateTime.Now:HH:mm:ss.fff} {line}");
    }

    public static void Warn(string message,
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
    {
        if (!_enabled) return;
        var short_file = System.IO.Path.GetFileNameWithoutExtension(file);
        var line = $"[WARN ][{short_file}.{member}] {message}";
        Trace.WriteLine(line);
        WriteToFile($"{DateTime.Now:HH:mm:ss.fff} {line}");
    }

    public static void Debug(string message,
        [CallerMemberName] string member = "",
        [CallerFilePath]   string file   = "")
    {
        if (!_enabled) return;
        var short_file = System.IO.Path.GetFileNameWithoutExtension(file);
        var line = $"[DEBUG][{short_file}.{member}] {message}";
        Trace.WriteLine(line);
        WriteToFile($"{DateTime.Now:HH:mm:ss.fff} {line}");
    }
}
