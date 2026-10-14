using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>A scored city location.</summary>
internal sealed record Site(TileId Tile, int Score, int Area);

/// <summary>Scores city sites on the hex radius-2/3 neighborhood and decides when more cities are wanted.</summary>
internal sealed class SitePlanner
{
    private const int MinSiteScore = 100, SearchRadius = 9;
    private readonly AiContext ctx;
    private readonly Lazy<int[]> _tileValue;
    private readonly Lazy<bool[]> _claimed;
    private readonly Lazy<IReadOnlyList<Site>> _sites;

    public SitePlanner(AiContext context)
    {
        ctx = context;
        _tileValue = new(() => BuildTileValues(ctx));
        _claimed = new(() => BuildClaimed(ctx));
        _sites = new(FindSites);
    }

    public IReadOnlyList<Site> Sites => _sites.Value;
    private int MinRange => ctx.Sim.Content.Get<RuleConstantsDef>().All.Single().Values.MinCityRange;

    public bool WantsMoreCities(int settlersInFlight, int queued)
    {
        int pending = settlersInFlight + queued;
        if (ctx.Cities.Count <= 1 && !EstablishedCapital()) return false;
        int cap = Math.Min(14, 4 + ctx.Turn / 15 + ctx.Weight("growth") / 10);
        if (pending >= 2 || ctx.Cities.Count + pending >= cap || ctx.Self.Treasury < 5 && ctx.Turn > 30) return false;
        return DistinctSites().Count > pending;
    }

    private bool EstablishedCapital()
    {
        // Do not rush free settlers while the capital cannot support growth or replace its defenders.
        bool worker = ctx.UnitsWithRole(UnitRole.Worker).Any();
        int soldiers = ctx.Units.Count(u => ctx.Role(u) == UnitRole.Melee);
        return worker && soldiers >= 2 && ctx.Turn >= 55;
    }

    public Site? FirstSite(Unit settler)
    {
        var here = ctx.Tile(settler.Position);
        var nearby = Sites.Where(s => ctx.Grid.Distance(here, s.Tile) <= 1).ToArray();
        int bestValue = nearby.Select(s => s.Score).DefaultIfEmpty(0).Max();
        return nearby.Where(s => s.Score * 100 >= bestValue * 85)
            .OrderByDescending(s => ctx.Sim.State.Plots[s.Tile.Value].Kind == PlotKind.Hills)
            .ThenByDescending(s => s.Score).ThenBy(s => s.Tile.Value).FirstOrDefault();
    }

    /// <summary>Greedy non-overlapping best sites.</summary>
    internal IReadOnlyList<Site> DistinctSites()
    {
        var picked = new List<Site>();
        foreach (var site in Sites)
            if (picked.All(p => ctx.Grid.Distance(p.Tile, site.Tile) > MinRange + 1)) picked.Add(site);
        return picked;
    }

    public bool CanFound(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        var terrain = ctx.Sim.Content.Get<TerrainDef>(plot.Terrain);
        if (ForbiddenPlot(plot, terrain)) return false;
        if (terrain.CanFoundCity) return true;
        if (terrain.FoundCoast && IsCoastal(tile)) return true;
        return terrain.FoundFreshWater && ctx.Service<IWaterService>().IsFreshWater(ctx.Sim, tile);
    }

    private bool ForbiddenPlot(Plot plot, TerrainDef terrain)
    {
        if (terrain.IsWater || terrain.Impassable || plot.Kind == PlotKind.Peak) return true;
        if (plot.Feature is { } f && ctx.Sim.Content.Get<FeatureDef>(f).NoCity) return true;
        return plot.Owner is { } owner && owner != ctx.PlayerId;
    }

    private bool IsCoastal(TileId tile) => ctx.Grid.Neighbors(tile).Any(n =>
        ctx.Sim.Content.Get<TerrainDef>(ctx.Sim.State.Plots[n.Value].Terrain).IsWater && !ctx.Sim.State.Plots[n.Value].IsLake);

    private IReadOnlyList<Site> FindSites() => CandidateTiles().Select(Score).Where(s => s.Score >= MinSiteScore)
        .OrderByDescending(s => s.Score).ThenBy(s => s.Tile.Value).ToArray();

    private IEnumerable<TileId> CandidateTiles()
    {
        var seen = new HashSet<TileId>();
        IEnumerable<Hex> anchors = ctx.Cities.Count > 0 ? ctx.Cities.Select(c => c.Position) : ctx.Units.Select(u => u.Position);
        foreach (var anchor in anchors)
            foreach (var tile in ctx.Grid.Range(ctx.Grid.TileAt(anchor), SearchRadius))
                if (seen.Add(tile) && Eligible(tile)) yield return tile;
    }

    private bool Eligible(TileId tile)
    {
        if (!ctx.Sim.State.Plots[tile.Value].Revealed.Contains(ctx.Team.Id) || !CanFound(tile)) return false;
        foreach (var city in ctx.Sim.State.Cities)
            if (ctx.Grid.Distance(ctx.Grid.TileAt(city.Position), tile) <= MinRange + (city.Owner == ctx.PlayerId ? 1 : 2)) return false;
        return true;
    }

    private Site Score(TileId tile)
    {
        int score = 0;
        foreach (var t in ctx.Grid.Range(tile, 3))
        {
            var plot = ctx.Sim.State.Plots[t.Value];
            if (plot.Owner is { } o && o != ctx.PlayerId) continue;
            int value = _tileValue.Value[t.Value];
            int dist = ctx.Grid.Distance(tile, t);
            if (_claimed.Value[t.Value]) value /= 3;
            score += dist <= 2 ? value : value / 2;
        }
        var center = ctx.Sim.State.Plots[tile.Value];
        if (IsCoastal(tile)) score += 10;
        if (ctx.Service<IWaterService>().IsFreshWater(ctx.Sim, tile)) score += 12;
        if (center.Kind == PlotKind.Hills) score += 14;
        if (ctx.Sim.State.Plots[tile.Value].Bonus is not null) score -= 6;
        return new(tile, score - DistancePenalty(tile), center.AreaId);
    }

    private int DistancePenalty(TileId tile)
    {
        if (ctx.Cities.Count == 0) return 0;
        int nearest = ctx.Cities.Min(c => ctx.Grid.Distance(ctx.Grid.TileAt(c.Position), tile));
        return Math.Max(0, nearest - 6) * 12;
    }

    private static int[] BuildTileValues(AiContext ctx)
    {
        var values = new int[ctx.Sim.State.Plots.Length];
        for (int i = 0; i < values.Length; i++) values[i] = PlotValue(ctx, ctx.Sim.State.Plots[i]);
        return values;
    }

    private static int PlotValue(AiContext ctx, Plot plot)
    {
        var terrain = ctx.Sim.Content.Get<TerrainDef>(plot.Terrain);
        if (terrain.Impassable || plot.Kind == PlotKind.Peak) return 0;
        var y = terrain.Yields;
        int value = (int)(y.Food * 4 + y.Production * 3 + y.Commerce);
        if (plot.Feature is { } f) { var fy = ctx.Sim.Content.Get<FeatureDef>(f).YieldChanges; value += (int)(fy.Food * 4 + fy.Production * 3 + fy.Commerce); }
        if (plot.Kind == PlotKind.Hills) value += 4;
        if (plot.RiverEdges != 0) value += 2;
        if (plot.Bonus is { } b) { var bonus = ctx.Sim.Content.Get<BonusDef>(b); value += 12 + 5 * (bonus.Happiness + bonus.Health) + (int)(bonus.YieldChanges.Food * 4 + bonus.YieldChanges.Production * 3); }
        return Math.Max(0, value);
    }

    private static bool[] BuildClaimed(AiContext ctx)
    {
        var claimed = new bool[ctx.Sim.State.Plots.Length];
        foreach (var city in ctx.Sim.State.Cities)
            foreach (var tile in ctx.Grid.Range(ctx.Grid.TileAt(city.Position), 2)) claimed[tile.Value] = true;
        return claimed;
    }
}
