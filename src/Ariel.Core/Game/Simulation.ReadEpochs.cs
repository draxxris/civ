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

public sealed partial class Simulation
{
    private long _epochs, _readEpoch;
    private ulong _epochHash;

    /// <summary>The open read epoch, or 0 when none is open. A rule may key memoised reads on (this simulation, this
    /// value) and reuse them while it stays the same: no write happens until it changes. The value of an ended epoch
    /// is never reused.</summary>
    public long ReadEpoch => _readEpoch;

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
        _readEpoch = ++_epochs;
        if (VerifyReadEpochs) _epochHash = StateHasher.Hash(State);
        return new ReadEpochScope(this, _readEpoch);
    }

    internal void EndReadEpoch(long epoch)
    {
        if (_readEpoch == epoch) EndReadEpoch();
    }

    /// <summary>End the open epoch, if any. Called before every kernel write.</summary>
    internal void EndReadEpoch()
    {
        if (_readEpoch == 0) return;
        long epoch = _readEpoch;
        _readEpoch = 0;
        if (VerifyReadEpochs && StateHasher.Hash(State) != _epochHash)
            throw new InvalidOperationException($"The state changed during read epoch {epoch} without a command");
    }
}
