namespace AVAFlight.Infrastructure.Audio;

/// <summary>
/// Small deterministic chiptune synthesizer. All AVAFlight music and sound effects are generated
/// here at startup (no sampled audio is shipped), in the spirit of the PC-speaker / AdLib-era
/// original. Output is mono float samples in [-1, 1]; the mixer pans to stereo.
/// </summary>
public static class Synth
{
    public const int SampleRate = 44100;

    public enum Wave { Square, Pulse25, Triangle, Sine, Saw, Noise }

    /// <summary>Frequency of a note name such as "A4", "C#3", "Eb5". "R" is a rest (0 Hz).</summary>
    public static double NoteFrequency(string note)
    {
        if (note is "R" or "r" or "-") return 0;
        int i = 0;
        int semis = char.ToUpperInvariant(note[i++]) switch
        {
            'C' => -9, 'D' => -7, 'E' => -5, 'F' => -4, 'G' => -2, 'A' => 0, 'B' => 2,
            _ => throw new FormatException($"Bad note '{note}'"),
        };
        while (i < note.Length && note[i] is '#' or 'b')
            semis += note[i++] == '#' ? 1 : -1;
        int octave = int.Parse(note.AsSpan(i));
        return 440.0 * Math.Pow(2, (semis + (octave - 4) * 12) / 12.0);
    }

    /// <summary>Renders one oscillator note with an attack/decay/sustain/release envelope.</summary>
    public static void RenderTone(float[] buffer, int start, int length, double freq, Wave wave, float volume,
        double attack = 0.005, double release = 0.05, double sustain = 0.8, double decay = 0.08,
        double freqEnd = -1, uint noiseSeed = 1)
    {
        if (freq <= 0 && wave != Wave.Noise) return;
        if (freqEnd < 0) freqEnd = freq;
        double phase = 0;
        uint lfsr = noiseSeed == 0 ? 1u : noiseSeed;
        float noiseVal = 0;
        double noiseAcc = 0;
        int a = (int)(attack * SampleRate), d = (int)(decay * SampleRate), r = (int)(release * SampleRate);
        for (int n = 0; n < length && start + n < buffer.Length; n++)
        {
            double t = (double)n / Math.Max(1, length - 1);
            double f = freq + (freqEnd - freq) * t;
            phase += f / SampleRate;
            phase -= Math.Floor(phase);
            float s = wave switch
            {
                Wave.Square => phase < 0.5 ? 1f : -1f,
                Wave.Pulse25 => phase < 0.25 ? 1f : -1f,
                Wave.Triangle => (float)(4 * Math.Abs(phase - 0.5) - 1),
                Wave.Sine => (float)Math.Sin(phase * 2 * Math.PI),
                Wave.Saw => (float)(2 * phase - 1),
                _ => 0f,
            };
            if (wave == Wave.Noise)
            {
                // 16-bit Galois LFSR clocked at 'f' Hz gives classic chip noise colour.
                noiseAcc += Math.Max(f, 100) / SampleRate;
                while (noiseAcc >= 1)
                {
                    noiseAcc -= 1;
                    lfsr = (lfsr >> 1) ^ (uint)(-(int)(lfsr & 1u) & 0xB400u);
                    noiseVal = (lfsr & 1) == 1 ? 1f : -1f;
                }
                s = noiseVal;
            }
            double env;
            if (n < a) env = (double)n / Math.Max(1, a);
            else if (n < a + d) env = 1 - (1 - sustain) * (n - a) / Math.Max(1, d);
            else env = sustain;
            int fromEnd = length - n;
            if (fromEnd < r) env *= (double)fromEnd / Math.Max(1, r);
            buffer[start + n] += (float)(s * env * volume);
        }
    }

    /// <summary>
    /// Renders a pattern written as space-separated "Note:Beats" tokens, e.g. "C4:1 E4:0.5 R:0.5".
    /// Returns the number of samples the pattern spans.
    /// </summary>
    public static int RenderPattern(float[] buffer, int start, string pattern, double bpm, Wave wave, float volume,
        double attack = 0.005, double release = 0.06, double sustain = 0.7, double legato = 0.9)
    {
        double beatSamples = SampleRate * 60.0 / bpm;
        double pos = start;
        foreach (var token in pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':');
            double beats = parts.Length > 1 ? double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1;
            int len = (int)(beats * beatSamples);
            foreach (var chordNote in parts[0].Split('+'))
                RenderTone(buffer, (int)pos, (int)(len * legato), NoteFrequency(chordNote), wave, volume, attack, release, sustain);
            pos += len;
        }
        return (int)pos - start;
    }

    /// <summary>One-pole low-pass, used to soften square waves for modern ears.</summary>
    public static void LowPass(float[] buffer, double cutoffHz)
    {
        double rc = 1.0 / (2 * Math.PI * cutoffHz), dt = 1.0 / SampleRate, alpha = dt / (rc + dt);
        float y = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            y += (float)(alpha * (buffer[i] - y));
            buffer[i] = y;
        }
    }

    public static void Normalize(float[] buffer, float peak = 0.9f)
    {
        float max = 0;
        foreach (var s in buffer) max = Math.Max(max, Math.Abs(s));
        if (max < 1e-6f) return;
        float k = peak / max;
        for (int i = 0; i < buffer.Length; i++) buffer[i] *= k;
    }

    public static int Samples(double seconds) => (int)(seconds * SampleRate);
}
