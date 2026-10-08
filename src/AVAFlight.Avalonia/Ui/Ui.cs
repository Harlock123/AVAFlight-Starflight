using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AVAFlight.Avalonia.Ui;

/// <summary>
/// Theme and control factories. AVAFlight builds most screens in C# (Avalonia supports code-built
/// UI natively), which keeps each screen's layout next to its behaviour. Colours come from the EGA
/// palette; text sizes are multiplied by the user's font scale.
/// </summary>
public static class Ui
{
    public static readonly FontFamily DataFont = new("avares://AVAFlight/Assets/Fonts#VT323");
    public static readonly FontFamily TitleFont = new("avares://AVAFlight/Assets/Fonts#Press Start 2P");
    public static readonly FontFamily TextFont = new("avares://AVAFlight/Assets/Fonts#IBM Plex Sans");
    public static readonly FontFamily MonoFont = new("avares://AVAFlight/Assets/Fonts#IBM Plex Mono");

    public static double FontScale { get; set; } = 1.0;
    public static bool HighContrast { get; set; }

    public static IBrush Bg => Brushes.Black;
    public static IBrush Panel => new SolidColorBrush(Color.FromRgb(0x00, 0x00, HighContrast ? (byte)0x00 : (byte)0x2A));
    public static IBrush Border => new SolidColorBrush(HighContrast ? Colors.White : Color.FromRgb(0x00, 0xAA, 0xAA));
    public static IBrush Text => new SolidColorBrush(HighContrast ? Colors.White : Color.FromRgb(0xAA, 0xAA, 0xAA));
    public static IBrush Bright => Brushes.White;
    public static IBrush Accent => new SolidColorBrush(Color.FromRgb(0x55, 0xFF, 0xFF));
    public static IBrush Highlight => new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x55));
    public static IBrush Good => new SolidColorBrush(Color.FromRgb(0x55, 0xFF, 0x55));
    public static IBrush Bad => new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
    public static IBrush Dim => new SolidColorBrush(HighContrast ? Colors.LightGray : Color.FromRgb(0x55, 0x55, 0x55));
    public static IBrush SelectedBg => new SolidColorBrush(HighContrast ? Color.FromRgb(0x40, 0x40, 0x00) : Color.FromRgb(0x00, 0x00, 0xAA));

    public static double S(double size) => Math.Round(size * FontScale, 1);

    public static TextBlock Label(string text, double size = 20, IBrush? color = null, FontFamily? font = null, FontWeight weight = FontWeight.Normal) =>
        new()
        {
            Text = text, FontSize = S(size), Foreground = color ?? Text, FontFamily = font ?? DataFont,
            FontWeight = weight, TextWrapping = TextWrapping.Wrap,
        };

    public static TextBlock Heading(string text, double size = 14, IBrush? color = null) =>
        new() { Text = text, FontSize = S(size), Foreground = color ?? Accent, FontFamily = TitleFont, Margin = new Thickness(0, 4, 0, 8), TextWrapping = TextWrapping.Wrap };

    public static TextBlock Prose(string text, double size = 16, IBrush? color = null) =>
        new() { Text = text, FontSize = S(size), Foreground = color ?? Bright, FontFamily = TextFont, TextWrapping = TextWrapping.Wrap, LineHeight = S(size) * 1.4 };

    public static Border Box(Control child, double pad = 8, IBrush? border = null) =>
        new()
        {
            Child = child, Background = Panel, BorderBrush = border ?? Border, BorderThickness = new Thickness(2),
            Padding = new Thickness(pad), CornerRadius = new CornerRadius(2),
        };

    public static StackPanel VStack(double spacing = 4, params Control[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    public static StackPanel HStack(double spacing = 8, params Control[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    /// <summary>A "label: value" row for dense data displays.</summary>
    public static Grid Row(string label, string value, IBrush? valueColor = null, double size = 20, string? tooltip = null)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var l = Label(label + " ", size, Dim);
        var v = Label(value, size, valueColor ?? Bright);
        v.HorizontalAlignment = HorizontalAlignment.Right;
        v.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(v, 1);
        g.Children.Add(l);
        g.Children.Add(v);
        if (tooltip is not null) ToolTip.SetTip(g, tooltip);
        return g;
    }

    public static Button Button(string text, Action onClick, double size = 18)
    {
        var b = new Button
        {
            Content = Label(text, size, Bright), Background = SelectedBg, BorderBrush = Border, BorderThickness = new Thickness(2),
            Padding = new Thickness(10, 2), Margin = new Thickness(2), CornerRadius = new CornerRadius(0), Focusable = false,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    public static string Money(int mu) => $"{mu:N0} M.U.";
}
