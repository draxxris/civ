using Civ.Content;

namespace Civ.Game;

/// <summary>Ruleset-owned map repair after generation, scenario configuration or save loading.</summary>
public interface IMapSanitizer
{
    /// <summary>Clear invalid map state without RNG, events, or generation.</summary>
    void Sanitize(Simulation sim);
}

/// <summary>Optional ruleset setup. Selected when SimulationSettings.WorldSize is supplied.</summary>
public interface INewGameSetup
{
    /// <summary>Validate setup content ids and resolve the effective map dimensions.</summary>
    MapSpec ResolveMap(ContentDb content, SimulationSettings settings);
    /// <summary>Create player and team data before map generation.</summary>
    void CreatePlayers(Simulation sim);
    /// <summary>Normalize starts, grant units and initialize sight after map generation and indexing.</summary>
    void Complete(Simulation sim);
}
/// <summary>Explainable integer plot yields for a player's team knowledge.</summary>
public sealed record PlotYieldResult(YieldVector Yields, Ledger Ledger);
/// <summary>Read-only, RNG-free plot calculation owned by World.</summary>
public interface IPlotYieldService
{
    /// <summary>Final plot yields, including registered modifier sources and final integer rounding.</summary>
    PlotYieldResult Calculate(Simulation sim, int player, TileId tile);
    /// <summary>Natural potential for worker validation; excludes improvements and modifier sources.</summary>
    PlotYieldResult CalculateNature(Simulation sim, int player, TileId tile, bool ignoreFeature = false);
}
/// <summary>Final yield adjustments, after ordinary modifiers. The supplied sink contains the plot ledger.</summary>
public interface IPlotYieldFinalizer
{
    YieldVector Adjust(Simulation sim, int player, TileId tile, Plot plot, YieldVector yields, ModifierSink sink);
}
/// <summary>Read-only fresh-water and team-gated irrigation queries.</summary>
public interface IWaterService
{
    /// <summary>Fresh water on a land plot from rivers, adjacent lakes or water-supplying features.</summary>
    bool IsFreshWater(Simulation sim, TileId tile);
    /// <summary>Fresh water or access through owned irrigation carriers and team capabilities.</summary>
    bool IsIrrigated(Simulation sim, int player, TileId tile);
}
/// <summary>Read-only resource access through a player's physical trade network.</summary>
public interface IResourceNetwork
{
    /// <summary>Physical connected copies, after reveal, trade and obsolescence gates; excludes traded allocations.</summary>
    int ConnectedCount(Simulation sim, int player, int city, string bonus);
    /// <summary>Tiles reachable through the actual physical network, before resource allocations.</summary>
    IReadOnlySet<TileId> ConnectedTiles(Simulation sim, int player, int city);
    /// <summary>All physical copies in one network traversal.</summary>
    IReadOnlyDictionary<string, int> ConnectedCounts(Simulation sim, int player, int city);
}
/// <summary>Physical city connection through routes, rivers and unlocked water trade.</summary>
public interface ICityConnectionService
{
    bool Connected(Simulation sim, int firstCity, int secondCity);
    bool IsTileBlockaded(Simulation sim, TileId tile, int team);
    bool IsCityBlockaded(Simulation sim, int city);
}
/// <summary>Authoritative team sight updates, owned by World.</summary>
public interface IVisibilityService
{
    bool IsUnitVisible(Simulation sim, int unit, int observerTeam);
    /// <summary>Terrain-limited sight, including high-ground outer-ring sight; aircraft bypass occlusion.</summary>
    bool CanSeePlot(Simulation sim, TileId observer, TileId target, int team, int range, bool aerial = false);
    /// <summary>Live observed state, last observed state in fog, or null when no observation exists.</summary>
    RevealedPlotSnapshot? SnapshotOf(Simulation sim, TileId tile, int team);
    /// <summary>Record an explicit map reveal without granting current sight.</summary>
    void RevealPlot(Simulation sim, TileId tile, int team);
    /// <summary>Military calls this after movement; founding and turn boundaries also refresh sight.</summary>
    void Recompute(Simulation sim);
}
/// <summary>Start evaluation and setup-only normalization.</summary>
public interface IStartingPositionService
{
    /// <summary>Select separated habitable locations without changing state or consuming RNG.</summary>
    IReadOnlyList<TileId> Select(Simulation sim, int count);
    /// <summary>Setup-only mutation: improve natural potential near weak starts without moving resources.</summary>
    void Normalize(Simulation sim, IReadOnlyList<TileId> starts);
}
/// <summary>Elevation is independent of terrain and features.</summary>
public enum PlotKind
{
    /// <summary>Ordinary flat land or water.</summary>
    Flat,
    /// <summary>Hill relief on land.</summary>
    Hills,
    /// <summary>Mountain relief, gated by peak capabilities.</summary>
    Peak,
}
/// <summary>Feature production is a pending city grant; Cities owns the overflow mutation.</summary>
public sealed record FeatureProduction(int Player, TileId Plot, int? City, int Production) : DomainEvent;

public sealed record GlobalWarmingOccurred(TileId Plot, string PreviousTerrain, string NewTerrain, string? PreviousFeature, string? NewFeature) : DomainEvent;
public sealed record FeatureChanged(TileId Plot, string? PreviousFeature, string? NewFeature) : DomainEvent;
public sealed record BonusDiscovered(TileId Plot, string Bonus, int Player) : DomainEvent;
public sealed record BonusDepleted(TileId Plot, string Bonus, int Player) : DomainEvent;
public sealed record MineDepleted(TileId Plot, string PreviousImprovement, string ReplacementImprovement, int Player) : DomainEvent;
