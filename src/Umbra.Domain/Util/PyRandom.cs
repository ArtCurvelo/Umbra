namespace Umbra.Domain.Util;

/// <summary>Random draws the skill engine needs (Python's random.random / randint / choice).</summary>
public interface IRandom
{
    double Random();
    int RandInt(int a, int b);
    T Choice<T>(IReadOnlyList<T> items);
}

/// <summary>
/// Exact port of CPython's `random` module generator (MT19937, init_by_array seeding from an int, random(), getrandbits,
/// _randbelow, randint, choice), so the parity tests can replay the Python engine draw for draw. The game itself may
/// use any IRandom; this one exists so C# and Python agree on every number.
/// </summary>
public sealed class PyRandom : IRandom
{
    private const int N = 624, M = 397;
    private readonly uint[] _mt = new uint[N];
    private int _index;

    public PyRandom(long seed) => Seed(seed);

    public void Seed(long seed)
    {
        ulong n = (ulong)Math.Abs(seed);
        var key = new List<uint>();
        do { key.Add((uint)(n & 0xffffffff)); n >>= 32; } while (n != 0);
        InitByArray(key);
    }

    private void InitGenrand(uint s)
    {
        _mt[0] = s;
        for (int i = 1; i < N; i++) _mt[i] = unchecked(1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30)) + (uint)i);
        _index = N;
    }

    private void InitByArray(IReadOnlyList<uint> key)
    {
        InitGenrand(19650218u);
        int i = 1, j = 0;
        for (int k = Math.Max(N, key.Count); k > 0; k--)
        {
            _mt[i] = unchecked((_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525u)) + key[j] + (uint)j);
            i++; j++;
            if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
            if (j >= key.Count) j = 0;
        }
        for (int k = N - 1; k > 0; k--)
        {
            _mt[i] = unchecked((_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941u)) - (uint)i);
            i++;
            if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
        }
        _mt[0] = 0x80000000u;
    }

    private uint Next()
    {
        if (_index >= N)
        {
            for (int k = 0; k < N; k++)
            {
                uint y = (_mt[k] & 0x80000000u) | (_mt[(k + 1) % N] & 0x7fffffffu);
                _mt[k] = _mt[(k + M) % N] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908b0dfu : 0u);
            }
            _index = 0;
        }
        uint v = _mt[_index++];
        v ^= v >> 11; v ^= (v << 7) & 0x9d2c5680u; v ^= (v << 15) & 0xefc60000u; v ^= v >> 18;
        return v;
    }

    public double Random()
    {
        uint a = Next() >> 5, b = Next() >> 6;
        return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
    }

    public uint GetRandBits(int k) => k <= 0 ? 0 : Next() >> (32 - k);

    private int RandBelow(int n)
    {
        int k = 0; for (int v = n; v > 0; v >>= 1) k++;      // n.bit_length()
        uint r = GetRandBits(k);
        while (r >= n) r = GetRandBits(k);
        return (int)r;
    }

    public int RandInt(int a, int b) => a + RandBelow(b + 1 - a);

    public T Choice<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0) throw new IndexOutOfRangeException("Cannot choose from an empty sequence");
        return items[RandBelow(items.Count)];
    }
}
