using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace WorldClock;

public partial class App : Application
{
    private static StreamWriter? _logWriter;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SetupLogging();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetupLogging()
    {
        var logPath = Path.Combine(
            Path.GetTempPath(),
            $"worldclock_diag_{Environment.ProcessId}.log");
        try
        {
            _logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
            Trace.Listeners.Add(new TextWriterTraceListener(_logWriter, "FileListener"));
            Trace.WriteLine($"[App.OnStartup] Log started. LogPath={logPath}");
            Trace.WriteLine($"[App.OnStartup] Machine local timezone: {TimeZoneInfo.Local.Id}");
            Trace.WriteLine($"[App.OnStartup] UTC now: {DateTime.UtcNow:HH:mm:ss}");
            Trace.WriteLine($"[App.OnStartup] Local now: {DateTime.Now:HH:mm:ss}");
        }
        catch { /* log file unavailable — continue without file tracing */ }
    }
}

