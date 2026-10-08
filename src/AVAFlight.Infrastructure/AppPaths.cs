namespace AVAFlight.Infrastructure;

/// <summary>
/// Per-OS locations for saves and settings. Never writes next to the executable, because a
/// single-file app may live in a read-only location (Program Files, /usr/bin, a .app bundle).
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string? rootOverride = null)
    {
        Root = rootOverride ?? DefaultRoot();
        Saves = Path.Combine(Root, "saves");
        SettingsFile = Path.Combine(Root, "settings.json");
    }

    /// <summary>Windows: %APPDATA%\AVAFlight. macOS: ~/Library/Application Support/AVAFlight.
    /// Linux: $XDG_DATA_HOME/AVAFlight (default ~/.local/share/AVAFlight).</summary>
    public string Root { get; }
    public string Saves { get; }
    public string SettingsFile { get; }

    public static string DefaultRoot()
    {
        var env = Environment.GetEnvironmentVariable("AVAFLIGHT_HOME");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AVAFlight");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "AVAFlight");

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(string.IsNullOrWhiteSpace(xdg) ? Path.Combine(home, ".local", "share") : xdg, "AVAFlight");
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Saves);
    }
}
