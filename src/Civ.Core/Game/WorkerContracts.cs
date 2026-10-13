namespace Civ.Game;

/// <summary>Worker command/query boundary shared with clients and Military.</summary>
public interface IWorkerBuildService
{
    /// <summary>Explain command rejection without changing state.</summary>
    string? Validate(Simulation sim, int player, int unit, string build);
    /// <summary>Start a validated build and charge its initial gold cost.</summary>
    void Start(Simulation sim, int player, int unit, string build);
    /// <summary>Integer work threshold after terrain, speed and starting-era scaling.</summary>
    int RequiredWork(Simulation sim, TileId tile, string build);
}
/// <summary>Assign an owned worker to a build on its current plot. World registers the command identity.</summary>
public sealed class StartBuildCommand : GameCommand
{
    /// <summary>Worker entity id.</summary>
    public int Unit { get; init; }
    /// <summary>Build definition id.</summary>
    public string Build { get; init; } = "";
    /// <inheritdoc/>
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IWorkerBuildService>().Single().Validate(sim, Player, Unit, Build);
    /// <inheritdoc/>
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IWorkerBuildService>().Single().Start(sim, Player, Unit, Build);
}
