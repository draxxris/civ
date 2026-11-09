using Ariel.Game;

namespace Ariel.Client;

/// <summary>What became of a submitted command: <see cref="Error"/> says why it was rejected, and <see cref="Applied"/>
/// whether the simulation already changed. A networked channel accepts a command without applying it yet.</summary>
public readonly record struct Submission(string? Error, bool Applied)
{
    public static Submission Rejected(string? error) => new(error ?? "rejected", false);
    public static readonly Submission Done = new(null, true);
    public static readonly Submission Queued = new(null, false);
}

/// <summary>
/// The only way a <see cref="GameSession"/> changes its simulation. A local game applies each command at once. A
/// networked game queues it, so every peer applies the same commands at the same step, and delivers them through
/// <see cref="Pump"/>.
/// </summary>
public interface ICommandChannel
{
    /// <summary>Whether this front end is the only one playing, so it also ends the turns of human players nobody controls.</summary>
    bool PlaysAbsentHumans { get; }

    /// <summary>What has gone wrong with the game's connection (desync, a lost peer), or null.</summary>
    string? Problem { get; }

    /// <summary>Issue a command for a player this front end controls.</summary>
    Submission Submit(Simulation sim, GameCommand command);

    /// <summary>Apply commands that became ready since the last call, and return whether the simulation changed. Front
    /// ends call it every frame, so it must return at once, without allocating, when nothing is ready.</summary>
    bool Pump(Simulation sim);
}

/// <summary>Single-player: commands apply immediately and nothing arrives later.</summary>
public sealed class LocalCommandChannel : ICommandChannel
{
    public static readonly LocalCommandChannel Instance = new();

    private LocalCommandChannel() { }

    public bool PlaysAbsentHumans => true;

    public string? Problem => null;

    public Submission Submit(Simulation sim, GameCommand command) =>
        sim.TrySubmit(command, out var error) ? Submission.Done : Submission.Rejected(error);

    public bool Pump(Simulation sim) => false;
}
