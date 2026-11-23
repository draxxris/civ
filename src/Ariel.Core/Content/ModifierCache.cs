using System.Collections.Concurrent;
using Ariel.Game;

namespace Ariel.Content;

/// <summary>One simulation's modifier contributions kept across writes: each source's lines for each calculation
/// context, with the write stamp they were computed at (<see cref="Simulation.WriteStamp"/>). A contribution is used
/// again while nothing the source reads was written since (<see cref="IStateReader.UnreadDomains"/>), and only
/// inside kernel work (<see cref="Simulation.EnterKernel"/>), where every write is in a window.
/// <para>Thread model: the table belongs to one simulation; forks and copies start with their own, empty. Several
/// threads may read and fill it at once (AI candidates are scored in parallel over one simulation), which is safe
/// because nothing writes the state while they read, entries are immutable once stored, and an entry stored by one
/// reader equals what another would have computed. Writes, stamps and clearing happen only on the thread that runs
/// the kernel, while no reader is running.</para>
/// <para>Memory: at most <see cref="Capacity"/> entries; the table is emptied when it reaches that, when the
/// outermost kernel work ends, and when the state is replaced.</para></summary>
internal sealed class ModifierCache
{
    /// <summary>The most entries kept before the table is emptied.</summary>
    internal const int Capacity = 32_768;

    internal sealed class Entry(LedgerLine[] lines, long stamp)
    {
        public readonly LedgerLine[] Lines = lines;
        public readonly long Stamp = stamp;
    }

    private readonly ConcurrentDictionary<(int Source, ModifierContext Context), Entry> _entries = new();
    // Entries added since the last clear. ConcurrentDictionary.Count takes every lock, which stalled the AI's parallel
    // scoring threads, so the bound is kept by counting additions instead.
    private int _count;

    internal bool TryGet(int source, ModifierContext context, out Entry entry) => _entries.TryGetValue((source, context), out entry!);

    internal void Set(int source, ModifierContext context, Entry entry)
    {
        if (Volatile.Read(ref _count) >= Capacity) Clear();
        if (_entries.TryAdd((source, context), entry)) Interlocked.Increment(ref _count);
        else _entries[(source, context)] = entry;
    }

    internal int Count => Volatile.Read(ref _count);

    internal void Clear()
    {
        if (_entries.IsEmpty) return;
        _entries.Clear();
        Volatile.Write(ref _count, 0);
    }
}
