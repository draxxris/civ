namespace Ariel.Game;

/// <summary>
/// The rule work a command left waiting under <see cref="Simulation.DeferTurnWork"/>, run on a private copy of the
/// simulation so it can take as long as it needs on another thread: AI players moving and, once everyone has ended,
/// the turn rolling over and the next turn's leading AI moving. Nothing reads or writes the original while it runs.
/// <see cref="Simulation.CompleteTurnWork"/> takes the result back, which is exactly what running the work in place
/// would have produced.
/// </summary>
public sealed class TurnWork
{
    internal TurnWork(Simulation fork, long version)
    {
        Fork = fork;
        Version = version;
    }

    internal Simulation Fork { get; }
    /// <summary>The original's command count when the copy was made; the result only fits that state.</summary>
    internal long Version { get; }
    /// <summary>Whether <see cref="Run"/> finished, so the result can be taken back.</summary>
    public bool Done { get; private set; }

    /// <summary>Do the work. Call it once, from any thread.</summary>
    public void Run()
    {
        if (Done) throw new InvalidOperationException("Turn work already ran");
        Fork.RunPendingTurnWork();
        Done = true;
    }
}

public sealed partial class Simulation
{
    private long _version;

    /// <summary>When set, a command that ends a turn or hands a player to the AI only records that the turn must move
    /// on, and <see cref="HasPendingTurnWork"/> turns true; the AI moves and the turn rollover wait for
    /// <see cref="RunPendingTurnWork"/> or a <see cref="TurnWork"/>. A front end sets it to keep that work off its
    /// frame loop. The next command still applies after the work, so the game plays out the same either way. Not part
    /// of the rules: it is not saved, and a save made while work waits holds everything needed to do it. Lockstep
    /// peers hash between commands, so they must all agree on it.</summary>
    public bool DeferTurnWork { get; set; }

    /// <summary>Whether the turn can move on without a human: an AI player is due to move, or every player has ended
    /// and the turn can roll over. Only true between commands with <see cref="DeferTurnWork"/>, or after loading a
    /// save made then.</summary>
    public bool HasPendingTurnWork
    {
        get
        {
            // Front ends ask every frame, so this walks the order without allocating.
            var order = State.TurnOrder;
            if (order.Finished)
            {
                foreach (var player in State.Players)
                    if (!player.EndedTurn) return false;
                return true;
            }
            for (int s = order.Active; s < order.Segments.Count; s++)
            {
                bool human = false;
                foreach (int id in order.Segments[s])
                {
                    var player = State.PlayerById(id);
                    if (player.Ai == null) human = true;
                    else if (!player.EndedTurn) return true;
                }
                if (human) return false; // AiDue stops at the first segment with a human.
            }
            return false;
        }
    }

    /// <summary>Do the pending turn work in place, on this thread.</summary>
    public void RunPendingTurnWork()
    {
        bool defer = DeferTurnWork;
        DeferTurnWork = false; // The AI's own commands must not wait for the work they are part of.
        try { Proceed(mayAdvance: true, defer: false); }
        finally { DeferTurnWork = defer; }
        _version++;
    }

    /// <summary>Copy the simulation for its pending turn work. Apply no command until the work is complete or
    /// abandoned: the result replaces the state wholesale.</summary>
    public TurnWork BeginTurnWork()
    {
        if (!HasPendingTurnWork) throw new InvalidOperationException("No turn work is pending");
        // Shared immutable geometry: materialize its lazy cache before two threads read it.
        _ = State.Grid.MaximumDistance;
        var (a, b, c, d) = Rng.State;
        var fork = new Simulation(Content, Settings, State.Clone(), DeterministicRng.Restore(a, b, c, d))
        {
            AiWaveLimit = AiWaveLimit,
        };
        fork.Events.CopyFrom(Events);
        return new TurnWork(fork, _version);
    }

    /// <summary>Take the finished work's state, random stream and events. Throws if a command applied since
    /// <see cref="BeginTurnWork"/>, since the result would erase it.</summary>
    public void CompleteTurnWork(TurnWork work)
    {
        if (!work.Done) throw new InvalidOperationException("Turn work has not run");
        if (work.Version != _version) throw new InvalidOperationException("The simulation changed while its turn work ran");
        State.RestoreFrom(work.Fork.State);
        Rng.Set(work.Fork.Rng.State);
        Events.CopyFrom(work.Fork.Events);
        _version++;
    }
}
