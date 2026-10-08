using AVAFlight.Core.Data;
using AVAFlight.Core.Random;

namespace AVAFlight.Core.Galaxy;

public enum TerrainKind { Water, Liquid, Lava, Ice, Sand, Lowland, Vegetation, Highland, Mountain, Crystal }

public sealed record MineralDeposit(int Id, int X, int Y, string MineralId, double Amount);

public sealed record LifeformSpecies(string Id, string Name, int Size, bool Hostile, bool Flying, int Danger, string Niche);

/// <summary>
/// A planet surface: a Mercator grid that wraps east–west. Generated deterministically from the
/// planet's terrain seed with fractal value noise, echoing the original's fractal planets (the
/// original algorithm is not reproduced). Rows run north (y=0) to south.
/// </summary>
public sealed class PlanetSurface
{
    public const int Width = 96;   // 3.75° of longitude per cell
    public const int Height = 48;  // 3.75° of latitude per cell

    public required Planet Planet { get; init; }
    public required float[,] Elevation { get; init; }
    public required TerrainKind[,] Kind { get; init; }
    public required IReadOnlyList<MineralDeposit> Deposits { get; init; }
    public required IReadOnlyList<LifeformSpecies> Species { get; init; }

    public static (int X, int Y) CellFromLatLon(int lat, int lon) =>
        (Math.Clamp((int)((lon + 180) / 3.75), 0, Width - 1), Math.Clamp((int)((90 - lat) / 3.75), 0, Height - 1));

    public static (int Lat, int Lon) LatLonFromCell(int x, int y) =>
        ((int)Math.Round(90 - (y + 0.5) * 3.75), (int)Math.Round(-180 + (x + 0.5) * 3.75));

    public static string FormatLatLon(int lat, int lon) =>
        $"{Math.Abs(lat)}{(lat >= 0 ? "N" : "S")} x {Math.Abs(lon)}{(lon >= 0 ? "E" : "W")}";

    public static int WrapX(int x) => ((x % Width) + Width) % Width;

    public TerrainKind At(int x, int y) => Kind[WrapX(x), Math.Clamp(y, 0, Height - 1)];
    public float ElevationAt(int x, int y) => Elevation[WrapX(x), Math.Clamp(y, 0, Height - 1)];

    public static bool Passable(TerrainKind k) => k is not (TerrainKind.Water or TerrainKind.Liquid or TerrainKind.Lava);

    /// <summary>TV fuel per step. RECONSTRUCTION: efficiency drops with altitude (manual), 1..3 units.</summary>
    public double StepCost(int x, int y) => At(x, y) switch
    {
        TerrainKind.Mountain => 3, TerrainKind.Highland => 2, TerrainKind.Ice => 1.5, TerrainKind.Sand => 1.5,
        TerrainKind.Crystal => 2, _ => 1,
    };

    public static PlanetSurface Generate(Planet planet, IReadOnlyList<MineralDefinition> minerals)
    {
        var rng = new SplitMix64(planet.TerrainSeed);
        var elev = new float[Width, Height];
        var lattice = new float[4][,];
        int[] sizes = [6, 12, 24, 48];
        for (int o = 0; o < lattice.Length; o++)
        {
            lattice[o] = new float[sizes[o], sizes[o] / 2 + 1];
            for (int i = 0; i < sizes[o]; i++)
                for (int j = 0; j <= sizes[o] / 2; j++)
                    lattice[o][i, j] = (float)rng.NextDouble();
        }
        float min = float.MaxValue, max = float.MinValue;
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            float v = 0, amp = 1, total = 0;
            for (int o = 0; o < lattice.Length; o++)
            {
                int n = sizes[o];
                double fx = x * n / (double)Width, fy = y * (n / 2) / (double)Height;
                int x0 = (int)fx, y0 = (int)fy;
                double tx = Smooth(fx - x0), ty = Smooth(fy - y0);
                var L = lattice[o];
                float a = L[x0 % n, y0], b = L[(x0 + 1) % n, y0], c = L[x0 % n, Math.Min(y0 + 1, n / 2)], d = L[(x0 + 1) % n, Math.Min(y0 + 1, n / 2)];
                double top = a + (b - a) * tx, bottom = c + (d - c) * tx;
                v += (float)(top + (bottom - top) * ty) * amp;
                total += amp;
                amp *= 0.5f;
            }
            v /= total;
            elev[x, y] = v;
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        var kind = new TerrainKind[Width, Height];
        double sea = planet.Type switch
        {
            PlanetType.Ocean => 0.62, PlanetType.Jungle => 0.38, PlanetType.Molten => 0.35,
            PlanetType.Rock when planet.Hydrosphere == Hydrosphere.Water => 0.3,
            PlanetType.Frozen when planet.Hydrosphere != Hydrosphere.None => 0.2,
            _ => -1,
        };
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            float e = (elev[x, y] - min) / Math.Max(1e-6f, max - min);
            elev[x, y] = e;
            bool polar = y < 4 || y >= Height - 4;
            kind[x, y] = Classify(planet, e, sea, polar);
        }

        // Minerals: denser at high altitude (manual). Count scales with mineral density.
        var deposits = new List<MineralDeposit>();
        int count = (int)planet.MineralDensity * 14;
        double weightTotal = minerals.Sum(m => m.Rarity);
        int guard = 0;
        while (deposits.Count < count && guard++ < count * 40)
        {
            int x = rng.Next(Width), y = rng.Next(Height);
            if (!Passable(kind[x, y])) continue;
            if (rng.NextDouble() > 0.25 + elev[x, y]) continue;
            if (deposits.Any(d => d.X == x && d.Y == y)) continue;
            double pick = rng.NextDouble() * weightTotal;
            var mineral = minerals[^1];
            foreach (var m in minerals) { pick -= m.Rarity; if (pick <= 0) { mineral = m; break; } }
            deposits.Add(new MineralDeposit(deposits.Count, x, y, mineral.Id, 1 + rng.Next(8)));
        }

        var species = new List<LifeformSpecies>();
        int speciesCount = (int)planet.BioDensity * 2;
        for (int i = 0; i < speciesCount; i++)
        {
            int size = 1 + rng.Next(5);
            bool flying = rng.Chance(0.2);
            bool hostile = rng.Chance(0.06 + 0.03 * size); // RECONSTRUCTION: most lifeforms are passive
            string niche = rng.Pick(Niches);
            species.Add(new LifeformSpecies($"{planet.Id}:{i}", LifeName(rng), size, hostile, flying, hostile ? size * 6 : 0, niche));
        }
        return new PlanetSurface { Planet = planet, Elevation = elev, Kind = kind, Deposits = deposits, Species = species };
    }

    private static readonly string[] Niches = ["grazer", "burrower", "predator", "filter-feeder", "scavenger", "photosynthesizer"];
    private static readonly string[] SyllA = ["bo", "kra", "zi", "mu", "tal", "ve", "qua", "dro", "fen", "lu", "sk", "py", "ga", "or"];
    private static readonly string[] SyllB = ["rr", "nok", "lith", "ma", "zor", "pid", "vex", "ula", "tik", "mon", "bel", "seth"];

    private static string LifeName(IGameRandom rng)
    {
        var n = rng.Pick(SyllA) + rng.Pick(SyllB) + (rng.Chance(0.4) ? rng.Pick(SyllA) : "");
        return char.ToUpperInvariant(n[0]) + n[1..];
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);

    private static TerrainKind Classify(Planet p, float e, double sea, bool polar)
    {
        if (p.Type == PlanetType.Crystal) return e > 0.55 ? TerrainKind.Crystal : TerrainKind.Highland;
        if (e < sea)
            return p.Type switch
            {
                PlanetType.Molten => TerrainKind.Lava,
                PlanetType.Frozen => TerrainKind.Liquid,
                _ => p.Hydrosphere == Hydrosphere.Water ? TerrainKind.Water : TerrainKind.Liquid,
            };
        if (polar && p.Temperature is not (Temperature.Scorching or Temperature.Hot)) return TerrainKind.Ice;
        if (e > 0.82) return TerrainKind.Mountain;
        if (e > 0.66) return TerrainKind.Highland;
        return p.Type switch
        {
            PlanetType.Frozen => TerrainKind.Ice,
            PlanetType.Desert => TerrainKind.Sand,
            PlanetType.Molten => e < sea + 0.08 ? TerrainKind.Lava : TerrainKind.Highland,
            PlanetType.Jungle or PlanetType.Ocean when p.BioDensity >= Density.Light => TerrainKind.Vegetation,
            _ => TerrainKind.Lowland,
        };
    }
}
