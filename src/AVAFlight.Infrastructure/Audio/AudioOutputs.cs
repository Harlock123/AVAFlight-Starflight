using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace AVAFlight.Infrastructure.Audio;

public interface IAudioOutput : IDisposable
{
    bool IsRunning { get; }
    string Status { get; }
    void Start(Mixer mixer);
}

/// <summary>Output that discards audio. Used when audio is disabled or no device exists.</summary>
public sealed class NullAudioOutput(string status) : IAudioOutput
{
    public bool IsRunning => false;
    public string Status { get; } = status;
    public void Start(Mixer mixer) { }
    public void Dispose() { }
}

/// <summary>
/// SDL3 audio stream output. Only the SDL audio subsystem is initialised; Avalonia owns all
/// windowing. A dedicated thread keeps ~60 ms of mixed audio queued in the stream, so the UI
/// thread never blocks on audio.
/// </summary>
public sealed unsafe class Sdl3AudioOutput : IAudioOutput
{
    private const int TargetQueuedFrames = Synth.SampleRate * 60 / 1000;
    private const int ChunkFrames = 512;

    private SDL_AudioStream* _stream;
    private Thread? _thread;
    private volatile bool _stop;

    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "Not started";

    public Sdl3AudioOutput()
    {
        if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO))
        {
            Status = "SDL audio init failed: " + SDL_GetError();
            return;
        }
        var spec = new SDL_AudioSpec { format = SDL_AudioFormat.SDL_AUDIO_F32LE, channels = 2, freq = Synth.SampleRate };
        _stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, null, IntPtr.Zero);
        if (_stream == null)
        {
            Status = "No audio device: " + SDL_GetError();
            SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO);
            return;
        }
        IsRunning = true;
        Status = "SDL3 audio, 44.1 kHz stereo";
    }

    public void Start(Mixer mixer)
    {
        if (!IsRunning) return;
        SDL_ResumeAudioStreamDevice(_stream);
        _thread = new Thread(() => Pump(mixer)) { IsBackground = true, Name = "AVAFlight audio", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    private void Pump(Mixer mixer)
    {
        var buffer = new float[ChunkFrames * 2];
        while (!_stop)
        {
            try
            {
                int queuedFrames = SDL_GetAudioStreamQueued(_stream) / (sizeof(float) * 2);
                if (queuedFrames < TargetQueuedFrames)
                {
                    mixer.Mix(buffer);
                    fixed (float* p = buffer)
                        SDL_PutAudioStreamData(_stream, (IntPtr)p, buffer.Length * sizeof(float));
                    continue;
                }
            }
            catch (Exception e)
            {
                Status = "Audio thread stopped: " + e.Message;
                IsRunning = false;
                return;
            }
            Thread.Sleep(4);
        }
    }

    public void Dispose()
    {
        _stop = true;
        _thread?.Join(500);
        if (_stream != null)
        {
            SDL_DestroyAudioStream(_stream);
            _stream = null;
            SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO);
        }
        IsRunning = false;
    }
}
