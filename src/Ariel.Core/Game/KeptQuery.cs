using System.Collections.Concurrent;
using Ariel.Content;

namespace Ariel.Game;

/// <summary>A derived answer a rule keeps across writes, as <see cref="ModifierSink.Collect"/> keeps modifier
/// contributions: the answer for a key is kept with the write stamp it was computed at and used again while nothing its
/// reader may read was written since. The reader declares what it never reads (<see cref="IStateReader.UnreadDomains"/>),
/// and writers declare what they write, exactly as for modifier sources ("Kept contributions, state domains and write
/// windows" in DESIGN.md). Answers are kept only inside kernel work (<see cref="Simulation.EnterKernel"/>), never while
/// an open write window may be writing what the reader reads, and they are forgotten with the modifier cache.
/// <para>Use it for an answer that is expensive, asked from many read spans between writes, and immutable: the value
/// is shared by every caller and every thread. A query is a static instance per rule; its key names the subject
/// (a city id, a team and area) and must not hold state objects. <see cref="Simulation.VerifyWrites"/> computes every
/// reused answer again and throws when it differs.</para></summary>
public sealed class KeptQuery<TKey, TValue> where TKey : notnull
{
    private readonly string _name;
    private readonly IStateReader _reader;
    private readonly IEqualityComparer<TValue> _equality;
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentDb, StrongBox> _masks = new();
    private sealed class StrongBox(ulong value) { public readonly ulong Value = value; }

    /// <summary>A query named <paramref name="name"/> (for diagnostics) whose answers read what
    /// <paramref name="reader"/> declares, compared with <paramref name="equality"/> when verifying.</summary>
    public KeptQuery(string name, IStateReader reader, IEqualityComparer<TValue>? equality = null)
    {
        _name = name;
        _reader = reader;
        _equality = equality ?? EqualityComparer<TValue>.Default;
    }

    /// <summary>The kept answer for <paramref name="key"/>, or <paramref name="compute"/>'s, kept for later when it may
    /// be. <paramref name="compute"/> must only read the state and depend on nothing but the key and the state.</summary>
    public TValue Get(Simulation sim, TKey key, Func<TValue> compute)
    {
        if (!sim.KeepsAnswers) return compute();
        ulong unread = _masks.GetValue(sim.Content, db => new(db.StateDomains.UnreadMask(_reader, db))).Value;
        if (sim.WritingNow(unread)) return compute();
        var table = sim.KeptAnswers.Table<TKey, TValue>(this);
        if (table.TryGetValue(key, out var kept) && !sim.WrittenSince(unread, kept.Stamp))
        {
            if (sim.VerifyWrites && !_equality.Equals(compute(), kept.Value))
                throw new InvalidOperationException($"Kept query {_name} kept a stale answer for {key} on turn {sim.State.Turn}. "
                    + "A writer wrote what the query reads without declaring it, or the query reads a domain it declares unread.");
            return kept.Value;
        }
        long stamp = sim.WriteStamp;
        var value = compute();
        sim.KeptAnswers.Set(table, key, new(value, stamp));
        return value;
    }
}

/// <summary>One simulation's kept query answers (<see cref="KeptQuery{TKey,TValue}"/>): a table per query. The thread
/// model and memory bound are the modifier cache's: AI scoring threads may read and fill it at once while nothing
/// writes, entries are immutable, and it is emptied when it reaches <see cref="Capacity"/> entries, when the
/// outermost kernel work ends and when the state is replaced.</summary>
internal sealed class KeptAnswers
{
    internal const int Capacity = 32_768;
    internal readonly record struct Entry<TValue>(TValue Value, long Stamp);
    private readonly ConcurrentDictionary<object, IClearable> _tables = new(ReferenceEqualityComparer.Instance);
    private int _count;

    private interface IClearable { void Clear(); }
    private sealed class QueryTable<TKey, TValue> : ConcurrentDictionary<TKey, Entry<TValue>>, IClearable where TKey : notnull
    {
        void IClearable.Clear() => Clear();
    }

    internal ConcurrentDictionary<TKey, Entry<TValue>> Table<TKey, TValue>(KeptQuery<TKey, TValue> query) where TKey : notnull =>
        (QueryTable<TKey, TValue>)_tables.GetOrAdd(query, static _ => new QueryTable<TKey, TValue>());

    internal void Set<TKey, TValue>(ConcurrentDictionary<TKey, Entry<TValue>> table, TKey key, Entry<TValue> entry) where TKey : notnull
    {
        if (Volatile.Read(ref _count) >= Capacity) Clear();
        if (table.TryAdd(key, entry)) Interlocked.Increment(ref _count);
        else table[key] = entry;
    }

    internal void Clear()
    {
        if (Volatile.Read(ref _count) == 0) return;
        foreach (var table in _tables.Values) table.Clear();
        Volatile.Write(ref _count, 0);
    }
}
