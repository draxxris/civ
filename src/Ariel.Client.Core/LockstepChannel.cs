using Ariel.Game;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// A networked game: commands are checked here for an immediate answer, then queued in lockstep and applied, with every
/// other peer's, when the ticks that carry them complete. A pump stops after a tick that started a new turn, so the
/// session reads each turn's events before the event bus moves on.
/// </summary>
public sealed class LockstepChannel(LockstepSession lockstep) : ICommandChannel
{
    public bool PlaysAbsentHumans => false;

    public Submission Submit(Simulation sim, GameCommand command)
    {
        // The same checks run again when the command applies; the state may have moved on by then.
        if (!sim.MayAct(command.Player)) return Submission.Rejected("Not your turn");
        if (command.Validate(sim) is { } error) return Submission.Rejected(error);
        lockstep.Enqueue(command);
        return Submission.Queued;
    }

    public bool Pump(Simulation sim)
    {
        long applied = lockstep.Applied;
        int turn = sim.State.Turn;
        while (sim.State.Turn == turn && lockstep.Pump(maxTicks: 1) > 0) { }
        return lockstep.Applied != applied;
    }
}
