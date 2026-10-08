namespace AVAFlight.Core.Galaxy;

/// <summary>Stellar spectral classes used by the original's star map (O,B,A,F,G,K,M).</summary>
public enum SpectralClass { O, B, A, F, G, K, M }

public enum PlanetType { Rock, Frozen, Molten, Ocean, Jungle, Desert, GasGiant, Asteroid, Crystal }

public enum Atmosphere { None, VeryThin, Thin, Moderate, Thick, VeryThick }
public enum Hydrosphere { None, Water, Ammonia, Methane, Liquid }
public enum Temperature { Frigid, Cold, Temperate, Hot, Scorching }
public enum Weather { Calm, Moderate, Stormy, Violent, VeryViolent }
public enum Density { None, Sparse, Light, Moderate, Dense, Rich }

/// <summary>A point of interest on a planet surface (ruin, artifact, message, special mineral).</summary>
public sealed record SurfaceSite(string Id, SiteKind Kind, int Lat, int Lon, string Ref);

public enum SiteKind { Ruin, Artifact, Message, Beacon, Nexus }

/// <summary>
/// A planet. Generated deterministically from the galaxy seed; story planets are overlaid from
/// authored data. Lat is -90..90 (N positive), Lon is -180..180 (E positive).
/// </summary>
public sealed class Planet
{
    public required int Id { get; init; }
    public required int SystemId { get; init; }
    public required int Orbit { get; init; }
    public required PlanetType Type { get; init; }
    public string? Name { get; init; }
    /// <summary>Relative mass class 1 (tiny) .. 5 (huge); drives gravity and landing cost.</summary>
    public required int Mass { get; init; }
    public required double Gravity { get; init; }
    public required Atmosphere Atmosphere { get; init; }
    public required string AtmosphereComposition { get; init; }
    public required Hydrosphere Hydrosphere { get; init; }
    public required Temperature Temperature { get; init; }
    public required Weather Weather { get; init; }
    public required Density MineralDensity { get; init; }
    public required Density BioDensity { get; init; }
    public required ulong TerrainSeed { get; init; }
    public List<SurfaceSite> Sites { get; init; } = [];
    /// <summary>True if the planet can be landed on at all (gas giants cannot).</summary>
    public bool Landable => Type is not PlanetType.GasGiant;
    public string DisplayName => Name ?? $"Planet {Orbit}";
}

public sealed class StarSystem
{
    public required int Id { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required SpectralClass Class { get; init; }
    public string? Name { get; init; }
    public List<Planet> Planets { get; init; } = [];
    /// <summary>Owning race territory id (e.g. "velox"), or null for unclaimed space.</summary>
    public string? Territory { get; set; }
    /// <summary>Stardate (hours since game start) at which this star flares; null = never.</summary>
    public long? FlareAtHour { get; set; }
    public string Label => Name ?? $"{X},{Y}";
}

/// <summary>A hyperspace flux: stepping onto one end instantly moves the ship to the other.</summary>
public sealed record Flux(int Id, int X1, int Y1, int X2, int Y2);

/// <summary>A hyperspace nebula: a disc region that blocks long-range sensors and drains shields.</summary>
public sealed record Nebula(int Id, double X, double Y, double Radius);

public sealed class GalaxyMap
{
    public const int Width = 250;
    public const int Height = 220;

    public required ulong Seed { get; init; }
    public required IReadOnlyList<StarSystem> Systems { get; init; }
    public required IReadOnlyList<Flux> Fluxes { get; init; }
    public required IReadOnlyList<Nebula> Nebulae { get; init; }

    private Dictionary<(int, int), StarSystem>? _byCoord;
    private Dictionary<int, Planet>? _planets;

    public StarSystem? SystemAt(int x, int y)
    {
        _byCoord ??= Systems.ToDictionary(s => (s.X, s.Y));
        return _byCoord.GetValueOrDefault((x, y));
    }

    public StarSystem System(int id) => Systems[id];

    public Planet Planet(int id)
    {
        _planets ??= Systems.SelectMany(s => s.Planets).ToDictionary(p => p.Id);
        return _planets[id];
    }

    public IEnumerable<Planet> AllPlanets => Systems.SelectMany(s => s.Planets);

    public bool InNebula(double x, double y) =>
        Nebulae.Any(n => (n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y) <= n.Radius * n.Radius);

    public static double Distance(double x1, double y1, double x2, double y2) =>
        Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
}
