using Avalonia;
using Avalonia.Headless;

// Wire Avalonia.Headless.XUnit to use HeadlessApp for all [AvaloniaFact] tests.
[assembly: AvaloniaTestApplication(typeof(WorldClock.Tests.HeadlessApp))]

namespace WorldClock.Tests;

/// <summary>
/// Minimal Avalonia application used by Avalonia.Headless.XUnit.
/// Does NOT load App.axaml styles so tests stay fast and isolated; individual
/// tests that need the full theme can call <see cref="WorldClock.App"/> explicitly.
/// </summary>
public sealed class HeadlessApp : Application
{
    public override void Initialize() { }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessApp>()
                     .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
