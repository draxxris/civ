using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

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
        time = time * Math.Max(0, 100 + sim.Content.Get<S.TerrainDef>(plot.Terrain).BuildModifier) / 100;
        time = time * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).BuildPercent / 100;
        time = time * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).BuildPercent / 100;
        return checked((int)Math.Max(1, time));
    }
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
        foreach (var tile in sim.State.Grid.Tiles) Upgrade(sim, tile, worked.Contains(tile));
    }
    private static void Upgrade(Simulation sim, TileId tile, bool worked)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Improvement is not { } id || plot.Owner is null) return;
        var improvement = sim.Content.Get<S.ImprovementDef>(id);
        if (improvement.ImprovementUpgrade is not { } next) return;
        var target = sim.Content.Get<S.ImprovementDef>(next);
        if (target.PrereqTech is { } tech && !sim.KnowsTech(plot.Owner.Value, tech)) return;
        if (!worked && !target.OutsideBorders) return;
        int time = improvement.UpgradeTime * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).ImprovementPercent / 100;
        time = time * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).ImprovementPercent / 100;
        plot.ImprovementTurns++;
        if (plot.ImprovementTurns < Math.Max(1, time)) return;
        plot.Improvement = next;
        plot.ImprovementTurns = 0;
    }
}
