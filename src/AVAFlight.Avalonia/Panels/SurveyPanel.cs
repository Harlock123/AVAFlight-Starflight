using Avalonia;
using Avalonia.Controls;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Galaxy;

namespace AVAFlight.Avalonia.Panels;

/// <summary>
/// Planet data screen shown in orbit: sensor and analysis results (uncertain values read
/// "NOT CERTAIN", as in the original), plus visible surface sites and colony suitability.
/// </summary>
public sealed class SurveyPanel : UserControl
{
    private readonly GameSession _s;
    private readonly StackPanel _panel = new() { Spacing = 1 };
    private SensorReport? _sensors;
    private AnalysisReport? _analysis;
    private int _lastHash;

    public SurveyPanel(GameSession s)
    {
        _s = s;
        Content = new Border { Padding = new Thickness(10), Child = new ScrollViewer { Content = _panel } };
        var timer = new global::Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(250), global::Avalonia.Threading.DispatcherPriority.Background, (_, _) => Refresh());
        AttachedToVisualTree += (_, _) => timer.Start();
        DetachedFromVisualTree += (_, _) => timer.Stop();
        Refresh();
    }

    /// <summary>Keeps the last scan results so the panel shows what the crew actually measured.</summary>
    public static readonly Dictionary<int, (SensorReport? S, AnalysisReport? A)> Cache = new();

    private static string V(object? v) => v?.ToString() ?? "NOT CERTAIN";

    public void Refresh()
    {
        var p = _s.CurrentPlanet;
        if (p is null) return;
        var rec = _s.State.PlanetRecord(p.Id);
        Cache.TryGetValue(p.Id, out var cached);
        _sensors = cached.S; _analysis = cached.A;
        int hash = HashCode.Combine(rec.Scanned, rec.Analyzed, rec.Logged, _sensors, _analysis, _s.Messages.Count);
        if (hash == _lastHash) return;
        _lastHash = hash;
        _panel.Children.Clear();
        var sys = _s.Galaxy.System(p.SystemId);
        _panel.Children.Add(Ui.Ui.Heading($"PLANET {p.Orbit}" + (p.Name is not null ? " - " + p.Name.ToUpperInvariant() : ""), 12));
        _panel.Children.Add(Ui.Ui.Row("System", $"{sys.X},{sys.Y} class {sys.Class}", null, 19));
        _panel.Children.Add(Ui.Ui.Label("SENSORS", 19, Ui.Ui.Accent));
        if (!rec.Scanned) _panel.Children.Add(Ui.Ui.Label("Not scanned. Science > Sensors.", 18, Ui.Ui.Dim));
        else if (_sensors is { } se)
        {
            _panel.Children.Add(Ui.Ui.Row("Mass", V(se.Mass), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Bio density", se.BioPercent is int b ? b + "%" : V(null), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Mineral density", se.MineralPercent is int m ? m + "%" : V(null), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Atmosphere", V(se.Atmosphere), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Hydrosphere", V(se.Hydrosphere), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Lithosphere", V(se.Lithosphere), null, 19));
        }
        _panel.Children.Add(Ui.Ui.Label("ANALYSIS", 19, Ui.Ui.Accent));
        if (!rec.Analyzed) _panel.Children.Add(Ui.Ui.Label("Not analysed. Science > Analysis.", 18, Ui.Ui.Dim));
        else if (_analysis is { } an)
        {
            _panel.Children.Add(Ui.Ui.Row("Surface", V(an.Surface), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Gravity", an.Gravity is double g ? $"{g:0.00} G" : V(null), an.Gravity > 8 ? Ui.Ui.Bad : null, 19));
            _panel.Children.Add(Ui.Ui.Row("Atmos. density", V(an.AtmosphereDensity), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Composition", V(an.Composition), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Temperature", V(an.Temperature), null, 19));
            _panel.Children.Add(Ui.Ui.Row("Weather", V(an.Weather), null, 19));
            if (an.Gravity > 8) _panel.Children.Add(Ui.Ui.Label("WARNING: landing above 8 G will crush the ship.", 18, Ui.Ui.Bad));
        }
        var sites = _s.Planets.VisibleSites(p).ToList();
        if (sites.Count > 0 && (rec.Scanned || _s.HasArtifact("red-cylinder") || sites.Any(x => x.Kind == SiteKind.Nexus)))
        {
            _panel.Children.Add(Ui.Ui.Label("SURFACE FEATURES", 19, Ui.Ui.Accent));
            foreach (var site in sites)
                _panel.Children.Add(Ui.Ui.Row(site.Kind == SiteKind.Nexus ? "NEXUS OF CONTROL" : site.Kind.ToString(), PlanetSurface.FormatLatLon(site.Lat, site.Lon), site.Kind == SiteKind.Nexus ? Ui.Ui.Highlight : null, 19));
        }
        _panel.Children.Add(Ui.Ui.Row("Logged", rec.Logged ? "yes" : "no", null, 19));
        if (rec.Destroyed) _panel.Children.Add(Ui.Ui.Label("This world has been destroyed.", 19, Ui.Ui.Bad));
    }
}
