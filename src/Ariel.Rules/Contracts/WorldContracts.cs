using Ariel.Content;

namespace Ariel.Game;

/// <summary>Ruleset-owned map geometry: which heights and cliffs the contract allows, and which ones a map breaks.
/// Nothing in the game calls it; <c>mise run validate</c> and the tests do, so a map is never corrected or refused.</summary>
public interface IMapGeometry
{
    /// <summary>The height a plot's terrain, lake flag and relief imply before any cliff raises it.</summary>
    sbyte HeightOf(Simulation sim, Plot plot);
    /// <summary>Every rule the map breaks, one line each, naming the tile, the direction and the rule. Never mutates
    /// the map, consumes no RNG and publishes no events.</summary>
    IReadOnlyList<string> Report(Simulation sim);
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
/// <summary>Feature production is a pending city grant; Cities owns the overflow mutation.</summary>
public sealed record FeatureProduction(int Player, TileId Plot, int? City, int Production) : DomainEvent;

public sealed record GlobalWarmingOccurred(TileId Plot, string PreviousTerrain, string NewTerrain, string? PreviousFeature, string? NewFeature) : DomainEvent;
public sealed record FeatureChanged(TileId Plot, string? PreviousFeature, string? NewFeature) : DomainEvent;
public sealed record BonusDiscovered(TileId Plot, string Bonus, int Player) : DomainEvent;
public sealed record BonusDepleted(TileId Plot, string Bonus, int Player) : DomainEvent;
public sealed record MineDepleted(TileId Plot, string PreviousImprovement, string ReplacementImprovement, int Player) : DomainEvent;
