namespace Ariel.Game;

/// <summary>Worker command/query boundary shared with clients and Military.</summary>
public interface IWorkerBuildService
{
    /// <summary>Explain command rejection without changing state.</summary>
    string? Validate(Simulation sim, int player, int unit, string build);
    /// <summary>Explain cancel rejection without changing state.</summary>
    string? ValidateCancel(Simulation sim, int player, int unit);
    /// <summary>Read-only build preview at a proposed tile. Includes legality, resulting yields and required work.</summary>
    WorkerBuildPreview Preview(Simulation sim, int player, int unit, TileId tile, string build);
    /// <summary>The legality part of <see cref="Preview"/> alone: why this worker cannot make the build at the tile, or
    /// null. A legal preview's yields and work depend only on the player, tile and build, not on which worker asks, so
    /// a planner weighing many workers against one unchanging state may preview each tile and build once.</summary>
    string? ValidateAt(Simulation sim, int player, int unit, TileId tile, string build);
    /// <summary>The part of <see cref="ValidateAt"/> that depends on the player, tile and build alone: why no worker of
    /// the player could make the build there, or null. <see cref="ValidateAt"/> rejects whatever this rejects, so a
    /// planner may ask it once per tile and build and skip the per-worker checks where it fails.</summary>
    string? ValidatePlot(Simulation sim, int player, TileId tile, string build);
    /// <summary>Whether a worker has a legal unfinished order on its current plot. Continuation needs no new payment.</summary>
    bool IsWorking(Simulation sim, int unit);
    /// <summary>Start a validated build and charge its initial gold cost.</summary>
    void Start(Simulation sim, int player, int unit, string build);
    /// <summary>Cancel an active build order on a worker.</summary>
    void Cancel(Simulation sim, int player, int unit);
    /// <summary>Integer work threshold after terrain, speed and starting-era scaling.</summary>
    int RequiredWork(Simulation sim, TileId tile, string build);
}
/// <summary>A remote worker task preview. Rejected tasks have no resulting yields.</summary>
public sealed record WorkerBuildPreview(string? Error, Ariel.Content.YieldVector Yields, int RequiredWork);

/// <summary>Assign an owned worker to a build on its current plot. World registers the command identity.</summary>
public sealed class StartBuildCommand : GameCommand
{
    /// <summary>Worker entity id.</summary>
    public int Unit { get; init; }
    /// <summary>Build definition id.</summary>
    public string Build { get; init; } = "";
    /// <summary>Starting a build pays its cost and gives the worker its order: the treasury and worker orders.</summary>
    public override IReadOnlyList<string>? WriteDomains => Writes;
    private static readonly IReadOnlyList<string> Writes = [EconomyStateDomains.Treasury, UnitStateDomains.WorkerOrders];
    /// <inheritdoc/>
    public override string? Validate(Simulation sim) => sim.Rules().WorkerBuilds.Validate(sim, Player, Unit, Build);
    /// <inheritdoc/>
    public override void Apply(Simulation sim) => sim.Rules().WorkerBuilds.Start(sim, Player, Unit, Build);
}

/// <summary>Cancel an active build order on a worker. World registers the command identity.</summary>
public sealed class CancelBuildCommand : GameCommand
{
    /// <summary>Worker entity id.</summary>
    public int Unit { get; init; }
    /// <inheritdoc/>
    public override string? Validate(Simulation sim) => sim.Rules().WorkerBuilds.ValidateCancel(sim, Player, Unit);
    /// <inheritdoc/>
    public override void Apply(Simulation sim) => sim.Rules().WorkerBuilds.Cancel(sim, Player, Unit);
}
