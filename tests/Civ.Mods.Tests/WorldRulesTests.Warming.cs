using System.Collections.Immutable;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed partial class WorldRulesTests
{
    private const string Warming = "warming_probe:";
    private static readonly Lazy<ContentDb> WarmingContentDb = new(() => WarmingContent());
    private static readonly Lazy<ContentDb> SlowWarmingContentDb = new(() => WarmingContent(slow: true));

    [Theory]
    [InlineData("world:snow", null, "world:tundra", null)]
    [InlineData("world:tundra", null, "world:grass", null)]
    [InlineData("world:marsh", null, "world:grass", null)]
    [InlineData("world:grass", null, "world:plains", null)]
    [InlineData("world:plains", null, "world:desert", null)]
    [InlineData("world:grass", "world:forest", "world:grass", "world:jungle")]
    [InlineData("world:plains", "world:forest", "world:plains", null)]
    [InlineData("world:tundra", "world:forest", "world:grass", "world:forest")]
    [InlineData("world:snow", "world:forest", "world:tundra", "world:forest")]
    [InlineData("world:marsh", "world:jungle", "world:grass", "world:jungle")]
    [InlineData("world:grass", "world:jungle", "world:grass", null)]
    [InlineData("world:coast", "world:ice", "world:coast", null)]
    [InlineData("world:snow", "warming_probe:cold_only", "world:tundra", null)]
    public void Warming_degrades_one_step_clears_improvements_and_emits_the_actual_change(
        string terrain, string? feature, string nextTerrain, string? nextFeature)
    {
        var (sim, tile, city) = WarmingScenario();
        city.Buildings.Add(Warming + "factory");
        var plot = sim.State.Plots[tile.Value];
        plot.Terrain = terrain; plot.Feature = feature;
        plot.Improvement = "world:improvement_improvement_farm"; plot.ImprovementTurns = 9;
        plot.Route = "world:route_route_road";
        plot.BuildProgress["world:build_build_farm"] = 17;
        var cities = sim.State.Cities.Select(c => sim.State.PlotAt(c.Position).Terrain).ToArray();
        WarmingTurn(sim);
        Assert.Equal(nextTerrain, plot.Terrain); Assert.Equal(nextFeature, plot.Feature);
        Assert.Null(plot.Improvement); Assert.Equal(0, plot.ImprovementTurns); Assert.Empty(plot.BuildProgress);
        Assert.Equal("world:route_route_road", plot.Route);
        Assert.Equal(new GlobalWarmingOccurred(tile, terrain, nextTerrain, feature, nextFeature), Assert.Single(sim.Events.Published.OfType<GlobalWarmingOccurred>()));
        Assert.Equal(cities, sim.State.Cities.Select(c => sim.State.PlotAt(c.Position).Terrain));
    }

    [Theory]
    [InlineData("world:grass", "world:fallout", PlotKind.Flat)]
    [InlineData("world:coast", null, PlotKind.Flat)]
    [InlineData("world:desert", null, PlotKind.Flat)]
    [InlineData("world:desert", null, PlotKind.Hills)]
    public void Warming_leaves_fallout_water_and_inland_desert_intact(string terrain, string? feature, PlotKind kind)
    {
        var (sim, tile, city) = WarmingScenario(); city.Buildings.Add(Warming + "factory");
        var plot = sim.State.Plots[tile.Value]; plot.Terrain = terrain; plot.Feature = feature; plot.Kind = kind;
        plot.Improvement = Warming + "marker"; plot.ImprovementTurns = 7;
        WarmingTurn(sim);
        Assert.Equal(terrain, plot.Terrain); Assert.Equal(feature, plot.Feature);
        Assert.Equal(Warming + "marker", plot.Improvement); Assert.Equal(7, plot.ImprovementTurns);
        Assert.Empty(sim.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Theory]
    [InlineData(PlotKind.Flat, true)]
    [InlineData(PlotKind.Hills, false)]
    [InlineData(PlotKind.Peak, false)]
    public void Warming_floods_only_flat_coastal_desert_and_rebuilds_water_areas(PlotKind kind, bool floods)
    {
        var (sim, tile, city) = WarmingScenario(); city.Buildings.Add(Warming + "factory");
        var plot = sim.State.Plots[tile.Value]; plot.Terrain = "world:desert"; plot.Kind = kind;
        plot.Route = "world:route_route_road";
        var sea = sim.State.Grid.Neighbors(tile).First(); sim.State.Plots[sea.Value].Terrain = "world:coast";
        WarmingTurn(sim);
        Assert.Equal(floods ? "world:coast" : "world:desert", plot.Terrain);
        Assert.Equal(floods ? null : "world:route_route_road", plot.Route);
        if (floods) Assert.Equal(plot.AreaId, sim.State.Plots[sea.Value].AreaId);
        Assert.Equal(floods ? 1 : 0, sim.Events.Published.OfType<GlobalWarmingOccurred>().Count());
    }

    [Theory]
    [InlineData("factory", 1, 0, 0, 1)]
    [InlineData("bonus_factory", 0, 2, 0, 4)]
    [InlineData("dirty", 0, 0, 2, 6)]
    [InlineData("immune_dirty", 0, 0, 2, 6)]
    [InlineData("bonus_dirty", 0, 1, 2, 8)]
    [InlineData("unfuelled", 0, 0, 0, 0)]
    [InlineData("clean", 0, 0, 0, 0)]
    [InlineData("obsolete", 0, 0, 0, 0)]
    [InlineData("inactive", 0, 0, 0, 0)]
    [InlineData("population", 1, 0, 0, 1)]
    [InlineData("regional", 3, 0, 0, 11)] // Area/global health in five cities, plus local health.
    [InlineData("healthy_factory", 0, 0, 0, 0)]
    [InlineData("healthy_bonus", 0, 2, 0, 4)] // Building-only health does not suppress bonus pollution.
    public void Warming_weights_authoritative_health_sources_not_net_health_or_population(
        string source, long buildings, long bonuses, long power, int attempts)
    {
        var (sim, tile, city) = WarmingScenario();
        city.Buildings.Add(Warming + source);
        city.Population = 1;
        sim.TeamOf(0).Techs.Add("research:agriculture");
        sim.State.Plots[tile.Value].Feature = "world:fallout";
        var health = Service<ICityWellbeingService>(sim).EnvironmentalUnhealth(sim, city.Id);
        Assert.Equal(new CityEnvironmentalUnhealth(buildings, bonuses, power), health);
        var expected = CopyRng(sim);
        for (int i = 0; i < attempts; i++) { expected.Next(100); expected.Next(1); }
        WarmingTurn(sim);
        Assert.Equal(expected.State, sim.Rng.State); // Fallout is immune but still receives the attempts.
        Assert.Empty(sim.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Fact]
    public void Clean_power_in_the_city_or_team_area_removes_dirty_power_pollution()
    {
        foreach (bool regional in new[] { false, true })
        {
            var (sim, _, city) = WarmingScenario();
            city.Buildings.Add(Warming + "dirty");
            (regional ? sim.State.Cities[1] : city).Buildings.Add(Warming + (regional ? "area_clean" : "clean"));
            Assert.Equal(0, Service<ICityWellbeingService>(sim).EnvironmentalUnhealth(sim, city.Id).Power);
            var rng = sim.Rng.State; WarmingTurn(sim); Assert.Equal(rng, sim.Rng.State);
        }
    }

    [Theory]
    [InlineData(null, 0, 3)]
    [InlineData("green", 0, 6)]
    [InlineData("negative_green", 0, 6)]
    [InlineData("green", 1, 3)] // Dead owner: no city or unit, so no civic multiplier.
    [InlineData("green", -1, 3)] // Unowned features still defend the world.
    public void Warming_defense_counts_features_and_living_owner_civics_and_scales_probability_by_speed(string? civic, int owner, int defense)
    {
        var (sim, tile, city) = WarmingScenario(SlowWarmingContentDb.Value);
        city.Buildings.Add(Warming + "factory");
        sim.State.Plots[tile.Value].Feature = Warming + "shield";
        sim.State.Plots[tile.Value].Owner = owner < 0 ? null : owner;
        if (civic is not null) sim.State.PlayerById(owner < 0 ? 0 : owner).Civics["government:civicoption_healthcare"] = Warming + civic;
        int changes = 0;
        for (int i = 0; i < 64; i++)
        {
            var plot = sim.State.Plots[tile.Value]; plot.Terrain = "world:grass"; plot.Feature = Warming + "shield";
            var expected = CopyRng(sim);
            bool changesNow = expected.Next(100) < (100 - defense) * 100 / 200;
            if (changesNow) expected.Next(1);
            int before = sim.Events.Published.Count;
            WarmingTurn(sim);
            Assert.Equal(expected.State, sim.Rng.State);
            Assert.Equal(changesNow ? null : Warming + "shield", plot.Feature);
            Assert.Equal(changesNow ? 1 : 0, sim.Events.Published.Skip(before).OfType<GlobalWarmingOccurred>().Count());
            if (changesNow) changes++;
        }
        Assert.InRange(changes, 1, 63);
    }

    [Fact]
    public void Warming_without_pollution_is_rng_free_and_reload_continues_identically()
    {
        var (sim, _, city) = WarmingScenario();
        var rng = sim.Rng.State; WarmingTurn(sim); Assert.Equal(rng, sim.Rng.State);
        city.Buildings.Add(Warming + "factory");
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        WarmingTurn(sim); WarmingTurn(loaded);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(sim.Events.Published.OfType<GlobalWarmingOccurred>(), loaded.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Fact]
    public void Warming_extreme_feature_and_civic_defense_does_not_overflow_or_skip_the_probability_roll()
    {
        var (sim, tile, city) = WarmingScenario(WarmingContent(extremeDefense: true));
        city.Buildings.Add(Warming + "factory");
        sim.State.Plots[tile.Value].Feature = Warming + "shield";
        sim.State.PlayerById(0).Civics["government:civicoption_healthcare"] = Warming + "green";
        var expected = CopyRng(sim); expected.Next(100);
        WarmingTurn(sim);
        Assert.Equal(expected.State, sim.Rng.State);
        Assert.Equal(Warming + "shield", sim.State.Plots[tile.Value].Feature);
        Assert.Empty(sim.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Fact]
    public void Warming_does_not_flood_when_disabled_and_never_selects_city_tiles()
    {
        var (sim, tile, city) = WarmingScenario(WarmingContent(flood: false));
        city.Buildings.Add(Warming + "factory");
        sim.State.Plots[tile.Value].Terrain = "world:desert";
        sim.State.Plots[sim.State.Grid.Neighbors(tile).First().Value].Terrain = "world:coast";
        WarmingTurn(sim);
        Assert.Equal("world:desert", sim.State.Plots[tile.Value].Terrain);
        Assert.Empty(sim.Events.Published.OfType<GlobalWarmingOccurred>());
        AddCity(sim, tile);
        var expected = CopyRng(sim); expected.Next(100);
        WarmingTurn(sim);
        Assert.Equal(expected.State, sim.Rng.State);
        Assert.Empty(sim.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Fact]
    public void Forest_warming_uses_team_neutral_natural_food_not_revealed_resources()
    {
        var (sim, tile, city) = WarmingScenario(); city.Buildings.Add(Warming + "factory");
        var plot = sim.State.Plots[tile.Value]; plot.Terrain = "world:plains"; plot.Feature = "world:forest";
        plot.Bonus = "world:corn"; // Revealed bonus food must not make this a jungle candidate.
        WarmingTurn(sim);
        Assert.Null(plot.Feature);
        Assert.Equal("world:plains", plot.Terrain);
        Assert.Equal("world:corn", plot.Bonus);
        Assert.Single(sim.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Fact]
    public void Civic_building_health_is_pollution_but_ordinary_civic_unhealth_is_not()
    {
        var (sim, _, city) = WarmingScenario(); city.Buildings.Add(Warming + "clean");
        sim.State.PlayerById(0).Civics["government:civicoption_healthcare"] = Warming + "class_unhealth";
        Assert.Equal(new CityEnvironmentalUnhealth(1, 0, 0), Service<ICityWellbeingService>(sim).EnvironmentalUnhealth(sim, city.Id));
        WarmingTurn(sim);
        Assert.Single(sim.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Fact]
    public void Warming_pollution_below_one_map_wide_attempt_does_not_round_up()
    {
        var (sim, _, city) = WarmingScenario(width: 4);
        city.Buildings.Add(Warming + "factory"); // Six points / eight plots truncates to zero.
        var rng = sim.Rng.State;
        WarmingTurn(sim);
        Assert.Equal(rng, sim.Rng.State);
        Assert.Empty(sim.Events.Published.OfType<GlobalWarmingOccurred>());
    }

    [Fact]
    public void Population_scaled_building_pollution_uses_wide_intermediates_before_division()
    {
        var (sim, _, city) = WarmingScenario();
        city.Population = int.MaxValue;
        city.Buildings.AddRange(new[] { "population_extreme_a", "population_extreme_b", "population_extreme_c" }.Select(id => Warming + id));
        // Three independent -2^31 percent effects, summed before scaling by population.
        const long expected = 138350580488397127;
        Assert.Equal(expected, Service<ICityWellbeingService>(sim).EnvironmentalUnhealth(sim, city.Id).Buildings);
    }

    private static void WarmingTurn(Simulation sim)
    {
        foreach (var system in sim.Content.TurnSystems.All.Where(s => s.Order == TurnPhase.Environment)) system.Run(sim);
    }

    private static (Simulation Sim, TileId Target, City Polluter) WarmingScenario(ContentDb? content = null, int width = 3)
    {
var sim = new Simulation(content ?? WarmingContentDb.Value, new SimulationSettings
        {
            WorldSize = "foundation:worldsize_duel", GameSpeed = Warming + "speed",
            Leaders = ["peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar"],
        });
        GameFactory.NeutralPolitics(sim);
        sim.State.Units.Clear();
sim.State.Map = new(width, 2, false);
        sim.State.Plots = Enumerable.Range(0, width * 2).Select(_ => new Plot { Terrain = "world:grass", Owner = 0 }).ToArray();
        sim.State.RebuildIndexes();
        // Only the last tile is not a city. Damage selection remains a real system boundary, not an injected predicate.
        foreach (var tile in sim.State.Grid.Tiles.Take(width * 2 - 1)) AddCity(sim, tile);
        return (sim, sim.State.Grid.Tiles.Last(), sim.State.Cities[0]);
    }

    private static ContentDb WarmingContent(bool slow = false, bool extremeDefense = false, bool flood = true)
    {
        var mods = new LoadedMods();
        return ModLoader.Build(mods.Sources.Append(new ModSource
        {
            Manifest = new() { Id = "warming_probe", Version = "1", Dependencies = ["cities", "government", "world", "foundation", "research"] },
            Root = mods.Root, Instance = new WarmingProbe(slow, extremeDefense, flood),
        }));
    }

    private sealed class WarmingProbe(bool slow, bool extremeDefense, bool flood) : IMod
    {
        public void Register(IModContext context)
        {
            var original = context.Content<S.RuleConstantsDef>().Get("foundation:rule_constants");
            context.Content<S.RuleConstantsDef>().Override(new()
            {
                Id = original.Id, Values = original.Values with { PowerHealthChange = 0, DirtyPowerHealthChange = -2 },
                Raw = original.Raw.SetItem("GLOBAL_WARMING_UNHEALTH_WEIGHT", "6").SetItem("GLOBAL_WARMING_BONUS_WEIGHT", "12")
                    .SetItem("GLOBAL_WARMING_POWER_WEIGHT", "18").SetItem("GLOBAL_WARMING_PROB", slow ? "100" : "1000")
                    .SetItem("GLOBAL_WARMING_FOREST", extremeDefense ? int.MaxValue.ToString() : "6")
                    .SetItem("TREEHUGGER_DEFENSE_BONUS", extremeDefense ? int.MaxValue.ToString() : "2")
                    .SetItem("GW_MOD_ENABLED", flood ? "1" : "0"),
            });
            context.Content<S.GameSpeedDef>().Register(new() { Id = Warming + "speed", VictoryDelayPercent = slow ? 200 : 100 });
            context.Content<S.FeatureDef>().Register(new() { Id = Warming + "shield", WarmingDefense = extremeDefense ? int.MaxValue : 3,
                Terrains = ImmutableDictionary<string, bool>.Empty.Add("world:grass", true) });
            context.Content<S.FeatureDef>().Register(new() { Id = Warming + "cold_only",
                Terrains = ImmutableDictionary<string, bool>.Empty.Add("world:snow", true) });
            context.Content<S.CivicDef>().Register(new() { Id = Warming + "green", ExtraHealth = 1 });
            context.Content<S.CivicDef>().Register(new() { Id = Warming + "negative_green", ExtraHealth = -1 });
            context.Content<S.CivicDef>().Register(new() { Id = Warming + "class_unhealth", ExtraHealth = -10,
                BuildingHealthChanges = ImmutableDictionary<string, int>.Empty.Add("cities:granary", -1) });
            var buildings = context.Content<S.BuildingDef>();
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "factory", Health = -1 });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "healthy_factory", Health = -1, BuildingOnlyHealthy = true });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "bonus_factory", FreeBonus = Warming + "toxic", FreeBonusCount = 1, BonusHealthChanges = ImmutableDictionary<string, int>.Empty.Add(Warming + "toxic", -1) });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "healthy_bonus", BuildingOnlyHealthy = true, FreeBonus = Warming + "toxic", FreeBonusCount = 1, BonusHealthChanges = ImmutableDictionary<string, int>.Empty.Add(Warming + "toxic", -1) });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "dirty", ProducesPower = true, DirtyPower = true });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "immune_dirty", ProducesPower = true, DirtyPower = true, NoUnhealthyPopulation = true });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "bonus_dirty", PowerBonus = Warming + "toxic", FreeBonus = Warming + "toxic", FreeBonusCount = 1, DirtyPower = true });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "unfuelled", PowerBonus = Warming + "toxic", DirtyPower = true });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "clean", ProducesPower = true });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "area_clean", AreaCleanPower = true });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "obsolete", Health = -99, ObsoleteTech = "research:agriculture" });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "inactive", Health = -99, RequiresActiveCivics = true,
                Requirements = new Requirement.Leaf("civic", Warming + "green") });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "population", HealthPercentPerPopulation = -100 });
            buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + "regional", Health = -1, AreaHealth = -1, GlobalHealth = -1 });
            foreach (string id in new[] { "population_extreme_a", "population_extreme_b", "population_extreme_c" })
                buildings.Register(new() { DefaultBuilding = "cities:granary", Id = Warming + id, HealthPercentPerPopulation = int.MinValue });
            context.Content<S.BonusDef>().Register(new() { Id = Warming + "toxic", Health = -1 });
            context.Content<S.ImprovementDef>().Register(new() { Id = Warming + "marker" });
        }
    }
}
