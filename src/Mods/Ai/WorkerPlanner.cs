using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Workers improve worked/planned tiles, connect resources, then link cities with roads.
/// World previews supply legality, yields and work; queries do not move units or consume RNG.</summary>
internal sealed class WorkerPlanner(AiContext ctx)
{
    private const int CandidateBatchSize = 8, ParallelTiles = 2;
    private readonly HashSet<TileId> _claimed = new();
    private SortedDictionary<int, UnitMission> _missions = new();
    private List<WorkTile>? _tiles;
    private HashSet<TileId>? _roads;
    private readonly Dictionary<string, BuildDef[]> _builds = new(StringComparer.Ordinal);
    private int _buildRevision = -1;
    private sealed record WorkTile(TileId Tile, int Priority);
    private sealed record Job(TileId Tile, string Build, long Value);

    public void Plan()
    {
        ctx.Issue(new PrepareWorkerMissionsCommand { Player = ctx.PlayerId });
        foreach (var worker in ctx.UnitsWithRole(UnitRole.Worker).OrderBy(u => u.Id).ToList())
        {
            ctx.Done.Add(worker.Id);
            if (AiIntent.EligibleUnit(ctx, worker) && worker.MovesLeft > 0) Act(worker);
        }
    }

    private void Act(Unit worker)
    {
        if (ctx.ThreatAround(worker.Position, 2) > 0) { Retreat(worker); return; }
        if (ctx.Service<IWorkerBuildService>().IsWorking(ctx.Sim, worker.Id))
        {
            _claimed.Add(ctx.Tile(worker.Position));
            return;
        }
        var mission = CurrentMission(worker);
        if (mission is null) { Retreat(worker); return; }
        var hex = ctx.Grid.HexOf(mission.Target);
        var before = worker.Position;
        if (before != hex) ctx.MoveToward(worker, hex);
        if (before != hex && worker.Position == before) { Forget(worker); Retreat(worker); return; }
        if (!ctx.Alive(worker)) { Forget(worker); return; }
        if (worker.Position != hex) return;
        ctx.Issue(new StartBuildCommand { Player = ctx.PlayerId, Unit = worker.Id, Build = mission.Build! });
        Forget(worker); // Active builds have their own saved order and no longer need a destination mission.
    }

    private UnitMission? CurrentMission(Unit worker)
    {
        var mission = AiIntent.Read(ctx).Missions.GetValueOrDefault(worker.Id);
        if (mission is null || Valid(worker, mission)) return mission;
        // An earlier worker can spend the build budget. Reassign against the live state before acting.
        ctx.Issue(new PrepareWorkerMissionsCommand { Player = ctx.PlayerId });
        return AiIntent.Read(ctx).Missions.GetValueOrDefault(worker.Id);
    }

    private void Retreat(Unit worker)
    {
        if (ctx.CityAtUnit(worker) is null && ctx.NearestCity(worker.Position) is { } city) ctx.MoveToward(worker, city.Position);
    }

    private void Forget(Unit worker) => ctx.Issue(new ForgetUnitMissionCommand { Player = ctx.PlayerId, Unit = worker.Id });

    // Command validation evaluates a detached intent value. Active builds reserve before destination assignment.
    internal SortedDictionary<int, UnitMission> EvaluateMissions()
    {
        _missions = AiIntent.CleanMissions(ctx);
        var workers = ctx.UnitsWithRole(UnitRole.Worker).OrderBy(u => u.Id).ToArray();
        foreach (var worker in workers.Where(u => AiIntent.EligibleUnit(ctx, u) && Working(u)))
            _claimed.Add(ctx.Tile(worker.Position));
        foreach (var worker in workers) Assign(worker);
        return _missions;
    }

    private bool Working(Unit worker) => ctx.Service<IWorkerBuildService>().IsWorking(ctx.Sim, worker.Id);

    private void Assign(Unit worker)
    {
        var held = _missions.GetValueOrDefault(worker.Id);
        if (held is not null && Valid(worker, held)) { _claimed.Add(held.Target); return; }
        _missions.Remove(worker.Id);
        if (!AiIntent.EligibleUnit(ctx, worker) || worker.MovesLeft <= 0 || Working(worker)) return;
        if (ctx.ThreatAround(worker.Position, 2) > 0) return;
        var job = ChooseJob(worker);
        if (job is null) return;
        _claimed.Add(job.Tile);
        _missions[worker.Id] = AiIntent.Create(ctx, worker, AiMissionKind.Improve, job.Tile, job.Build);
    }

    private bool Valid(Unit worker, UnitMission mission)
    {
        if (!AiIntent.Matches(ctx, worker, mission, AiMissionKind.Improve) || Working(worker)) return false;
        if (ctx.ThreatAround(worker.Position, 2) > 0 || !Available(mission.Target, ctx.Sim.State.PlotAt(worker.Position).AreaId)) return false;
        return LegalBuild(worker, mission) && AiIntent.Reachable(ctx, worker, mission.Target);
    }

    private bool LegalBuild(Unit worker, UnitMission mission)
    {
        var build = AvailableBuilds(worker).FirstOrDefault(b => b.Id == mission.Build);
        if (build is null || !Unfinished(build, mission.Target)) return false;
        if (!ctx.Sim.State.Plots[mission.Target.Value].Revealed.Contains(ctx.Team.Id)) return false;
        bool legalTile = build.RouteType is null ? Workable(mission.Target) : RoadPlot(mission.Target);
        return legalTile && ctx.Service<IWorkerBuildService>().Preview(ctx.Sim, ctx.PlayerId, worker.Id, mission.Target, build.Id).Error is null;
    }

    private Job? ChooseJob(Unit worker)
    {
        var here = ctx.Tile(worker.Position);
        int area = ctx.Sim.State.Plots[here.Value].AreaId;
        var candidates = Tiles().Where(t => Available(t.Tile, area))
            .OrderByDescending(t => t.Priority - 7 * ctx.Grid.Distance(here, t.Tile)).ThenBy(t => t.Tile.Value)
            .ToArray();
        if (candidates.Length == 0) return null;
        // Resolve the cached build list and road set before tiles are previewed on several threads.
        var builds = AvailableBuilds(worker);
        var roads = RoadTiles();
        // Unreachable high-priority jobs must not hide all usable work beyond the first batch.
        foreach (var batch in candidates.Chunk(CandidateBatchSize))
        {
            var jobs = ParallelScoring.Select(ctx.Sim, batch, ParallelTiles, t => BestBuildAt(worker, t.Tile, builds, roads));
            var reachable = jobs.FirstOrDefault(job => job is not null && AiIntent.Reachable(ctx, worker, job.Tile));
            if (reachable is not null) return reachable;
        }
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
        foreach (var road in RoadTiles())
        {
            if (seen.Add(road)) _tiles.Add(new(road, 14));
            else if (ctx.Sim.State.Plots[road.Value].Route is not null)
            {
                int index = _tiles.FindIndex(tile => tile.Tile == road);
                if (index >= 0) _tiles[index] = _tiles[index] with { Priority = Math.Max(14, _tiles[index].Priority) };
            }
        }
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
        if (plot.Improvement is { } id)
            priority += NeedsRepair(plot, id) ? 15 : -18;
        if (plot.BuildProgress.Count > 0) priority += 100; // Finish work before starting another task.
        return priority;
    }

    private bool NeedsRepair(Plot plot, string id)
    {
        if (id is "world:city_ruins" or "world:city_ruins_arcology") return true;
        var improvement = ctx.Sim.Content.Get<ImprovementDef>(id);
        var yields = improvement.YieldChanges;
        return improvement.DepletedMine || yields.Food + yields.Production + yields.Commerce < 0;
    }

    private Job? BestBuildAt(Unit worker, TileId tile, BuildDef[] builds, HashSet<TileId> roads)
    {
        var world = ctx.Service<IWorkerBuildService>();
        YieldVector? before = null;
        Job? best = null;
        foreach (var build in builds)
        {
            if (!CanPreview(build, tile, roads)) continue;
            var preview = world.Preview(ctx.Sim, ctx.PlayerId, worker.Id, tile, build.Id);
            if (preview.Error is not null) continue;
            before ??= ctx.Service<IPlotYieldService>().Calculate(ctx.Sim, ctx.PlayerId, tile).Yields;
            long value = Gain(build, tile, before.Value, preview.Yields);
            if (value <= 0) continue;
            value = value * 1000 / Math.Max(100, preview.RequiredWork);
            if (best is null || value > best.Value) best = new(tile, build.Id, value);
        }
        return best;
    }

    private bool CanPreview(BuildDef build, TileId tile, HashSet<TileId> roads) => Unfinished(build, tile)
        && (build.RouteType is null || roads.Contains(tile));

    private bool Unfinished(BuildDef build, TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        if (build.ImprovementType is { } improvement && plot.Improvement == improvement) return false;
        if (build.Mine && plot.DepletedMine) return false;
        if (build.ObsoleteTech is { } obsolete && ctx.Sim.KnowsTech(ctx.PlayerId, obsolete)) return false;
        if (build.RouteType is not { } route) return true;
        return plot.Route is not { } existing
            || ctx.Sim.Content.Get<RouteDef>(existing).Value < ctx.Sim.Content.Get<RouteDef>(route).Value;
    }

    private BuildDef[] AvailableBuilds(Unit worker)
    {
        // Resolve serially before parallel tile previews. Budget and technology are fixed only in this segment.
        if (_buildRevision != ctx.Revision)
        {
            _builds.Clear();
            _buildRevision = ctx.Revision;
        }
        if (_builds.TryGetValue(worker.Type, out var builds)) return builds;
        var unit = ctx.Def(worker);
        var rc = new RequirementContext(ctx.Sim, ctx.PlayerId);
        long treasury = ctx.Self.Treasury;
        builds = ctx.Catalog.WorkerBuilds.Where(b => unit.Builds.GetValueOrDefault(b.Id) && b.Cost <= treasury
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
        if (VisibleBonus(plot) is { } bonus && ConnectsBonus(improvement, plot, bonus))
            value += 30 + 8 * bonus.Happiness + 6 * bonus.Health;
        return value;
    }

    private bool ConnectsBonus(ImprovementDef candidate, Plot plot, BonusDef bonus)
    {
        bool alreadyConnected = plot.Improvement is { } existingId
            && ctx.Sim.Content.Get<ImprovementDef>(existingId).BonusEffects.GetValueOrDefault(bonus.Id)?.Connects == true;
        return !alreadyConnected && candidate.BonusEffects.GetValueOrDefault(bonus.Id)?.Connects == true;
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
        .MinBy(c => (ctx.Grid.Distance(c.Position, city.Position), c.Id));

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
            if (RoadPlot(tile) && CanBuildRouteHere(tile)) _roads!.Add(tile);
        }
    }

    private bool CanBuildRouteHere(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        if (plot.Route is not { } existing) return true;
        if (HasUnimprovedWorkedLandTile()) return false;
        int currentValue = ctx.Sim.Content.Get<RouteDef>(existing).Value;
        return ctx.UnitsWithRole(UnitRole.Worker).Any(worker => AvailableBuilds(worker).Any(build =>
            build.RouteType is { } route && ctx.Sim.Content.Get<RouteDef>(route).Value > currentValue));
    }

    // Route upgrades wait for work only on tiles a worker can improve; water and blocked land are not blockers.
    private bool HasUnimprovedWorkedLandTile() => ctx.Cities.SelectMany(city => city.Worked)
        .Select(ctx.Grid.TileAt).Where(Workable)
        .Any(tile => ctx.Sim.State.Plots[tile.Value].Improvement is null);
}
