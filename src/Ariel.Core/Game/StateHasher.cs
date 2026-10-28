using System.Text;

namespace Ariel.Game;

/// <summary>FNV-1a over canonical JSON of every authoritative state field and the RNG words.
/// Collection order is part of state; object keys are ordinal. Runtime caches and notifications are excluded.</summary>
public static class StateHasher
{
    /// <summary>Compute a deterministic peer state hash.</summary>
    public static ulong Hash(GameState state, DeterministicRng rng)
    {
        var (a, b, c, d) = rng.State;
        string json = CanonicalJson.Serialize(new { State = state, Rng = new[] { a, b, c, d } });
        ulong hash = 14695981039346656037UL;
        foreach (byte value in Encoding.UTF8.GetBytes(json)) { hash ^= value; hash = unchecked(hash * 1099511628211UL); }
        return hash;
    }
}
