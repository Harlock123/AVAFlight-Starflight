using AVAFlight.Core.Engine;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;
using SkiaSharp;

namespace AVAFlight.Avalonia.Rendering;

/// <summary>
/// Hyperspace main view: a window on the star map centred on the ship (the original's hyperspace
/// display), with nebulae, visible fluxes, the cruise course and Modern waypoints.
/// </summary>
public sealed class HyperspaceView : SkiaView
{
    private readonly GameSession _s;
    public double Zoom { get; set; } = 1.0;

    public HyperspaceView(GameSession s) { _s = s; Animated = true; }

    protected override void RenderSkia(SKCanvas c, SKSize size)
    {
        c.Clear(Ega.Black);
        var loc = _s.State.Location;
        float cell = (float)(18 * Zoom);
        float cx = size.Width / 2, cy = size.Height / 2;
        SKPoint ToScreen(double x, double y) => new(cx + (float)((x - loc.HyperX) * cell), cy + (float)((y - loc.HyperY) * cell));

        // Background dust for a sense of motion.
        using (var dust = Palette.Fill(Ega.DarkGray))
        {
            var rng = new SplitMix64(17);
            for (int i = 0; i < 260; i++)
            {
                double dx = rng.NextDouble() * 400, dy = rng.NextDouble() * 400;
                float px = (float)(((dx - loc.HyperX * 3) % 400 + 400) % 400 / 400 * size.Width);
                float py = (float)(((dy - loc.HyperY * 3) % 400 + 400) % 400 / 400 * size.Height);
                c.DrawCircle(px, py, 0.8f, dust);
            }
        }

        foreach (var n in _s.Galaxy.Nebulae)
        {
            var p = ToScreen(n.X, n.Y);
            float r = (float)(n.Radius * cell);
            if (p.X + r < 0 || p.X - r > size.Width || p.Y + r < 0 || p.Y - r > size.Height) continue;
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(p, r, [new SKColor(0xAA, 0x00, 0xAA, 0x70), new SKColor(0x55, 0x00, 0x55, 0x00)], SKShaderTileMode.Clamp),
            };
            c.DrawCircle(p, r, paint);
        }

        if (_s.Navigation.CanSeeFluxes)
        {
            using var fp = Palette.Stroke(Ega.LightMagenta, 1.5f);
            foreach (var f in _s.Galaxy.Fluxes)
                foreach (var (x, y) in new[] { (f.X1, f.Y1), (f.X2, f.Y2) })
                {
                    var p = ToScreen(x, y);
                    for (int k = 0; k < 3; k++)
                        c.DrawArc(new SKRect(p.X - 6 - k * 3, p.Y - 6 - k * 3, p.X + 6 + k * 3, p.Y + 6 + k * 3), (float)(Time * 120 + k * 60), 220, false, fp);
                }
        }

        if (loc.CruiseX is int tx && loc.CruiseY is int ty)
        {
            using var lp = Palette.Stroke(Ega.Green, 1.5f);
            lp.PathEffect = SKPathEffect.CreateDash([6, 6], (float)(-Time * 20));
            c.DrawLine(ToScreen(loc.HyperX, loc.HyperY), ToScreen(tx, ty), lp);
        }

        if (_s.Policy.Waypoints)
            foreach (var w in _s.State.Waypoints)
            {
                var p = ToScreen(w.X, w.Y);
                using var wp = Palette.Stroke(Ega.LightGreen, 2);
                c.DrawRect(p.X - 5, p.Y - 5, 10, 10, wp);
                Palette.Text(c, w.Note, p.X + 8, p.Y + 4, 16, Ega.LightGreen);
            }

        foreach (var sys in _s.Galaxy.Systems)
        {
            var p = ToScreen(sys.X, sys.Y);
            if (p.X < -20 || p.X > size.Width + 20 || p.Y < -20 || p.Y > size.Height + 20) continue;
            var col = Palette.Star(sys.Class);
            float r = 2.5f + (6 - (int)sys.Class) * 0.5f;
            using (var glow = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(p, r * 3, [col.WithAlpha(0x90), col.WithAlpha(0)], SKShaderTileMode.Clamp) })
                c.DrawCircle(p, r * 3, glow);
            c.DrawCircle(p, r, Palette.Fill(col));
            if (_s.State.VisitedSystems.Contains(sys.Id))
                c.DrawCircle(p, r + 4, Palette.Stroke(Ega.DarkGray));
        }

        // Ship.
        var dir = Math.Atan2(loc.VelY, loc.VelX);
        if (loc.CruiseX is int ax && loc.CruiseY is int ay) dir = Math.Atan2(ay - loc.HyperY, ax - loc.HyperX);
        DrawShip(c, cx, cy, (float)dir, Ega.White, 9);

        string coords = loc.PositionLost ? "POSITION UNKNOWN" : $"{loc.HyperX:0},{loc.HyperY:0}";
        Palette.Text(c, "HYPERSPACE  " + coords, 12, 24, 22, loc.PositionLost ? Ega.LightRed : Ega.LightCyan);
        if (_s.Galaxy.InNebula(loc.HyperX, loc.HyperY)) Palette.Text(c, "NEBULA: SHIELDS INOPERATIVE", 12, 46, 18, Ega.LightMagenta);
    }

    public static void DrawShip(SKCanvas c, float x, float y, float heading, SKColor color, float size)
    {
        using var path = new SKPath();
        path.MoveTo(size, 0);
        path.LineTo(-size * 0.7f, size * 0.6f);
        path.LineTo(-size * 0.35f, 0);
        path.LineTo(-size * 0.7f, -size * 0.6f);
        path.Close();
        c.Save();
        c.Translate(x, y);
        c.RotateRadians(heading);
        c.DrawPath(path, Palette.Fill(color));
        c.DrawPath(path, Palette.Stroke(Ega.Black, 1));
        c.Restore();
    }
}

/// <summary>
/// Full galaxy star map with a cursor for picking a destination. Shows distance / fuel / time to
/// the cursor (the original's MAP readout); Modern adds the fuel-range circle and waypoints.
/// </summary>
public sealed class StarMapView : SkiaView
{
    private readonly GameSession _s;
    public double CursorX { get; set; }
    public double CursorY { get; set; }
    public double Zoom { get; set; } = 1;

    public StarMapView(GameSession s)
    {
        _s = s;
        Animated = true;
        CursorX = s.State.Location.HyperX;
        CursorY = s.State.Location.HyperY;
    }

    protected override void RenderSkia(SKCanvas c, SKSize size)
    {
        c.Clear(new SKColor(0, 0, 0x14));
        float baseScale = Math.Min((size.Width - 20) / GalaxyMap.Width, (size.Height - 70) / GalaxyMap.Height);
        float scale = (float)(baseScale * Zoom);
        // Centre on the cursor when zoomed in.
        double viewCx = Zoom <= 1.01 ? GalaxyMap.Width / 2.0 : CursorX;
        double viewCy = Zoom <= 1.01 ? GalaxyMap.Height / 2.0 : CursorY;
        float ox = size.Width / 2 - (float)(viewCx * scale), oy = (size.Height - 50) / 2 + 4 - (float)(viewCy * scale);
        SKPoint P(double x, double y) => new(ox + (float)(x * scale), oy + (float)(y * scale));

        using (var grid = Palette.Stroke(new SKColor(0x00, 0x00, 0x55), 1))
        {
            for (int x = 0; x <= GalaxyMap.Width; x += 25) c.DrawLine(P(x, 0), P(x, GalaxyMap.Height), grid);
            for (int y = 0; y <= GalaxyMap.Height; y += 25) c.DrawLine(P(0, y), P(GalaxyMap.Width, y), grid);
        }
        foreach (var n in _s.Galaxy.Nebulae)
            c.DrawCircle(P(n.X, n.Y), (float)(n.Radius * scale), Palette.Fill(new SKColor(0xAA, 0x00, 0xAA, 0x40)));

        var loc = _s.State.Location;
        bool modern = _s.Policy.FuelWarningAndRange;
        if (modern)
        {
            float rr = (float)(_s.Navigation.Range * scale);
            using var rp = Palette.Stroke(new SKColor(0x55, 0xFF, 0x55, 0x90), 1.5f);
            rp.PathEffect = SKPathEffect.CreateDash([4, 4], 0);
            c.DrawCircle(P(loc.HyperX, loc.HyperY), rr, rp);
        }
        if (_s.Navigation.CanSeeFluxes)
            foreach (var f in _s.Galaxy.Fluxes)
            {
                using var fp = Palette.Stroke(new SKColor(0xFF, 0x55, 0xFF, 0x60), 1);
                c.DrawLine(P(f.X1, f.Y1), P(f.X2, f.Y2), fp);
            }
        foreach (var sys in _s.Galaxy.Systems)
        {
            float r = Math.Max(1.2f, scale * 0.45f);
            c.DrawCircle(P(sys.X, sys.Y), r, Palette.Fill(Palette.Star(sys.Class)));
            if (_s.State.VisitedSystems.Contains(sys.Id)) c.DrawCircle(P(sys.X, sys.Y), r + 2.5f, Palette.Stroke(Ega.LightGray, 1));
        }
        if (_s.Policy.Waypoints)
            foreach (var w in _s.State.Waypoints)
            {
                var p = P(w.X, w.Y);
                c.DrawRect(p.X - 4, p.Y - 4, 8, 8, Palette.Stroke(Ega.LightGreen, 2));
                Palette.Text(c, w.Note, p.X + 6, p.Y - 4, 15, Ega.LightGreen);
            }
        if (loc.CruiseX is int tx && loc.CruiseY is int ty)
            c.DrawLine(P(loc.HyperX, loc.HyperY), P(tx, ty), Palette.Stroke(Ega.Green, 1.5f));

        var me = P(loc.HyperX, loc.HyperY);
        float pulse = 5 + (float)Math.Sin(Time * 6) * 2;
        c.DrawCircle(me, pulse, Palette.Stroke(Ega.White, 2));

        var cur = P(CursorX, CursorY);
        using (var cp = Palette.Stroke(Ega.Yellow, 1.5f))
        {
            c.DrawLine(cur.X - 12, cur.Y, cur.X - 4, cur.Y, cp);
            c.DrawLine(cur.X + 4, cur.Y, cur.X + 12, cur.Y, cp);
            c.DrawLine(cur.X, cur.Y - 12, cur.X, cur.Y - 4, cp);
            c.DrawLine(cur.X, cur.Y + 4, cur.X, cur.Y + 12, cp);
        }

        // Readout bar.
        int ix = (int)Math.Round(CursorX), iy = (int)Math.Round(CursorY);
        var target = _s.Galaxy.SystemAt(ix, iy);
        double d = _s.Navigation.Distance(ix, iy);
        double fuel = _s.Navigation.FuelFor(d);
        string pos = loc.PositionLost ? "??,??" : $"{loc.HyperX:0},{loc.HyperY:0}";
        string line1 = $"SHIP {pos}   CURSOR {ix},{iy}{(target is null ? "" : $"  STAR CLASS {target.Class}{(target.Name is not null && _s.State.VisitedSystems.Contains(target.Id) ? " " + target.Name.ToUpperInvariant() : "")}")}";
        string line2 = $"DISTANCE {d:0.0}   FUEL {fuel:0.0} M3   TIME {_s.Navigation.HoursFor(d) / 24:0.0} DAYS";
        Palette.Text(c, line1, 12, size.Height - 30, 20, Ega.LightCyan);
        var col = modern && fuel > _s.State.Ship.Endurium ? Ega.LightRed : Ega.White;
        Palette.Text(c, line2 + (modern && fuel > _s.State.Ship.Endurium ? "   BEYOND FUEL RANGE!" : ""), 12, size.Height - 8, 20, col);
        Palette.Text(c, "STAR MAP", size.Width - 12, 22, 14, Ega.LightCyan, SKTextAlign.Right, title: true);
    }
}

/// <summary>In-system ("star approach") view: the star, orbits, planets and the ship.</summary>
public sealed class SystemView : SkiaView
{
    private readonly GameSession _s;
    public SystemView(GameSession s) { _s = s; Animated = true; }

    protected override void RenderSkia(SKCanvas c, SKSize size)
    {
        c.Clear(Ega.Black);
        var sys = _s.CurrentSystem;
        if (sys is null) return;
        float scale = Math.Min(size.Width, size.Height) / 2 / (float)(NavigationService.SystemRadius + 4);
        float cx = size.Width / 2, cy = size.Height / 2;
        SKPoint P(double x, double y) => new(cx + (float)(x * scale), cy + (float)(y * scale));

        c.DrawCircle(cx, cy, (float)(NavigationService.SystemRadius * scale), Palette.Stroke(new SKColor(0x00, 0x00, 0x55), 1));
        var starCol = Palette.Star(sys.Class);
        float sr = 6 * scale;
        using (var glow = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(new SKPoint(cx, cy), sr * 3, [starCol, starCol.WithAlpha(0)], SKShaderTileMode.Clamp) })
            c.DrawCircle(cx, cy, sr * 3, glow);
        c.DrawCircle(cx, cy, sr, Palette.Fill(starCol));

        foreach (var p in sys.Planets)
        {
            var (px, py) = NavigationService.PlanetPosition(p);
            float orbitR = (float)Math.Sqrt(px * px + py * py) * scale;
            c.DrawCircle(cx, cy, orbitR, Palette.Stroke(new SKColor(0x22, 0x22, 0x44), 1));
            var pp = P(px, py);
            float pr = (2 + p.Mass) * scale * 0.9f;
            bool destroyed = _s.State.PlanetRecord(p.Id).Destroyed;
            c.DrawCircle(pp, pr, Palette.Fill(destroyed ? Ega.DarkGray : Palette.PlanetColor(p.Type)));
            if (p.Type == PlanetType.Crystal && !destroyed)
                c.DrawCircle(pp, pr + 3 + (float)Math.Sin(Time * 4) * 2, Palette.Stroke(Ega.LightCyan, 1.5f));
            Palette.Text(c, p.Orbit.ToString(), pp.X + pr + 3, pp.Y - pr, 16, Ega.LightGray);
        }

        var loc = _s.State.Location;
        var (dx, dy) = Screens.HeldInput.Direction();
        var heading = Math.Abs(dx) + Math.Abs(dy) > 0 ? Math.Atan2(dy, dx) : -Math.PI / 2;
        HyperspaceView.DrawShip(c, P(loc.SysX, loc.SysY).X, P(loc.SysX, loc.SysY).Y, (float)heading, Ega.White, 8);

        var near = _s.Navigation.NearestPlanet(out double nd);
        Palette.Text(c, $"SYSTEM {sys.X},{sys.Y}  CLASS {sys.Class}{(sys.Name is not null ? "  " + sys.Name.ToUpperInvariant() : "")}", 12, 24, 22, Ega.LightCyan);
        Palette.Text(c, "STELLAR CONDITION: " + _s.Story.StellarCondition(sys), 12, 46, 18,
            _s.Story.StellarCondition(sys).StartsWith("UNSTABLE") ? Ega.LightRed : Ega.LightGray);
        if (near is not null && nd <= NavigationService.OrbitDistance)
            Palette.Text(c, $"PLANET {near.Orbit} IN RANGE - NAVIGATOR > ORBIT (Enter)", 12, size.Height - 12, 20, Ega.Yellow);
    }
}
