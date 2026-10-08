using static AVAFlight.Infrastructure.Audio.Synth;

namespace AVAFlight.Infrastructure.Audio;

public enum MusicCue { None, Title, Starport, Hyperspace, System, Planet, Comms, Combat, GameOver, Victory }

public enum SoundEffect
{
    MenuMove, MenuSelect, MenuBack, Error,
    Laser, Missile, ShieldHit, HullHit, Explosion,
    CommsChime, Artifact, MineralPickup, Scan, Warning,
    HyperspaceJump, Launch, Land, Purchase, EngineHum,
}

/// <summary>
/// All AVAFlight audio, newly composed and synthesized procedurally (see docs/THIRD_PARTY.md:
/// no sampled or original-game audio is used). Rendering is deterministic: the same build always
/// produces bit-identical buffers, which the tests rely on.
/// </summary>
public static class SoundBank
{
    public sealed record Clip(float[] Samples, bool Loop);

    public static Clip RenderMusic(MusicCue cue) => cue switch
    {
        MusicCue.Title => new(Title(), true),
        MusicCue.Starport => new(Starport(), true),
        MusicCue.Hyperspace => new(Hyperspace(), true),
        MusicCue.System => new(SystemAmbient(), true),
        MusicCue.Planet => new(Planet(), true),
        MusicCue.Comms => new(Comms(), true),
        MusicCue.Combat => new(Combat(), true),
        MusicCue.GameOver => new(GameOver(), false),
        MusicCue.Victory => new(Victory(), false),
        _ => new(new float[Samples(0.1)], false),
    };

    // ---------------------------------------------------------------- music
    // Each loop is built from a bass line, a chord pad and a lead, all original material.

    private static float[] Title()
    {
        const double bpm = 84;
        var buf = new float[Samples(60.0 / bpm * 32)];
        string pad = "A2+E3+A3:8 F2+C3+A3:8 G2+D3+B3:8 E2+B2+G#3:8";
        RenderPattern(buf, 0, pad, bpm, Wave.Triangle, 0.16f, attack: 0.6, release: 0.8, sustain: 0.9, legato: 1.0);
        string lead = "E4:2 A4:2 B4:1 C5:1 B4:2 A4:4 C5:2 B4:2 " +
                      "D5:2 B4:2 G4:4 E4:2 G#4:2 B4:2 E5:2 R:4";
        RenderPattern(buf, 0, lead, bpm, Wave.Pulse25, 0.10f, attack: 0.02, release: 0.3, sustain: 0.6);
        string sparkle = string.Concat(Enumerable.Repeat("A5:0.5 E5:0.5 R:3 ", 8));
        RenderPattern(buf, 0, sparkle, bpm, Wave.Sine, 0.05f, release: 0.4);
        return Finish(buf, 2600);
    }

    private static float[] Starport()
    {
        const double bpm = 104;
        var buf = new float[Samples(60.0 / bpm * 32)];
        string bass = string.Concat(Enumerable.Repeat("C3:1 G3:1 C3:1 G3:1 ", 2)) +
                      string.Concat(Enumerable.Repeat("A2:1 E3:1 A2:1 E3:1 ", 2)) +
                      string.Concat(Enumerable.Repeat("F2:1 C3:1 F2:1 C3:1 ", 2)) +
                      string.Concat(Enumerable.Repeat("G2:1 D3:1 G2:1 D3:1 ", 2));
        RenderPattern(buf, 0, bass, bpm, Wave.Triangle, 0.22f, sustain: 0.5);
        string arp = string.Concat(Enumerable.Repeat("C4:0.5 E4:0.5 G4:0.5 E4:0.5 ", 4)) +
                     string.Concat(Enumerable.Repeat("A3:0.5 C4:0.5 E4:0.5 C4:0.5 ", 4)) +
                     string.Concat(Enumerable.Repeat("F3:0.5 A3:0.5 C4:0.5 A3:0.5 ", 4)) +
                     string.Concat(Enumerable.Repeat("G3:0.5 B3:0.5 D4:0.5 B3:0.5 ", 4));
        RenderPattern(buf, 0, arp, bpm, Wave.Square, 0.05f, sustain: 0.4, legato: 0.6);
        string lead = "G4:3 E4:1 C5:4 A4:3 G4:1 E4:4 F4:2 A4:2 C5:2 D5:2 B4:6 R:2";
        RenderPattern(buf, 0, lead, bpm, Wave.Pulse25, 0.08f, attack: 0.02, release: 0.2);
        return Finish(buf, 3000);
    }

    private static float[] Hyperspace()
    {
        const double bpm = 60;
        var buf = new float[Samples(60.0 / bpm * 32)];
        // Slow drone with a wandering minor-mode line: vast, quiet, slightly uneasy.
        RenderPattern(buf, 0, "D2+A2:16 C2+G2:8 Bb1+F2:8", bpm, Wave.Triangle, 0.2f, attack: 2, release: 2, sustain: 1, legato: 1);
        RenderPattern(buf, 0, "D4:4 F4:4 E4:6 A3:2 Bb3:4 C4:4 A3:8", bpm, Wave.Sine, 0.12f, attack: 0.8, release: 1.5, sustain: 0.8);
        RenderPattern(buf, 0, string.Concat(Enumerable.Repeat("R:3 A5:0.25 R:0.75 ", 8)), bpm, Wave.Sine, 0.04f, release: 0.6);
        return Finish(buf, 1800);
    }

    private static float[] SystemAmbient()
    {
        const double bpm = 72;
        var buf = new float[Samples(60.0 / bpm * 32)];
        RenderPattern(buf, 0, "E2+B2:8 C2+G2:8 D2+A2:8 B1+F#2:8", bpm, Wave.Triangle, 0.2f, attack: 1, release: 1.5, sustain: 1, legato: 1);
        RenderPattern(buf, 0, string.Concat(Enumerable.Repeat("E4:0.5 B4:0.5 G4:0.5 B4:0.5 ", 4)) +
                              string.Concat(Enumerable.Repeat("C4:0.5 G4:0.5 E4:0.5 G4:0.5 ", 4)) +
                              string.Concat(Enumerable.Repeat("D4:0.5 A4:0.5 F#4:0.5 A4:0.5 ", 4)) +
                              string.Concat(Enumerable.Repeat("B3:0.5 F#4:0.5 D4:0.5 F#4:0.5 ", 4)),
            bpm, Wave.Sine, 0.06f, release: 0.25, sustain: 0.4);
        return Finish(buf, 2200);
    }

    private static float[] Planet()
    {
        const double bpm = 90;
        var buf = new float[Samples(60.0 / bpm * 32)];
        RenderPattern(buf, 0, "A1:8 G1:8 F1:8 E1:8", bpm, Wave.Saw, 0.12f, attack: 0.5, release: 1, sustain: 1, legato: 1);
        string pulse = string.Concat(Enumerable.Repeat("A2:0.5 R:0.5 A2:0.25 R:0.75 ", 16));
        RenderPattern(buf, 0, pulse, bpm, Wave.Triangle, 0.18f, sustain: 0.3);
        RenderPattern(buf, 0, "R:4 E4:2 D4:2 C4:4 R:4 B3:2 C4:2 E4:4 R:4 A3:4", bpm, Wave.Pulse25, 0.06f, attack: 0.05, release: 0.3);
        return Finish(buf, 2000);
    }

    private static float[] Comms()
    {
        const double bpm = 80;
        var buf = new float[Samples(60.0 / bpm * 16)];
        RenderPattern(buf, 0, "F2+C3:8 Eb2+Bb2:8", bpm, Wave.Triangle, 0.18f, attack: 1, release: 1, sustain: 1, legato: 1);
        RenderPattern(buf, 0, string.Concat(Enumerable.Repeat("C5:0.25 R:0.25 G4:0.25 R:1.25 ", 8)), bpm, Wave.Sine, 0.07f, release: 0.2);
        return Finish(buf, 2400);
    }

    private static float[] Combat()
    {
        const double bpm = 150;
        var buf = new float[Samples(60.0 / bpm * 32)];
        string bass = string.Concat(Enumerable.Repeat("E2:0.5 E2:0.5 E3:0.5 E2:0.5 ", 4)) +
                      string.Concat(Enumerable.Repeat("C2:0.5 C2:0.5 C3:0.5 C2:0.5 ", 2)) +
                      string.Concat(Enumerable.Repeat("D2:0.5 D2:0.5 D3:0.5 D2:0.5 ", 2));
        RenderPattern(buf, 0, bass + bass, bpm, Wave.Square, 0.09f, sustain: 0.5, legato: 0.7);
        string lead = "E4:1.5 G4:0.5 B4:2 A4:1 G4:1 F#4:2 E4:1.5 G4:0.5 C5:2 B4:4 " +
                      "E5:1.5 D5:0.5 B4:2 C5:1 A4:1 B4:2 G4:1 F#4:1 E4:6";
        RenderPattern(buf, 0, lead, bpm, Wave.Pulse25, 0.08f, attack: 0.01, release: 0.1);
        for (int beat = 0; beat < 32; beat++) // snare-ish noise on off-beats
            if (beat % 2 == 1) RenderTone(buf, Samples(60.0 / bpm * beat), Samples(0.08), 6000, Wave.Noise, 0.08f, release: 0.06);
        return Finish(buf, 3500);
    }

    private static float[] GameOver()
    {
        var buf = new float[Samples(6)];
        RenderPattern(buf, 0, "A3+E4:2 F3+C4:2 D3+A3:2 E3+G#3:4", 60, Wave.Triangle, 0.22f, attack: 0.1, release: 1.2, sustain: 0.8, legato: 1);
        RenderPattern(buf, 0, "E5:2 C5:2 A4:2 G#4:4", 60, Wave.Pulse25, 0.07f, attack: 0.05, release: 1);
        return Finish(buf, 2000);
    }

    private static float[] Victory()
    {
        var buf = new float[Samples(10)];
        RenderPattern(buf, 0, "C3+G3:2 F3+C4:2 G3+D4:2 C3+G3+E4:6", 72, Wave.Triangle, 0.22f, attack: 0.2, release: 1.5, sustain: 0.9, legato: 1);
        RenderPattern(buf, 0, "G4:1 C5:1 E5:1 D5:1 G5:2 F5:1 E5:1 D5:1 E5:1 C5:4", 72, Wave.Pulse25, 0.09f, attack: 0.02, release: 0.5);
        return Finish(buf, 3000);
    }

    private static float[] Finish(float[] buf, double lowPassHz)
    {
        LowPass(buf, lowPassHz);
        Normalize(buf, 0.7f);
        return buf;
    }

    // ---------------------------------------------------------------- effects

    public static float[] RenderEffect(SoundEffect fx)
    {
        float[] b;
        switch (fx)
        {
            case SoundEffect.MenuMove:
                b = new float[Samples(0.04)]; RenderTone(b, 0, b.Length, 880, Wave.Square, 0.3f, release: 0.02); break;
            case SoundEffect.MenuSelect:
                b = new float[Samples(0.12)];
                RenderTone(b, 0, Samples(0.05), 660, Wave.Square, 0.3f, release: 0.01);
                RenderTone(b, Samples(0.05), Samples(0.07), 990, Wave.Square, 0.3f, release: 0.03); break;
            case SoundEffect.MenuBack:
                b = new float[Samples(0.1)]; RenderTone(b, 0, b.Length, 660, Wave.Square, 0.3f, freqEnd: 330); break;
            case SoundEffect.Error:
                b = new float[Samples(0.25)]; RenderTone(b, 0, b.Length, 140, Wave.Square, 0.35f, release: 0.05); break;
            case SoundEffect.Laser:
                b = new float[Samples(0.22)]; RenderTone(b, 0, b.Length, 1800, Wave.Square, 0.35f, release: 0.08, freqEnd: 300); break;
            case SoundEffect.Missile:
                b = new float[Samples(0.6)];
                RenderTone(b, 0, b.Length, 900, Wave.Noise, 0.25f, attack: 0.05, release: 0.3, freqEnd: 3000, noiseSeed: 7);
                RenderTone(b, 0, b.Length, 200, Wave.Saw, 0.15f, freqEnd: 500, release: 0.3); break;
            case SoundEffect.ShieldHit:
                b = new float[Samples(0.3)];
                RenderTone(b, 0, b.Length, 400, Wave.Sine, 0.4f, release: 0.25, freqEnd: 900);
                RenderTone(b, 0, b.Length, 5000, Wave.Noise, 0.12f, release: 0.2, noiseSeed: 3); break;
            case SoundEffect.HullHit:
                b = new float[Samples(0.35)]; RenderTone(b, 0, b.Length, 1500, Wave.Noise, 0.5f, release: 0.3, freqEnd: 300, noiseSeed: 11); break;
            case SoundEffect.Explosion:
                b = new float[Samples(1.2)];
                RenderTone(b, 0, b.Length, 2500, Wave.Noise, 0.6f, attack: 0.002, decay: 0.3, sustain: 0.4, release: 0.9, freqEnd: 150, noiseSeed: 5);
                RenderTone(b, 0, b.Length, 90, Wave.Sine, 0.4f, release: 0.9, freqEnd: 30);
                LowPass(b, 4000); break;
            case SoundEffect.CommsChime:
                b = new float[Samples(0.6)];
                RenderTone(b, 0, Samples(0.3), 1047, Wave.Sine, 0.35f, release: 0.2);
                RenderTone(b, Samples(0.15), Samples(0.45), 1568, Wave.Sine, 0.3f, release: 0.35); break;
            case SoundEffect.Artifact:
                b = new float[Samples(0.9)];
                string[] notes = ["C5", "E5", "G5", "C6", "E6"];
                for (int i = 0; i < notes.Length; i++)
                    RenderTone(b, Samples(0.1 * i), Samples(0.45), NoteFrequency(notes[i]), Wave.Sine, 0.25f, release: 0.35);
                break;
            case SoundEffect.MineralPickup:
                b = new float[Samples(0.15)]; RenderTone(b, 0, b.Length, 500, Wave.Pulse25, 0.3f, freqEnd: 1200, release: 0.04); break;
            case SoundEffect.Scan:
                b = new float[Samples(0.8)];
                for (int i = 0; i < 4; i++) RenderTone(b, Samples(0.2 * i), Samples(0.12), 1200 + 200 * i, Wave.Sine, 0.25f, release: 0.06);
                break;
            case SoundEffect.Warning:
                b = new float[Samples(0.6)];
                RenderTone(b, 0, Samples(0.25), 880, Wave.Square, 0.25f);
                RenderTone(b, Samples(0.3), Samples(0.25), 880, Wave.Square, 0.25f); break;
            case SoundEffect.HyperspaceJump:
                b = new float[Samples(1.4)];
                RenderTone(b, 0, b.Length, 80, Wave.Saw, 0.3f, attack: 0.3, release: 0.4, freqEnd: 1600);
                RenderTone(b, 0, b.Length, 400, Wave.Noise, 0.08f, attack: 0.5, release: 0.4, freqEnd: 6000, noiseSeed: 9);
                LowPass(b, 5000); break;
            case SoundEffect.Launch:
                b = new float[Samples(1.5)];
                RenderTone(b, 0, b.Length, 300, Wave.Noise, 0.4f, attack: 0.2, release: 0.8, freqEnd: 2000, noiseSeed: 13);
                RenderTone(b, 0, b.Length, 60, Wave.Saw, 0.2f, attack: 0.2, release: 0.6, freqEnd: 200);
                LowPass(b, 3000); break;
            case SoundEffect.Land:
                b = new float[Samples(1.3)];
                RenderTone(b, 0, b.Length, 1800, Wave.Noise, 0.35f, attack: 0.1, release: 0.5, freqEnd: 200, noiseSeed: 17);
                LowPass(b, 2500); break;
            case SoundEffect.Purchase:
                b = new float[Samples(0.25)];
                RenderTone(b, 0, Samples(0.08), 1319, Wave.Square, 0.2f, release: 0.02);
                RenderTone(b, Samples(0.09), Samples(0.15), 1760, Wave.Square, 0.2f, release: 0.08); break;
            case SoundEffect.EngineHum:
                b = new float[Samples(1.0)];
                RenderTone(b, 0, b.Length, 55, Wave.Saw, 0.3f, attack: 0.1, release: 0.1, sustain: 1);
                RenderTone(b, 0, b.Length, 110, Wave.Triangle, 0.15f, attack: 0.1, release: 0.1, sustain: 1);
                LowPass(b, 600); break;
            default:
                b = new float[Samples(0.05)]; break;
        }
        return b;
    }
}
