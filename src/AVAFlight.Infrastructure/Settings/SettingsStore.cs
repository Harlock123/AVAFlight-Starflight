using System.Text.Json;
using System.Text.Json.Serialization;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Infrastructure.Settings;

/// <summary>User preferences (not part of a save game).</summary>
public sealed class GameSettings
{
    public double MusicVolume { get; set; } = 0.6;
    public double EffectsVolume { get; set; } = 0.8;
    public bool AudioEnabled { get; set; } = true;
    public bool Fullscreen { get; set; }
    /// <summary>Multiplier applied to all text-heavy screens (Modern mode exposes 0.8–1.6).</summary>
    public double FontScale { get; set; } = 1.0;
    /// <summary>Characters per second for dialogue reveal; 0 = instant.</summary>
    public int TextSpeed { get; set; } = 60;
    public bool HighContrast { get; set; }
    public bool ShowHints { get; set; } = true;
    public double GamepadDeadzone { get; set; } = 0.25;
    /// <summary>Default preset offered by "New Game": "Classic" or "Modern".</summary>
    public string DefaultPreset { get; set; } = "Modern";
    public List<InputBinding>? Bindings { get; set; }

    public void Clamp()
    {
        MusicVolume = Math.Clamp(MusicVolume, 0, 1);
        EffectsVolume = Math.Clamp(EffectsVolume, 0, 1);
        FontScale = Math.Clamp(FontScale, 0.8, 1.6);
        TextSpeed = Math.Clamp(TextSpeed, 0, 400);
        GamepadDeadzone = Math.Clamp(GamepadDeadzone, 0.05, 0.6);
        if (DefaultPreset is not ("Classic" or "Modern")) DefaultPreset = "Modern";
    }
}

public sealed class SettingsStore(AppPaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Loads settings; a missing or corrupt file yields defaults rather than an error.</summary>
    public GameSettings Load()
    {
        try
        {
            if (File.Exists(paths.SettingsFile))
            {
                var s = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(paths.SettingsFile), Options);
                if (s is not null) { s.Clamp(); return s; }
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fall through to defaults; a broken settings file must never block the game.
        }
        return new GameSettings();
    }

    public void Save(GameSettings settings)
    {
        settings.Clamp();
        paths.EnsureCreated();
        var tmp = paths.SettingsFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, paths.SettingsFile, overwrite: true);
    }
}
