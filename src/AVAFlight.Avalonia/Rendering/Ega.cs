using SkiaSharp;

namespace AVAFlight.Avalonia.Rendering;

/// <summary>
/// The standard 16-colour IBM EGA/CGA RGBI palette (default EGA palette registers).
/// AVAFlight draws its retro views from this palette so they read like the 1986 DOS original,
/// with modern anti-aliasing and resolution for legibility.
/// </summary>
public static class Ega
{
    public static readonly SKColor Black = new(0x00, 0x00, 0x00);
    public static readonly SKColor Blue = new(0x00, 0x00, 0xAA);
    public static readonly SKColor Green = new(0x00, 0xAA, 0x00);
    public static readonly SKColor Cyan = new(0x00, 0xAA, 0xAA);
    public static readonly SKColor Red = new(0xAA, 0x00, 0x00);
    public static readonly SKColor Magenta = new(0xAA, 0x00, 0xAA);
    public static readonly SKColor Brown = new(0xAA, 0x55, 0x00);
    public static readonly SKColor LightGray = new(0xAA, 0xAA, 0xAA);
    public static readonly SKColor DarkGray = new(0x55, 0x55, 0x55);
    public static readonly SKColor LightBlue = new(0x55, 0x55, 0xFF);
    public static readonly SKColor LightGreen = new(0x55, 0xFF, 0x55);
    public static readonly SKColor LightCyan = new(0x55, 0xFF, 0xFF);
    public static readonly SKColor LightRed = new(0xFF, 0x55, 0x55);
    public static readonly SKColor LightMagenta = new(0xFF, 0x55, 0xFF);
    public static readonly SKColor Yellow = new(0xFF, 0xFF, 0x55);
    public static readonly SKColor White = new(0xFF, 0xFF, 0xFF);

    public static readonly SKColor[] All =
    [
        Black, Blue, Green, Cyan, Red, Magenta, Brown, LightGray,
        DarkGray, LightBlue, LightGreen, LightCyan, LightRed, LightMagenta, Yellow, White,
    ];
}
