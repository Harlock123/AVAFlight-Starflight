using SkiaSharp;

namespace AVAFlight.Avalonia.Rendering;

/// <summary>Animated parallax star field used behind the title screen.</summary>
public sealed class StarfieldView : SkiaView
{
    private readonly record struct Star(float X, float Y, float Z, SKColor Color);

    private readonly Star[] _stars;
    private readonly SKPaint _paint = new() { IsAntialias = true };

    public StarfieldView()
    {
        Animated = true;
        // Fixed seed: the title star field looks the same every launch (and in screenshots).
        var rng = new AVAFlight.Core.Random.SplitMix64(1986);
        SKColor[] tints = [Ega.White, Ega.LightGray, Ega.LightCyan, Ega.Yellow, Ega.LightBlue, Ega.DarkGray];
        _stars = new Star[420];
        for (int i = 0; i < _stars.Length; i++)
            _stars[i] = new Star((float)rng.NextDouble(), (float)rng.NextDouble(),
                0.15f + (float)rng.NextDouble() * 0.85f, tints[rng.Next(tints.Length)]);
    }

    protected override void RenderSkia(SKCanvas canvas, SKSize size)
    {
        canvas.Clear(Ega.Black);
        float t = (float)Time;
        foreach (var s in _stars)
        {
            // Slow drift to the left; nearer stars (higher Z) move faster and look bigger.
            float x = (s.X - t * 0.012f * s.Z) % 1f;
            if (x < 0) x += 1f;
            float r = 0.5f + s.Z * 1.4f;
            _paint.Color = s.Color.WithAlpha((byte)(90 + 165 * s.Z));
            canvas.DrawCircle(x * size.Width, s.Y * size.Height, r, _paint);
        }
    }
}
