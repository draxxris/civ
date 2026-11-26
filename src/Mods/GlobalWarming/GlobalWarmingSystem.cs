using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.GlobalWarming;

/// <summary>Global pollution rolls and stepped environmental damage, after map evolution.</summary>
internal sealed class GlobalWarmingSystem : ITurnSystem
{
    public string Id => "global_warming:warming";
    public int Order => TurnPhase.Environment;
    /// <summary>Weighing the cities and rolling writes nothing: the rolls draw on the random stream, which is not
    /// state a reader sees. A plot that degrades widens first (<see cref="Degrade"/>), so the cities' ledgers the
    /// weighing reads are kept answers until then.</summary>
    public IReadOnlyList<string>? WriteDomains => Nothing;
    private static readonly IReadOnlyList<string> Nothing = [];

    public void Run(Simulation sim)
    {
        Int128 attempts = WarmingValue(sim);
        if (attempts <= 0) return;
        Int128 odds = ((Int128)Raw(sim, "GLOBAL_WARMING_PROB", 10) - Defense(sim)) * 100
            / Math.Max(1, sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).VictoryDelayPercent);
        var candidates = sim.State.Grid.Tiles.Where(t => sim.State.CityAt(sim.State.Grid.HexOf(t)) is null).ToArray();
        var terrain = sim.Rules().TerrainChanges;
        // One pass: areas are rebuilt once after every flood, as nothing a degrade step reads depends on them.
        using var pass = terrain.Begin(sim);
        for (Int128 i = 0; i < attempts; i++)
        {
            // The reference consumes a probability roll even when defense makes warming impossible.
            if (sim.Rng.Next(100) >= odds || candidates.Length == 0) continue;
            Degrade(sim, terrain, pass, candidates[sim.Rng.Next(candidates.Length)]);
        }
    }

    private static int Raw(Simulation sim, string key, int fallback) =>
        sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value)
            && int.TryParse(value, out int parsed) ? parsed : fallback;

    private static bool Water(Simulation sim, string terrain) => sim.Content.Get<S.TerrainDef>(terrain).IsWater;

    private static Int128 WarmingValue(Simulation sim)
    {
        var wellbeing = sim.Rules().CityWellbeing;
        if (wellbeing is null) return 0; // No city rules: there is no health accounting to pollute with.
        int buildingWeight = Raw(sim, "GLOBAL_WARMING_UNHEALTH_WEIGHT", 10);
        int bonusWeight = Raw(sim, "GLOBAL_WARMING_BONUS_WEIGHT", 5);
        int powerWeight = Raw(sim, "GLOBAL_WARMING_POWER_WEIGHT", 5);
        Int128 points = 0;
        // Nothing is written until every city is weighed, so one read span lets the cities share what they derive:
        // their owners' ledgers, building grants and resource networks.
        using var span = sim.OpenReadSpan();
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
            && p.Civics.Values.Any(c => sim.Content.Get<S.CivicDef>(c).Effects.Total(ModifierScope.City, "cities:health") != 0)).Select(p => p.Id).ToHashSet();
        int civicBonus = Raw(sim, "TREEHUGGER_DEFENSE_BONUS", 2);
        Int128 points = 0;
        int land = 0;
        foreach (var plot in sim.State.Plots)
        {
            if (!Water(sim, plot.Terrain)) land++;
            if (plot.Feature is not { } id) continue;
            int weight = (sim.Content.Attachment<FeatureGlobalWarmingDef>(id) ?? FeatureGlobalWarmingDef.None).WarmingDefense;
            if (weight <= 0) continue;
            points += (Int128)weight * (plot.Owner is { } owner && environmentalists.Contains(owner) ? civicBonus : 1);
        }
        return points * Raw(sim, "GLOBAL_WARMING_FOREST", 60) / Math.Max(1, land);
    }

    /// <summary>Degrade one plot by one step. A plot that changes loses its improvement and work in progress; a flooded
    /// one also loses its route. World settles the rest and announces the change.</summary>
    private static void Degrade(Simulation sim, ITerrainChanges terrain, ITerrainChangePass pass, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        var (nextTerrain, nextFeature) = plot.Feature is not null ? DegradeFeature(sim, terrain, tile, plot)
            : !Water(sim, plot.Terrain) ? DegradeTerrain(sim, tile, plot)
            : (plot.Terrain, plot.Feature);
        if (nextTerrain == plot.Terrain && nextFeature == plot.Feature) return;
        // A degraded plot changes its terrain or feature and loses its improvement: beyond any narrow declaration.
        sim.WidenWriteWindow();
        if (Water(sim, nextTerrain) && !Water(sim, plot.Terrain)) plot.Route = null;
        plot.Improvement = null;
        plot.ImprovementTurns = 0;
        plot.BuildProgress.Clear();
        pass.Change(tile, nextTerrain, nextFeature);
    }

    private static (string Terrain, string? Feature) DegradeFeature(Simulation sim, ITerrainChanges terrain, TileId tile, Plot plot)
    {
        if (plot.Feature == "world:fallout") return (plot.Terrain, plot.Feature);
        if (plot.Feature == "world:ice") return (plot.Terrain, null);
        if (plot.Feature == "world:forest" && BestNaturalFood(sim, tile) > 1 && terrain.FeatureFits(sim, tile, "world:jungle"))
            return (plot.Terrain, "world:jungle");
        return MeltedTerrain(plot.Terrain) is { } melted ? (melted, plot.Feature) : (plot.Terrain, null);
    }

    // The best natural food with no team knowledge, not player zero's revealed resources.
    private static long BestNaturalFood(Simulation sim, TileId tile)
    {
        var yields = sim.Rules().PlotYields;
        return Math.Max(yields.CalculateNature(sim, null, tile).Yields.Food, yields.CalculateNature(sim, null, tile, ignoreFeature: true).Yields.Food);
    }

    private static string? MeltedTerrain(string terrain) => terrain switch
    {
        "world:snow" => "world:tundra",
        "world:tundra" or "world:marsh" => "world:grass",
        _ => null,
    };

    private static (string Terrain, string? Feature) DegradeTerrain(Simulation sim, TileId tile, Plot plot)
    {
        if (MeltedTerrain(plot.Terrain) is { } melted) return (melted, plot.Feature);
        if (plot.Terrain == "world:grass") return ("world:plains", plot.Feature);
        if (plot.Terrain == "world:plains") return ("world:desert", plot.Feature);
        if (plot.Terrain != "world:desert" || plot.Kind != PlotKind.Flat) return (plot.Terrain, plot.Feature);
        if (Raw(sim, "GW_MOD_ENABLED", 1) == 0 || !Coastal(sim, tile)) return (plot.Terrain, plot.Feature);
        return ("world:coast", plot.Feature);
    }

    private static bool Coastal(Simulation sim, TileId tile) =>
        sim.State.Grid.Neighbors(tile).Any(n => Water(sim, sim.State.Plots[n.Value].Terrain));
}
