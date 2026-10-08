using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;

namespace AVAFlight.Core.Engine;

/// <summary>
/// Hyperspace and in-system flight. Time model: TIME-STEPPED. The view calls <see cref="Fly"/>
/// with a direction and a real-time delta; the engine converts that to game hours at a fixed rate,
/// moves the ship, burns fuel, advances the clock and rolls for encounters. Same inputs + same RNG
/// give the same result, so flights can be replayed in tests.
/// </summary>
public sealed class NavigationService(GameSession s)
{
    /// <summary>Game hours that pass per real second of flight (presentation pacing only).</summary>
    public const double HyperspaceHoursPerSecond = 6;
    public const double SystemHoursPerSecond = 1.5;
    /// <summary>In-system radius in system units; flying past it returns to hyperspace.</summary>
    public const double SystemRadius = 100;
    /// <summary>System units travelled per game hour (in-system flight is short-range sub-light).</summary>
    public const double SystemSpeedPerHour = 8;
    public const double OrbitDistance = 5;

    private bool _crystalWarned;
    private bool _starHeatWarned;

    public double EncounterRateMultiplier { get; set; } = 1.0;

    // ------------------------------------------------------------------ star map / course

    public double Distance(double x, double y) =>
        GalaxyMap.Distance(s.State.Location.HyperX, s.State.Location.HyperY, x, y);

    public double FuelFor(double distance) =>
        distance * ShipRules.FuelPerCoordinate(s.State.Ship, s.HasAnalyzedArtifact("tesseract"));

    public double HoursFor(double distance) => distance * ShipRules.HoursPerCoordinate(s.State.Ship);

    public double Range => ShipRules.RangeCoordinates(s.State.Ship, s.HasAnalyzedArtifact("tesseract"));

    /// <summary>Navigator can see fluxes above skill 150 (manual) or with the Ring Device.</summary>
    public bool CanSeeFluxes => s.Skill(CrewRole.Navigator) > 150 || s.HasArtifact("ring-device");

    /// <summary>Science above 150 detects aliens at long range (manual).</summary>
    public bool LongRangeSensors => s.Skill(CrewRole.Science) > 150;

    /// <summary>
    /// Sets a straight-line cruise heading toward a map coordinate (the original's cruise control,
    /// with the star-map readout). Not an autopilot: no pathing around hazards, fluxes or nebulae.
    /// </summary>
    public ActionResult SetCourse(int x, int y)
    {
        if (s.State.Location.Mode != GameMode.Hyperspace) return ActionResult.Fail("Courses are plotted in hyperspace.");
        if (s.State.Location.PositionLost) return ActionResult.Fail("Position unknown. The navigator must re-fix our position first.");
        x = Math.Clamp(x, 0, GalaxyMap.Width - 1);
        y = Math.Clamp(y, 0, GalaxyMap.Height - 1);
        s.State.Location.CruiseX = x;
        s.State.Location.CruiseY = y;
        double d = Distance(x, y);
        return ActionResult.Success($"Course laid in for {x},{y}: {d:0.0} coordinates, {FuelFor(d):0.0} m³ endurium, about {HoursFor(d) / 24:0.0} days.");
    }

    public void ClearCourse()
    {
        s.State.Location.CruiseX = null;
        s.State.Location.CruiseY = null;
    }

    // ------------------------------------------------------------------ flight

    /// <summary>
    /// Advances flight by <paramref name="realSeconds"/>. (dirX, dirY) is the steering input
    /// (-1..1 each); zero input with a course set cruises toward the destination.
    /// </summary>
    public void Fly(double dirX, double dirY, double realSeconds)
    {
        var loc = s.State.Location;
        if (s.IsOver || realSeconds <= 0) return;
        if (loc.Mode == GameMode.Hyperspace) FlyHyperspace(dirX, dirY, realSeconds * HyperspaceHoursPerSecond);
        else if (loc.Mode == GameMode.System) FlySystem(dirX, dirY, realSeconds * SystemHoursPerSecond);
    }

    private (double dx, double dy) Normalize(double dx, double dy)
    {
        double len = Math.Sqrt(dx * dx + dy * dy);
        return len < 1e-6 ? (0, 0) : (dx / len, dy / len);
    }

    private bool EngineFails()
    {
        int dmg = s.State.Ship.Damage[ShipSystem.Engines];
        return dmg > 0 && s.Rng.Next(100) < dmg / 10; // per-step failure chance scaled down for continuous flight
    }

    public void FlyHyperspace(double dirX, double dirY, double hours)
    {
        var loc = s.State.Location;
        var ship = s.State.Ship;
        bool manual = Math.Abs(dirX) > 0.01 || Math.Abs(dirY) > 0.01;
        if (manual) ClearCourse();
        (double dx, double dy) = manual ? Normalize(dirX, dirY)
            : loc.CruiseX is int cx && loc.CruiseY is int cy ? Normalize(cx - loc.HyperX, cy - loc.HyperY) : (0, 0);

        if (dx == 0 && dy == 0)
        {
            s.AdvanceHours(hours * 0.25); // drifting/idle: the clock still runs, slowly
            TryRefixPosition(hours * 0.25);
            return;
        }
        if (ship.Endurium <= 0)
        {
            s.Emit(EventKind.Danger, "Out of fuel! Use Communications > Distress to call for a tow.", "error");
            ClearCourse();
            return;
        }
        if (EngineFails()) { s.Emit(EventKind.Warning, "Engines sputter: damaged engines lose thrust."); return; }

        double speed = 1.0 / ShipRules.HoursPerCoordinate(ship);
        double step = speed * hours;
        if (loc.CruiseX is int tx && loc.CruiseY is int ty && !manual)
            step = Math.Min(step, GalaxyMap.Distance(loc.HyperX, loc.HyperY, tx, ty));
        double fuel = FuelFor(step);
        if (fuel > ship.Endurium) { step *= ship.Endurium / fuel; fuel = ship.Endurium; }

        double oldX = loc.HyperX, oldY = loc.HyperY;
        loc.HyperX = Math.Clamp(loc.HyperX + dx * step, 0, GalaxyMap.Width - 1);
        loc.HyperY = Math.Clamp(loc.HyperY + dy * step, 0, GalaxyMap.Height - 1);
        ship.Endurium = Math.Max(0, ship.Endurium - fuel);
        s.AdvanceHours(step * ShipRules.HoursPerCoordinate(ship));
        if (s.IsOver) return;
        TryRefixPosition(step * ShipRules.HoursPerCoordinate(ship));
        LowFuelWarning();

        if (s.Galaxy.InNebula(loc.HyperX, loc.HyperY) && ship.ShieldsUp)
        {
            ship.ShieldsUp = false;
            s.Emit(EventKind.Warning, "Shields do not function inside a nebula.");
        }

        // Fluxes: entering an endpoint jumps the ship to the paired endpoint.
        foreach (var f in s.Galaxy.Fluxes)
        {
            foreach (var (ax, ay, bx, by) in new[] { (f.X1, f.Y1, f.X2, f.Y2), (f.X2, f.Y2, f.X1, f.Y1) })
            {
                if (GalaxyMap.Distance(loc.HyperX, loc.HyperY, ax, ay) < 0.6 && GalaxyMap.Distance(oldX, oldY, ax, ay) >= 0.6)
                {
                    loc.HyperX = bx + dx * 1.0;
                    loc.HyperY = by + dy * 1.0;
                    ClearCourse();
                    bool lost = s.Skill(CrewRole.Navigator) <= 150;
                    loc.PositionLost = lost;
                    s.Emit(EventKind.Warning, lost
                        ? "We've hit a continuum flux! Our position is unknown until the navigator can re-fix it."
                        : $"Continuum flux traversed. Emerged at {bx},{by}.", "hyperspace");
                    s.Story.Log(LogCategory.Navigation, $"Flux discovered linking {ax},{ay} and {bx},{by}.");
                    return;
                }
            }
        }

        // Arrival: cruising to a star enters it; manual flight enters when crossing onto a star.
        var sys = s.Galaxy.SystemAt((int)Math.Round(loc.HyperX), (int)Math.Round(loc.HyperY));
        bool arrivedAtCourse = loc.CruiseX is int ax2 && loc.CruiseY is int ay2 &&
                               GalaxyMap.Distance(loc.HyperX, loc.HyperY, ax2, ay2) < 0.05;
        if (sys is not null)
        {
            bool onStar = GalaxyMap.Distance(loc.HyperX, loc.HyperY, sys.X, sys.Y) < 0.5;
            bool wasOnStar = GalaxyMap.Distance(oldX, oldY, sys.X, sys.Y) < 0.5;
            if ((arrivedAtCourse && onStar) || (manual && onStar && !wasOnStar))
            {
                EnterSystem(sys, dx, dy);
                return;
            }
        }
        if (arrivedAtCourse)
        {
            ClearCourse();
            s.Emit(EventKind.Info, "Destination reached.");
        }

        RollEncounter(step * ShipRules.HoursPerCoordinate(ship));
    }

    private double _lowFuelLevel = double.MaxValue;

    private void LowFuelWarning()
    {
        double perCoord = FuelFor(1);
        double e = s.State.Ship.Endurium;
        // Decompiled (-ENDURIUM): warnings below 150x and 75x the per-tick use.
        foreach (double threshold in new[] { perCoord * 75 / 6, perCoord * 150 / 6 })
        {
            if (e < threshold && _lowFuelLevel >= threshold)
                s.Emit(EventKind.Warning, $"Low fuel: {e:0.0} m³ endurium remaining.", "warning");
        }
        _lowFuelLevel = e;
    }

    private void TryRefixPosition(double hours)
    {
        var loc = s.State.Location;
        if (!loc.PositionLost) return;
        // Fan model: per-hour chance to re-fix = navigation efficiency.
        double p = 1 - Math.Pow(1 - Math.Max(0.02, CrewRules.Efficiency(s.Skill(CrewRole.Navigator))), hours);
        if (s.Rng.NextDouble() < p)
        {
            loc.PositionLost = false;
            s.Emit(EventKind.Success, $"Navigator reports: position re-fixed at {loc.HyperX:0},{loc.HyperY:0}.");
        }
    }

    public void EnterSystem(StarSystem sys, double dx = 1, double dy = 0)
    {
        var loc = s.State.Location;
        loc.SystemId = sys.Id;
        loc.HyperX = sys.X;
        loc.HyperY = sys.Y;
        ClearCourse();
        (dx, dy) = Normalize(dx, dy);
        if (dx == 0 && dy == 0) dx = 1;
        loc.SysX = -dx * (SystemRadius - 8);
        loc.SysY = -dy * (SystemRadius - 8);
        loc.OrbitPlanetId = null;
        _crystalWarned = false;
        _starHeatWarned = false;
        bool first = s.State.VisitedSystems.Add(sys.Id);
        if (first) s.State.Stats.SystemsVisited++;
        s.SetMode(GameMode.System);
        string cond = s.Story.StellarCondition(sys);
        s.Emit(EventKind.Info, $"Entering system {sys.Label} (class {sys.Class}, {sys.Planets.Count} planets). Stellar condition: {cond}.");
        if (first && sys.Name is not null) s.Story.Log(LogCategory.Navigation, $"Visited {sys.Name} at {sys.X},{sys.Y}.");
        if (sys.X == 192 && sys.Y == 152 && s.HasArtifact("crystal-orb"))
            s.Emit(EventKind.Info, "The Crystal Orb is glowing.", "artifact");
    }

    /// <summary>In-system planet position (static orbits; angle derived from the planet id).</summary>
    public static (double X, double Y) PlanetPosition(Planet p)
    {
        double r = 11 * p.Orbit + 4;
        double a = (p.Id * 2.399963) % (2 * Math.PI); // golden-angle spread
        return (Math.Cos(a) * r, Math.Sin(a) * r);
    }

    public Planet? NearestPlanet(out double distance)
    {
        distance = double.MaxValue;
        Planet? best = null;
        if (s.CurrentSystem is not { } sys) return null;
        foreach (var p in sys.Planets)
        {
            var (px, py) = PlanetPosition(p);
            double d = GalaxyMap.Distance(px, py, s.State.Location.SysX, s.State.Location.SysY);
            if (d < distance) { distance = d; best = p; }
        }
        return best;
    }

    public void FlySystem(double dirX, double dirY, double hours)
    {
        var loc = s.State.Location;
        var ship = s.State.Ship;
        var (dx, dy) = Normalize(dirX, dirY);
        if (dx == 0 && dy == 0) { s.AdvanceHours(hours * 0.25); return; }
        if (ship.Endurium <= 0) { s.Emit(EventKind.Danger, "Out of fuel! Use Communications > Distress.", "error"); return; }
        double step = SystemSpeedPerHour * (0.6 + ShipRules.Acceleration(ship) / 12.5) * hours;
        // RECONSTRUCTION: in-system manoeuvring burns 1/50 of the hyperspace rate per system unit.
        ship.Endurium = Math.Max(0, ship.Endurium - FuelFor(step) / 50);
        loc.SysX += dx * step;
        loc.SysY += dy * step;
        s.AdvanceHours(hours);
        if (s.IsOver) return;

        double r = Math.Sqrt(loc.SysX * loc.SysX + loc.SysY * loc.SysY);
        if (r > SystemRadius) { LeaveSystem(dx, dy); return; }
        StarHeat(r, hours);
        if (s.IsOver) return;
        CrystalDefence();
        if (s.IsOver) return;
        RollEncounter(hours * 0.5);
    }

    private void StarHeat(double r, double hours)
    {
        if (r > 9) { _starHeatWarned = false; return; }
        if (!_starHeatWarned) { s.Emit(EventKind.Danger, "Temperature is increasing!", "warning"); _starHeatWarned = true; }
        if (r < 5)
        {
            var ship = s.State.Ship;
            int dmg = (int)Math.Ceiling(60 * hours);
            ship.HullPoints -= dmg;
            s.Emit(EventKind.Danger, "The hull is melting!");
            if (ship.HullPoints <= 0) s.GameOver($"The ISS {ship.Name} flew too close to the star and melted.");
        }
    }

    private void CrystalDefence()
    {
        if (s.CurrentSystem is not { X: 192, Y: 152 } || s.Story.HasFlag(StoryService.FlagCrystalDestroyed)) return;
        var crystal = s.CurrentSystem.Planets[0];
        var (px, py) = PlanetPosition(crystal);
        double d = GalaxyMap.Distance(px, py, s.State.Location.SysX, s.State.Location.SysY);
        if (s.HasArtifact("crystal-orb")) return;
        if (d < 30 && !_crystalWarned)
        {
            _crystalWarned = true;
            s.Emit(EventKind.Danger, "A mighty force radiates from the crystal world. Hull temperature is rising rapidly!", "warning");
        }
        if (d < 15) s.GameOver("Approaching the Crystal Planet without protection: the ship melted.");
    }

    public void LeaveSystem(double dx = 1, double dy = 0)
    {
        var loc = s.State.Location;
        var sys = s.CurrentSystem;
        loc.OrbitPlanetId = null;
        loc.SystemId = null;
        (dx, dy) = Normalize(dx, dy);
        if (sys is not null)
        {
            loc.HyperX = Math.Clamp(sys.X + dx * 0.7, 0, GalaxyMap.Width - 1);
            loc.HyperY = Math.Clamp(sys.Y + dy * 0.7, 0, GalaxyMap.Height - 1);
        }
        s.SetMode(GameMode.Hyperspace);
        s.Emit(EventKind.Info, "Entering hyperspace.", "hyperspace");
    }

    public ActionResult EnterOrbit()
    {
        if (s.State.Location.Mode != GameMode.System) return ActionResult.Fail("Not in a star system.");
        var p = NearestPlanet(out double d);
        if (p is null || d > OrbitDistance) return ActionResult.Fail("No planet close enough to orbit. Fly over a planet first.");
        if (p.SystemId == s.CurrentSystem!.Id && p.Type == PlanetType.Crystal && !s.HasArtifact("crystal-orb"))
        {
            s.GameOver("Approaching the Crystal Planet without protection: the ship melted.");
            return ActionResult.Fail("The ship melted.");
        }
        s.State.Location.OrbitPlanetId = p.Id;
        s.SetMode(GameMode.Orbit);
        var sys = s.CurrentSystem;
        s.Emit(EventKind.Info, $"Entering orbit of {(p.Name ?? $"planet {p.Orbit}")} in system {sys.Label}.");
        return ActionResult.Success("In orbit.");
    }

    public ActionResult LeaveOrbit()
    {
        if (s.State.Location.Mode != GameMode.Orbit) return ActionResult.Fail("Not in orbit.");
        var p = s.CurrentPlanet!;
        var (px, py) = PlanetPosition(p);
        s.State.Location.SysX = px + OrbitDistance + 1;
        s.State.Location.SysY = py;
        s.State.Location.OrbitPlanetId = null;
        // Ruins restock after leaving orbit (fan observation, PC version).
        s.SetMode(GameMode.System);
        s.Emit(EventKind.Info, "Leaving orbit.");
        return ActionResult.Success("Left orbit.");
    }

    // ------------------------------------------------------------------ encounters

    /// <summary>
    /// Encounter roll. RECONSTRUCTION ASSUMPTION: per-hour rates (unclaimed 0.4%, territory 2%,
    /// Uhlek space 4%, halved once the Uhlek mind world is destroyed); fleet race by territory.
    /// </summary>
    public void RollEncounter(double hours)
    {
        if (s.State.Location.Mode is not (GameMode.Hyperspace or GameMode.System) || s.IsOver) return;
        var loc = s.State.Location;
        string? territory = TerritoryAt(loc.HyperX, loc.HyperY);
        double rate = territory switch
        {
            null => 0.004,
            "uhlek" => s.Story.HasFlag(StoryService.FlagUhlekMindDestroyed) ? 0.012 : 0.04,
            _ => 0.02,
        };
        if (s.HasArtifact("dodecahedron")) rate *= 2;
        rate *= EncounterRateMultiplier;
        double p = 1 - Math.Pow(1 - rate, hours);
        if (s.Rng.NextDouble() >= p) return;

        string race = territory ?? PickWanderer();
        if (race == "mechan" && territory is null) race = "nomad";
        s.Combat.BeginEncounter(race);
    }

    private string PickWanderer()
    {
        int r = s.Rng.Next(100);
        return r switch { < 30 => "nomad", < 50 => "minstrel", < 62 => "unknown", < 75 => "spemin", < 88 => "thrynn", _ => "velox" };
    }

    public string? TerritoryAt(double x, double y)
    {
        var sys = s.CurrentSystem;
        if (sys?.Territory is not null && s.State.Location.Mode != GameMode.Hyperspace) return sys.Territory;
        return s.Data.World.Territories
            .Select(t => (t.RaceId, d: GalaxyMap.Distance(t.X, t.Y, x, y), t.Radius))
            .Where(t => t.d <= t.Radius)
            .OrderBy(t => t.d)
            .Select(t => t.RaceId)
            .FirstOrDefault();
    }
}
