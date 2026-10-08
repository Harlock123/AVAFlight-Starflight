using AVAFlight.Core.Data;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;

namespace AVAFlight.Core.Engine;

/// <summary>Orbital sensor results. Null fields display as "NOT CERTAIN" (low science skill).</summary>
public sealed record SensorReport(string? Mass, int? BioPercent, int? MineralPercent, string? Atmosphere, string? Hydrosphere, string? Lithosphere);

public sealed record AnalysisReport(int Orbit, string? Surface, double? Gravity, string? AtmosphereDensity,
    string? Composition, string? Temperature, string? Weather);

/// <summary>
/// Planet survey from orbit, landing, and the terrain vehicle. Terrain time model: STEP-BASED.
/// Each TV move (or Wait) is one step: lifeforms move, weather rolls, fuel is spent. This is a
/// reconstruction of the original's real-time TV (see FIDELITY.md); held keys repeat steps at a
/// fixed rate in the view, so it plays in real time while staying deterministic.
/// </summary>
public sealed class PlanetService(GameSession s)
{
    public const double TvFuelCapacity = 100;
    public const double TvHold = 50;
    public const int EggFuseSteps = 60;

    private PlanetSurface? _surface;

    /// <summary>Surface for a planet (cached for the current landing).</summary>
    public PlanetSurface SurfaceFor(Planet p)
    {
        if (_surface?.Planet.Id != p.Id) _surface = PlanetSurface.Generate(p, s.Data.Minerals);
        return _surface;
    }

    public PlanetSurface? CurrentSurface => s.State.Surface is { } st ? SurfaceFor(s.Galaxy.Planet(st.PlanetId)) : null;

    // ------------------------------------------------------------------ orbit

    private bool Reveal(double eff) => s.Rng.NextDouble() < 0.35 + 0.65 * eff;

    private bool SensorsFail()
    {
        int dmg = s.State.Ship.Damage[ShipSystem.Sensors];
        return dmg > 0 && s.Rng.Next(100) < dmg;
    }

    public ActionResult<SensorReport> Sensors()
    {
        var p = s.CurrentPlanet;
        if (p is null || s.State.Location.Mode != GameMode.Orbit) return ActionResult<SensorReport>.Fail("Sensors need a planet in orbit.");
        if (SensorsFail()) return ActionResult<SensorReport>.Fail("Sensor malfunction! (damaged)");
        double eff = CrewRules.Efficiency(s.Skill(CrewRole.Science));
        var rec = s.State.PlanetRecord(p.Id);
        rec.Scanned = true;
        s.AdvanceHours(1);
        s.PlaySound("scan");
        var report = new SensorReport(
            Reveal(eff) ? $"{p.Mass * 0.8 + 0.2:0.0} x 10^24 kg" : null,
            Reveal(eff) ? (int)p.BioDensity * 20 : null,
            Reveal(eff) ? (int)p.MineralDensity * 20 : null,
            Reveal(eff) ? p.Atmosphere.ToString() : null,
            Reveal(eff) ? p.Hydrosphere.ToString() : null,
            Reveal(eff) ? (p.MineralDensity >= Density.Moderate ? "Mineral-rich crust" : "Common silicates") : null);
        return ActionResult<SensorReport>.Success(report, "Sensor sweep complete.");
    }

    public ActionResult<AnalysisReport> Analyze()
    {
        var p = s.CurrentPlanet;
        if (p is null || s.State.Location.Mode != GameMode.Orbit) return ActionResult<AnalysisReport>.Fail("Analysis needs a planet in orbit.");
        if (SensorsFail()) return ActionResult<AnalysisReport>.Fail("Sensor malfunction! (damaged)");
        double eff = CrewRules.Efficiency(s.Skill(CrewRole.Science));
        var rec = s.State.PlanetRecord(p.Id);
        rec.Analyzed = true;
        s.AdvanceHours(1);
        var report = new AnalysisReport(p.Orbit,
            Reveal(eff) ? p.Type.ToString() : null,
            Reveal(eff) ? p.Gravity : null,
            Reveal(eff) ? p.Atmosphere.ToString() : null,
            Reveal(eff) ? p.AtmosphereComposition : null,
            Reveal(eff) ? p.Temperature.ToString() : null,
            Reveal(eff) ? p.Weather.ToString() : null);
        if (p.Type == PlanetType.Crystal && s.HasArtifact("crystal-cone"))
            s.Emit(EventKind.Info, "The Crystal Cone points to the Nexus of Control at " + NexusLabel(p) + ".", "artifact");
        return ActionResult<AnalysisReport>.Success(report, "Analysis complete.");
    }

    private static string NexusLabel(Planet p)
    {
        var site = p.Sites.First(x => x.Kind == SiteKind.Nexus);
        return PlanetSurface.FormatLatLon(site.Lat, site.Lon);
    }

    /// <summary>Captain → Log Planet: enters the planet in the ship's log (needed for colony recommendations).</summary>
    public ActionResult LogPlanet()
    {
        var p = s.CurrentPlanet;
        if (p is null) return ActionResult.Fail("No planet in orbit.");
        var rec = s.State.PlanetRecord(p.Id);
        if (!rec.Analyzed) return ActionResult.Fail("Analyse the planet before logging it.");
        if (rec.Logged) return ActionResult.Fail("This planet is already logged.");
        rec.Logged = true;
        var sys = s.Galaxy.System(p.SystemId);
        s.Story.Log(LogCategory.PlanetLog, $"Logged {sys.Label} planet {p.Orbit}: {p.Type}, {p.Gravity:0.00} G, {p.Temperature}, {p.Weather}, atmosphere {p.AtmosphereComposition}.");
        return ActionResult.Success("Planet entered in the ship's log.");
    }

    /// <summary>Sites the landing map shows: ruins/messages/artifacts always; the Nexus only with the Cone.</summary>
    public IEnumerable<SurfaceSite> VisibleSites(Planet p)
    {
        var rec = s.State.PlanetRecord(p.Id);
        foreach (var site in p.Sites)
        {
            if (site.Kind == SiteKind.Nexus && !s.HasArtifact("crystal-cone")) continue;
            if (rec.CollectedSites.Contains(site.Id) && site.Kind == SiteKind.Artifact) continue;
            yield return site;
        }
    }

    // ------------------------------------------------------------------ landing

    public ActionResult Land(int lat, int lon)
    {
        var p = s.CurrentPlanet;
        var ship = s.State.Ship;
        if (p is null || s.State.Location.Mode != GameMode.Orbit) return ActionResult.Fail("Not in orbit.");
        if (p.Name == "Arth") return ActionResult.Fail("We can't land on Arth. Dock at Starport instead.");
        if (s.State.PlanetRecord(p.Id).Destroyed) return ActionResult.Fail("Nothing remains of this world but debris.");
        if (!ship.HasTerrainVehicle) return ActionResult.Fail("The terrain vehicle was lost. Replace it at Starport.");
        double cost = ShipRules.LandingFuelPerG * p.Gravity;
        if (ship.Endurium < cost * 2) return ActionResult.Fail($"Insufficient fuel to land and launch ({cost * 2:0.0} m³ needed).");
        ship.Endurium -= cost;
        s.AdvanceHours(1);
        if (s.IsOver) return ActionResult.Fail("");
        if (p.Gravity > ShipRules.MaxLandingGravity || p.Type == PlanetType.GasGiant)
        {
            s.GameOver($"Gravity of {p.Gravity:0.0} G crushed the ISS {ship.Name} during descent.");
            return ActionResult.Fail("Crushed by gravity.");
        }
        var surface = SurfaceFor(p);
        var (x, y) = PlanetSurface.CellFromLatLon(lat, lon);
        // Autopilot sets down on the nearest solid ground.
        (x, y) = NearestPassable(surface, x, y);
        var rec = s.State.PlanetRecord(p.Id);
        if (!rec.Landed) { rec.Landed = true; s.State.Stats.PlanetsLanded++; }
        var st = new SurfaceState { PlanetId = p.Id, TvX = x, TvY = y, ShipX = x, ShipY = y, TvFuel = TvFuelCapacity };
        PopulateCreatures(surface, st);
        s.State.Surface = st;
        s.SetMode(GameMode.Surface);
        s.Emit(EventKind.Info, $"Landed at {PlanetSurface.FormatLatLon(lat, lon)}. The terrain vehicle is deployed.", "land");
        return ActionResult.Success("Landed.");
    }

    private static (int, int) NearestPassable(PlanetSurface surf, int x, int y)
    {
        for (int r = 0; r < 30; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= PlanetSurface.Height) continue;
                    if (PlanetSurface.Passable(surf.At(x + dx, yy))) return (PlanetSurface.WrapX(x + dx), yy);
                }
        return (x, y);
    }

    private void PopulateCreatures(PlanetSurface surf, SurfaceState st)
    {
        var rng = new Random.SplitMix64(surf.Planet.TerrainSeed ^ 0xC0FFEE ^ (ulong)s.State.Clock.Hours);
        int id = 0;
        var rec = s.State.PlanetRecord(surf.Planet.Id);
        foreach (var sp in surf.Species)
        {
            int n = 2 + rng.Next(4);
            for (int i = 0; i < n; i++)
            {
                for (int tries = 0; tries < 50; tries++)
                {
                    int x = PlanetSurface.WrapX(st.TvX + rng.Next(-20, 21));
                    int y = Math.Clamp(st.TvY + rng.Next(-12, 13), 0, PlanetSurface.Height - 1);
                    var k = surf.At(x, y);
                    if (!PlanetSurface.Passable(k) || k == TerrainKind.Mountain) continue;
                    if (WrapDist(x, st.TvX) < 4 && Math.Abs(y - st.TvY) < 4) continue; // keep the landing site clear
                    st.Creatures.Add(new SurfaceCreature
                    {
                        Id = id++, Species = sp.Id, X = x, Y = y, Health = 10 * sp.Size, Hostile = sp.Hostile, Flying = sp.Flying, Size = sp.Size,
                    });
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------------ terrain vehicle

    public LifeformSpecies? Species(string id) => CurrentSurface?.Species.FirstOrDefault(x => x.Id == id);

    public double TvCargoUsed => s.State.Surface?.TvCargo.Sum(c => c.Volume) ?? 0;

    /// <summary>Mineral deposits within sensor range of the TV (range grows with science skill).</summary>
    public IEnumerable<MineralDeposit> VisibleDeposits()
    {
        if (s.State.Surface is not { } st || CurrentSurface is not { } surf) return [];
        int range = 4 + (int)(6 * CrewRules.Efficiency(s.Skill(CrewRole.Science)));
        var mined = s.State.PlanetRecord(st.PlanetId).MinedDeposits;
        return surf.Deposits.Where(d => !mined.Contains(d.Id) && WrapDist(d.X, st.TvX) <= range && Math.Abs(d.Y - st.TvY) <= range);
    }

    public static int WrapDist(int a, int b)
    {
        int d = Math.Abs(a - b) % PlanetSurface.Width;
        return Math.Min(d, PlanetSurface.Width - d);
    }

    public ActionResult MoveTv(int dx, int dy)
    {
        if (s.State.Surface is not { } st || CurrentSurface is not { } surf) return ActionResult.Fail("Not on a planet surface.");
        dx = Math.Sign(dx); dy = Math.Sign(dy);
        if (dx == 0 && dy == 0) return Wait();
        int nx = PlanetSurface.WrapX(st.TvX + dx), ny = st.TvY + dy;
        if (ny < 0 || ny >= PlanetSurface.Height) return ActionResult.Fail("Polar terrain is impassable.");
        if (!PlanetSurface.Passable(surf.At(nx, ny))) return ActionResult.Fail($"The terrain vehicle cannot cross {surf.At(nx, ny).ToString().ToLowerInvariant()}.");
        if (st.Creatures.Any(c => c.X == nx && c.Y == ny && !c.Flying)) return ActionResult.Fail("A lifeform blocks the way.");
        double cost = surf.StepCost(nx, ny) * TemperatureFactor(surf.Planet);
        if (!st.OnFoot)
        {
            if (st.TvFuel < cost)
            {
                st.OnFoot = true;
                s.Emit(EventKind.Danger, "Terrain vehicle out of fuel! The crew is returning to the ship on foot.", "warning");
            }
            else st.TvFuel -= cost;
        }
        st.TvX = nx; st.TvY = ny; st.Steps++;
        return Step();
    }

    private static double TemperatureFactor(Planet p) => p.Temperature is Temperature.Scorching or Temperature.Frigid ? 1.5 : 1.0;

    public ActionResult Wait()
    {
        if (s.State.Surface is null) return ActionResult.Fail("Not on a planet surface.");
        s.State.Surface.Steps++;
        return Step();
    }

    /// <summary>One world step: time, weather, lifeforms, egg fuse.</summary>
    private ActionResult Step()
    {
        var st = s.State.Surface!;
        var surf = CurrentSurface!;
        s.AdvanceHours(0.25);
        if (s.IsOver) return ActionResult.Fail("");
        Weather(surf.Planet);
        if (s.IsOver) return ActionResult.Fail("");
        MoveCreatures(surf, st);
        if (s.IsOver) return ActionResult.Fail("");
        if (st.OnFoot && st.TvX == st.ShipX && st.TvY == st.ShipY)
        {
            s.State.Ship.HasTerrainVehicle = false;
            st.TvCargo.Clear();
            s.Emit(EventKind.Warning, "The crew reached the ship on foot. The terrain vehicle and its cargo are lost.");
            return ReturnToShip();
        }
        if (st.EggFuseAt >= 0 && st.Steps >= st.EggFuseAt)
        {
            s.GameOver("The Black Egg detonated with the crew still on the surface.");
            return ActionResult.Fail("");
        }
        return ActionResult.Success("");
    }

    private void Weather(Planet p)
    {
        if (p.Weather < Galaxy.Weather.Stormy) return;
        double chance = p.Weather switch { Galaxy.Weather.Stormy => 0.01, Galaxy.Weather.Violent => 0.025, _ => 0.05 };
        if (s.Rng.NextDouble() >= chance) return;
        s.Emit(EventKind.Warning, "A violent storm sweeps over the terrain vehicle!", "hullhit");
        s.Crew.InjureRandom(p.Weather >= Galaxy.Weather.VeryViolent ? 25 : 12, "storm");
        // Navigator below 200: the TV can get lost in storms (manual).
        var st = s.State.Surface!;
        if (s.Skill(CrewRole.Navigator) < 200 && s.Rng.Chance(0.5))
        {
            var surf = CurrentSurface!;
            int nx = PlanetSurface.WrapX(st.TvX + s.Rng.Next(-2, 3)), ny = Math.Clamp(st.TvY + s.Rng.Next(-2, 3), 0, PlanetSurface.Height - 1);
            if (PlanetSurface.Passable(surf.At(nx, ny))) { st.TvX = nx; st.TvY = ny; s.Emit(EventKind.Warning, "Disoriented by the storm, the vehicle drifts off course."); }
        }
    }

    private void MoveCreatures(PlanetSurface surf, SurfaceState st)
    {
        foreach (var c in st.Creatures)
        {
            if (c.StunnedTurns > 0) { c.StunnedTurns--; continue; }
            int dist = Math.Max(WrapDist(c.X, st.TvX), Math.Abs(c.Y - st.TvY));
            int mx, my;
            if (c.Hostile && dist <= 6)
            {
                if (dist <= 1)
                {
                    var sp = surf.Species.First(x => x.Id == c.Species);
                    if (s.Rng.Chance(0.3)) s.Crew.InjureRandom(sp.Danger, $"attacked by a {sp.Name}");
                    if (s.IsOver) return;
                    continue;
                }
                int ddx = st.TvX - c.X;
                if (Math.Abs(ddx) > PlanetSurface.Width / 2) ddx = -Math.Sign(ddx);
                mx = Math.Sign(ddx); my = Math.Sign(st.TvY - c.Y);
            }
            else
            {
                if (!s.Rng.Chance(0.4)) continue;
                mx = s.Rng.Next(-1, 2); my = s.Rng.Next(-1, 2);
            }
            int nx = PlanetSurface.WrapX(c.X + mx), ny = Math.Clamp(c.Y + my, 0, PlanetSurface.Height - 1);
            if ((nx == st.TvX && ny == st.TvY) || (!c.Flying && !PlanetSurface.Passable(surf.At(nx, ny)))) continue;
            if (st.Creatures.Any(o => o != c && o.X == nx && o.Y == ny)) continue;
            c.X = nx; c.Y = ny;
        }
    }

    public ActionResult PickUp()
    {
        if (s.State.Surface is not { } st || CurrentSurface is not { } surf) return ActionResult.Fail("Not on a planet surface.");
        var rec = s.State.PlanetRecord(st.PlanetId);

        var site = surf.Planet.Sites.FirstOrDefault(x => PlanetSurface.CellFromLatLon(x.Lat, x.Lon) == (st.TvX, st.TvY));
        if (site is not null && site.Kind != SiteKind.Nexus) return CollectSite(site, st, rec);

        var dep = surf.Deposits.FirstOrDefault(d => d.X == st.TvX && d.Y == st.TvY && !rec.MinedDeposits.Contains(d.Id));
        if (dep is null) return ActionResult.Fail("Nothing here to pick up.");
        double room = TvHold - TvCargoUsed;
        if (room <= 0.01) return ActionResult.Fail("The terrain vehicle's hold is full (50 m³). Return to the ship.");
        double take = Math.Min(room, dep.Amount);
        var name = s.Data.Mineral(dep.MineralId).Name;
        AddTo(st.TvCargo, new CargoItem { Kind = CargoKind.Mineral, Id = dep.MineralId, Name = name, Quantity = take, ValuePerUnit = s.Data.Mineral(dep.MineralId).ValuePerUnit, SourcePlanetId = st.PlanetId });
        if (take >= dep.Amount - 1e-9) rec.MinedDeposits.Add(dep.Id);
        s.State.Stats.MineralsCollected += take;
        s.Emit(EventKind.Success, $"Collected {take:0.#} m³ of {name}.", "mineral");
        return Step() is { Ok: false } r && s.IsOver ? r : ActionResult.Success($"Collected {take:0.#} m³ of {name}.");
    }

    private ActionResult CollectSite(SurfaceSite site, SurfaceState st, PlanetRecord rec)
    {
        switch (site.Kind)
        {
            case SiteKind.Artifact:
                if (rec.CollectedSites.Contains(site.Id)) return ActionResult.Fail("The site has already been excavated.");
                if (TvCargoUsed + 1 > TvHold) return ActionResult.Fail("The terrain vehicle's hold is full.");
                var def = s.Data.Artifact(site.Ref);
                rec.CollectedSites.Add(site.Id);
                AddTo(st.TvCargo, new CargoItem { Kind = CargoKind.Artifact, Id = def.Id, Name = def.Name, Quantity = 1, SourcePlanetId = st.PlanetId });
                s.Story.Log(LogCategory.Artifact, $"Recovered an artifact: the {def.Name}, at {DescribeHere(st)}.");
                if (def.Id == "crystal-orb") s.Story.SetFlag(StoryService.FlagOrbAboard);
                s.Emit(EventKind.Success, $"Artifact recovered: {def.Name}!", "artifact");
                return ActionResult.Success($"Artifact recovered: {def.Name}.");
            case SiteKind.Message:
                var msg = s.Data.Message(site.Ref);
                if (!rec.CollectedSites.Contains(site.Id))
                {
                    rec.CollectedSites.Add(site.Id);
                    AddTo(st.TvCargo, new CargoItem { Kind = CargoKind.Message, Id = msg.Id, Name = msg.Title, Quantity = 1, VolumePerUnit = 0, SourcePlanetId = st.PlanetId });
                    s.Story.Reveal(msg.SetsFlag, msg.LogNote);
                }
                s.Emit(EventKind.Info, $"{msg.Title}: {msg.Text}", "comms");
                return ActionResult.Success(msg.Text);
            case SiteKind.Ruin:
                if (rec.RuinEmptiedHour >= 0 && s.State.Clock.Hours - rec.RuinEmptiedHour < 48)
                    return ActionResult.Fail("The ruins have been picked clean.");
                double room = TvHold - TvCargoUsed;
                double amount = Math.Min(room, 8);
                if (amount <= 0) return ActionResult.Fail("The terrain vehicle's hold is full.");
                rec.RuinEmptiedHour = s.State.Clock.Hours;
                AddTo(st.TvCargo, new CargoItem { Kind = CargoKind.Mineral, Id = "endurium", Name = "Endurium", Quantity = amount, ValuePerUnit = 1000, SourcePlanetId = st.PlanetId });
                s.Emit(EventKind.Success, $"Ancient ruins! Recovered {amount:0.#} m³ of endurium.", "artifact");
                s.Story.Log(LogCategory.Discovery, $"Ancient ruins holding endurium found at {DescribeHere(st)}.");
                return ActionResult.Success("Endurium recovered from the ruins.");
            case SiteKind.Beacon:
                s.Emit(EventKind.Info, "Massive organic structures pulse beneath the ground: a vast hive intelligence.");
                return ActionResult.Success("");
            default:
                return ActionResult.Fail("Nothing here to pick up.");
        }
    }

    private string DescribeHere(SurfaceState st)
    {
        var p = s.Galaxy.Planet(st.PlanetId);
        var sys = s.Galaxy.System(p.SystemId);
        var (lat, lon) = PlanetSurface.LatLonFromCell(st.TvX, st.TvY);
        return $"{sys.Label} planet {p.Orbit}, {PlanetSurface.FormatLatLon(lat, lon)}";
    }

    private static void AddTo(List<CargoItem> list, CargoItem item)
    {
        var same = list.FirstOrDefault(c => c.Kind == item.Kind && c.Id == item.Id && c.Kind == CargoKind.Mineral);
        if (same is not null) same.Quantity += item.Quantity;
        else list.Add(item);
    }

    /// <summary>Lifeform value. RECONSTRUCTION (manual gives criteria only): size, niche and distance from Arth.</summary>
    public int LifeformValue(LifeformSpecies sp, Planet p)
    {
        var sys = s.Galaxy.System(p.SystemId);
        double dist = GalaxyMap.Distance(sys.X, sys.Y, 125, 100);
        double niche = sp.Niche == "predator" ? 1.5 : 1.0;
        return (int)(250 * sp.Size * niche * (1 + dist / 100));
    }

    /// <summary>Fires the TV stunner (stun=true) or laser at the nearest creature in range (3 cells).</summary>
    public ActionResult Fire(bool stun)
    {
        if (s.State.Surface is not { } st || CurrentSurface is not { } surf) return ActionResult.Fail("Not on a planet surface.");
        if (st.OnFoot) return ActionResult.Fail("The crew on foot carries no heavy weapons.");
        var target = st.Creatures
            .Select(c => (c, d: Math.Max(WrapDist(c.X, st.TvX), Math.Abs(c.Y - st.TvY))))
            .Where(t => t.d <= 3).OrderBy(t => t.d).Select(t => t.c).FirstOrDefault();
        if (target is null) return ActionResult.Fail("No lifeform in weapon range.");
        var sp = surf.Species.First(x => x.Id == target.Species);
        if (stun)
        {
            target.StunnedTurns = 8;
            s.Emit(EventKind.Info, $"The {sp.Name} is stunned.", "laser");
        }
        else
        {
            target.Health -= 30;
            s.Emit(EventKind.Info, target.Health <= 0 ? $"The {sp.Name} is destroyed." : $"The {sp.Name} is hit.", "laser");
            if (target.Health <= 0) st.Creatures.Remove(target);
        }
        return Step();
    }

    /// <summary>Captures an adjacent stunned, non-flying lifeform as a specimen.</summary>
    public ActionResult Capture()
    {
        if (s.State.Surface is not { } st || CurrentSurface is not { } surf) return ActionResult.Fail("Not on a planet surface.");
        var target = st.Creatures.FirstOrDefault(c => Math.Max(WrapDist(c.X, st.TvX), Math.Abs(c.Y - st.TvY)) <= 1 && c.StunnedTurns > 0);
        if (target is null) return ActionResult.Fail("No stunned lifeform adjacent to the vehicle.");
        var sp = surf.Species.First(x => x.Id == target.Species);
        if (target.Flying) return ActionResult.Fail("Flying and floating lifeforms cannot be captured.");
        if (st.TvCargo.Any(c => c.Kind == CargoKind.Lifeform && c.Id == sp.Id) ||
            s.State.Ship.Cargo.Any(c => c.Kind == CargoKind.Lifeform && c.Id == sp.Id))
            return ActionResult.Fail("Duplicate specimen.");
        if (TvCargoUsed + sp.Size > TvHold) return ActionResult.Fail("The terrain vehicle's hold is full.");
        st.Creatures.Remove(target);
        int value = LifeformValue(sp, surf.Planet);
        AddTo(st.TvCargo, new CargoItem { Kind = CargoKind.Lifeform, Id = sp.Id, Name = $"Specimen: {sp.Name}", Quantity = 1, VolumePerUnit = sp.Size, ValuePerUnit = value, SourcePlanetId = st.PlanetId });
        s.State.RecordedSpecies.Add(sp.Id);
        s.Emit(EventKind.Success, $"{sp.Name} specimen placed in stasis.", "mineral");
        s.Story.Log(LogCategory.Discovery, $"Captured a specimen of {sp.Name} ({sp.Niche}, size {sp.Size}).");
        return ActionResult.Success("Specimen captured.");
    }

    /// <summary>TV scan: records bio-data for lifeforms within 4 cells (scan works on lifeforms only).</summary>
    public ActionResult ScanLife()
    {
        if (s.State.Surface is not { } st || CurrentSurface is not { } surf) return ActionResult.Fail("Not on a planet surface.");
        var near = st.Creatures.Where(c => Math.Max(WrapDist(c.X, st.TvX), Math.Abs(c.Y - st.TvY)) <= 4)
            .Select(c => surf.Species.First(x => x.Id == c.Species)).Distinct().ToList();
        if (near.Count == 0) return ActionResult.Fail("No lifeforms within scanner range.");
        var lines = new List<string>();
        foreach (var sp in near)
        {
            lines.Add($"{sp.Name}: size {sp.Size}, {sp.Niche}, {(sp.Hostile ? "HOSTILE" : "passive")}{(sp.Flying ? ", airborne" : "")}");
            string recId = "rec:" + sp.Id;
            if (s.State.RecordedSpecies.Add(recId) && TvCargoUsed <= TvHold)
                AddTo(st.TvCargo, new CargoItem { Kind = CargoKind.Lifeform, Id = recId, Name = $"Bio-data: {sp.Name}", Quantity = 1, VolumePerUnit = 0, ValuePerUnit = LifeformValue(sp, surf.Planet) * 3 / 10, SourcePlanetId = st.PlanetId });
        }
        s.PlaySound("scan");
        var text = string.Join("\n", lines);
        s.Emit(EventKind.Info, text);
        return ActionResult.Success(text);
    }

    /// <summary>Plants a Black Egg at the TV's position. It detonates when the ship launches.</summary>
    public ActionResult PlantEgg()
    {
        if (s.State.Surface is not { } st || CurrentSurface is not { } surf) return ActionResult.Fail("Not on a planet surface.");
        var egg = st.TvCargo.FirstOrDefault(c => c.Id == "black-egg") ?? s.State.Ship.Cargo.FirstOrDefault(c => c.Id == "black-egg");
        if (egg is null) return ActionResult.Fail("No Black Egg aboard.");
        if (s.Story.HasFlag("egg_fuse_" + st.PlanetId)) return ActionResult.Fail("A Black Egg is already armed here.");
        (st.TvCargo.Contains(egg) ? st.TvCargo : s.State.Ship.Cargo).Remove(egg);
        s.Story.SetFlag("egg_fuse_" + st.PlanetId);
        st.EggFuseAt = st.Steps + EggFuseSteps;
        var nexus = surf.Planet.Sites.FirstOrDefault(x => x.Kind == SiteKind.Nexus);
        bool atNexus = nexus is not null && PlanetSurface.CellFromLatLon(nexus.Lat, nexus.Lon) == (st.TvX, st.TvY);
        if (atNexus) s.Story.SetFlag(StoryService.FlagEggPlanted);
        s.Emit(EventKind.Danger, $"Black Egg armed{(atNexus ? " at the Nexus of Control" : "")}! Return to the ship and launch before it detonates ({EggFuseSteps} steps).", "warning");
        return ActionResult.Success("Egg armed.");
    }

    public bool AtShip => s.State.Surface is { } st && st.TvX == st.ShipX && st.TvY == st.ShipY;

    /// <summary>Re-entering the ship refuels the TV and moves its cargo into the hold.</summary>
    public ActionResult ReturnToShip()
    {
        if (s.State.Surface is not { } st) return ActionResult.Fail("Not on a planet surface.");
        if (!AtShip) return ActionResult.Fail("The terrain vehicle must be at the ship.");
        var ship = s.State.Ship;
        var cat = s.Data.Ship;
        var lost = new List<string>();
        foreach (var item in st.TvCargo.ToList())
        {
            if (item.Kind == CargoKind.Mineral && item.Id == "endurium")
            {
                double room = ShipRules.EnduriumCapacity(ship, cat) - ship.Endurium;
                double take = Math.Min(room, item.Quantity);
                ship.Endurium += take;
                if (take < item.Quantity) lost.Add($"{item.Quantity - take:0.#} m³ endurium");
                continue;
            }
            double free = ShipRules.CargoFree(ship, cat);
            if (item.Volume <= free + 1e-9) AddTo(ship.Cargo, item);
            else if (item.Kind == CargoKind.Mineral && free > 0)
            {
                AddTo(ship.Cargo, new CargoItem { Kind = item.Kind, Id = item.Id, Name = item.Name, Quantity = free, ValuePerUnit = item.ValuePerUnit, SourcePlanetId = item.SourcePlanetId });
                lost.Add($"{item.Quantity - free:0.#} m³ {item.Name}");
            }
            else lost.Add(item.Name);
        }
        st.TvCargo.Clear();
        st.TvFuel = TvFuelCapacity;
        st.OnFoot = false;
        if (lost.Count > 0) s.Emit(EventKind.Warning, "Cargo hold full. Left on the surface: " + string.Join(", ", lost) + ".");
        else s.Emit(EventKind.Info, "Terrain vehicle aboard. Cargo transferred to the hold.");
        return ActionResult.Success("Back aboard.");
    }

    /// <summary>Launch from the surface back to orbit. Detonates any armed Black Egg.</summary>
    public ActionResult Launch()
    {
        if (s.State.Surface is not { } st) return ActionResult.Fail("Not on a planet surface.");
        if (!AtShip && !st.OnFoot) return ActionResult.Fail("Return the terrain vehicle to the ship before launching.");
        if (AtShip) ReturnToShip();
        var p = s.Galaxy.Planet(st.PlanetId);
        var ship = s.State.Ship;
        ship.Endurium = Math.Max(0, ship.Endurium - ShipRules.LandingFuelPerG * p.Gravity);
        s.State.Surface = null;
        s.SetMode(GameMode.Orbit);
        s.Emit(EventKind.Info, "Lift-off. Returning to orbit.", "launch");
        if (s.Story.HasFlag("egg_fuse_" + p.Id)) Detonate(p);
        return ActionResult.Success("Launched.");
    }

    private void Detonate(Planet p)
    {
        s.State.Flags.Remove("egg_fuse_" + p.Id);
        var rec = s.State.PlanetRecord(p.Id);
        var sys = s.Galaxy.System(p.SystemId);
        if (p.Type == PlanetType.Crystal)
        {
            if (s.Story.HasFlag(StoryService.FlagEggPlanted))
            {
                rec.Destroyed = true;
                s.Story.SetFlag(StoryService.FlagCrystalDestroyed);
                s.Emit(EventKind.Success, "The Black Egg detonates at the Nexus of Control. The Crystal Planet shatters!", "explosion");
                s.Story.TryWin();
            }
            else s.Emit(EventKind.Warning, "The Black Egg detonates, but the crystal world is unharmed. The Nexus of Control must be struck.", "explosion");
            return;
        }
        rec.Destroyed = true;
        s.Emit(EventKind.Warning, $"The Black Egg detonates. {(p.Name ?? $"Planet {p.Orbit}")} is destroyed.", "explosion");
        s.Story.Log(LogCategory.Mission, $"Destroyed {sys.Label} planet {p.Orbit} with a Black Egg.");
        if (p.Sites.Any(x => x.Ref == "uhlek-mind"))
        {
            s.Story.SetFlag(StoryService.FlagUhlekMindDestroyed);
            s.Story.Log(LogCategory.Mission, "The Uhlek hive mind is destroyed. Uhlek fleets are in disarray.");
        }
        if (p.Name == "Elan")
        {
            var rel = s.State.Relation("elowan");
            rel.PermanentEnemy = true;
            rel.Attitude = -100;
            s.Story.Log(LogCategory.Contact, "Elan was the Elowan nursery world. The Elowan are now our mortal enemies forever.");
        }
    }

    public void AddArtifact(string id, bool analyzed)
    {
        var def = s.Data.Artifact(id);
        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Artifact, Id = id, Name = def.Name, Quantity = 1, Analyzed = analyzed, ValuePerUnit = analyzed ? def.Value : 0 });
    }
}

/// <summary>Action result carrying a value.</summary>
public readonly record struct ActionResult<T>(bool Ok, string Message, T? Value)
{
    public static ActionResult<T> Success(T value, string msg = "") => new(true, msg, value);
    public static ActionResult<T> Fail(string msg) => new(false, msg, default);
}
