namespace Ariel.Game;

/// <summary>A span in which the caller that opened it promises not to write the game state, so rules may reuse what
/// they derived from it. Drawing from the RNG stream is not a write of the state: rules derive nothing from it, and AI
/// planners draw from it while planning. The kernel ends the span itself before any write it performs: every command (<see
/// cref="Simulation.TryApply"/>), every turn rollover and every wholesale state replacement. Disposing the handle ends
/// it early; a handle whose span already ended does nothing.</summary>
public readonly struct ReadEpochScope : IDisposable
{
    private readonly Simulation? _sim;
    private readonly long _epoch;
    internal ReadEpochScope(Simulation sim, long epoch) { _sim = sim; _epoch = epoch; }
    /// <summary>End the span if it is still the open one.</summary>
    public void Dispose() => _sim?.EndReadEpoch(_epoch);
}

/// <summary>A read span (<see cref="Simulation.OpenReadSpan"/>): one bracketed, read-only query on one thread.
/// Disposing it ends the span if it is still open; a handle that opened nothing does nothing.</summary>
public readonly struct ReadSpanScope : IDisposable
{
    private readonly Simulation? _sim;
    private readonly long _epoch;
    internal ReadSpanScope(Simulation sim, long epoch) { _sim = sim; _epoch = epoch; }
    /// <summary>End the span if it is still this thread's open one.</summary>
    public void Dispose() => _sim?.EndReadSpan(_epoch);
}

public sealed partial class Simulation
{
    private long _epochs, _readEpoch;
    private ulong _epochHash;
    // The read span open on this thread, if any: one per thread, for one simulation.
    [ThreadStatic] private static Simulation? t_spanSim;
    [ThreadStatic] private static long t_spanEpoch;
    [ThreadStatic] private static ulong t_spanHash;

    /// <summary>The open read epoch, or this thread's open read span (<see cref="OpenReadSpan"/>), or 0 when neither
    /// is open. A rule may key memoised reads on (this simulation, this value) and reuse them while it stays the same:
    /// no write happens until it changes. The value of an ended epoch or span is never reused.</summary>
    public long ReadEpoch => _readEpoch != 0 ? _readEpoch : ReferenceEquals(t_spanSim, this) ? t_spanEpoch : 0;

    /// <summary>When set, opening a read epoch hashes the state (without the RNG words) and ending one hashes it again,
    /// throwing if anything changed in between: a write that bypassed the kernel while a caller had promised none. Costs two state hashes
    /// per epoch, so it is for tests and verification runs. Not saved.</summary>
    public bool VerifyReadEpochs { get; set; }

    /// <summary>Open a read epoch, ending any open one. The caller promises not to write the state, directly or through
    /// services, until it disposes the handle; commands it issues end the epoch first. Inside a command, which may
    /// write, no epoch opens and the handle does nothing.</summary>
    public ReadEpochScope OpenReadEpoch()
    {
        EndReadEpoch();
        if (_commandDepth > 0) return default;
        _readEpoch = Interlocked.Increment(ref _epochs);
        if (VerifyReadEpochs) _epochHash = StateHasher.Hash(State);
        return new ReadEpochScope(this, _readEpoch);
    }

    internal void EndReadEpoch(long epoch)
    {
        if (_readEpoch == epoch) EndReadEpoch();
    }

    /// <summary>Open a read span on this thread for one bracketed query that only reads, unless an epoch or a span is
    /// already open, in which case the query reads under that one and the handle does nothing. Unlike an epoch, a
    /// span is seen only by the thread that opened it and may open inside a command: its opener disposes it before
    /// returning, so it never spans a write. Several threads may each hold their own span over one simulation as long
    /// as none writes, which any concurrent read already requires.</summary>
    public ReadSpanScope OpenReadSpan()
    {
        if (ReadEpoch != 0) return default;
        long epoch = Interlocked.Increment(ref _epochs);
        if (VerifyReadEpochs) t_spanHash = StateHasher.Hash(State);
        (t_spanSim, t_spanEpoch) = (this, epoch);
        return new ReadSpanScope(this, epoch);
    }

    internal void EndReadSpan(long epoch)
    {
        if (ReferenceEquals(t_spanSim, this) && t_spanEpoch == epoch) EndReadSpan();
    }

    /// <summary>End this thread's open span over this simulation, if any.</summary>
    private void EndReadSpan()
    {
        if (!ReferenceEquals(t_spanSim, this)) return;
        long epoch = t_spanEpoch;
        (t_spanSim, t_spanEpoch) = (null, 0);
        if (VerifyReadEpochs && StateHasher.Hash(State) != t_spanHash)
            throw new InvalidOperationException($"The state changed during read span {epoch} without a command");
    }

    /// <summary>End the open epoch and this thread's open span, if any. Called before every kernel write.</summary>
    internal void EndReadEpoch()
    {
        EndReadSpan();
        if (_readEpoch == 0) return;
        long epoch = _readEpoch;
        _readEpoch = 0;
        if (VerifyReadEpochs && StateHasher.Hash(State) != _epochHash)
            throw new InvalidOperationException($"The state changed during read epoch {epoch} without a command");
    }
}
