namespace Civ.Mods.Ai;

/// <summary>Index-preserving map for read-only planner scoring. Each result lands at its source index, so callers see
/// the same deterministic order whether the work ran on one thread or many. Callbacks must not issue commands,
/// mutate simulation state or draw from the simulation RNG.</summary>
internal static class ParallelScoring
{
    public static TResult[] Select<TSource, TResult>(IReadOnlyList<TSource> items, int threshold, Func<TSource, TResult> select)
    {
        var results = new TResult[items.Count];
        if (items.Count >= threshold) Parallel.For(0, items.Count, i => results[i] = select(items[i]));
        else for (int i = 0; i < items.Count; i++) results[i] = select(items[i]);
        return results;
    }
}
