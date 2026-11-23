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
    /// <summary>Natural potential for worker validation; excludes improvements and modifier sources. With no
    /// <paramref name="player"/> it uses no team's knowledge: no resource counts, and peaks are not unworkable.</summary>
    PlotYieldResult CalculateNature(Simulation sim, int? player, TileId tile, bool ignoreFeature = false);
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
    /// <summary>The cities split into groups in their given order: a city joins the first group whose first member is
    /// <see cref="Connected"/> to it, otherwise it starts a group. The default asks <see cref="Connected"/> pair by
    /// pair; the network answers with one flood per group.</summary>
    IReadOnlyList<IReadOnlyList<int>> Groups(Simulation sim, IReadOnlyList<int> cities)
    {
        var groups = new List<List<int>>();
        foreach (int city in cities)
        {
            var group = groups.FirstOrDefault(g => Connected(sim, g[0], city));
            if (group is null) groups.Add([city]);
            else group.Add(city);
        }
        return groups;
    }
    /// <summary>For each of <paramref name="others"/>, in order, whether <see cref="Connected"/> from
    /// <paramref name="city"/> to it holds. The default asks pair by pair; the network answers with one flood.</summary>
    bool[] ConnectedFrom(Simulation sim, int city, IReadOnlyList<int> others)
    {
        var result = new bool[others.Count];
        for (int i = 0; i < result.Length; i++) result[i] = Connected(sim, city, others[i]);
        return result;
    }
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
/// <summary>World's entry point for a rule that reshapes plots once the map exists, such as a mod's environmental
/// change. World keeps the map consistent: heights and cliffs, features that no longer fit, areas and lake flags.</summary>
public interface ITerrainChanges
{
    /// <summary>Whether <paramref name="feature"/> may stand on the plot as it is now: its terrain, relief, rivers and
    /// neighbours. Read-only and RNG-free.</summary>
    bool FeatureFits(Simulation sim, TileId tile, string feature);
    /// <summary>Start a pass of changes. Areas and lake flags are rebuilt once, when the pass is disposed, if a change in
    /// it turned land to water or back; until then they describe the map before the pass.</summary>
    ITerrainChangePass Begin(Simulation sim);
}

/// <summary>A pass of plot changes, from <see cref="ITerrainChanges.Begin"/>.</summary>
public interface ITerrainChangePass : IDisposable
{
    /// <summary>Give a plot this terrain and feature. Returns false, and does nothing, when it already has both.
    /// Otherwise it settles the plot's and its neighbours' heights and cliffs, drops a feature that no longer fits, and
    /// then publishes <see cref="TerrainChanged"/>. The caller settles anything else the change means for the plot (its
    /// improvement or route, say) before calling.</summary>
    bool Change(TileId tile, string terrain, string? feature);
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

/// <summary>A rule changed a plot's terrain or feature through <see cref="ITerrainChangePass.Change"/>. The plot is
/// settled when this is published; areas and lake flags are rebuilt when the pass ends.</summary>
public sealed record TerrainChanged(TileId Plot, string PreviousTerrain, string NewTerrain, string? PreviousFeature, string? NewFeature) : DomainEvent;
public sealed record FeatureChanged(TileId Plot, string? PreviousFeature, string? NewFeature) : DomainEvent;
public sealed record BonusDiscovered(TileId Plot, string Bonus, int Player) : DomainEvent;
public sealed record BonusDepleted(TileId Plot, string Bonus, int Player) : DomainEvent;
public sealed record MineDepleted(TileId Plot, string PreviousImprovement, string ReplacementImprovement, int Player) : DomainEvent;
