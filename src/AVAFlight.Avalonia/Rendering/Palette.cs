using Avalonia.Platform;
using AVAFlight.Core.Galaxy;
using SkiaSharp;

namespace AVAFlight.Avalonia.Rendering;

/// <summary>Colour mapping and shared Skia text helpers for the game's canvas views.</summary>
public static class Palette
{
    private static SKTypeface? _data, _title;

    public static SKTypeface DataTypeface => _data ??= Load("VT323-Regular.ttf");
    public static SKTypeface TitleTypeface => _title ??= Load("PressStart2P-Regular.ttf");

    private static SKTypeface Load(string file)
    {
        try
        {
            using var s = AssetLoader.Open(new Uri($"avares://AVAFlight/Assets/Fonts/{file}"));
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return SKTypeface.FromStream(ms) ?? SKTypeface.Default;
        }
        catch (Exception)
        {
            return SKTypeface.Default;
        }
    }

    public static SKColor Star(SpectralClass c) => c switch
    {
        SpectralClass.O => Ega.LightBlue,
        SpectralClass.B => Ega.LightCyan,
        SpectralClass.A => Ega.White,
        SpectralClass.F => new SKColor(0xFF, 0xFF, 0xAA),
        SpectralClass.G => Ega.Yellow,
        SpectralClass.K => new SKColor(0xFF, 0xAA, 0x55),
        _ => Ega.LightRed,
    };

    public static SKColor PlanetColor(PlanetType t) => t switch
    {
        PlanetType.Rock => Ega.LightGray,
        PlanetType.Frozen => Ega.LightCyan,
        PlanetType.Molten => Ega.LightRed,
        PlanetType.Ocean => Ega.LightBlue,
        PlanetType.Jungle => Ega.Green,
        PlanetType.Desert => Ega.Brown,
        PlanetType.GasGiant => Ega.LightMagenta,
        PlanetType.Asteroid => Ega.DarkGray,
        _ => Ega.White,
    };

    public static SKColor Terrain(TerrainKind k, PlanetType planet) => k switch
    {
        TerrainKind.Water => Ega.Blue,
        TerrainKind.Liquid => planet == PlanetType.Frozen ? Ega.Cyan : Ega.Magenta,
        TerrainKind.Lava => Ega.Red,
        TerrainKind.Ice => Ega.White,
        TerrainKind.Sand => new SKColor(0xCC, 0xAA, 0x55),
        TerrainKind.Lowland => planet is PlanetType.Molten ? Ega.DarkGray : Ega.Brown,
        TerrainKind.Vegetation => Ega.Green,
        TerrainKind.Highland => Ega.DarkGray,
        TerrainKind.Mountain => Ega.LightGray,
        TerrainKind.Crystal => Ega.LightCyan,
        _ => Ega.Magenta,
    };

    /// <summary>Terrain colour shaded by elevation for relief.</summary>
    public static SKColor TerrainShaded(TerrainKind k, PlanetType planet, float elevation)
    {
        var c = Terrain(k, planet);
        float f = 0.55f + 0.7f * elevation;
        return new SKColor((byte)Math.Min(255, c.Red * f), (byte)Math.Min(255, c.Green * f), (byte)Math.Min(255, c.Blue * f));
    }

    public static SKPaint Fill(SKColor c) => new() { Color = c, IsAntialias = true, Style = SKPaintStyle.Fill };
    public static SKPaint Stroke(SKColor c, float w = 1) => new() { Color = c, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = w };

    public static void Text(SKCanvas canvas, string text, float x, float y, float size, SKColor color, SKTextAlign align = SKTextAlign.Left, bool title = false)
    {
        using var font = new SKFont(title ? TitleTypeface : DataTypeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, x, y, align, font, paint);
    }
}
