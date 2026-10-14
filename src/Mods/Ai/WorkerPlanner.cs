using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Workers improve worked/planned tiles, connect resources, then link cities with roads.
/// World previews supply legality, yields and work; queries do not move units or consume RNG.</summary>
internal sealed class WorkerPlanner(AiContext ctx)
{
    private readonly HashSet<TileId> _claimed = new();
    private List<WorkTile>? _tiles;
    private HashSet<TileId>? _roads;
    private readonly Dictionary<string, BuildDef[]> _builds = new(StringComparer.Ordinal);
    private sealed record WorkTile(TileId Tile, int Priority);
    private sealed record Job(TileId Tile, string Build, long Value);

    public void Plan()
    {
        foreach (var worker in ctx.UnitsWithRole(UnitRole.Worker).ToList())
        {
            ctx.Done.Add(worker.Id);
            if (worker.MovesLeft > 0) Act(worker);
        }
    }

    private void Act(Unit worker)
    {
        if (ctx.ThreatAround(worker.Position, 2) > 0) { Retreat(worker); return; }
        var job = ChooseJob(worker);
        if (job is null) { Retreat(worker); return; }
        _claimed.Add(job.Tile);
        var hex = ctx.Grid.HexOf(job.Tile);
        if (worker.Position != hex) ctx.MoveToward(worker, hex);
        if (ctx.Alive(worker) && worker.Position == hex)
            ctx.Issue(new StartBuildCommand { Player = ctx.PlayerId, Unit = worker.Id, Build = job.Build });
    }

    private void Retreat(Unit worker)
    {
        if (ctx.CityAtUnit(worker) is null && ctx.NearestCity(worker.Position) is { } city) ctx.MoveToward(worker, city.Position);
    }

    private Job? ChooseJob(Unit worker)
    {
        var here = ctx.Tile(worker.Position);
        int area = ctx.Sim.State.Plots[here.Value].AreaId;
        var candidates = Tiles().Where(t => Available(t.Tile, area))
            .OrderByDescending(t => t.Priority - 7 * ctx.Grid.Distance(here, t.Tile)).ThenBy(t => t.Tile.Value).Take(8);
        foreach (var tile in candidates)
            if (BestBuildAt(worker, tile.Tile) is { } job) return job;
        return null;
    }

    private bool Available(TileId tile, int area) => !_claimed.Contains(tile)
        && ctx.Sim.State.Plots[tile.Value].AreaId == area && ctx.ThreatAround(ctx.Grid.HexOf(tile), 2) == 0;

    private List<WorkTile> Tiles()
    {
        if (_tiles is not null) return _tiles;
        var worked = ctx.Cities.SelectMany(c => c.Worked).Select(ctx.Grid.TileAt).ToHashSet();
        var seen = new HashSet<TileId>();
        _tiles = new();
        foreach (var city in ctx.Cities)
            foreach (var tile in ctx.Service<ICityWorkService>().WorkArea(ctx.Sim, city.Id))
                if (seen.Add(tile) && Workable(tile)) _tiles.Add(new(tile, TilePriority(tile, worked.Contains(tile))));
        foreach (var road in RoadTiles().Where(seen.Add)) _tiles.Add(new(road, 14));
        return _tiles;
    }

    private bool Workable(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        if (plot.Owner != ctx.PlayerId || ctx.Sim.State.CityAt(ctx.Grid.HexOf(tile)) is not null) return false;
        var terrain = ctx.Sim.Content.Get<TerrainDef>(plot.Terrain);
        return !terrain.IsWater && !terrain.Impassable && plot.Kind != PlotKind.Peak;
    }

    private int TilePriority(TileId tile, bool worked)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        int priority = worked ? 25 : 8;
        if (VisibleBonus(plot) is not null) priority += 20;
        if (plot.Improvement is not null) priority -= 18;
        if (plot.BuildProgress.Count > 0) priority += 100; // Finish work before starting another task.
        return priority;
    }

    private Job? BestBuildAt(Unit worker, TileId tile)
    {
        var world = ctx.Service<IWorkerBuildService>();
        var before = ctx.Service<IPlotYieldService>().Calculate(ctx.Sim, ctx.PlayerId, tile).Yields;
        Job? best = null;
        foreach (var build in AvailableBuilds(worker))
        {
            if (build.RouteType is not null && !RoadTiles().Contains(tile)) continue;
            var preview = world.Preview(ctx.Sim, ctx.PlayerId, worker.Id, tile, build.Id);
            if (preview.Error is not null) continue;
            long value = Gain(build, tile, before, preview.Yields);
            if (value <= 0) continue;
            value = value * 1000 / Math.Max(100, preview.RequiredWork);
            if (best is null || value > best.Value) best = new(tile, build.Id, value);
        }
        return best;
    }

    private BuildDef[] AvailableBuilds(Unit worker)
    {
        if (_builds.TryGetValue(worker.Type, out var builds)) return builds;
        var unit = ctx.Def(worker);
        var rc = new RequirementContext(ctx.Sim, ctx.PlayerId);
        builds = ctx.Catalog.WorkerBuilds.Where(b => unit.Builds.GetValueOrDefault(b.Id) && b.Cost == 0
            && ctx.Sim.Content.Requirements.EvaluateKind(b.Requirements, rc, "tech").Success).ToArray();
        _builds[worker.Type] = builds;
        return builds;
    }

    private long Gain(BuildDef build, TileId tile, YieldVector before, YieldVector after)
    {
        long gain = (after.Food - before.Food) * 8 + (after.Production - before.Production) * 7 + (after.Commerce - before.Commerce) * 5;
        if (build.RouteType is not null) gain += 12;
        if (build.ImprovementType is { } id) gain += ConnectionValue(id, ctx.Sim.State.Plots[tile.Value]);
        return gain;
    }

    private int ConnectionValue(string id, Plot plot)
    {
        var improvement = ctx.Sim.Content.Get<ImprovementDef>(id);
        int value = (ctx.Leader?.ImprovementWeightModifiers.GetValueOrDefault(id) ?? 0) / 5;
        if (plot.Improvement is not null) return 0;
        if (VisibleBonus(plot) is { } bonus && improvement.BonusEffects.GetValueOrDefault(bonus.Id)?.Connects == true)
            value += 30 + 8 * bonus.Happiness + 6 * bonus.Health;
        return value;
    }

    private BonusDef? VisibleBonus(Plot plot)
    {
        if (plot.Bonus is not { } id) return null;
        var bonus = ctx.Sim.Content.Get<BonusDef>(id);
        return bonus.RevealTech is { } tech && !ctx.Team.Techs.Contains(tech) ? null : bonus;
    }

    private HashSet<TileId> RoadTiles()
    {
        if (_roads is not null) return _roads;
        _roads = new();
        foreach (var city in ctx.Cities.Skip(1)) AddLine(city.Position, NearestOther(city)?.Position);
        foreach (var tile in ctx.Grid.Tiles.Where(t => ConnectsResource(ctx.Sim.State.Plots[t.Value])))
            AddLine(ctx.Grid.HexOf(tile), ctx.NearestCity(ctx.Grid.HexOf(tile))?.Position);
        return _roads;
    }

    private City? NearestOther(City city) => ctx.Cities.Where(c => c.Id != city.Id)
        .OrderBy(c => ctx.Grid.Distance(c.Position, city.Position)).ThenBy(c => c.Id).FirstOrDefault();

    private bool ConnectsResource(Plot plot)
    {
        if (plot.Owner != ctx.PlayerId || plot.Route is not null || plot.Improvement is not { } id) return false;
        var bonus = VisibleBonus(plot);
        return bonus is not null && ctx.Sim.Content.Get<ImprovementDef>(id).BonusEffects.GetValueOrDefault(bonus.Id)?.Connects == true;
    }

    private bool RoadPlot(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        if (plot.Owner is { } owner && owner != ctx.PlayerId) return false;
        if (!plot.Revealed.Contains(ctx.Team.Id) || ctx.Sim.State.CityAt(ctx.Grid.HexOf(tile)) is not null) return false;
        var terrain = ctx.Sim.Content.Get<TerrainDef>(plot.Terrain);
        return !terrain.IsWater && !terrain.Impassable && plot.Kind != PlotKind.Peak;
    }

    private void AddLine(Hex from, Hex? to)
    {
        if (to is not { } end) return;
        foreach (var hex in HexLine.Between(ctx.Grid, from, end))
        {
            if (!ctx.Grid.TryTile(hex, out var tile)) continue;
            var plot = ctx.Sim.State.Plots[tile.Value];
            if (plot.Route is null && RoadPlot(tile)) _roads!.Add(tile);
        }
    }
}
