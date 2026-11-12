using Ariel.Game;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// A networked game: commands are checked here for an immediate answer, then queued in lockstep and applied, with every
/// other peer's, when the ticks that carry them complete. A pump stops after a tick that started a new turn, so the
/// session reads each turn's events before the event bus moves on, and after a tick that left turn work pending (see
/// <see cref="Simulation.DeferTurnWork"/>), which the session must finish first. A failed handshake stops the game and becomes
/// <see cref="Problem"/>; a desync is the owner's to resolve (see <see cref="MultiplayerGame"/>).
/// </summary>
public sealed class LockstepChannel : ICommandChannel
{
    /// <summary>Most ticks one pump runs, so a frame never stalls.</summary>
    public const int MaxTicksPerPump = 64;

    private readonly LockstepSession _lockstep;

    public LockstepChannel(LockstepSession lockstep) => _lockstep = lockstep;

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
            // Turn work a tick left pending must finish before the next tick: keep receiving, but run nothing.
            if (sim.HasPendingTurnWork) { _lockstep.Pump(maxTicks: 0); return false; }
            // Bounded: a peer left alone in lockstep needs nobody else's input and could run ticks forever.
            for (int i = 0; i < MaxTicksPerPump && Problem == null && sim.State.Turn == turn && !sim.HasPendingTurnWork
                && _lockstep.Pump(maxTicks: 1) > 0; i++) { }
        }
        catch (InvalidDataException e) { Problem = e.Message; }
        // A rule that throws while a tick applies stops this game here, once, rather than on every frame.
        catch (Exception e) when (e is not OutOfMemoryException) { Problem = $"The game stopped: {e.Message}"; }
        return _lockstep.Applied != applied;
    }
}
