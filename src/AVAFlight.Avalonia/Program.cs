using Avalonia;

namespace AVAFlight.Avalonia;

internal static class Program
{
    /// <summary>--mute disables audio and gamepad devices (useful on machines without audio).</summary>
    public static bool Muted { get; private set; }

    /// <summary>--smoke-test: start, reach the main menu, then exit with code 0 (used to verify published binaries).</summary>
    public static bool SmokeTest { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Muted = args.Contains("--mute");
        SmokeTest = args.Contains("--smoke-test");
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
