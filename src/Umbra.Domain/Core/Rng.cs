namespace Umbra.Domain.Core;

/// <summary>Deterministic random numbers (SplitMix64). Same seed, same sequence, on every machine.</summary>
public sealed class Rng
{
    private ulong _state;

    public Rng(ulong seed) => _state = seed;

    public ulong NextULong()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform integer in [min, max] (both inclusive).</summary>
    public int Range(int min, int max) => min + (int)(NextULong() % (ulong)(max - min + 1));

    public bool Chance(double probability) => NextDouble() < probability;
}
