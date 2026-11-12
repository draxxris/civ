namespace Ariel.Game;

/// <summary>FNV-1a over canonical JSON of every authoritative state field and the RNG words.
/// Collection order is part of state; object keys are ordinal. Runtime caches and notifications are excluded.</summary>
public static class StateHasher
{
    /// <summary>Compute a deterministic peer state hash.</summary>
    public static ulong Hash(GameState state, DeterministicRng rng)
    {
        var (a, b, c, d) = rng.State;
        return CanonicalJson.Fnv1a64(new { State = state, Rng = new[] { a, b, c, d } });
    }
}
