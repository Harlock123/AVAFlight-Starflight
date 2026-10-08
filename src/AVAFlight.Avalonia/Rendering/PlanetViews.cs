using AVAFlight.Core.Engine;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;
using SkiaSharp;

namespace AVAFlight.Avalonia.Rendering;

/// <summary>
/// Rotating planet seen from orbit (the original's "planet approach"), rendered by projecting the
/// generated surface map onto a lit sphere. In landing mode it shows the flat Mercator map with a
/// site-selection cursor and lat/long readout.
/// </summary>
public sealed class PlanetGlobeView : SkiaView
{
    private readonly GameSession _s;
    private SKBitmap? _map;
    private int _mapPlanet = -1;

    public bool LandingMap { get; set; }
    public int CursorX { get; set; } = PlanetSurface.Width / 2;
    public int CursorY { get; set; } = PlanetSurface.Height / 2;

    public PlanetGlobeView(GameSession s) { _s = s; Animated = true; }

    private SKBitmap MapFor(Planet p)
    {
        if (_map is not null && _mapPlanet == p.Id) return _map;
        var surf = _s.Planets.SurfaceFor(p);
        var bmp = new SKBitmap(PlanetSurface.Width, PlanetSurface.Height);
        bool destroyed = _s.State.PlanetRecord(p.Id).Destroyed;
        for (int x = 0; x < PlanetSurface.Width; x++)
            for (int y = 0; y < PlanetSurface.Height; y++)
            {
                var col = p.Type == PlanetType.GasGiant
                    ? GasBand(p, y, surf.Elevation[x, y])
                    : Palette.TerrainShaded(surf.Kind[x, y], p.Type, surf.Elevation[x, y]);
                if (destroyed) col = new SKColor((byte)(col.Red / 4 + 40), (byte)(col.Green / 4 + 30), (byte)(col.Blue / 4 + 30));
                bmp.SetPixel(x, y, col);
            }
        _map?.Dispose();
        _map = bmp;
        _mapPlanet = p.Id;
        return bmp;
    }

    private static SKColor GasBand(Planet p, int y, float e)
    {
        var baseCol = (p.Id % 3) switch { 0 => Ega.LightMagenta, 1 => new SKColor(0xFF, 0xAA, 0x55), _ => Ega.LightCyan };
        float band = 0.6f + 0.4f * (float)Math.Sin(y * 0.7 + e * 3);
        return new SKColor((byte)(baseCol.Red * band), (byte)(baseCol.Green * band), (byte)(baseCol.Blue * band));
    }

    protected override void RenderSkia(SKCanvas c, SKSize size)
    {
        c.Clear(Ega.Black);
        var p = _s.CurrentPlanet ?? (_s.State.Surface is { } st ? _s.Galaxy.Planet(st.PlanetId) : null);
        if (p is null) return;
        var map = MapFor(p);
        if (LandingMap) { DrawMercator(c, size, p, map); return; }

        // Background stars.
        var rng = new SplitMix64((ulong)p.Id + 5);
        for (int i = 0; i < 120; i++)
            c.DrawCircle((float)rng.NextDouble() * size.Width, (float)rng.NextDouble() * size.Height, 0.8f, Palette.Fill(Ega.DarkGray));

        float radius = Math.Min(size.Width, size.Height) * 0.38f;
        float cx = size.Width / 2, cy = size.Height / 2;
        int r = (int)radius;
        double rot = Time * 0.15;
        using var sphere = new SKBitmap(2 * r + 1, 2 * r + 1);
        for (int py = -r; py <= r; py++)
        for (int px = -r; px <= r; px++)
        {
            double nx = px / radius, ny = py / radius;
            double d2 = nx * nx + ny * ny;
            if (d2 > 1) continue;
            double nz = Math.Sqrt(1 - d2);
            double lat = Math.Asin(-ny);
            double lon = Math.Atan2(nx, nz) + rot;
            int mx = (int)(((lon / (2 * Math.PI) + 0.5) % 1 + 1) % 1 * PlanetSurface.Width) % PlanetSurface.Width;
            int my = Math.Clamp((int)((0.5 - lat / Math.PI) * PlanetSurface.Height), 0, PlanetSurface.Height - 1);
            var col = map.GetPixel(mx, my);
            double light = Math.Clamp(0.25 + 0.85 * (nx * -0.5 + ny * -0.35 + nz * 0.8), 0.12, 1.1);
            sphere.SetPixel(px + r, py + r, new SKColor((byte)Math.Min(255, col.Red * light), (byte)Math.Min(255, col.Green * light), (byte)Math.Min(255, col.Blue * light)));
        }
        c.DrawBitmap(sphere, cx - r, cy - r);
        if (p.Atmosphere > Atmosphere.Thin)
            c.DrawCircle(cx, cy, radius + 3, Palette.Stroke(new SKColor(0x55, 0xFF, 0xFF, 0x50), 4));
        var sys = _s.Galaxy.System(p.SystemId);
        Palette.Text(c, $"ORBITING {(p.Name ?? $"PLANET {p.Orbit}").ToUpperInvariant()}  -  SYSTEM {sys.X},{sys.Y}", 12, 24, 22, Ega.LightCyan);
        if (_s.State.PlanetRecord(p.Id).Destroyed) Palette.Text(c, "PLANET DESTROYED", cx, cy + radius + 30, 22, Ega.LightRed, SKTextAlign.Center);
    }

    private void DrawMercator(SKCanvas c, SKSize size, Planet p, SKBitmap map)
    {
        float w = size.Width - 20, h = w / 2;
        if (h > size.Height - 60) { h = size.Height - 60; w = h * 2; }
        float ox = (size.Width - w) / 2, oy = 34;
        using (var img = SKImage.FromBitmap(map))
            c.DrawImage(img, new SKRect(ox, oy, ox + w, oy + h), new SKSamplingOptions(SKFilterMode.Nearest));
        float cw = w / PlanetSurface.Width, ch = h / PlanetSurface.Height;
        foreach (var site in _s.Planets.VisibleSites(p))
        {
            var (sx, sy) = PlanetSurface.CellFromLatLon(site.Lat, site.Lon);
            var col = site.Kind switch { SiteKind.Nexus => Ega.Yellow, SiteKind.Artifact => Ega.LightMagenta, SiteKind.Message => Ega.LightCyan, _ => Ega.White };
            c.DrawRect(ox + sx * cw - 2, oy + sy * ch - 2, cw + 4, ch + 4, Palette.Stroke(col, 2));
        }
        float x = ox + CursorX * cw + cw / 2, y = oy + CursorY * ch + ch / 2;
        using var cp = Palette.Stroke(Ega.Yellow, 1.5f);
        c.DrawLine(ox, y, ox + w, y, cp);
        c.DrawLine(x, oy, x, oy + h, cp);
        var (lat, lon) = PlanetSurface.LatLonFromCell(CursorX, CursorY);
        var surf = _s.Planets.SurfaceFor(p);
        Palette.Text(c, $"SELECT LANDING SITE   {PlanetSurface.FormatLatLon(lat, lon)}   TERRAIN: {surf.At(CursorX, CursorY).ToString().ToUpperInvariant()}", 12, 24, 22, Ega.LightCyan);
        Palette.Text(c, "Arrows: move cursor   Enter: descend   Esc: cancel", 12, oy + h + 22, 18, Ega.LightGray);
    }
}

/// <summary>
/// Terrain vehicle view: overhead tiles around the TV with mineral deposits (within sensor range),
/// lifeforms, ruins/artifacts, the ship and the vehicle.
/// </summary>
public sealed class TerrainView : SkiaView
{
    private readonly GameSession _s;
    public TerrainView(GameSession s) { _s = s; Animated = true; }

    protected override void RenderSkia(SKCanvas c, SKSize size)
    {
        c.Clear(Ega.Black);
        if (_s.State.Surface is not { } st || _s.Planets.CurrentSurface is not { } surf) return;
        float tile = 28;
        int cols = (int)(size.Width / tile) + 2, rows = (int)((size.Height - 30) / tile) + 2;
        float ox = size.Width / 2 - tile / 2, oy = (size.Height + 30) / 2 - tile / 2;
        SKRect Cell(int dx, int dy) => new(ox + dx * tile, oy + dy * tile, ox + dx * tile + tile, oy + dy * tile + tile);

        for (int dy = -rows / 2; dy <= rows / 2; dy++)
        for (int dx = -cols / 2; dx <= cols / 2; dx++)
        {
            int x = st.TvX + dx, y = st.TvY + dy;
            if (y < 0 || y >= PlanetSurface.Height) { c.DrawRect(Cell(dx, dy), Palette.Fill(new SKColor(0x10, 0x10, 0x10))); continue; }
            var kind = surf.At(x, y);
            var col = Palette.TerrainShaded(kind, surf.Planet.Type, surf.ElevationAt(x, y));
            if (kind is TerrainKind.Water or TerrainKind.Liquid or TerrainKind.Lava)
            {
                float wave = (float)Math.Sin(Time * 2 + x * 0.7 + y * 0.4) * 12;
                col = new SKColor((byte)Math.Clamp(col.Red + wave, 0, 255), (byte)Math.Clamp(col.Green + wave, 0, 255), (byte)Math.Clamp(col.Blue + wave, 0, 255));
            }
            c.DrawRect(Cell(dx, dy), Palette.Fill(col));
            if (kind == TerrainKind.Mountain)
            {
                var r = Cell(dx, dy);
                using var path = new SKPath();
                path.MoveTo(r.Left + 4, r.Bottom - 4); path.LineTo(r.MidX, r.Top + 4); path.LineTo(r.Right - 4, r.Bottom - 4); path.Close();
                c.DrawPath(path, Palette.Stroke(Ega.White, 1.5f));
            }
        }
        // Grid lines (subtle).
        using (var gp = Palette.Stroke(new SKColor(0, 0, 0, 0x30), 1))
            for (int dx = -cols / 2; dx <= cols / 2 + 1; dx++) c.DrawLine(ox + dx * tile, 0, ox + dx * tile, size.Height, gp);

        int Rel(int x) { int d = x - st.TvX; if (d > PlanetSurface.Width / 2) d -= PlanetSurface.Width; if (d < -PlanetSurface.Width / 2) d += PlanetSurface.Width; return d; }

        foreach (var site in surf.Planet.Sites)
        {
            if (site.Kind == SiteKind.Nexus && !_s.HasArtifact("crystal-cone")) continue;
            var (sx, sy) = PlanetSurface.CellFromLatLon(site.Lat, site.Lon);
            var r = Cell(Rel(sx), sy - st.TvY);
            bool collected = _s.State.PlanetRecord(surf.Planet.Id).CollectedSites.Contains(site.Id);
            var col = site.Kind switch { SiteKind.Nexus => Ega.Yellow, SiteKind.Artifact => collected ? Ega.DarkGray : Ega.LightMagenta, SiteKind.Message => Ega.LightCyan, SiteKind.Ruin => Ega.White, _ => Ega.LightRed };
            c.DrawRect(SKRect.Inflate(r, -3, -3), Palette.Stroke(col, 2.5f));
            c.DrawRect(SKRect.Inflate(r, -9, -9), Palette.Fill(col));
        }
        foreach (var d in _s.Planets.VisibleDeposits())
        {
            var r = Cell(Rel(d.X), d.Y - st.TvY);
            bool endurium = d.MineralId == "endurium";
            using var path = new SKPath();
            path.MoveTo(r.MidX, r.Top + 6); path.LineTo(r.Right - 6, r.MidY); path.LineTo(r.MidX, r.Bottom - 6); path.LineTo(r.Left + 6, r.MidY); path.Close();
            c.DrawPath(path, Palette.Fill(endurium ? Ega.LightMagenta : Ega.Yellow));
            c.DrawPath(path, Palette.Stroke(Ega.Black, 1));
        }
        foreach (var cr in st.Creatures)
        {
            var r = Cell(Rel(cr.X), cr.Y - st.TvY);
            var col = cr.StunnedTurns > 0 ? Ega.LightBlue : cr.Hostile ? Ega.LightRed : Ega.LightGreen;
            float rad = 4 + cr.Size * 1.6f;
            c.DrawCircle(r.MidX, r.MidY + (cr.Flying ? (float)Math.Sin(Time * 5 + cr.Id) * 3 : 0), rad, Palette.Fill(col));
            c.DrawCircle(r.MidX - rad * 0.35f, r.MidY - rad * 0.3f, 1.8f, Palette.Fill(Ega.Black));
            c.DrawCircle(r.MidX + rad * 0.35f, r.MidY - rad * 0.3f, 1.8f, Palette.Fill(Ega.Black));
        }
        var shipR = Cell(Rel(st.ShipX), st.ShipY - st.TvY);
        using (var sp = new SKPath())
        {
            sp.MoveTo(shipR.MidX, shipR.Top + 2); sp.LineTo(shipR.Right - 3, shipR.Bottom - 3); sp.LineTo(shipR.Left + 3, shipR.Bottom - 3); sp.Close();
            c.DrawPath(sp, Palette.Fill(Ega.White));
            c.DrawPath(sp, Palette.Stroke(Ega.Blue, 1.5f));
        }
        var tv = Cell(0, 0);
        c.DrawRoundRect(SKRect.Inflate(tv, -5, -7), 3, 3, Palette.Fill(st.OnFoot ? Ega.LightRed : Ega.Yellow));
        c.DrawRoundRect(SKRect.Inflate(tv, -5, -7), 3, 3, Palette.Stroke(Ega.Black, 1.5f));

        var (lat, lon) = PlanetSurface.LatLonFromCell(st.TvX, st.TvY);
        c.DrawRect(0, 0, size.Width, 30, Palette.Fill(new SKColor(0, 0, 0, 0xC0)));
        Palette.Text(c, $"{PlanetSurface.FormatLatLon(lat, lon)}   {surf.At(st.TvX, st.TvY).ToString().ToUpperInvariant()}   TV FUEL {st.TvFuel:0}/{PlanetService.TvFuelCapacity:0}   HOLD {_s.Planets.TvCargoUsed:0.#}/{PlanetService.TvHold:0} M3{(st.EggFuseAt >= 0 ? $"   EGG FUSE {st.EggFuseAt - st.Steps}" : "")}",
            10, 22, 21, st.OnFoot ? Ega.LightRed : Ega.LightCyan);
    }
}

/// <summary>Tactical (combat) view: ships, shields, beams and projectiles in the encounter arena.</summary>
public sealed class CombatView : SkiaView
{
    private readonly GameSession _s;
    public CombatView(GameSession s) { _s = s; Animated = true; }

    protected override void RenderSkia(SKCanvas c, SKSize size)
    {
        c.Clear(Ega.Black);
        if (_s.State.Encounter is not { } e) return;
        var race = _s.Data.Race(e.RaceId);
        float scale = Math.Min(size.Width, size.Height) / 160f;
        float cx = size.Width / 2 - (float)(e.PlayerX * scale) * 0.5f, cy = size.Height / 2 - (float)(e.PlayerY * scale) * 0.5f;
        SKPoint P(double x, double y) => new(cx + (float)(x * scale), cy + (float)(y * scale));

        var rng = new SplitMix64(99);
        for (int i = 0; i < 150; i++)
            c.DrawCircle((float)rng.NextDouble() * size.Width, (float)rng.NextDouble() * size.Height, 0.8f, Palette.Fill(Ega.DarkGray));

        var me = P(e.PlayerX, e.PlayerY);
        c.DrawCircle(me, (float)(CombatService.LaserRange * scale), Palette.Stroke(new SKColor(0x00, 0x55, 0x00), 1));
        var alienCol = SKColor.Parse(race.PortraitColor);
        foreach (var a in e.Ships)
        {
            var p = P(a.X, a.Y);
            if (a.Destroyed)
            {
                c.DrawCircle(p, 6, Palette.Stroke(Ega.DarkGray, 1));
                c.DrawCircle(p, 2, Palette.Fill(Ega.Brown));
                continue;
            }
            DrawAlienShip(c, p, (float)a.Heading, alienCol, race.Id, 10 * scale / 2.4f + 6);
            if (a.Shield > 0 && e.Hostile) c.DrawCircle(p, 15, Palette.Stroke(alienCol.WithAlpha(0x60), 1.5f));
            float hp = Math.Clamp(a.Hull / (float)a.MaxHull, 0, 1);
            c.DrawRect(p.X - 12, p.Y + 16, 24, 3, Palette.Fill(Ega.DarkGray));
            c.DrawRect(p.X - 12, p.Y + 16, 24 * hp, 3, Palette.Fill(hp > 0.5 ? Ega.LightGreen : Ega.LightRed));
        }
        foreach (var pr in e.Projectiles)
        {
            var p = P(pr.X, pr.Y);
            var col = pr.Kind == ProjectileKind.Plasma ? Ega.LightMagenta : pr.FromPlayer ? Ega.Yellow : Ega.LightRed;
            c.DrawCircle(p, pr.Kind == ProjectileKind.Plasma ? 5 : 3, Palette.Fill(col));
        }
        foreach (var b in e.Beams)
        {
            using var bp = Palette.Stroke(b.FromPlayer ? Ega.LightCyan : Ega.LightRed, 2.5f);
            c.DrawLine(P(b.X1, b.Y1), P(b.X2, b.Y2), bp);
        }
        var ship = _s.State.Ship;
        if (ship.ShieldsUp && ship.ShieldPoints > 0)
            c.DrawCircle(me, 16, Palette.Stroke(new SKColor(0x55, 0xFF, 0xFF, (byte)(0x60 + 0x30 * Math.Sin(Time * 5))), 2));
        HyperspaceView.DrawShip(c, me.X, me.Y, (float)e.PlayerHeading, Ega.White, 11);

        int alive = e.Ships.Count(a => !a.Destroyed);
        string known = _s.State.Relation(e.RaceId).Contacted || _s.Navigation.LongRangeSensors ? race.Name.ToUpperInvariant() : "UNIDENTIFIED";
        Palette.Text(c, $"TACTICAL - {alive} {known} VESSEL{(alive == 1 ? "" : "S")} - {(e.Hostile && !e.Surrendered ? "HOSTILE" : e.Surrendered ? "SURRENDERED" : "NOT HOSTILE")}",
            12, 24, 22, e.Hostile && !e.Surrendered ? Ega.LightRed : Ega.LightGreen);
        string weapons = ship.WeaponsArmed ? (e.ArmingTimer > 0 ? $"ARMING {e.ArmingTimer:0.0}s" : "ARMED") : "DISARMED";
        Palette.Text(c, $"SHIELDS {(ship.ShieldsUp ? "UP" : "DOWN")}  WEAPONS {weapons}  Arrows: steer/thrust  Space/F: fire  Fly off-screen to escape",
            12, size.Height - 10, 18, Ega.LightGray);
    }

    public static void DrawAlienShip(SKCanvas c, SKPoint p, float heading, SKColor col, string race, float s)
    {
        c.Save();
        c.Translate(p.X, p.Y);
        c.RotateRadians(heading);
        using var path = new SKPath();
        switch (race)
        {
            case "uhlek":
                path.MoveTo(s, 0); path.LineTo(0, s * 0.8f); path.LineTo(-s, s * 0.3f); path.LineTo(-s * 0.6f, 0); path.LineTo(-s, -s * 0.3f); path.LineTo(0, -s * 0.8f); break;
            case "minstrel":
                c.DrawOval(0, 0, s, s * 0.5f, Palette.Fill(col.WithAlpha(0xA0)));
                c.Restore();
                return;
            case "gazurtoid":
                path.MoveTo(s, 0); path.LineTo(-s * 0.4f, s); path.LineTo(-s, 0); path.LineTo(-s * 0.4f, -s); break;
            default:
                path.MoveTo(s, 0); path.LineTo(-s * 0.6f, s * 0.7f); path.LineTo(-s * 0.6f, -s * 0.7f); break;
        }
        path.Close();
        c.DrawPath(path, Palette.Fill(col));
        c.DrawPath(path, Palette.Stroke(Ega.Black, 1));
        c.Restore();
    }
}

/// <summary>Procedural alien portrait for the comm screen (newly designed; no original artwork).</summary>
public sealed class AlienPortraitView : SkiaView
{
    private readonly string _race;
    public AlienPortraitView(string race) { _race = race; Animated = true; }

    protected override void RenderSkia(SKCanvas c, SKSize size)
    {
        c.Clear(new SKColor(0, 0, 0x20));
        float cx = size.Width / 2, cy = size.Height / 2, r = Math.Min(size.Width, size.Height) * 0.36f;
        float t = (float)Time;
        var fill = Palette.Fill(Ega.Black);
        switch (_race)
        {
            case "elowan": // a flowering plant-being
                c.DrawRect(cx - 5, cy, 10, r * 1.3f, Palette.Fill(Ega.Green));
                for (int i = 0; i < 7; i++)
                {
                    float a = i * MathF.Tau / 7 + MathF.Sin(t) * 0.1f;
                    c.DrawOval(cx + MathF.Cos(a) * r * 0.55f, cy - r * 0.2f + MathF.Sin(a) * r * 0.55f, r * 0.35f, r * 0.18f, Palette.Fill(Ega.LightGreen));
                }
                c.DrawCircle(cx, cy - r * 0.2f, r * 0.3f, Palette.Fill(Ega.Yellow));
                c.DrawCircle(cx - r * 0.1f, cy - r * 0.25f, 4, fill); c.DrawCircle(cx + r * 0.1f, cy - r * 0.25f, 4, fill);
                break;
            case "thrynn": // reptilian head
                c.DrawOval(cx, cy, r * 0.6f, r * 0.8f, Palette.Fill(new SKColor(0x88, 0xAA, 0x33)));
                c.DrawOval(cx, cy + r * 0.45f, r * 0.45f, r * 0.25f, Palette.Fill(new SKColor(0x66, 0x88, 0x22)));
                c.DrawOval(cx - r * 0.25f, cy - r * 0.15f, r * 0.14f, r * 0.08f, Palette.Fill(Ega.Yellow));
                c.DrawOval(cx + r * 0.25f, cy - r * 0.15f, r * 0.14f, r * 0.08f, Palette.Fill(Ega.Yellow));
                c.DrawRect(cx - r * 0.27f, cy - r * 0.2f, 4, r * 0.1f, fill); c.DrawRect(cx + r * 0.23f, cy - r * 0.2f, 4, r * 0.1f, fill);
                for (int i = -2; i <= 2; i++) c.DrawLine(cx + i * r * 0.12f, cy - r * 0.8f, cx + i * r * 0.16f, cy - r * 1.0f, Palette.Stroke(Ega.Brown, 3));
                break;
            case "velox": // insectoid: mandibles and compound eyes
                c.DrawOval(cx, cy, r * 0.5f, r * 0.7f, Palette.Fill(Ega.Magenta));
                c.DrawCircle(cx - r * 0.25f, cy - r * 0.25f, r * 0.2f, Palette.Fill(Ega.LightRed));
                c.DrawCircle(cx + r * 0.25f, cy - r * 0.25f, r * 0.2f, Palette.Fill(Ega.LightRed));
                c.DrawLine(cx - r * 0.2f, cy + r * 0.5f, cx - r * 0.4f + MathF.Sin(t * 4) * 4, cy + r * 0.9f, Palette.Stroke(Ega.LightMagenta, 4));
                c.DrawLine(cx + r * 0.2f, cy + r * 0.5f, cx + r * 0.4f - MathF.Sin(t * 4) * 4, cy + r * 0.9f, Palette.Stroke(Ega.LightMagenta, 4));
                c.DrawLine(cx - r * 0.15f, cy - r * 0.65f, cx - r * 0.5f, cy - r * 1.05f, Palette.Stroke(Ega.LightMagenta, 2));
                c.DrawLine(cx + r * 0.15f, cy - r * 0.65f, cx + r * 0.5f, cy - r * 1.05f, Palette.Stroke(Ega.LightMagenta, 2));
                break;
            case "spemin": // quivering blob
                using (var blob = new SKPath())
                {
                    for (int i = 0; i <= 32; i++)
                    {
                        float a = i * MathF.Tau / 32;
                        float rr = r * (0.75f + 0.08f * MathF.Sin(a * 5 + t * 6));
                        var pt = new SKPoint(cx + MathF.Cos(a) * rr, cy + MathF.Sin(a) * rr * 0.8f);
                        if (i == 0) blob.MoveTo(pt); else blob.LineTo(pt);
                    }
                    c.DrawPath(blob, Palette.Fill(Ega.Cyan));
                }
                c.DrawCircle(cx - r * 0.2f, cy - r * 0.1f, r * 0.12f, Palette.Fill(Ega.White));
                c.DrawCircle(cx + r * 0.25f, cy - r * 0.15f, r * 0.08f, Palette.Fill(Ega.White));
                c.DrawCircle(cx - r * 0.2f, cy - r * 0.1f, r * 0.05f, fill);
                c.DrawCircle(cx + r * 0.25f, cy - r * 0.15f, r * 0.04f, fill);
                break;
            case "mechan": // android face plate
                c.DrawRoundRect(cx - r * 0.55f, cy - r * 0.75f, r * 1.1f, r * 1.5f, 10, 10, Palette.Fill(Ega.LightGray));
                c.DrawRect(cx - r * 0.4f, cy - r * 0.3f, r * 0.8f, r * 0.18f, Palette.Fill(Ega.Black));
                c.DrawRect(cx - r * 0.35f + (MathF.Sin(t * 2) + 1) * r * 0.3f, cy - r * 0.27f, r * 0.1f, r * 0.12f, Palette.Fill(Ega.LightRed));
                for (int i = 0; i < 5; i++) c.DrawRect(cx - r * 0.3f + i * r * 0.13f, cy + r * 0.3f, r * 0.08f, r * 0.2f, Palette.Fill(Ega.DarkGray));
                break;
            case "gazurtoid": // tentacled
                c.DrawCircle(cx, cy - r * 0.2f, r * 0.5f, Palette.Fill(Ega.Blue));
                for (int i = 0; i < 6; i++)
                {
                    float x0 = cx - r * 0.45f + i * r * 0.18f;
                    using var tp = new SKPath();
                    tp.MoveTo(x0, cy + r * 0.2f);
                    tp.CubicTo(x0 + MathF.Sin(t * 3 + i) * 15, cy + r * 0.5f, x0 - MathF.Sin(t * 2 + i) * 15, cy + r * 0.8f, x0, cy + r * 1.1f);
                    c.DrawPath(tp, Palette.Stroke(Ega.LightBlue, 5));
                }
                c.DrawCircle(cx, cy - r * 0.3f, r * 0.12f, Palette.Fill(Ega.Yellow));
                break;
            case "minstrel": // shimmering light
                for (int i = 0; i < 5; i++)
                    c.DrawCircle(cx, cy, r * (0.3f + i * 0.15f + 0.05f * MathF.Sin(t * 2 + i)), Palette.Stroke(new SKColor(0xFF, 0xFF, 0xFF, (byte)(0xC0 - i * 0x20)), 2));
                break;
            case "nomad":
                c.DrawRect(cx - r * 0.4f, cy - r * 0.3f, r * 0.8f, r * 0.6f, Palette.Fill(Ega.Brown));
                c.DrawLine(cx, cy - r * 0.3f, cx, cy - r * 0.8f, Palette.Stroke(Ega.LightGray, 2));
                c.DrawCircle(cx, cy - r * 0.8f, 5 + MathF.Sin(t * 6) * 2, Palette.Fill(Ega.LightRed));
                break;
            default:
                Palette.Text(c, ((int)(t * 10) % 2 == 0) ? "10110" : "01001", cx, cy, 40, Ega.Cyan, SKTextAlign.Center);
                break;
        }
    }
}
