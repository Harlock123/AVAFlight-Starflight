namespace AVAFlight.Core.Random;

/// <summary>
/// Injectable randomness source. Every random decision the engine makes goes through this
/// interface so tests can supply deterministic or scripted values.
/// </summary>
public interface IGameRandom
{
    /// <summary>Returns a uniformly distributed integer in [0, maxExclusive).</summary>
    int Next(int maxExclusive);

    /// <summary>Returns a uniformly distributed integer in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);

    /// <summary>Returns a uniformly distributed double in [0, 1).</summary>
    double NextDouble();

    /// <summary>The serializable internal state, so a save captures the exact RNG position.</summary>
    ulong State { get; set; }
}

/// <summary>
/// SplitMix64 generator. Chosen because it is tiny, fast, has a single 64-bit state word that
/// round-trips through JSON trivially, and is stable across .NET versions (unlike System.Random
/// seeded output, whose algorithm is not contractually fixed).
/// </summary>
public sealed class SplitMix64 : IGameRandom
{
    public SplitMix64(ulong seed) => State = seed;

    public ulong State { get; set; }

    public ulong NextUInt64()
    {
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return minInclusive + Next(maxExclusive - minInclusive);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Derives an independent child stream (e.g. one per star system) from a parent seed.</summary>
    public static SplitMix64 Derive(ulong seed, ulong salt)
    {
        var mixer = new SplitMix64(seed ^ (salt * 0xD6E8FEB86659FD93UL));
        return new SplitMix64(mixer.NextUInt64());
    }
}

public static class GameRandomExtensions
{
    public static T Pick<T>(this IGameRandom rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    public static bool Chance(this IGameRandom rng, double probability) => rng.NextDouble() < probability;

    /// <summary>Approximately normal value via the sum of three uniforms (cheap, bounded).</summary>
    public static double NextBell(this IGameRandom rng) =>
        (rng.NextDouble() + rng.NextDouble() + rng.NextDouble()) / 3.0;
}
