using Civ.Content;
using Civ.Game;
using Civ.Modding;
using S = Civ.Content.Schema;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class CityRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private Simulation Scenario()
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(39)), Name = "Test" });
        return sim;
    }
    [Fact]
    public void Granary_retains_food_history_and_growth_uses_sequential_era_scaling()
    {
        var sim = Scenario(); var city = sim.State.Cities.Single();
        city.Buildings.Add("cities:granary");
        Assert.True(sim.TryApply(new SetCityAssignmentsCommand { Player = 0, CityId = city.Id }, out _));
        city.FoodStored = 31;
        var result = Service<ICityGrowthService>(sim).Calculate(sim, city.Id);
        Assert.Equal(31, result.Threshold); // (30 + 4) * 110 / 100 * 85 / 100.
        Assert.Equal(3, result.Consumption);
        Assert.Equal(0, result.Difference);
        sim.AdvanceTurn();
        Assert.Equal(2, city.Population);
        Assert.Equal(0, city.FoodStored);
    }
    [Fact]
    public void Angry_citizens_do_not_work_and_unhealthiness_consumes_food()
    {
        var sim = Scenario(); var city = sim.State.Cities.Single(); city.Population = 8;
        var health = Service<ICityWellbeingService>(sim).Calculate(sim, city.Id);
        Assert.Equal(4, health.Angry); Assert.Equal(6, health.FoodPenalty);
        Assert.Equal(8, health.Ledger.Total("cities:happiness_bad"));
        Service<ICityWorkService>(sim).Assign(sim, city.Id);
        Assert.Equal(4, city.Worked.Count);
        city.Worked.Clear(); city.Specialists.Clear();
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        sim.AdvanceTurn();
        Assert.Equal(7, city.Population); Assert.Equal(0, city.FoodStored);
    }
    [Fact]
    public void Governor_and_mid_production_save_are_deterministic()
    {
        var sim = Scenario(); var city = sim.State.Cities.Single();
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:unit", ContentId = "military:warrior" }, out var error), error);
        sim.AdvanceTurn();
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        for (int i = 0; i < 16; i++) { sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash()); }
        Assert.Contains(sim.State.Units, u => u.Type == "military:warrior");
    }
    [Fact]
    public void Requirements_explain_missing_technology_without_mutating_state()
    {
        var sim = Scenario(); var before = sim.ComputeHash();
        var result = Service<ICityProductionService>(sim).Validate(sim, 1, "cities:building", "cities:granary");
        Assert.False(result.Success);
        Assert.Contains(result.Failures, f => f.Explanation.Contains("research:pottery"));
        Assert.Equal(before, sim.ComputeHash());
    }
    [Fact]
    public void Third_ring_unlock_doubles_growth_threshold()
    {
        var sim = Scenario(); var city = sim.State.Cities.Single();
        Assert.Equal(2, Service<ICityWorkService>(sim).Radius(sim, city.Id));
        city.Culture = 24000;
        Assert.Equal(3, Service<ICityWorkService>(sim).Radius(sim, city.Id));
        Assert.Equal(62, Service<ICityGrowthService>(sim).Calculate(sim, city.Id).Threshold);
    }
    [Fact]
    public void Founding_consumes_settler_grants_capital_and_claims_initial_ring()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var position = sim.State.Grid.HexOf(new TileId(39));
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:settler", Position = position });
        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = 1 }, out var error), error);
        Assert.Empty(sim.State.Units);
        var city = Assert.Single(sim.State.Cities);
        Assert.Contains("cities:palace", city.Buildings);
        Assert.Equal(mods.Content.Get<S.CivilizationDef>(sim.State.PlayerById(0).Civilization).CityNames[0], city.Name);
        Assert.Equal(2, sim.State.PlotAt(position).Culture[0]);
        Assert.All(sim.State.Grid.Range(position, 1), h => Assert.Equal(0, sim.State.PlotAt(h).Owner));
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:settler", Position = position.Neighbor(0) });
        Assert.False(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = 2 }, out error));
        Assert.Contains("close", error);
    }
    [Fact]
    public void Single_city_maintenance_has_zero_distance_and_early_count_discount()
    {
        var sim = Scenario();
        var result = Service<ICityMaintenanceService>(sim).Calculate(sim, 1);
        Assert.Equal(0, result.DistanceHundredths);
        Assert.Equal(15, result.CityCountHundredths); // 100 * 18 / 18 * 30% * 100%, then 50%.
        Assert.Equal(15, result.Ledger.Total("cities:maintenance_hundredths"));
    }
    private Simulation ProbeScenario()
    {
        var source = new ModSource { Manifest = new() { Id = "probe", Version = "1", References = ["research", "foundation", "military"] }, Root = mods.Root, Instance = new ProbeMod() };
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(source)));
        GameFactory.NeutralPolitics(sim);
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(39)), Name = "Test" });
        sim.State.Cities[0].Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        return sim;
    }
    [Fact]
    public void Production_modifiers_normalize_overflow_and_wonders_are_globally_unique()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Culture = 1;
        var service = Service<ICityProductionService>(sim);
        var result = service.Calculate(sim, 1, "cities:building", "probe:wonder");
        Assert.Equal(7, result.Production); // (center 2 + flat 3) * 150 / 100.
        Assert.Equal(150, result.Ledger.Total("cities:production_factor_percent"));
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = 1, ContentId = "probe:wonder" }, out var error), error);
        sim.AdvanceTurn();
        Assert.Contains("probe:wonder", city.Buildings);
        Assert.Equal(4, city.Extensions.Get<CityRuleState>(sim.Content)!.Overflow); // (7 - cost 1) * 100 / 150.
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(44)), Culture = 1 });
        var denied = service.Validate(sim, 2, "cities:building", "probe:wonder");
        Assert.Contains(denied.Failures, f => f.Explanation == "WorldLimitReached");
    }
    [Fact]
    public void Raw_commerce_and_direct_commerce_remain_separate_and_queries_are_read_only()
    {
        var sim = ProbeScenario(); var before = sim.ComputeHash();
        var output = Service<ICityYieldService>(sim).Calculate(sim, 1);
        Assert.Equal(new YieldVector(7, 7, 5), output.Yields);
        Assert.Equal(200, output.DirectCommerceHundredths.Culture);
        Assert.Equal(25, output.CommercePercent.Research);
        Assert.Equal(5, output.Ledger.Total("cities:commerce"));
        Assert.Equal(before, sim.ComputeHash());
    }
    [Fact]
    public void Culture_spreads_by_hex_distance_and_preserves_foreign_culture()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Culture = 39;
        var ring = sim.State.Grid.Ring(city.Position, 2).First();
        sim.State.PlotAt(ring).Culture[1] = 10;
        sim.AdvanceTurn();
        Assert.Equal(41, city.Culture);
        Assert.Equal(0, sim.State.PlotAt(ring).Owner);
        Assert.Equal(25, sim.State.PlotAt(ring).Culture[0]); // (level 2 - distance 2 + 1) * 22 + rate 2 + 1.
        Assert.Equal(10, sim.State.PlotAt(ring).Culture[1]);
    }
    [Fact]
    public void Granary_accumulates_kept_food_before_growth_and_avoid_growth_caps_box()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Buildings.Add("cities:granary"); city.FoodStored = 29;
        sim.AdvanceTurn();
        Assert.Equal(2, city.Population); Assert.Equal(6, city.FoodStored);
        Assert.Equal(4, city.Extensions.Get<CityRuleState>(sim.Content)!.FoodKept);
        city.FoodStored = 100;
        Assert.True(sim.TryApply(new SetCityAssignmentsCommand { Player = 0, CityId = 1, AvoidGrowth = true }, out _));
        var threshold = Service<ICityGrowthService>(sim).Calculate(sim, 1).Threshold;
        sim.AdvanceTurn(); Assert.Equal(2, city.Population); Assert.Equal(threshold, city.FoodStored);
    }
    [Fact]
    public void Food_production_suppresses_growth_and_adds_surplus_after_hammer_modifiers()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0];
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:unit", ContentId = "probe:colonist" }, out var error), error);
        var result = Service<ICityProductionService>(sim).Calculate(sim, 1, "cities:unit", "probe:colonist");
        Assert.Equal(11, result.Production); // 5 * 150 / 100 + (7 - 3).
        Assert.Equal(4, result.Ledger.Total("cities:food_production"));
        sim.AdvanceTurn();
        Assert.Equal(0, city.FoodStored); Assert.Equal(1, city.Population);
        Assert.Equal(11, city.ProductionQueue[0].Progress);
    }
    [Fact]
    public void Distance_maintenance_uses_palace_hex_distance_and_city_count_exponent()
    {
        var sim = Scenario(); var capital = sim.State.Cities[0]; capital.Buildings.Add("cities:palace");
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(44)) });
        var result = Service<ICityMaintenanceService>(sim).Calculate(sim, 2);
        Assert.Equal(888, result.DistanceHundredths); // 2500 * 5 * 8/10 * 80/100 * 100/100 / diameter 9.
        Assert.Equal(17, result.CityCountHundredths); // ((1 + sixth-root(5))/2) * 30 * 50/100, truncated.
        Assert.Equal(905, result.TotalHundredths);
    }
    [Fact]
    public void Great_person_pools_survive_saves_and_birth_advances_player_threshold()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Buildings.Add("probe:academy");
        var service = Service<ICityGreatPeopleService>(sim);
        Assert.Equal(320, service.Calculate(sim, 1).Threshold);
        sim.AdvanceTurn();
        Assert.Equal(160, city.Extensions.Get<CityRuleState>(sim.Content)!.GreatPeopleProgress);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Contains(sim.State.Units, u => u.Type == "military:scientist");
        Assert.Equal(480, service.Calculate(sim, 1).Threshold);
        Assert.Empty(city.Extensions.Get<CityRuleState>(sim.Content)!.GreatPeopleWeights);
    }
    [Fact]
    public void Fifty_city_turn_stays_within_interactive_budget()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Map = new(60, 36, true);
        sim.State.Plots = Enumerable.Range(0, 2160).Select(_ => new Plot { Terrain = "world:grass", Owner = 0 }).ToArray();
        sim.State.RebuildIndexes();
        for (int i = 0; i < 50; i++)
            sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Culture = 1, Position = sim.State.Grid.HexOf(sim.State.Grid.TileAt(3 + i % 10 * 6, 3 + i / 10 * 6)) });
        var clock = System.Diagnostics.Stopwatch.StartNew();
        sim.AdvanceTurn(); clock.Stop();
        Assert.Equal(50, sim.State.Cities.Count);
        Assert.True(clock.ElapsedMilliseconds < 1500, $"Fifty-city turn took {clock.ElapsedMilliseconds}ms");
    }

    private sealed class ProbeMod : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:wonder", Name = "Wonder", DefaultBuilding = "probe:wonder", Cost = 1, Limits = new() { World = 1 } });
            context.Content<S.UnitDef>().Register(new() { Id = "probe:colonist", Name = "Colonist", DefaultUnit = "probe:colonist", Cost = 10, FoodProduction = true, CanFoundCity = true });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:academy", Name = "Academy", DefaultBuilding = "probe:academy", Cost = 10, GreatPeopleRateChange = 160, GreatPersonUnit = "military:scientist" });
            context.RegisterService<IModifierSource>("effects", new Effects());
        }
    }
    private sealed class Effects : IModifierSource
    {
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope != ModifierScope.City) return;
            sink.AddYield("probe:flat", new(4, 3, 4)); sink.AddYield("probe:percent", new(0, 50, 0), true);
            sink.AddCommerce("probe:direct", new(0, 0, 2, 0)); sink.AddCommerce("probe:percent", new(0, 25, 0, 0), true);
        }
    }
}
