using Ariel.Game;

namespace Ariel.Ai;

/// <summary>Index-preserving map for read-only planner scoring. Each result lands at its source index, so callers see
/// the same deterministic order whether the work ran on one thread or many. Callbacks must not issue commands,
/// mutate simulation state or draw from the simulation RNG.</summary>
internal static class ParallelScoring
{
    /// <summary>Whether <paramref name="test"/> holds for every item. In parallel the items left unasked once one fails
    /// are skipped; the answer does not depend on which, or on how many threads asked.</summary>
    public static bool All<TSource>(Simulation sim, IReadOnlyList<TSource> items, int threshold, Func<TSource, bool> test)
    {
        if (sim.IsParallelAiTurn || items.Count < threshold)
        {
            for (int i = 0; i < items.Count; i++)
                if (!test(items[i])) return false;
            return true;
        }
        // Stopping only ever follows a failed test, and every test runs when none fails.
        return Parallel.For(0, items.Count, (i, loop) => { if (!test(items[i])) loop.Stop(); }).IsCompleted;
    }

    public static TResult[] Select<TSource, TResult>(Simulation sim, IReadOnlyList<TSource> items, int threshold, Func<TSource, TResult> select) =>
        For(sim, items.Count, threshold, i => select(items[i]));

    /// <summary>The same map over the indices <c>0</c> to <paramref name="count"/> − 1, for scoring that needs the index
    /// (a tile id, say) as well as the item.</summary>
    public static TResult[] For<TResult>(Simulation sim, int count, int threshold, Func<int, TResult> select)
    {
        var results = new TResult[count];
        // The wave already parallelizes players; reserve candidate-level parallelism for a single-player turn.
        if (!sim.IsParallelAiTurn && count >= threshold) Parallel.For(0, count, i => results[i] = select(i));
        else for (int i = 0; i < count; i++) results[i] = select(i);
        return results;
    }
}
