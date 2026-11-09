using Ariel.Game;

namespace Ariel.Client;

/// <summary>
/// Other players' moves and the turn rollover, off the front end's thread. With <see cref="BackgroundTurns"/>, a command
/// that ends the turn leaves that work pending; the session copies the simulation, works on the copy on a pool thread
/// and takes the result back in <see cref="Pump"/>. Until then the simulation the front end reads stays exactly as it
/// was, so it never sees a half-made turn and never races the worker, and the local player's commands are refused.
/// </summary>
public sealed partial class GameSession
{
    private TurnWork? _turnWork;
    private Task? _turnTask;

    /// <summary>Whether other players' moves and the turn rollover run on another thread, so the front end keeps
    /// drawing while they take their time. Without it they run inside the command that ends the turn. Lockstep peers
    /// compare states between commands, so they must all agree on it.</summary>
    public bool BackgroundTurns { get; }

    /// <summary>Whether other players are moving on another thread; the turn carries on once they finish.</summary>
    public bool TurnInProgress => _turnWork != null;

    /// <summary>Block until the other players have finished moving, and carry on from there, as a frame's
    /// <see cref="Pump"/> would. For scripted runs that end several turns in a row; front ends keep pumping instead.</summary>
    public void WaitForOtherPlayers()
    {
        while (_turnTask is { } task)
        {
            task.Wait();
            Pump();
        }
    }

    /// <summary>Start the pending turn work, if any: on a pool thread with <see cref="BackgroundTurns"/>, otherwise in
    /// place. Returns whether work is now running in the background.</summary>
    private bool StartTurnWork()
    {
        if (_turnWork != null) return true;
        if (!Sim.HasPendingTurnWork) return false;
        if (!BackgroundTurns)
        {
            Sim.RunPendingTurnWork();
            CollectNotifications();
            return false;
        }
        var work = Sim.BeginTurnWork();
        _turnWork = work;
        _turnTask = Task.Run(work.Run);
        Revision++;
        return true;
    }

    /// <summary>Take back finished turn work. Returns whether the simulation changed; a failure in the work is
    /// rethrown here, on the front end's thread, as it would have been in place.</summary>
    private bool FinishTurnWork()
    {
        if (_turnTask is not { IsCompleted: true } task) return false;
        var work = _turnWork!;
        _turnWork = null;
        _turnTask = null;
        task.GetAwaiter().GetResult();
        Sim.CompleteTurnWork(work);
        return true;
    }

    /// <summary>Forget running work whose simulation is being replaced. The worker only touches its own copy, so it
    /// may finish unobserved.</summary>
    private void AbandonTurnWork()
    {
        _turnWork = null;
        _turnTask = null;
    }
}
