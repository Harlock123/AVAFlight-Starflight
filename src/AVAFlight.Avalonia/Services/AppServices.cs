using AVAFlight.Core.Engine;
using AVAFlight.Infrastructure;
using AVAFlight.Infrastructure.Audio;
using AVAFlight.Infrastructure.Input;
using AVAFlight.Infrastructure.Persistence;
using AVAFlight.Infrastructure.Settings;

namespace AVAFlight.Avalonia.Services;

/// <summary>
/// Process-wide services (composition root). Created once at startup; tests and the screenshot
/// tool create an instance with silent audio and a temporary data directory.
/// </summary>
public sealed class AppServices : IDisposable
{
    public AppPaths Paths { get; }
    public SettingsStore SettingsStore { get; }
    public GameSettings Settings { get; }
    public SaveStore Saves { get; }
    public IAudioEngine Audio { get; }
    public InputMapper Input { get; }
    public GamepadService? Gamepad { get; }

    public static AppServices Current { get; set; } = null!;

    public AppServices(AppPaths paths, bool enableDevices)
    {
        Paths = paths;
        SettingsStore = new SettingsStore(paths);
        Settings = SettingsStore.Load();
        Saves = new SaveStore(paths);
        Audio = enableDevices ? AudioEngine.Create(Settings.AudioEnabled) : AudioEngine.CreateSilent("Audio disabled (headless)");
        Audio.MusicVolume = Settings.MusicVolume;
        Audio.EffectsVolume = Settings.EffectsVolume;
        Input = new InputMapper(Settings.Bindings ?? DefaultBindings.Create());
        if (enableDevices)
        {
            Gamepad = new GamepadService { Deadzone = Settings.GamepadDeadzone };
        }
    }

    public void SaveSettings()
    {
        SettingsStore.Save(Settings);
        Audio.MusicVolume = Settings.MusicVolume;
        Audio.EffectsVolume = Settings.EffectsVolume;
        if (Gamepad is not null) Gamepad.Deadzone = Settings.GamepadDeadzone;
    }

    /// <summary>Maps engine sound cue names to synthesized effects.</summary>
    public void PlayCue(string? cue)
    {
        if (cue is null) return;
        SoundEffect? fx = cue switch
        {
            "laser" => SoundEffect.Laser, "missile" => SoundEffect.Missile, "shieldhit" => SoundEffect.ShieldHit,
            "hullhit" => SoundEffect.HullHit, "explosion" => SoundEffect.Explosion, "comms" => SoundEffect.CommsChime,
            "artifact" => SoundEffect.Artifact, "mineral" => SoundEffect.MineralPickup, "scan" => SoundEffect.Scan,
            "warning" => SoundEffect.Warning, "hyperspace" => SoundEffect.HyperspaceJump, "launch" => SoundEffect.Launch,
            "land" => SoundEffect.Land, "purchase" => SoundEffect.Purchase, "error" => SoundEffect.Error,
            "victory" => null, _ => null,
        };
        if (fx is { } f) Audio.Play(f);
        if (cue == "victory") Audio.PlayMusic(MusicCue.Victory);
    }

    public void Dispose()
    {
        Audio.Dispose();
        Gamepad?.Dispose();
    }
}
