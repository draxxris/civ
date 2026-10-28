using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

public sealed class WorkerOrder
{
    public string? Build { get; set; }
    public TileId Tile { get; set; }
}

internal sealed class WorkerBuildService(IWaterService water) : IWorkerBuildService, IPlayerTurnSystem
{
    private readonly BuildEligibility _eligibility = new(water);
    public string Id => "world:workers";
    public int Order => TurnPhase.Units;
    public string? Validate(Simulation sim, int player, int unit, string build) => _eligibility.Validate(sim, player, unit, build);
    public WorkerBuildPreview Preview(Simulation sim, int player, int unit, TileId tile, string build)
    {
        var error = _eligibility.ValidateAt(sim, player, unit, tile, build);
        if (error is not null) return new(error, default, 0);
        var proposed = BuildResult(sim.State.Plots[tile.Value], sim.Content.Get<S.BuildDef>(build));
        var yields = new PlotYieldService(water).CalculatePlot(sim, player, tile, proposed).Yields;
        return new(null, yields, RequiredWork(sim, tile, build));
    }
    private static Plot BuildResult(Plot plot, S.BuildDef build)
    {
        bool remove = plot.Feature is { } feature && build.FeatureEffects.GetValueOrDefault(feature)?.Remove == true;
        return new Plot
        {
            Terrain = build.TerrainChange ?? plot.Terrain, Kind = plot.Kind, IsLake = plot.IsLake,
            RiverEdges = plot.RiverEdges, Bonus = plot.Bonus, Owner = plot.Owner, AreaId = plot.AreaId,
            Improvement = build.ImprovementType ?? plot.Improvement, Route = build.RouteType ?? plot.Route,
            Feature = build.FeatureChange ?? (remove ? null : plot.Feature),
        };
    }
    public bool IsWorking(Simulation sim, int unitId)
    {
        var unit = sim.State.FindUnit(unitId);
        var order = unit?.Extensions.Get<WorkerOrder>(sim.Content);
        if (unit is null || order?.Build is not { } build) return false;
        return sim.State.Grid.TileAt(unit.Position) == order.Tile
            && _eligibility.Validate(sim, unit.Owner, unit.Id, build, continuing: true) is null;
    }
    public void Start(Simulation sim, int player, int unitId, string buildId)
    {
        var unit = sim.State.UnitById(unitId);
        var build = sim.Content.Get<S.BuildDef>(buildId);
        sim.State.PlayerById(player).Treasury -= build.Cost;
        unit.Extensions.Set(sim.Content, new WorkerOrder { Build = buildId, Tile = sim.State.Grid.TileAt(unit.Position) });
    }
    public int RequiredWork(Simulation sim, TileId tile, string buildId)
    {
        var build = sim.Content.Get<S.BuildDef>(buildId);
        var plot = sim.State.Plots[tile.Value];
        long time = build.Time;
        if (plot.Feature is { } f) time += build.FeatureEffects.GetValueOrDefault(f)?.Time ?? 0;
        if (plot.Kind == PlotKind.Peak)
            time = time * Math.Max(0, 100 + Raw(sim, "PEAK_BUILD_TIME_MODIFIER", 100)) / 100;
        if (build.RouteType is { } route && plot.Route is { } existingRoute
            && Raw(sim, "ROUTES_UPGRADE", 1) != 0
            && sim.Content.Get<S.RouteDef>(route).Value > sim.Content.Get<S.RouteDef>(existingRoute).Value)
        {
            var existingBuild = sim.Content.Get<S.BuildDef>().All.FirstOrDefault(b => b.RouteType == existingRoute);
            if (existingBuild is not null) time = Math.Max(1, time - existingBuild.Time);
        }
        time = time * Math.Max(0, 100 + sim.Content.Get<S.TerrainDef>(plot.Terrain).BuildModifier) / 100;
        time = time * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).BuildPercent / 100;
        time = time * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).BuildPercent / 100;
        return checked((int)Math.Max(1, time));
    }
    private static int Raw(Simulation sim, string key, int fallback) =>
        sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value)
            && int.TryParse(value, out int parsed) ? parsed : fallback;
    public void Run(Simulation sim, int player)
    {
        foreach (var unit in sim.State.Units.Where(u => u.Owner == player).OrderBy(u => u.Id).ToArray()) Work(sim, unit);
    }
    private void Work(Simulation sim, Unit unit)
    {
        var order = unit.Extensions.Get<WorkerOrder>(sim.Content);
        if (order?.Build is not { } id) return;
        if (sim.State.Grid.TileAt(unit.Position) != order.Tile || _eligibility.Validate(sim, unit.Owner, unit.Id, id, continuing: true) is not null)
        { order.Build = null; return; }
        var plot = sim.State.Plots[order.Tile.Value];
        plot.BuildProgress[id] = plot.BuildProgress.GetValueOrDefault(id) + WorkRate(sim, unit);
        unit.MovesLeft = 0;
        if (plot.BuildProgress[id] < RequiredWork(sim, order.Tile, id)) return;
        Complete(sim, unit, order.Tile, sim.Content.Get<S.BuildDef>(id));
        plot.BuildProgress.Remove(id);
        order.Build = null;
    }
    private static int WorkRate(Simulation sim, Unit unit)
    {
        int modifier = sim.TeamOf(unit.Owner).Techs.Sum(t => sim.Content.Get<S.TechDef>(t).WorkerSpeedModifier);
        modifier += (int)ModifierSink.Collect(new(sim, ModifierScope.Player, unit.Owner)).Ledger.Total("world:worker_speed_percent");
        if (sim.State.PlayerById(unit.Owner).Ai is not null) modifier += sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap).AiWorkRateModifier;
        return Math.Max(0, sim.Content.Get<S.UnitDef>(unit.Type).WorkRate * (100 + modifier) / 100);
    }
    private static void Complete(Simulation sim, Unit worker, TileId tile, S.BuildDef build)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Feature is { } feature && build.FeatureEffects.TryGetValue(feature, out var effect) && effect.Remove)
        {
            plot.Feature = null;
            bool gainsProduction = sim.Content.Requirements.Evaluate(effect.Requirements, new(sim, worker.Owner, Plot: tile, Unit: worker)).Success;
            FeatureProduction(sim, worker.Owner, tile, gainsProduction ? effect.Production : 0);
        }
        if (build.ImprovementType is { } improvement) { plot.Improvement = improvement; plot.ImprovementTurns = 0; }
        if (build.RouteType is { } route) plot.Route = route;
        if (build.TerrainChange is { } terrain) { plot.Terrain = terrain; Geography.AssignAreas(sim.State.Grid, sim.State.Plots, sim.Content); }
        if (build.FeatureChange is { } newFeature) plot.Feature = newFeature;
        if (build.Kill) sim.State.RemoveUnit(worker.Id);
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
    }
    private static void FeatureProduction(Simulation sim, int player, TileId tile, int production)
    {
        if (production <= 0) return;
        var city = sim.State.Cities.Where(c => c.Owner == player)
            .OrderBy(c => sim.State.Grid.Distance(tile, sim.State.Grid.TileAt(c.Position))).ThenBy(c => c.Id).FirstOrDefault();
        production = production * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).FeatureProductionPercent / 100;
        int techModifier = sim.TeamOf(player).Techs.Sum(t => sim.Content.Get<S.TechDef>(t).FeatureProductionModifier);
        production = production * Math.Max(0, 100 + techModifier) / 100;
        sim.Events.Publish(new FeatureProduction(player, tile, city?.Id, production));
    }
}

internal sealed class ImprovementTurnSystem : ITurnSystem
{
    public string Id => "world:improvements";
    public int Order => TurnPhase.Map;
    public void Run(Simulation sim)
    {
        var worked = sim.State.Cities.SelectMany(c => c.Worked.Append(c.Position)).Select(sim.State.Grid.TileAt).ToHashSet();
        // Disorder stops productive work, but assigned plots still resist feature overgrowth.
        var productiveWorked = sim.State.Cities.Where(c => !GovernmentStatus.IsDisorder(sim, c))
            .SelectMany(c => c.Worked.Append(c.Position)).Select(sim.State.Grid.TileAt).ToHashSet();
        var features = new FeatureTurnRules(sim, worked);
        var resources = new ResourceTurnRules(sim);
        foreach (var tile in sim.State.Grid.Tiles)
        {
            features.Advance(tile);
            bool productive = productiveWorked.Contains(tile);
            resources.Advance(tile, productive); // Deposit exhaustion remains independent of work.
            Upgrade(sim, tile, assigned: worked.Contains(tile), productive);
        }
    }
    private static void Upgrade(Simulation sim, TileId tile, bool assigned, bool productive)
    {
        // Outside-border targets can grow without assignment, never through a disordered assignment.
        if (assigned && !productive) return;
        var plot = sim.State.Plots[tile.Value];
        if (plot.Improvement is not { } id || plot.Owner is null) return;
        var improvement = sim.Content.Get<S.ImprovementDef>(id);
        if (improvement.ImprovementUpgrade is not { } next) return;
        var target = sim.Content.Get<S.ImprovementDef>(next);
        if (target.PrereqTech is { } tech && !sim.KnowsTech(plot.Owner.Value, tech)) return;
        if (!productive && !target.OutsideBorders) return;
        AdvanceUpgrade(sim, plot, improvement, next);
    }
    private static void AdvanceUpgrade(Simulation sim, Plot plot, S.ImprovementDef improvement, string next)
    {
        int time = improvement.UpgradeTime * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).ImprovementPercent / 100;
        time = time * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).ImprovementPercent / 100;
        int modifier = (int)ModifierSink.Collect(new(sim, ModifierScope.Player, plot.Owner!.Value)).Ledger.Total("world:improvement_upgrade_percent");
        time = time * 100 / Math.Max(1, 100 + modifier);
        plot.ImprovementTurns++;
        if (plot.ImprovementTurns < Math.Max(1, time)) return;
        plot.Improvement = next;
        plot.ImprovementTurns = 0;
    }
}
