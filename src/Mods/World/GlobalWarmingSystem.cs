using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

/// <summary>Global pollution rolls and stepped environmental damage, after map evolution.</summary>
internal sealed class GlobalWarmingSystem(PlotYieldService yields) : ITurnSystem
{
    public string Id => "world:global_warming";
    public int Order => TurnPhase.Environment;

    public void Run(Simulation sim)
    {
        Int128 attempts = WarmingValue(sim);
        if (attempts <= 0) return;
        Int128 odds = ((Int128)WorldTurnMath.Raw(sim, "GLOBAL_WARMING_PROB", 10) - Defense(sim)) * 100
            / Math.Max(1, sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).VictoryDelayPercent);
        var candidates = sim.State.Grid.Tiles.Where(t => sim.State.CityAt(sim.State.Grid.HexOf(t)) is null).ToArray();
        for (Int128 i = 0; i < attempts; i++)
        {
            // The reference consumes a probability roll even when defense makes warming impossible.
            if (sim.Rng.Next(100) >= odds || candidates.Length == 0) continue;
            Degrade(sim, candidates[sim.Rng.Next(candidates.Length)]);
        }
    }

    private static Int128 WarmingValue(Simulation sim)
    {
        var wellbeing = sim.Content.Services.Get<ICityWellbeingService>().SingleOrDefault();
        if (wellbeing is null) return 0; // No city rules: World must not invent its own health accounting.
        int buildingWeight = WorldTurnMath.Raw(sim, "GLOBAL_WARMING_UNHEALTH_WEIGHT", 10);
        int bonusWeight = WorldTurnMath.Raw(sim, "GLOBAL_WARMING_BONUS_WEIGHT", 5);
        int powerWeight = WorldTurnMath.Raw(sim, "GLOBAL_WARMING_POWER_WEIGHT", 5);
        Int128 points = 0;
        foreach (var city in sim.State.Cities.OrderBy(c => c.Owner).ThenBy(c => c.Id))
        {
            var health = wellbeing.EnvironmentalUnhealth(sim, city.Id);
            points += (Int128)health.Buildings * buildingWeight + (Int128)health.Bonuses * bonusWeight + (Int128)health.Power * powerWeight;
        }
        return points / Math.Max(1, sim.State.Plots.Length);
    }

    private static Int128 Defense(Simulation sim)
    {
        var living = sim.State.Cities.Select(c => c.Owner).Concat(sim.State.Units.Select(u => u.Owner)).ToHashSet();
        var environmentalists = sim.State.Players.Where(p => living.Contains(p.Id)
            && p.Civics.Values.Any(c => sim.Content.Get<S.CivicDef>(c).ExtraHealth != 0)).Select(p => p.Id).ToHashSet();
        int civicBonus = WorldTurnMath.Raw(sim, "TREEHUGGER_DEFENSE_BONUS", 2);
        Int128 points = 0;
        int land = 0;
        foreach (var plot in sim.State.Plots)
        {
            if (!Geography.Water(sim.Content, plot)) land++;
            if (plot.Feature is not { } id) continue;
            int weight = sim.Content.Get<S.FeatureDef>(id).WarmingDefense;
            if (weight <= 0) continue;
            points += (Int128)weight * (plot.Owner is { } owner && environmentalists.Contains(owner) ? civicBonus : 1);
        }
        return points * WorldTurnMath.Raw(sim, "GLOBAL_WARMING_FOREST", 60) / Math.Max(1, land);
    }

    private void Degrade(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        string terrain = plot.Terrain;
        string? feature = plot.Feature;
        if (feature is not null) DegradeFeature(sim, tile, plot);
        else if (!Geography.Water(sim.Content, plot)) DegradeTerrain(sim, tile, plot);
        if (terrain == plot.Terrain && feature == plot.Feature) return;
        CliffRules.SanitizeTile(sim.State, sim.Content, tile);
        if (plot.Feature is { } remaining && !Placement.FeatureValid(sim.State.Grid, sim.State.Plots, sim.Content, tile, sim.Content.Get<S.FeatureDef>(remaining)))
            plot.Feature = null;
        plot.Improvement = null;
        plot.ImprovementTurns = 0;
        plot.BuildProgress.Clear();
        sim.Events.Publish(new GlobalWarmingOccurred(tile, terrain, plot.Terrain, feature, plot.Feature));
    }

    private void DegradeFeature(Simulation sim, TileId tile, Plot plot)
    {
        if (plot.Feature == "world:fallout") return;
        if (plot.Feature == "world:ice") { plot.Feature = null; return; }
        if (plot.Feature == "world:forest" && yields.BestNatureFood(sim, tile) > 1
            && Placement.FeatureValid(sim.State.Grid, sim.State.Plots, sim.Content, tile, sim.Content.Get<S.FeatureDef>("world:jungle")))
        {
            plot.Feature = "world:jungle";
            return;
        }
        string? melted = MeltedTerrain(plot.Terrain);
        if (melted is not null) plot.Terrain = melted;
        else plot.Feature = null;
    }

    private static string? MeltedTerrain(string terrain) => terrain switch
    {
        "world:snow" => "world:tundra",
        "world:tundra" or "world:marsh" => "world:grass",
        _ => null,
    };

    private static void DegradeTerrain(Simulation sim, TileId tile, Plot plot)
    {
        string? melted = MeltedTerrain(plot.Terrain);
        if (melted is not null) { plot.Terrain = melted; return; }
        if (plot.Terrain == "world:grass") { plot.Terrain = "world:plains"; return; }
        if (plot.Terrain == "world:plains") { plot.Terrain = "world:desert"; return; }
        if (plot.Terrain != "world:desert" || plot.Kind != PlotKind.Flat) return;
        if (WorldTurnMath.Raw(sim, "GW_MOD_ENABLED", 1) == 0 || !Geography.Coast(sim.State.Grid, sim.State.Plots, sim.Content, tile)) return;
        plot.Terrain = "world:coast";
        plot.Route = null;
        Geography.AssignAreas(sim.State.Grid, sim.State.Plots, sim.Content);
    }
}
