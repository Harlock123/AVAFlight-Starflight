using AVAFlight.Core.Data;
using AVAFlight.Core.Random;

namespace AVAFlight.Core.Galaxy;

/// <summary>
/// Builds the galaxy: authored anchor systems (story locations, homeworlds) from
/// <see cref="WorldDefinition"/> are placed first, then the remaining systems are generated from
/// the seed until the documented totals of the original (270 systems, 811 planets) are reached.
/// RECONSTRUCTION ASSUMPTION: the original star map is not copied; only story-relevant coordinates
/// documented in public walkthroughs are fixed. Same seed + same data => identical galaxy.
/// </summary>
public static class GalaxyGenerator
{
    public const int TargetSystems = 270;
    public const int TargetPlanets = 811;
    public const ulong DefaultSeed = 0x5F1986_0815UL;

    public static GalaxyMap Generate(ulong seed, WorldDefinition world)
    {
        var rng = new SplitMix64(seed);
        var systems = new List<StarSystem>();
        var occupied = new HashSet<(int, int)>();
        int planetId = 0;

        // 1. Authored anchors.
        foreach (var a in world.Systems)
        {
            var sys = new StarSystem
            {
                Id = systems.Count, X = a.X, Y = a.Y, Class = a.Class, Name = a.Name, Territory = a.Territory,
            };
            var sysRng = SplitMix64.Derive(seed, (ulong)(a.X * 1000 + a.Y));
            int count = Math.Max(a.PlanetCount, a.Planets.Count == 0 ? 0 : a.Planets.Max(p => p.Orbit));
            for (int orbit = 1; orbit <= count; orbit++)
            {
                var def = a.Planets.FirstOrDefault(p => p.Orbit == orbit);
                sys.Planets.Add(def is null
                    ? GeneratePlanet(sysRng, planetId++, sys, orbit)
                    : FromDefinition(sysRng, planetId++, sys, def));
            }
            systems.Add(sys);
            occupied.Add((a.X, a.Y));
        }

        // 2. Procedural systems: rejection-sample positions keeping a minimum spacing so the map
        //    reads clearly, with mild clustering toward "constellation" centres.
        var clusters = Enumerable.Range(0, 34)
            .Select(_ => (X: rng.Next(8, GalaxyMap.Width - 8), Y: rng.Next(8, GalaxyMap.Height - 8)))
            .ToArray();
        int remainingPlanets = TargetPlanets - systems.Sum(s => s.Planets.Count);
        int guard = 0;
        while (systems.Count < TargetSystems && guard++ < 200_000)
        {
            int x, y;
            if (rng.Chance(0.7))
            {
                var c = clusters[rng.Next(clusters.Length)];
                x = c.X + (int)Math.Round((rng.NextBell() - 0.5) * 34);
                y = c.Y + (int)Math.Round((rng.NextBell() - 0.5) * 34);
            }
            else
            {
                x = rng.Next(2, GalaxyMap.Width - 2);
                y = rng.Next(2, GalaxyMap.Height - 2);
            }
            if (x < 2 || y < 2 || x >= GalaxyMap.Width - 2 || y >= GalaxyMap.Height - 2) continue;
            if (occupied.Any(o => Math.Abs(o.Item1 - x) + Math.Abs(o.Item2 - y) < 5)) continue;

            int systemsLeft = TargetSystems - systems.Count;
            var cls = RollClass(rng);
            var sys = new StarSystem { Id = systems.Count, X = x, Y = y, Class = cls };
            // Spread the remaining planet budget so the total lands exactly on the target.
            double avg = (double)remainingPlanets / systemsLeft;
            int lo = Math.Max(0, remainingPlanets - 8 * (systemsLeft - 1));
            int hi = Math.Min(8, remainingPlanets);
            int n = Math.Clamp((int)Math.Round(avg + (rng.NextDouble() - 0.5) * 4), lo, hi);
            var sysRng = SplitMix64.Derive(seed, (ulong)(x * 1000 + y));
            for (int orbit = 1; orbit <= n; orbit++) sys.Planets.Add(GeneratePlanet(sysRng, planetId++, sys, orbit));
            remainingPlanets -= n;
            systems.Add(sys);
            occupied.Add((x, y));
        }

        // 3. Territories: unclaimed systems near a race's centre belong to that race.
        foreach (var sys in systems.Where(s => s.Territory is null))
        {
            var owner = world.Territories
                .Select(t => (t, d: GalaxyMap.Distance(t.X, t.Y, sys.X, sys.Y)))
                .Where(p => p.d <= p.t.Radius)
                .OrderBy(p => p.d)
                .Select(p => p.t.RaceId)
                .FirstOrDefault();
            sys.Territory = owner;
        }

        var fluxes = world.Fluxes.Select((f, i) => new Flux(i, f.X1, f.Y1, f.X2, f.Y2)).ToList();
        var nebulae = world.Nebulae.Select((n, i) => new Nebula(i, n.X, n.Y, n.Radius)).ToList();

        return new GalaxyMap { Seed = seed, Systems = systems, Fluxes = fluxes, Nebulae = nebulae };
    }

    private static SpectralClass RollClass(IGameRandom rng)
    {
        // Skewed toward cooler stars, as in real stellar populations.
        int r = rng.Next(100);
        return r switch
        {
            < 2 => SpectralClass.O,
            < 6 => SpectralClass.B,
            < 14 => SpectralClass.A,
            < 26 => SpectralClass.F,
            < 44 => SpectralClass.G,
            < 68 => SpectralClass.K,
            _ => SpectralClass.M,
        };
    }

    private static readonly string[] Gases =
        ["Nitrogen", "Oxygen", "Carbon dioxide", "Methane", "Ammonia", "Hydrogen", "Helium", "Argon", "Sulfur dioxide", "Chlorine"];

    public static Planet GeneratePlanet(IGameRandom rng, int id, StarSystem sys, int orbit)
    {
        // Star heat falls off with orbit; hotter stars push the habitable band outward.
        double starHeat = sys.Class switch
        {
            SpectralClass.O => 3.2, SpectralClass.B => 2.6, SpectralClass.A => 2.0, SpectralClass.F => 1.5,
            SpectralClass.G => 1.2, SpectralClass.K => 0.9, _ => 0.65,
        };
        double heat = starHeat * 2.4 / orbit + (rng.NextDouble() - 0.5) * 0.4;
        int mass = Math.Clamp(1 + (int)(rng.NextBell() * 5), 1, 5);

        PlanetType type;
        int roll = rng.Next(100);
        if (orbit >= 4 && roll < 28) type = PlanetType.GasGiant;
        else if (roll < 8) type = PlanetType.Asteroid;
        else if (heat > 2.2) type = PlanetType.Molten;
        else if (heat < 0.55) type = PlanetType.Frozen;
        else if (heat is > 0.8 and < 1.6 && rng.Chance(0.35)) type = rng.Chance(0.5) ? PlanetType.Ocean : PlanetType.Jungle;
        else if (heat > 1.4 && rng.Chance(0.4)) type = PlanetType.Desert;
        else type = PlanetType.Rock;
        if (type == PlanetType.GasGiant) mass = 5;
        if (type == PlanetType.Asteroid) mass = 1;

        var temperature = heat switch
        {
            > 2.2 => Temperature.Scorching, > 1.5 => Temperature.Hot, > 0.8 => Temperature.Temperate,
            > 0.5 => Temperature.Cold, _ => Temperature.Frigid,
        };
        double gravity = type == PlanetType.GasGiant
            ? Math.Round(4 + rng.NextDouble() * 8, 2)
            : Math.Round(Math.Max(0.05, mass * 0.45 + (rng.NextDouble() - 0.5) * 0.6), 2);

        var atmosphere = type switch
        {
            PlanetType.Asteroid => Atmosphere.None,
            PlanetType.GasGiant => Atmosphere.VeryThick,
            _ => (Atmosphere)Math.Clamp(mass - 1 + rng.Next(-1, 2), 0, 5),
        };
        string comp = atmosphere == Atmosphere.None ? "None"
            : string.Join(", ", Enumerable.Range(0, 1 + rng.Next(2)).Select(_ => rng.Pick(Gases)).Distinct());
        if (type is PlanetType.Ocean or PlanetType.Jungle && atmosphere >= Atmosphere.Thin)
            comp = rng.Chance(0.6) ? "Nitrogen, Oxygen" : comp;

        var hydro = type switch
        {
            PlanetType.Ocean => Hydrosphere.Water,
            PlanetType.Jungle => Hydrosphere.Water,
            PlanetType.Frozen => rng.Chance(0.5) ? Hydrosphere.Methane : Hydrosphere.Ammonia,
            PlanetType.Molten => Hydrosphere.Liquid, // molten rock seas
            PlanetType.Rock when temperature == Temperature.Temperate && rng.Chance(0.4) => Hydrosphere.Water,
            _ => Hydrosphere.None,
        };
        var weather = atmosphere == Atmosphere.None ? Weather.Calm
            : (Weather)Math.Clamp((int)atmosphere / 2 + rng.Next(-1, 3), 0, 4);
        var minerals = type == PlanetType.GasGiant ? Density.None : (Density)Math.Clamp(rng.Next(0, 6), 0, 5);
        bool lifeFriendly = hydro == Hydrosphere.Water && temperature is Temperature.Temperate or Temperature.Hot or Temperature.Cold;
        var bio = lifeFriendly ? (Density)Math.Clamp(2 + rng.Next(0, 4), 0, 5)
            : type is PlanetType.GasGiant or PlanetType.Asteroid or PlanetType.Molten ? Density.None
            : (Density)(rng.Chance(0.25) ? rng.Next(1, 3) : 0);

        return new Planet
        {
            Id = id, SystemId = sys.Id, Orbit = orbit, Type = type, Mass = mass, Gravity = gravity,
            Atmosphere = atmosphere, AtmosphereComposition = comp, Hydrosphere = hydro,
            Temperature = temperature, Weather = weather, MineralDensity = minerals, BioDensity = bio,
            TerrainSeed = (ulong)rng.Next(int.MaxValue) << 20 ^ (ulong)id,
        };
    }

    private static Planet FromDefinition(IGameRandom rng, int id, StarSystem sys, PlanetDefinition d)
    {
        var g = GeneratePlanet(rng, id, sys, d.Orbit);
        if (d.Type == PlanetType.GasGiant && g.Type != PlanetType.GasGiant)
        {
            // Keep authored gas giants physically consistent with generated ones.
            g = new Planet
            {
                Id = id, SystemId = sys.Id, Orbit = d.Orbit, Type = PlanetType.GasGiant, Mass = 5,
                Gravity = Math.Round(4 + rng.NextDouble() * 8, 2), Atmosphere = Atmosphere.VeryThick,
                AtmosphereComposition = "Hydrogen, Helium", Hydrosphere = Hydrosphere.None, Temperature = g.Temperature,
                Weather = Weather.Violent, MineralDensity = Density.None, BioDensity = Density.None, TerrainSeed = g.TerrainSeed,
            };
        }
        return new Planet
        {
            Id = id, SystemId = sys.Id, Orbit = d.Orbit, Name = d.Name,
            Type = d.Type ?? g.Type, Mass = d.Mass ?? g.Mass, Gravity = d.Gravity ?? g.Gravity,
            Atmosphere = d.Atmosphere ?? g.Atmosphere, AtmosphereComposition = d.AtmosphereComposition ?? g.AtmosphereComposition,
            Hydrosphere = d.Hydrosphere ?? g.Hydrosphere, Temperature = d.Temperature ?? g.Temperature,
            Weather = d.Weather ?? ((d.Atmosphere ?? g.Atmosphere) == Atmosphere.None ? Weather.Calm : g.Weather), MineralDensity = d.MineralDensity ?? g.MineralDensity,
            BioDensity = d.BioDensity ?? g.BioDensity, TerrainSeed = g.TerrainSeed,
            Sites = d.Sites.Select(s => new SurfaceSite(s.Id, s.Kind, s.Lat, s.Lon, s.Ref)).ToList(),
        };
    }
}
