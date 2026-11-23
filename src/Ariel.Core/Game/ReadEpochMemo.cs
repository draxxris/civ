namespace Ariel.Game;

/// <summary>A rule's answers within one read epoch (<see cref="Simulation.ReadEpoch"/>), one table per thread. While the
/// epoch stays open the state cannot change, so an answer derived from it holds; the table empties when the thread next
/// asks under another simulation or epoch. Outside an epoch nothing is kept and every call computes afresh. A stored
/// value is handed to every later asker in the epoch, so it must be immutable or copied by the caller.</summary>
public sealed class ReadEpochMemo<TKey, TValue> where TKey : notnull
{
    private sealed class Table
    {
        public Simulation? Sim;
        public long Epoch;
        public readonly Dictionary<TKey, TValue> Values;
        public Table(IEqualityComparer<TKey>? comparer) => Values = new(comparer);
    }

    private readonly ThreadLocal<Table> _tables;

    public ReadEpochMemo(IEqualityComparer<TKey>? comparer = null) => _tables = new(() => new Table(comparer));

    /// <summary>The epoch's value for <paramref name="key"/>, made by <paramref name="create"/> on the first ask.</summary>
    public TValue GetOrAdd<TArg>(Simulation sim, TKey key, TArg arg, Func<Simulation, TKey, TArg, TValue> create)
    {
        long epoch = sim.ReadEpoch;
        if (epoch == 0) return create(sim, key, arg);
        if (Current(sim, epoch).Values.TryGetValue(key, out var value)) return value;
        value = create(sim, key, arg);
        // Making the value may have asked under another simulation on this thread, so look the table up again.
        if (sim.ReadEpoch == epoch) Current(sim, epoch).Values[key] = value;
        return value;
    }

    /// <summary>Whether the open epoch already holds a value for <paramref name="key"/>; never outside an epoch.</summary>
    public bool TryGet(Simulation sim, TKey key, out TValue value)
    {
        long epoch = sim.ReadEpoch;
        if (epoch != 0) return Current(sim, epoch).Values.TryGetValue(key, out value!);
        value = default!;
        return false;
    }

    /// <summary>Keep <paramref name="value"/> for the rest of the open epoch; does nothing outside one.</summary>
    public void Set(Simulation sim, TKey key, TValue value)
    {
        long epoch = sim.ReadEpoch;
        if (epoch != 0) Current(sim, epoch).Values[key] = value;
    }

    private Table Current(Simulation sim, long epoch)
    {
        var table = _tables.Value!;
        if (!ReferenceEquals(table.Sim, sim) || table.Epoch != epoch)
        {
            (table.Sim, table.Epoch) = (sim, epoch);
            table.Values.Clear();
        }
        return table;
    }
}
