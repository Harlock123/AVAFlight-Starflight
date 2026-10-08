using System.Text.Json.Nodes;
using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;
using AVAFlight.Core.Serialization;
using AVAFlight.Infrastructure;
using AVAFlight.Infrastructure.Audio;
using AVAFlight.Infrastructure.Input;
using AVAFlight.Infrastructure.Persistence;
using AVAFlight.Infrastructure.Settings;
using AVAFlight.Tests.Core;

namespace AVAFlight.Tests.Infrastructure;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "avaflight-test-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}

public class SaveTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsTheFullState()
    {
        using var dir = new TempDir();
        var store = new SaveStore(new AppPaths(dir.Path));
        var s = TestHelpers.InOrbitOf("Arth", 3, GamePreset.Modern);
        s.Planets.Land(12, -40);
        s.State.Surface!.Creatures.Clear();
        s.Story.SetFlag("knows_crystal_planet");
        s.State.Waypoints.Add(new Waypoint(10, 20, "test"));
        store.Save("slot1", "My game", s.Snapshot());

        var loaded = store.Load("slot1");
        Assert.Equal(GameJson.Serialize(s.State), GameJson.Serialize(loaded));
        // A session rebuilt from the save regenerates the identical galaxy.
        var s2 = new GameSession(loaded, GameData.Default);
        Assert.Equal(s.Galaxy.AllPlanets.Select(p => p.Gravity), s2.Galaxy.AllPlanets.Select(p => p.Gravity));
        Assert.Equal(s.Rng.State, s2.Rng.State);
        var info = Assert.Single(store.List());
        Assert.Equal("My game", info.DisplayName);
        Assert.Equal("Modern", info.Preset);
    }

    [Fact]
    public void CrewDeath_PersistsAcrossSaveLoad()
    {
        using var dir = new TempDir();
        var store = new SaveStore(new AppPaths(dir.Path));
        var s = TestHelpers.ReadyToLaunch(GamePreset.Classic);
        var victim = s.State.Roster[1];
        s.Crew.Injure(victim, 1000, "test");
        Assert.True(victim.IsDead);
        store.Save("perma", "Perma", s.Snapshot());
        var loaded = store.Load("perma");
        var again = loaded.Roster.Single(c => c.Id == victim.Id);
        Assert.True(again.IsDead);
        var s2 = new GameSession(loaded, GameData.Default);
        Assert.False(s2.Crew.Assign(CrewRole.Science, again.Id).Ok);
        Assert.False(s2.Crew.Train(again.Id, Skill.Science).Ok);
    }

    [Fact]
    public void OlderSchema_IsMigrated()
    {
        var s = TestHelpers.ReadyToLaunch();
        var node = JsonNode.Parse(GameJson.Serialize(s.State))!.AsObject();
        node.Remove("log");
        var env = new JsonObject { ["format"] = SaveStore.FormatTag, ["schemaVersion"] = 1, ["state"] = node };
        var gs = SaveStore.FromEnvelope(env);
        Assert.NotNull(gs.Log);
        Assert.Empty(gs.Log);
    }

    [Fact]
    public void NewerSchema_IsRejectedWithClearMessage()
    {
        var env = new JsonObject { ["format"] = SaveStore.FormatTag, ["schemaVersion"] = 99, ["state"] = new JsonObject() };
        var ex = Assert.Throws<SaveLoadException>(() => SaveStore.FromEnvelope(env));
        Assert.Contains("newer", ex.Message);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("[]")]
    [InlineData("{\"format\":\"Something.Else\",\"schemaVersion\":2}")]
    [InlineData("{\"format\":\"AVAFlight.Save\",\"schemaVersion\":2,\"state\":{\"credits\":-5}}")]
    [InlineData("")]
    public void CorruptSave_GivesClearError_NotCrash(string content)
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        paths.EnsureCreated();
        File.WriteAllText(Path.Combine(paths.Saves, "bad.avasave"), content);
        var store = new SaveStore(paths);
        var ex = Assert.Throws<SaveLoadException>(() => store.Load("bad"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.Single(store.List()); // listed (possibly as corrupt) rather than crashing the list
    }

    [Fact]
    public void Settings_CorruptFileFallsBackToDefaults_AndValuesAreClamped()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        paths.EnsureCreated();
        File.WriteAllText(paths.SettingsFile, "garbage");
        var store = new SettingsStore(paths);
        Assert.Equal(1.0, store.Load().FontScale);
        store.Save(new GameSettings { FontScale = 9, MusicVolume = -1 });
        var s = store.Load();
        Assert.Equal(1.6, s.FontScale);
        Assert.Equal(0, s.MusicVolume);
    }
}

public class AudioTests
{
    [Fact]
    public void SoundBank_RendersEveryCueAndEffect_Deterministically_WithinRange()
    {
        foreach (var cue in Enum.GetValues<MusicCue>())
        {
            var a = SoundBank.RenderMusic(cue).Samples;
            Assert.Equal(a, SoundBank.RenderMusic(cue).Samples);
            Assert.All(a, v => Assert.InRange(v, -1f, 1f));
        }
        foreach (var fx in Enum.GetValues<SoundEffect>())
        {
            var b = SoundBank.RenderEffect(fx);
            Assert.NotEmpty(b);
            Assert.All(b, v => Assert.False(float.IsNaN(v)));
        }
    }

    [Fact]
    public void Mixer_RapidTriggering_NeverExceedsVoiceLimit_OrClips()
    {
        var m = new Mixer();
        var buf = new float[1024];
        for (int i = 0; i < 500; i++)
        {
            m.QueueEffect(SoundEffect.Laser);
            m.QueueEffect(SoundEffect.Explosion);
            if (i % 50 == 0) m.QueueMusic(i % 100 == 0 ? MusicCue.Combat : MusicCue.Hyperspace);
            m.Mix(buf);
            Assert.True(m.ActiveEffectCount <= Mixer.MaxVoices);
            Assert.All(buf, v => Assert.InRange(v, -1f, 1f));
        }
    }

    [Fact]
    public void SilentEngine_WithoutDevice_DoesNotThrow()
    {
        using var e = AudioEngine.CreateSilent("test: no device");
        Assert.False(e.IsAvailable);
        for (int i = 0; i < 100; i++) { e.Play(SoundEffect.MenuMove); e.PlayMusic((MusicCue)(i % 9)); }
        e.MusicVolume = 2;
        Assert.Equal(1, e.MusicVolume);
    }

    [Fact]
    public void EngineCreation_NeverThrows_EvenWhenNativeAudioIsMissing()
    {
        using var e = AudioEngine.Create(enabled: true);
        Assert.False(string.IsNullOrEmpty(e.Status));
        e.Play(SoundEffect.Laser);
    }
}

public class InputTests
{
    [Fact]
    public void HeldConfirm_FiresOnce_UntilReleased()
    {
        long now = 0;
        var m = new InputMapper(DefaultBindings.Create(), () => now);
        Assert.Equal(InputAction.Confirm, m.Press(InputDevice.Keyboard, "Enter"));
        for (int i = 0; i < 20; i++) { now += 50; Assert.Equal(InputAction.None, m.Press(InputDevice.Keyboard, "Enter")); }
        m.Release(InputDevice.Keyboard, "Enter");
        Assert.Equal(InputAction.Confirm, m.Press(InputDevice.Keyboard, "Enter"));
    }

    [Fact]
    public void HeldDirection_RepeatsAfterDelay_AtControlledRate()
    {
        long now = 0;
        var m = new InputMapper(DefaultBindings.Create(), () => now);
        int fired = 0;
        if (m.Press(InputDevice.Keyboard, "Down") == InputAction.Down) fired++;
        for (int t = 10; t <= 1000; t += 10) { now = t; if (m.Press(InputDevice.Keyboard, "Down") == InputAction.Down) fired++; }
        // 1 initial + repeats every 90 ms after a 350 ms delay: (1000-350)/90 + 1 ≈ 8.
        Assert.InRange(fired, 7, 10);
    }

    [Fact]
    public void Gamepad_AndKeyboard_MapToSameActions_AndRebindWorks()
    {
        var m = new InputMapper(DefaultBindings.Create());
        Assert.Equal(InputAction.Confirm, m.Lookup(InputDevice.Gamepad, "South"));
        Assert.Equal(InputAction.Back, m.Lookup(InputDevice.Gamepad, "East"));
        m.Rebind([new InputBinding(InputDevice.Keyboard, "Q", InputAction.Back)]);
        Assert.Equal(InputAction.Back, m.Lookup(InputDevice.Keyboard, "Q"));
        Assert.Equal(InputAction.None, m.Lookup(InputDevice.Keyboard, "Escape"));
    }

    [Fact]
    public void Deadzone_ZeroesSmallInputs_AndRescales()
    {
        Assert.Equal((0.0, 0.0), GamepadService.ApplyDeadzone(0.1, 0.1, 0.25));
        var (x, y) = GamepadService.ApplyDeadzone(1, 0, 0.25);
        Assert.Equal(1, x, 6);
        Assert.Equal(0, y, 6);
    }
}
