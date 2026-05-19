using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WorldClock;

public partial class App : Application
{
    private static StreamWriter? _logWriter;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Write all Trace output to a temp file so we can diagnose without VS attached.
        var logPath = Path.Combine(Path.GetTempPath(), "worldclock_diag.log");
        _logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
        Trace.Listeners.Add(new TextWriterTraceListener(_logWriter, "FileListener"));
        Trace.WriteLine($"[App.OnStartup] Log started. LogPath={logPath}");
        Trace.WriteLine($"[App.OnStartup] Machine local timezone: {TimeZoneInfo.Local.Id}");
        Trace.WriteLine($"[App.OnStartup] UTC now: {DateTime.UtcNow:HH:mm:ss}");
        Trace.WriteLine($"[App.OnStartup] Local now: {DateTime.Now:HH:mm:ss}");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Trace.WriteLine("[App.OnExit] Shutting down.");
        _logWriter?.Dispose();
        base.OnExit(e);
    }
}
