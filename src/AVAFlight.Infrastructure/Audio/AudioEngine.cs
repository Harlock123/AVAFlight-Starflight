using System.Collections.Concurrent;

namespace AVAFlight.Infrastructure.Audio;

public interface IAudioEngine : IDisposable
{
    bool IsAvailable { get; }
    string Status { get; }
    double MusicVolume { get; set; }
    double EffectsVolume { get; set; }
    MusicCue CurrentMusic { get; }
    void PlayMusic(MusicCue cue);
    void Play(SoundEffect effect);
}

/// <summary>
/// Software mixer. Thread-safe: game code calls <see cref="PlayMusic"/>/<see cref="Play"/> from any
/// thread; the output backend pulls mixed stereo frames via <see cref="Mix"/>. Music cross-fades
/// over ~0.8 s; at most <see cref="MaxVoices"/> effects play at once (oldest is stolen), so rapid
/// triggering can never grow memory or stall the audio thread.
/// </summary>
public sealed class Mixer
{
    public const int MaxVoices = 16;
    private const int FadeSamples = Synth.SampleRate * 8 / 10;

    private sealed class Voice(float[] data, bool loop, float gain)
    {
        public readonly float[] Data = data;
        public readonly bool Loop = loop;
        public float Gain = gain;
        public int Pos;
        public int FadeIn = FadeSamples;
        public int FadeOut = -1; // -1 = not fading out
    }

    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly List<Voice> _effects = new();
    private readonly List<Voice> _music = new(); // current + fading-out tracks
    private readonly Lazy<float[]>[] _effectCache;
    private readonly ConcurrentDictionary<MusicCue, SoundBank.Clip> _musicCache = new();

    public Mixer()
    {
        _effectCache = Enum.GetValues<SoundEffect>()
            .Select(e => new Lazy<float[]>(() => SoundBank.RenderEffect(e), LazyThreadSafetyMode.ExecutionAndPublication))
            .ToArray();
    }

    public volatile float MusicVolume = 0.6f;
    public volatile float EffectsVolume = 0.8f;
    public int ActiveEffectCount { get; private set; }

    public SoundBank.Clip GetMusic(MusicCue cue) => _musicCache.GetOrAdd(cue, SoundBank.RenderMusic);
    public float[] GetEffect(SoundEffect fx) => _effectCache[(int)fx].Value;

    public void QueueMusic(MusicCue cue)
    {
        var clip = cue == MusicCue.None ? null : GetMusic(cue); // render outside the audio thread
        _commands.Enqueue(() =>
        {
            foreach (var v in _music) if (v.FadeOut < 0) v.FadeOut = FadeSamples;
            if (clip is not null) _music.Add(new Voice(clip.Samples, clip.Loop, 1f));
        });
    }

    public void QueueEffect(SoundEffect fx)
    {
        var data = GetEffect(fx);
        _commands.Enqueue(() =>
        {
            if (_effects.Count >= MaxVoices) _effects.RemoveAt(0);
            _effects.Add(new Voice(data, false, 1f) { FadeIn = 0 });
        });
    }

    /// <summary>Fills <paramref name="stereo"/> with interleaved L/R float frames.</summary>
    public void Mix(Span<float> stereo)
    {
        while (_commands.TryDequeue(out var cmd)) cmd();
        stereo.Clear();
        int frames = stereo.Length / 2;
        float mv = MusicVolume, ev = EffectsVolume;

        for (int vi = _music.Count - 1; vi >= 0; vi--)
        {
            var v = _music[vi];
            for (int i = 0; i < frames; i++)
            {
                if (v.Pos >= v.Data.Length)
                {
                    if (!v.Loop) { v.FadeOut = 0; break; }
                    v.Pos = 0;
                }
                float g = mv * v.Gain;
                if (v.FadeIn > 0) { g *= 1f - (float)v.FadeIn / FadeSamples; v.FadeIn--; }
                if (v.FadeOut >= 0) { g *= (float)v.FadeOut / FadeSamples; if (v.FadeOut == 0) break; v.FadeOut--; }
                float s = v.Data[v.Pos++] * g;
                stereo[2 * i] += s;
                stereo[2 * i + 1] += s;
            }
            if (v.FadeOut == 0) _music.RemoveAt(vi);
        }

        for (int vi = _effects.Count - 1; vi >= 0; vi--)
        {
            var v = _effects[vi];
            int n = Math.Min(frames, v.Data.Length - v.Pos);
            for (int i = 0; i < n; i++)
            {
                float s = v.Data[v.Pos++] * ev;
                stereo[2 * i] += s;
                stereo[2 * i + 1] += s;
            }
            if (v.Pos >= v.Data.Length) _effects.RemoveAt(vi);
        }
        ActiveEffectCount = _effects.Count;

        for (int i = 0; i < stereo.Length; i++) stereo[i] = MathF.Tanh(stereo[i]); // soft clip
    }
}

/// <summary>Audio engine that drives a <see cref="Mixer"/> through a pluggable output.</summary>
public sealed class AudioEngine : IAudioEngine
{
    private readonly Mixer _mixer = new();
    private readonly IAudioOutput _output;

    private AudioEngine(IAudioOutput output) => _output = output;

    public Mixer Mixer => _mixer;
    public bool IsAvailable => _output.IsRunning;
    public string Status => _output.Status;
    public MusicCue CurrentMusic { get; private set; }

    public double MusicVolume
    {
        get => _mixer.MusicVolume;
        set => _mixer.MusicVolume = (float)Math.Clamp(value, 0, 1);
    }

    public double EffectsVolume
    {
        get => _mixer.EffectsVolume;
        set => _mixer.EffectsVolume = (float)Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Creates an engine on the best available output. Never throws: if SDL3 audio cannot be
    /// initialised (no device, missing native library, headless CI) the engine runs silently.
    /// </summary>
    public static AudioEngine Create(bool enabled = true)
    {
        IAudioOutput output = new NullAudioOutput("Audio disabled in settings.");
        if (enabled)
        {
            try
            {
                var sdl = new Sdl3AudioOutput();
                output = sdl.IsRunning ? sdl : new NullAudioOutput(sdl.Status);
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
            {
                output = new NullAudioOutput("Audio unavailable: " + e.Message);
            }
        }
        var engine = new AudioEngine(output);
        output.Start(engine._mixer);
        return engine;
    }

    /// <summary>Engine with no device at all (tests, --mute).</summary>
    public static AudioEngine CreateSilent(string reason = "Silent") => new(new NullAudioOutput(reason));

    public void PlayMusic(MusicCue cue)
    {
        if (cue == CurrentMusic) return;
        CurrentMusic = cue;
        try { _mixer.QueueMusic(cue); } catch (Exception) { /* audio must never crash the game */ }
    }

    public void Play(SoundEffect effect)
    {
        try { _mixer.QueueEffect(effect); } catch (Exception) { /* audio must never crash the game */ }
    }

    public void Dispose() => _output.Dispose();
}
