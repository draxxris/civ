using Ariel.Game;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// A networked game: commands are checked here for an immediate answer, then queued in lockstep and applied, with every
/// other peer's, when the ticks that carry them complete. A pump stops after a tick that started a new turn, so the
/// session reads each turn's events before the event bus moves on. A desync or a failed handshake stops the game and
/// becomes <see cref="Problem"/>.
/// </summary>
public sealed class LockstepChannel : ICommandChannel
{
    private readonly LockstepSession _lockstep;

    public LockstepChannel(LockstepSession lockstep)
    {
        _lockstep = lockstep;
        lockstep.Desync += tick => Problem ??= $"The game is out of sync (step {tick}).";
    }

    public bool PlaysAbsentHumans => false;

    public string? Problem { get; private set; }

    /// <summary>Report a problem found outside lockstep, such as a lost peer; the first one stands.</summary>
    public void Fail(string problem) => Problem ??= problem;

    public Submission Submit(Simulation sim, GameCommand command)
    {
        if (Problem != null) return Submission.Rejected(Problem);
        // The same checks run again when the command applies; the state may have moved on by then.
        if (!sim.MayAct(command.Player)) return Submission.Rejected("Not your turn");
        if (command.Validate(sim) is { } error) return Submission.Rejected(error);
        _lockstep.Enqueue(command);
        return Submission.Queued;
    }

    public bool Pump(Simulation sim)
    {
        if (Problem != null) return false;
        long applied = _lockstep.Applied;
        int turn = sim.State.Turn;
        try
        {
            while (Problem == null && sim.State.Turn == turn && _lockstep.Pump(maxTicks: 1) > 0) { }
        }
        catch (InvalidDataException e) { Problem = e.Message; }
        return _lockstep.Applied != applied;
    }
}
