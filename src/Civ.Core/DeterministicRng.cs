namespace Civ;

/// <summary>xoshiro256** seeded through SplitMix64. Pure integer math, identical on every machine.</summary>
public sealed class DeterministicRng
{
    private ulong _s0, _s1, _s2, _s3;

    /// <summary>Initialize a random stream from an integer seed.</summary>
    public DeterministicRng(ulong seed)
    {
        _s0 = SplitMix(ref seed);
        _s1 = SplitMix(ref seed);
        _s2 = SplitMix(ref seed);
        _s3 = SplitMix(ref seed);
    }

    private static ulong SplitMix(ref ulong x)
    {
        ulong z = x += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    /// <summary>Advance the stream and return its next 64-bit value.</summary>
    public ulong NextUInt64()
    {
        ulong result = Rotl(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;
        _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);
        return result;
    }

    /// <summary>Uniform-enough integer in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    /// <summary>Four words needed to resume the exact random stream.</summary>
    public (ulong, ulong, ulong, ulong) State => (_s0, _s1, _s2, _s3);

    /// <summary>Restore a saved random stream. The all-zero state is invalid.</summary>
    public static DeterministicRng Restore(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        if ((s0 | s1 | s2 | s3) == 0) throw new InvalidDataException("RNG state cannot be all zero");
        return new DeterministicRng(0) { _s0 = s0, _s1 = s1, _s2 = s2, _s3 = s3 };
    }
}
