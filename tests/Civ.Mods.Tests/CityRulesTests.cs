using System.Collections.Immutable;
using Civ.Client;
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
        Assert.Equal(2, city.Extensions.Get<CityRuleState>(sim.Content)!.HighestPopulation);
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
        Assert.Equal(7, city.Population); Assert.True(city.FoodStored > 0); // Starvation refills the lower tier instead of emptying it.
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
        sim.State.Turn = 12;
        var position = sim.State.Grid.HexOf(new TileId(39));
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:settler", Position = position });
        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = 1 }, out var error), error);
        Assert.Empty(sim.State.Units);
        var city = Assert.Single(sim.State.Cities);
        Assert.Contains("cities:palace", city.Buildings);
        Assert.Equal(12, city.Extensions.Get<CityRuleState>(sim.Content)!.TurnAcquired);
        Assert.Equal(city.Population, city.Extensions.Get<CityRuleState>(sim.Content)!.HighestPopulation);
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
    // Only frozen definitions and stateless services are shared; each call creates fresh state and RNG.
    private static readonly Lazy<ContentDb> ProbeContent = new(() => BuildProbeContent());
    private static readonly Lazy<ContentDb> RefundProbeContent = new(() => BuildProbeContent(50));
    private static readonly Lazy<ContentDb> ExtraCultureProbeContent = new(() => BuildProbeContent(extraCultureLevels: 2));
    private static ContentDb BuildProbeContent(int? buildingGoldPercent = null, int extraCultureLevels = 0)
    {
        var loaded = new LoadedMods();
        var constants = loaded.Content.Get<S.RuleConstantsDef>().All.Single();
        var source = new ModSource { Manifest = new() { Id = "probe", Version = "1", References = ["research", "foundation", "military", "cities"] }, Root = loaded.Root, Instance = new ProbeMod(constants, buildingGoldPercent, extraCultureLevels) };
        return ModLoader.Build(loaded.Sources.Append(source));
    }
    private Simulation ProbeScenario(ContentDb? content = null)
    {
        var sim = GameFactory.Scenario(content ?? ProbeContent.Value);
        GameFactory.NeutralPolitics(sim);
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(39)), Name = "Test" });
        sim.State.Cities[0].Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        return sim;
    }
    [Theory]
    [InlineData("cities:unit", "probe:colonist", 150, 120, false)]
    [InlineData("cities:building", "probe:enhanced_building", 200, 90, false)]
    [InlineData("probe:project", "probe:project", 150, 120, false)]
    [InlineData("cities:building", "probe:enhanced_building", 200, 45, true)]
    public void Excess_production_converts_to_gold_after_modifier_normalization(string kind, string contentId, int expectedModifierPercent, long expectedGold, bool reducedGoldPercent)
    {
        var sim = ProbeScenario(reducedGoldPercent ? RefundProbeContent.Value : ProbeContent.Value); var city = sim.State.Cities[0];

        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = kind, ContentId = contentId }, out var error), error);
        var service = Service<ICityProductionService>(sim);
        var result = service.Calculate(sim, city.Id, kind, contentId);
        Assert.Equal(expectedModifierPercent, result.ModifierPercent);
        int basePercent = Math.Max(0, 100 + (int)Service<ICityYieldService>(sim).Calculate(sim, city.Id).Ledger.Total("yield.percent.production"));
        Assert.Equal(150, basePercent);
        long cap = Math.Max(result.Cost, result.Production);
        const long excessBeyondCap = 120;
        city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = result.Cost + cap + excessBeyondCap - result.Production };
        RunCityPhase(sim, "production");
        long treasury = sim.State.PlayerById(city.Owner).Treasury;

        // Completion alone must award the gold; no economy settlement runs in this interval.
        RunCityPhase(sim, "completion");

        Assert.Empty(city.ProductionQueue);
        Assert.Equal(cap * 100 / expectedModifierPercent, city.Extensions.Get<CityRuleState>(sim.Content)!.Overflow);
        Assert.Equal(expectedGold, sim.State.PlayerById(city.Owner).Treasury - treasury);
    }

    [Fact]
    public void Dropping_an_invalid_building_refunds_only_its_configured_gold_percent()
    {
        var sim = ProbeScenario(RefundProbeContent.Value); var city = sim.State.Cities[0];
        var control = ProbeScenario(RefundProbeContent.Value); var controlCity = control.State.Cities[0];
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:building", ContentId = "probe:obsolete_building" }, out var error), error);
        city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = 100 };
        sim.State.TeamById(0).Techs.Add("research:pottery");
        control.State.TeamById(0).Techs.Add("research:pottery");
        long treasury = sim.State.PlayerById(city.Owner).Treasury;
        long controlTreasury = control.State.PlayerById(controlCity.Owner).Treasury;

        sim.AdvanceTurn();
        control.AdvanceTurn();

        Assert.Empty(city.ProductionQueue);
        long refund = sim.State.PlayerById(city.Owner).Treasury - treasury;
        long passiveIncome = control.State.PlayerById(controlCity.Owner).Treasury - controlTreasury;
        Assert.Equal(50, refund - passiveIncome);
    }

    [Fact]
    public void Production_modifiers_normalize_overflow_and_wonders_are_globally_unique()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Culture = 1;
        var service = Service<ICityProductionService>(sim);
        var result = service.Calculate(sim, 1, "cities:building", "probe:wonder");
        Assert.Equal(7, result.Production); // (center 2 + flat 3) * 150 / 100.
        Assert.Equal(150, result.Ledger.Total("cities:production_factor_percent"));
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:building", ContentId = "probe:wonder" }, out var error), error);
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
    private static void PrepareCultureMap(Simulation sim, bool realistic = false)
    {
        sim.Settings.GameOptions["foundation:gameoption_realistic_culture_spread"] = realistic;
        foreach (var plot in sim.State.Plots)
        {
            plot.Kind = PlotKind.Peak;
            plot.Owner = null;
            plot.Culture.Clear();
        }
        CulturePlot(sim, 0, 0).Kind = PlotKind.Flat;
    }
    private static Plot CulturePlot(Simulation sim, int q, int r) =>
        sim.State.PlotAt(sim.State.Grid.HexOf(new TileId(39)) + new Hex(q, r));
    private static void OpenCulturePlots(Simulation sim, params Hex[] offsets)
    {
        foreach (var offset in offsets) CulturePlot(sim, offset.Q, offset.R).Kind = PlotKind.Flat;
    }
    private static void SetCultureRadius(Simulation sim, City city, int radius)
    {
        // Both initial tiers have threshold zero, but spread requires positive stored culture.
        city.Culture = Math.Max(1, sim.Content.Get<S.CultureLevelDef>().All
            .Where(l => sim.Content.Requirements.Evaluate(l.Requirements, new(sim, city.Owner, city)).Success)
            .OrderBy(l => l.SpeedThresholds[sim.Settings.GameSpeed]).ThenBy(l => l.Id, StringComparer.Ordinal)
            .ElementAt(radius).SpeedThresholds[sim.Settings.GameSpeed]);
        Assert.Equal(radius, Service<ICityCultureService>(sim).LevelIndex(sim, city.Id));
    }
    private Simulation CultureScenario(int radius, bool realistic = false)
    {
        var sim = ProbeScenario();
        PrepareCultureMap(sim, realistic);
        SetCultureRadius(sim, sim.State.Cities[0], radius);
        return sim;
    }
    private static void BlockCulturePlot(Simulation sim, string barrier)
    {
        var plot = CulturePlot(sim, 1, 0);
        switch (barrier)
        {
            case "peak": plot.Kind = PlotKind.Peak; break;
            case "terrain": plot.Terrain = "probe:culture_barrier"; break;
            case "feature": plot.Feature = "world:ice"; break;
            case "enemy":
                plot.Owner = 1;
                sim.State.SetWar(sim.TeamOf(0).Id, sim.TeamOf(1).Id, true);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(barrier));
        }
    }
    [Theory]
    [InlineData("peak", false)]
    [InlineData("terrain", false)]
    [InlineData("feature", false)]
    [InlineData("enemy", false)]
    [InlineData("peak", true)]
    [InlineData("terrain", true)]
    [InlineData("feature", true)]
    [InlineData("enemy", true)]
    public void Culture_barriers_block_their_tile_and_corridor_but_allow_a_bounded_detour(string barrier, bool realistic)
    {
        var sim = CultureScenario(2, realistic); var city = sim.State.Cities[0];
        OpenCulturePlots(sim, new(1, 0), new(2, 0));
        BlockCulturePlot(sim, barrier);
        var culture = Service<ICityCultureService>(sim);
        culture.AddCulture(sim, city.Id, 0, 1);
        Assert.Equal(0, CulturePlot(sim, 1, 0).Culture.GetValueOrDefault(0));
        Assert.Equal(0, CulturePlot(sim, 2, 0).Culture.GetValueOrDefault(0));
        Assert.Null(CulturePlot(sim, 2, 0).Owner);

        // Both detour tiles lie inside ring 2, but the full path takes three steps.
        OpenCulturePlots(sim, new(1, -1), new(2, -1));
        culture.AddCulture(sim, city.Id, 0, 1);
        Assert.Equal(0, CulturePlot(sim, 2, 0).Culture.GetValueOrDefault(0));
        SetCultureRadius(sim, city, 3);
        culture.AddCulture(sim, city.Id, 0, 1);
        Assert.Equal(23, CulturePlot(sim, 2, 0).Culture[0]);
        Assert.Equal(0, CulturePlot(sim, 2, 0).Owner);
        Assert.Equal(0, CulturePlot(sim, 1, 0).Culture.GetValueOrDefault(0));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Peaceful_foreign_borders_allow_culture_to_spread_onto_and_through_them(bool realistic)
    {
        var sim = CultureScenario(2, realistic); var city = sim.State.Cities[0];
        OpenCulturePlots(sim, new(1, 0), new(2, 0));
        var foreign = P7ModFixture.City(sim, sim.State.Grid.TileAt(city.Position + new Hex(1, 0)).Value, owner: 1);
        sim.State.PlotAt(foreign.Position).Owner = 1;
        Service<ICityCultureService>(sim).AddCulture(sim, city.Id, 0, 1);
        Assert.Equal(45, CulturePlot(sim, 1, 0).Culture[0]);
        Assert.Equal(1, CulturePlot(sim, 1, 0).Owner);
        Assert.Equal(23, CulturePlot(sim, 2, 0).Culture[0]);
        Assert.Equal(0, CulturePlot(sim, 2, 0).Owner);
    }
    [Theory]
    [InlineData(false, "world:desert", "world:forest", true, 1)]
    [InlineData(true, "world:grass", null, false, 1)]
    [InlineData(true, "world:desert", null, false, 3)]
    [InlineData(true, "world:grass", "world:forest", false, 2)]
    [InlineData(true, "world:desert", "world:forest", false, 4)]
    [InlineData(true, "world:desert", "world:flood_plains", false, 1)]
    [InlineData(true, "world:grass", null, true, 2)]
    [InlineData(true, "world:desert", "world:flood_plains", true, 2)]
    [InlineData(true, "world:desert", "world:forest", true, 5)]
    [InlineData(true, "world:grass", "probe:culture_discount", false, 1)]
    public void Realistic_culture_uses_destination_penalties_terrain_exemption_and_river_edges(
        bool realistic, string terrain, string? feature, bool river, int distance)
    {
        var sim = CultureScenario(4, realistic); var city = sim.State.Cities[0];
        OpenCulturePlots(sim, new(1, 0), new(2, 0));
        var plot = CulturePlot(sim, 1, 0); plot.Terrain = terrain; plot.Feature = feature;
        if (river) sim.State.SetRiver(new TileId(39), 0, true);
        Service<ICityCultureService>(sim).AddCulture(sim, city.Id, 0, 1);
        long expected = distance <= 4 ? (5 - distance) * 22 + 1 : 0;
        Assert.Equal(expected, plot.Culture.GetValueOrDefault(0));
        Assert.Equal(distance <= 4 ? (int?)0 : null, plot.Owner);
        long behind = distance < 4 ? (4 - distance) * 22 + 1 : 0;
        Assert.Equal(behind, CulturePlot(sim, 2, 0).Culture.GetValueOrDefault(0));
        Assert.Equal(distance < 4 ? (int?)0 : null, CulturePlot(sim, 2, 0).Owner);
    }
    [Fact]
    public void Realistic_culture_chooses_the_cheaper_longer_path_instead_of_the_direct_terrain_path()
    {
        var sim = CultureScenario(4, realistic: true); var city = sim.State.Cities[0];
        OpenCulturePlots(sim, new(1, 0), new(2, 0), new(1, -1), new(2, -1));
        CulturePlot(sim, 1, 0).Terrain = "world:desert";
        Service<ICityCultureService>(sim).AddCulture(sim, city.Id, 0, 1);
        Assert.Equal(45, CulturePlot(sim, 2, 0).Culture[0]); // Three grass steps, not four weighted steps via desert.
        Assert.Equal(0, CulturePlot(sim, 2, 0).Owner);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unreachable_stored_culture_does_not_qualify_for_plot_ownership(bool realistic)
    {
        var sim = CultureScenario(2, realistic);
        OpenCulturePlots(sim, new(2, 0), new(3, 0));
        var foreign = P7ModFixture.City(sim, sim.State.Grid.TileAt(sim.State.Cities[0].Position + new Hex(3, 0)).Value, owner: 1);
        SetCultureRadius(sim, foreign, 1);
        var target = CulturePlot(sim, 2, 0);
        target.Culture[0] = 1000; target.Owner = 0;
        Service<ICityCultureService>(sim).AddCulture(sim, foreign.Id, 0, 1);
        Assert.Equal(1000, target.Culture[0]);
        Assert.Equal(23, target.Culture[1]);
        Assert.Equal(1, target.Owner);
    }
    [Theory]
    [InlineData("peak", false)]
    [InlineData("terrain", false)]
    [InlineData("feature", false)]
    [InlineData("enemy", false)]
    [InlineData("peak", true)]
    [InlineData("terrain", true)]
    [InlineData("feature", true)]
    [InlineData("enemy", true)]
    public void Founding_respects_culture_barriers_for_the_initial_ring_and_extra_levels(string barrier, bool extraLevels)
    {
        var sim = GameFactory.Scenario(extraLevels ? ExtraCultureProbeContent.Value : ProbeContent.Value);
        GameFactory.NeutralPolitics(sim);
        PrepareCultureMap(sim);
        OpenCulturePlots(sim, new(1, 0), new(2, 0), new(-1, 0));
        BlockCulturePlot(sim, barrier);
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:settler", Position = sim.State.Grid.HexOf(new TileId(39)) });
        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = 1 }, out var error), error);
        var city = Assert.Single(sim.State.Cities);
        Assert.Equal(extraLevels ? 3 : 1, Service<ICityCultureService>(sim).LevelIndex(sim, city.Id));
        Assert.True(CulturePlot(sim, 0, 0).Culture[0] > 0);
        Assert.True(CulturePlot(sim, -1, 0).Culture[0] > 0);
        Assert.Equal(0, CulturePlot(sim, -1, 0).Owner);
        Assert.Equal(0, CulturePlot(sim, 1, 0).Culture.GetValueOrDefault(0));
        Assert.Equal(0, CulturePlot(sim, 2, 0).Culture.GetValueOrDefault(0));
        Assert.Null(CulturePlot(sim, 2, 0).Owner);
    }
    [Theory]
    [InlineData("world:coast", null, false)]
    [InlineData("world:desert", null, false)]
    [InlineData("world:grass", "world:forest", false)]
    [InlineData("world:grass", null, true)]
    [InlineData("world:desert", "world:forest", true)]
    public void Founding_grants_and_claims_the_first_ring_despite_realistic_distance_penalties(string terrain, string? feature, bool river)
    {
        var sim = GameFactory.Scenario(ProbeContent.Value);
        GameFactory.NeutralPolitics(sim);
        PrepareCultureMap(sim, realistic: true);
        OpenCulturePlots(sim, new(1, 0), new(2, 0));
        var adjacent = CulturePlot(sim, 1, 0);
        adjacent.Terrain = terrain;
        adjacent.Feature = feature;
        if (river) sim.State.SetRiver(new TileId(39), 0, true);
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:settler", Position = sim.State.Grid.HexOf(new TileId(39)) });

        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = 1 }, out var error), error);

        Assert.Equal(1, adjacent.Culture[0]);
        Assert.Equal(0, adjacent.Owner);
        Assert.Equal(0, CulturePlot(sim, 2, 0).Culture.GetValueOrDefault(0));
        Assert.Null(CulturePlot(sim, 2, 0).Owner);
    }
    [Fact]
    public void Granary_accumulates_kept_food_before_growth_and_avoid_growth_caps_box()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Buildings.Add("cities:granary"); city.FoodStored = 29;
        sim.AdvanceTurn();
        Assert.Equal(2, city.Population); Assert.Equal(6, city.FoodStored);
        Assert.Equal(0, city.Extensions.Get<CityRuleState>(sim.Content)!.FoodKept); // Reset once the kept food is applied.
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
    public void Draft_costs_population_halves_all_experience_and_preserves_limits_through_save()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Population = 20;
        sim.State.PlayerById(0).Civics["government:civicoption_military"] = "government:conscription1";
        var service = Service<ICityConscriptService>(sim);
        var before = sim.ComputeHash();
        var result = Assert.IsType<ConscriptResult>(service.Calculate(sim, city.Id));
        Assert.Equal("probe:conscript", result.UnitId);
        Assert.Equal(3, result.PopulationCost);
        Assert.Equal(4, result.Experience); // (5 city + 3 unit-scope experience) / 2.
        Assert.Null(service.Validate(sim, 0, city.Id));
        Assert.Equal(before, sim.ComputeHash());
        var session = new GameSession(sim);
        var option = Assert.IsType<ConscriptOption>(session.DescribeCity(city)!.Conscript);
        Assert.Equal("probe:conscript", option.UnitId);
        Assert.Equal(3, option.PopulationCost);
        Assert.Null(option.Blocked);
        Assert.True(session.Conscript(city), session.Message);
        Assert.Equal(17, city.Population);
        var unit = Assert.Single(sim.State.Units);
        Assert.Equal(4, unit.Experience);
        Assert.Equal(0, unit.MovesLeft);
        Assert.Equal(city.Position, unit.Position);
        Assert.True(city.Extensions.Get<CityRuleState>(sim.Content)!.DraftedThisTurn);
        Assert.Equal(1, sim.State.PlayerById(0).Extensions.Get<CityPlayerState>(sim.Content)!.ConscriptCountThisTurn);
        Assert.Equal(result.AngerTurns, city.Extensions.Get<CityRuleState>(sim.Content)!.ConscriptAngerTurns);
        Assert.Contains(Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines, l => l.Source == "cities:conscript" && l.Amount > 0);
        before = sim.ComputeHash();
        Assert.False(sim.TryApply(new ConscriptCityCommand { Player = 0, CityId = city.Id }, out _));
        Assert.Equal(before, sim.ComputeHash());
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(before, loaded.ComputeHash());
        Assert.NotNull(service.Validate(loaded, 0, city.Id));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.False(city.Extensions.Get<CityRuleState>(sim.Content)!.DraftedThisTurn);
        Assert.Equal(0, sim.State.PlayerById(0).Extensions.Get<CityPlayerState>(sim.Content)!.ConscriptCountThisTurn);
        Assert.Equal(result.AngerTurns - 1, city.Extensions.Get<CityRuleState>(sim.Content)!.ConscriptAngerTurns);
    }

    [Fact]
    public void Draft_rejections_are_atomic_and_population_and_nationality_thresholds_are_inclusive()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Population = 20;
        var command = new ConscriptCityCommand { Player = 0, CityId = city.Id };
        void Reject(ConscriptCityCommand request, string expected)
        {
            var before = sim.ComputeHash();
            Assert.False(sim.TryApply(request, out var error));
            Assert.Contains(expected, error);
            Assert.Equal(before, sim.ComputeHash());
        }
        Reject(command, "civic");
        sim.State.PlayerById(0).Civics["government:civicoption_military"] = "government:conscription1";
        Assert.Null(command.Validate(sim));
        Reject(new ConscriptCityCommand { Player = 1, CityId = city.Id }, "Not your city");
        var state = city.Extensions.Get<CityRuleState>(sim.Content)!;
        state.Disorder = true; Reject(command, "disorder"); state.Disorder = false;
        city.Population = 7; Reject(command, "population");
        city.Population = 8; Assert.Null(command.Validate(sim)); // Five minimum plus three drafted citizens.
        var plot = sim.State.PlotAt(city.Position); plot.Culture[0] = 9; plot.Culture[1] = 91;
        Reject(command, "team culture");
        plot.Culture[0] = 10; plot.Culture[1] = 90;
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.Equal(5, city.Population);
    }
    [Fact]
    public void Draft_quota_is_shared_across_cities_and_resets_next_turn()
    {
        var sim = ProbeScenario(); var first = sim.State.Cities[0]; first.Population = 20;
        sim.State.PlayerById(0).Civics["government:civicoption_military"] = "government:junta"; // Two drafts at standard size.
        var cities = new[] { first,
            new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(44)), Population = 20 },
            new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(80)), Population = 20 } };
        foreach (var city in cities.Skip(1)) sim.State.AddCity(city);
        foreach (var city in cities.Take(2))
            Assert.True(sim.TryApply(new ConscriptCityCommand { Player = 0, CityId = city.Id }, out var error), error);
        var third = new ConscriptCityCommand { Player = 0, CityId = cities[2].Id };
        var before = sim.ComputeHash();
        Assert.False(sim.TryApply(third, out var reason)); Assert.Contains("Player draft limit", reason);
        Assert.Equal(before, sim.ComputeHash());
        sim.AdvanceTurn();
        Assert.Null(third.Validate(sim));
        Assert.True(sim.TryApply(third, out reason), reason);
    }
    private static long BuildingProduction(Simulation sim, City city, string building) =>
        Service<ICityYieldService>(sim).Calculate(sim, city.Id).Ledger.Lines
            .Where(l => l.Source == building && l.Key == "yield.flat.production").Sum(l => l.Amount);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Free_buildings_follow_ownership_founding_capture_team_sharing_and_civilization(bool teamShare)
    {
        var sim = ProbeScenario(); var source = sim.State.Cities[0];
        sim.Settings.GameOptions["foundation:gameoption_assimilation"] = false;
        sim.State.Players[0] = new Player { Id = 0, TeamId = 0, Civilization = "probe:civilization" };
        sim.State.Players.Add(new Player { Id = 2, TeamId = 0, Civilization = "probe:plain" });
        sim.State.Teams.Single(t => t.Id == 0).Members.Add(2);
        sim.State.RebuildIndexes();
        var teammate = P7ModFixture.City(sim, 70, owner: 2);
        var foreign = P7ModFixture.City(sim, 44, owner: 1);
        source.Buildings.Add(teamShare ? "probe:shared_source" : "probe:free_source");
        Assert.Equal(7, BuildingProduction(sim, source, "probe:unique_basic"));
        Assert.Equal(teamShare ? 2 : 0, BuildingProduction(sim, teammate, "probe:basic"));
        Assert.Equal(0, BuildingProduction(sim, foreign, "probe:basic"));
        Assert.DoesNotContain("probe:unique_basic", source.Buildings);
        Assert.Contains("Unique Basic", new GameSession(sim).DescribeCity(source)!.Buildings);
        Assert.Contains(Service<ICityProductionService>(sim).Validate(sim, source.Id, "cities:building", "probe:unique_basic").Failures,
            f => f.Explanation == "Building already exists");
        Assert.True(sim.Content.Requirements.Evaluate(new Requirement.Leaf("building-class", "probe:basic"), new(sim, 0, source)).Success);

        var position = sim.State.Grid.Tiles.First(t => sim.State.Cities.All(c => sim.State.Grid.Distance(t, sim.State.Grid.TileAt(c.Position)) > 2));
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:settler", Position = sim.State.Grid.HexOf(position) });
        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = sim.State.Units.Single().Id }, out var error), error);
        var founded = sim.State.Cities.Single(c => c.Position == sim.State.Grid.HexOf(position));
        Assert.Equal(7, BuildingProduction(sim, founded, "probe:unique_basic"));
        sim.State.SetWar(0, 1, true);
        Service<ICityCaptureService>(sim).Capture(sim, foreign.Id, 0);
        var captured = sim.State.CityById(foreign.Id);
        Assert.Equal(7, BuildingProduction(sim, captured, "probe:unique_basic"));
        sim.Settings.GameOptions["foundation:gameoption_assimilation"] = true;
        captured.Extensions.Get<CityRuleState>(sim.Content)!.OriginalCivilization = "probe:plain";
        Assert.Equal(2, BuildingProduction(sim, captured, "probe:basic"));
        captured.Extensions.Get<CityRuleState>(sim.Content)!.OriginalCivilization = "probe:disabled_civilization";
        Assert.Equal(0, BuildingProduction(sim, captured, "probe:basic"));
        Assert.Equal(0, BuildingProduction(sim, captured, "probe:unique_basic"));
        sim.Settings.GameOptions["foundation:gameoption_assimilation"] = false;

        sim.TeamOf(0).Techs.Add("research:writing");
        Assert.Equal(0, BuildingProduction(sim, founded, "probe:unique_basic"));
        sim.TeamOf(0).Techs.Remove("research:writing");
        Assert.Equal(7, BuildingProduction(sim, founded, "probe:unique_basic"));
        source.Culture = 40;
        // Team culture preserves the source building during recapture.
        sim.State.PlotAt(source.Position).Culture.Clear();
        sim.State.PlotAt(source.Position).Culture[1] = 100;
        Service<ICityCaptureService>(sim).Capture(sim, source.Id, 1);
        Assert.Equal(0, BuildingProduction(sim, founded, "probe:unique_basic"));
        Assert.Equal(2, BuildingProduction(sim, sim.State.CityById(source.Id), "probe:basic"));
        Assert.Equal(0, BuildingProduction(sim, teammate, "probe:basic"));
        var before = sim.ComputeHash();
        _ = new GameSession(sim).DescribeCity(founded);
        Assert.Equal(before, sim.ComputeHash());
    }

    [Theory]
    [InlineData("probe:basic", "Basic", "probe:plain")]
    [InlineData("probe:unique_basic", "Unique Basic", "probe:civilization")]
    public void Replacements_preserve_constructed_buildings_suppress_effects_and_reactivate(string lower, string name, string civilization)
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0];
        sim.State.Players[0] = new Player { Id = 0, TeamId = 0, Civilization = civilization };
        sim.State.RebuildIndexes();
        city.Buildings.Add(lower);
        var production = Service<ICityProductionService>(sim);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:building", ContentId = "probe:upgrade" }, out var error), error);
        sim.AdvanceTurn();
        Assert.Contains("probe:upgrade", city.Buildings);
        Assert.Contains(lower, city.Buildings);
        Assert.Equal(0, BuildingProduction(sim, city, lower));
        Assert.Equal(11, BuildingProduction(sim, city, "probe:upgrade"));
        Assert.DoesNotContain(name, new GameSession(sim).DescribeCity(city)!.Buildings);
        Assert.Contains(production.Validate(sim, city.Id, "cities:building", lower).Failures, f => f.Explanation == "Replaced by an upgraded building");
        Assert.False(sim.Content.Requirements.Evaluate(new Requirement.Leaf("building-class", "probe:basic"), new(sim, 0, city)).Success);
        Assert.DoesNotContain(Service<ICityYieldService>(sim).Calculate(sim, city.Id).Ledger.Lines, l => l.Source == lower + ":safe");

        sim.TeamOf(0).Techs.Add("research:writing");
        Assert.True(BuildingProduction(sim, city, lower) > 0);
        Assert.Contains(name, new GameSession(sim).DescribeCity(city)!.Buildings);
        Assert.DoesNotContain(production.Validate(sim, city.Id, "cities:building", lower).Failures, f => f.Explanation == "Replaced by an upgraded building");
        sim.TeamOf(0).Techs.Remove("research:writing");
        Assert.Equal(0, BuildingProduction(sim, city, lower));
        city.Buildings.Remove("probe:upgrade");
        Assert.True(BuildingProduction(sim, city, lower) > 0);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Contains(name, new GameSession(loaded).DescribeCity(loaded.State.CityById(city.Id))!.Buildings);
    }

    [Fact]
    public void Extension_requires_an_active_base_and_loses_effects_when_the_base_is_removed_or_obsolete()
    {
        var sim = ProbeScenario(); var city = sim.State.Cities[0];
        var production = Service<ICityProductionService>(sim);
        Assert.Contains(production.Validate(sim, city.Id, "cities:building", "probe:extension").Failures,
            f => f.Explanation == "Requires active building class: probe:basic");
        city.Buildings.Add("probe:basic");
        Assert.True(production.Validate(sim, city.Id, "cities:building", "probe:extension").Success);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:building", ContentId = "probe:extension" }, out var error), error);
        sim.AdvanceTurn();
        Assert.Equal(13, BuildingProduction(sim, city, "probe:extension"));
        city.Buildings.Remove("probe:basic");
        Assert.Equal(0, BuildingProduction(sim, city, "probe:extension"));
        Assert.DoesNotContain("Extension", new GameSession(sim).DescribeCity(city)!.Buildings);
        Assert.Contains("probe:extension", city.Buildings);
        city.Buildings.Add("probe:free_source"); // A free base also supports an extension.
        Assert.Equal(13, BuildingProduction(sim, city, "probe:extension"));
        sim.TeamOf(0).Techs.Add("research:pottery");
        Assert.Equal(0, BuildingProduction(sim, city, "probe:extension"));
        Assert.Equal(0, BuildingProduction(sim, city, "probe:basic"));
        Assert.Contains(Service<ICityYieldService>(sim).Calculate(sim, city.Id).Ledger.Lines,
            l => l.Source == "probe:basic:safe" && l.Key == "commerce.flat.culture" && l.Amount == 3);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 20)]
    public void Hurry_anger_counts_the_free_instance_and_the_replacement_once_it_takes_over(bool replaced, int turns)
    {
        // The whip-anger mechanics themselves live in ResearchEconomyTests.Whip_anger_length_uses_only_active_buildings…;
        // what only this fixture can prove is that a free building's modifier counts while it is active, and that the
        // replacement which supersedes it counts instead.
        var sim = ProbeScenario(); var city = sim.State.Cities[0]; city.Population = 20;
        city.Buildings.Add("probe:free_source");
        if (replaced) city.Buildings.Add("probe:upgrade");
        sim.State.PlayerById(0).Civics["probe:option"] = "probe:whipping";
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:unit", ContentId = "military:warrior" }, out var error), error);
        Assert.True(sim.TryApply(new HurryCityCommand { Player = 0, CityId = city.Id, HurryId = "cities:hurry_population" }, out error), error);
        Assert.Equal(turns, city.Extensions.Get<EconomyCityState>(sim.Content)!.HurryAngerTurns);
    }

    private static readonly Lazy<ContentDb> PowerContent = new(() => BuildPowerContent());
    private static readonly Lazy<ContentDb> FreePowerContent = new(() => BuildPowerContent(scripted: true));
    private static ContentDb BuildPowerContent(bool scripted = false)
    {
        var loaded = new LoadedMods();
        IEnumerable<ModSource> sources = loaded.Sources;
        if (scripted) sources = sources.Append(ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "ScriptedWonders")));
        var source = new ModSource { Manifest = new() { Id = "grid", Version = "1", References = ["research", "foundation", "cities", "world"] },
            Root = loaded.Root, Instance = new PowerProbeMod() };
        return ModLoader.Build(sources.Append(source));
    }
    private static Simulation PowerScenario(ContentDb? content = null)
    {
        var sim = GameFactory.Scenario(content ?? PowerContent.Value);
        GameFactory.NeutralPolitics(sim);
        var city = P7ModFixture.City(sim, 39);
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        return sim;
    }
    private static void RunCityPhase(Simulation sim, string phase, int player = 0) =>
        ((IPlayerTurnSystem)sim.Content.TurnSystems.Get("cities:" + phase)).Run(sim, player);

    [Theory]
    [InlineData("grid:free_source")]
    [InlineData("cities:pyramid")]
    public void Dynamic_free_power_plants_activate_consumers_until_the_source_is_suppressed_or_lost(string sourceBuilding)
    {
        var sim = PowerScenario(FreePowerContent.Value); var city = sim.State.Cities[0];
        ReplaceCivilization(sim, 0, "grid:grant_civilization");
        var source = P7ModFixture.City(sim, 70);
        var buildings = Service<ICityBuildingService>(sim);
        Assert.Contains(PowerEligibility(sim, city).Failures, f => f.Explanation == "Requires power");
        city.Buildings.Add("grid:consumer");
        Assert.Equal(0, BuildingProduction(sim, city, "grid:consumer"));

        source.Buildings.Add(sourceBuilding);
        sim.Events.Publish(new BuildingCompleted(source.Id, sourceBuilding));
        AssertFreePowerConsumer(sim, city, powered: true);
        Assert.True(PowerEligibility(sim, source).Success);
        source.Buildings.Add("grid:grant_blocker");
        AssertFreePowerConsumer(sim, city, powered: false);
        Assert.Contains(PowerEligibility(sim, source).Failures, f => f.Explanation == "Requires power");
        Assert.Contains(sourceBuilding, source.Buildings);
        source.Buildings.Remove("grid:grant_blocker");
        AssertFreePowerConsumer(sim, city, powered: true);
        source.Buildings.Remove(sourceBuilding);
        AssertFreePowerConsumer(sim, city, powered: false);
        Assert.DoesNotContain("grid:plant", buildings.EffectiveBuildings(sim, city.Id));
        Assert.Contains("grid:consumer", city.Buildings);
    }
    private static void AssertFreePowerConsumer(Simulation sim, City city, bool powered)
    {
        var buildings = Service<ICityBuildingService>(sim);
        Assert.Equal(powered, buildings.IsPowered(sim, city.Id));
        Assert.Equal(powered, buildings.ActiveBuildings(sim, city.Id).Contains("grid:consumer"));
        Assert.Equal(powered ? 13 : 0, BuildingProduction(sim, city, "grid:consumer"));
        Assert.DoesNotContain("grid:plant", city.Buildings);
    }

    [Fact]
    public void Captured_puppet_recovers_builds_autonomously_and_joins_the_empire_power_and_culture_networks()
    {
        var sim = GameFactory.Scenario(PowerContent.Value);
        GameFactory.NeutralPolitics(sim);
        sim.State.Turn = 30;
        var city = P7ModFixture.City(sim, 39, owner: 1, population: 4);
        city.Extensions.Set(sim.Content, new CityRuleState { TurnAcquired = 7, HighestPopulation = 6 });
        city.Buildings.Add("grid:workshop");
        sim.State.PlotAt(city.Position).Culture[1] = 100;
        sim.State.SetWar(0, 1, true);
        city = Capture(sim, city);
        var state = city.Extensions.Get<CityRuleState>(sim.Content)!;
        Assert.Equal(3, city.Population);
        Assert.Equal(6, state.HighestPopulation);
        Assert.True(state.PendingConquestChoice);
        Assert.True(state.IsPuppet);
        Assert.InRange(state.OccupationTurns, 1, 4);

        for (int remaining = state.OccupationTurns; remaining > 0; remaining--)
        {
            Assert.True(GovernmentStatus.IsDisorder(sim, city));
            sim.AdvanceTurn();
            Assert.Equal(remaining - 1, state.OccupationTurns);
            Assert.False(state.PendingConquestChoice);
            Assert.True(state.IsPuppet);
            var order = Assert.Single(city.ProductionQueue);
            Assert.Equal("grid:modern_workshop", order.ContentId);
            Assert.Equal(0, order.Progress);
            Assert.DoesNotContain("grid:modern_workshop", city.Buildings);
        }
        // The last occupied turn still produces nothing. Production resumes on the following turn.
        Assert.False(GovernmentStatus.IsDisorder(sim, city));
        sim.AdvanceTurn();
        Assert.Contains("grid:modern_workshop", city.Buildings);
        Assert.Contains("grid:workshop", city.Buildings);
        Assert.Equal(0, BuildingProduction(sim, city, "grid:workshop"));
        Assert.Equal(11, BuildingProduction(sim, city, "grid:modern_workshop"));
        Assert.True(state.IsPuppet);
        ChooseConquest(sim, city, CityConquestOption.Annex);
        Assert.False(state.IsPuppet);

        var source = P7ModFixture.City(sim, 70);
        Assert.Contains(PowerEligibility(sim, city).Failures, f => f.Explanation == "Requires power");
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = source.Id, Kind = "cities:building", ContentId = "grid:free_source" }, out var error), error);
        sim.AdvanceTurn();
        Assert.Contains("grid:free_source", source.Buildings);
        Assert.True(PowerEligibility(sim, city).Success);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:building", ContentId = "grid:consumer" }, out error), error);
        sim.AdvanceTurn();
        AssertFreePowerConsumer(sim, city, powered: true);
        Assert.Contains("grid:consumer", city.Buildings);
        Assert.Equal(0, BuildingProduction(sim, city, "grid:workshop"));
        AssertConqueredCultureDetour(sim, city);
    }
    private static void AssertConqueredCultureDetour(Simulation sim, City city)
    {
        PrepareCultureMap(sim);
        SetCultureRadius(sim, city, 3);
        OpenCulturePlots(sim, new(1, 0), new(2, 0), new(1, -1), new(2, -1), new(-2, 0));
        CulturePlot(sim, 1, 0).Owner = 1;
        Service<ICityCultureService>(sim).AddCulture(sim, city.Id, 0, 1);
        Assert.Equal(23, CulturePlot(sim, 2, 0).Culture[0]); // Three steps around the hostile tile.
        Assert.Equal(0, CulturePlot(sim, 2, 0).Owner);
        Assert.Equal(0, CulturePlot(sim, 1, 0).Culture.GetValueOrDefault(0));
        Assert.Equal(1, CulturePlot(sim, 1, 0).Owner);
        Assert.Equal(0, CulturePlot(sim, -1, 0).Culture.GetValueOrDefault(0));
        Assert.Equal(0, CulturePlot(sim, -2, 0).Culture.GetValueOrDefault(0));
        Assert.Null(CulturePlot(sim, -2, 0).Owner);
    }

    private static Plot ConnectPowerFuel(Simulation sim)
    {
        sim.TeamOf(0).Techs.UnionWith(["research:mining", "research:bronze_working"]);
        foreach (int tile in new[] { 40, 41, 42 })
        {
            sim.State.Plots[tile].Owner = 0;
            sim.State.Plots[tile].Route = "world:route_route_road";
        }
        var fuel = sim.State.Plots[42];
        fuel.Bonus = "world:copper";
        fuel.Improvement = "world:improvement_improvement_mine";
        return sim.State.Plots[41];
    }
    private static RequirementResult PowerEligibility(Simulation sim, City city, string building = "grid:consumer") =>
        Service<ICityProductionService>(sim).Validate(sim, city.Id, "cities:building", building);

    [Theory]
    [InlineData("grid:plant")]
    [InlineData("grid:fuel_plant")]
    public void Local_power_enables_construction_and_tracks_fuel_connection_and_source_activity(string plant)
    {
        var sim = PowerScenario(); var city = sim.State.Cities[0];
        Assert.False(PowerEligibility(sim, city).Success);
        city.Buildings.Add(plant);
        var bridge = ConnectPowerFuel(sim);
        Assert.Equal(1, Service<IResourceNetwork>(sim).ConnectedCount(sim, 0, city.Id, "world:copper"));
        Assert.True(PowerEligibility(sim, city).Success);
        bridge.Route = null;
        Assert.Equal(0, Service<IResourceNetwork>(sim).ConnectedCount(sim, 0, city.Id, "world:copper"));
        Assert.Equal(plant == "grid:plant", PowerEligibility(sim, city).Success);
        bridge.Route = "world:route_route_road";
        Assert.True(PowerEligibility(sim, city).Success);
        sim.TeamOf(0).Techs.Add("research:writing");
        Assert.False(PowerEligibility(sim, city).Success);
        sim.TeamOf(0).Techs.Remove("research:writing");
        Assert.True(PowerEligibility(sim, city).Success);
        city.Buildings.Add("grid:replacement");
        Assert.False(PowerEligibility(sim, city).Success);
        city.Buildings.Remove("grid:replacement");
        Assert.True(PowerEligibility(sim, city).Success);
    }

    [Fact]
    public void Power_consumers_lose_activity_and_effects_then_reactivate_without_reconstruction()
    {
        var sim = PowerScenario(); var city = sim.State.Cities[0];
        var buildings = Service<ICityBuildingService>(sim);
        city.Buildings.Add("grid:consumer");
        Assert.DoesNotContain("grid:consumer", buildings.ActiveBuildings(sim, city.Id));
        Assert.Equal(0, BuildingProduction(sim, city, "grid:consumer"));
        city.Buildings.Add("grid:plant");
        Assert.Contains("grid:consumer", buildings.ActiveBuildings(sim, city.Id));
        Assert.Equal(13, BuildingProduction(sim, city, "grid:consumer"));
        Assert.Contains(Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines,
            l => l.Source == "grid:consumer" && l.Key == "cities:health_good" && l.Amount == 3);
        city.Buildings.Remove("grid:plant");
        Assert.Contains("grid:consumer", city.Buildings);
        Assert.DoesNotContain("grid:consumer", buildings.ActiveBuildings(sim, city.Id));
        Assert.Equal(0, BuildingProduction(sim, city, "grid:consumer"));
        Assert.DoesNotContain(Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines, l => l.Source == "grid:consumer");
        city.Buildings.Add("grid:plant");
        var before = sim.ComputeHash();
        Assert.Equal(13, BuildingProduction(sim, city, "grid:consumer"));
        Assert.Equal(before, sim.ComputeHash());
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(13, BuildingProduction(loaded, loaded.State.CityById(city.Id), "grid:consumer"));
        Assert.Equal(before, loaded.ComputeHash());
    }

    [Fact]
    public void Power_yield_modifiers_change_all_city_yields_only_while_power_is_available()
    {
        var sim = PowerScenario(); var city = sim.State.Cities[0];
        city.Buildings.Add("grid:factory");
        var yields = Service<ICityYieldService>(sim);
        Assert.Equal(new YieldVector(3, 2, 1), yields.Calculate(sim, city.Id).Yields);
        city.Buildings.Add("grid:plant");
        var powered = yields.Calculate(sim, city.Id);
        Assert.Equal(new YieldVector(6, 4, 2), powered.Yields);
        Assert.Contains(powered.Ledger.Lines, l => l.Source == "grid:factory:power" && l.Key == "yield.percent.production" && l.Amount == 100);
        city.Buildings.Remove("grid:plant");
        var unpowered = yields.Calculate(sim, city.Id);
        Assert.Equal(new YieldVector(3, 2, 1), unpowered.Yields);
        Assert.DoesNotContain(unpowered.Ledger.Lines, l => l.Source == "grid:factory:power");
    }

    [Fact]
    public void Fuel_power_uses_free_resources_and_resource_blocks_but_consumers_cannot_supply_their_own_power()
    {
        var sim = PowerScenario(); var city = sim.State.Cities[0];
        city.Buildings.AddRange(["grid:fuel_plant", "grid:factory", "grid:powered_bonus"]);
        var yields = Service<ICityYieldService>(sim);
        Assert.Equal(new YieldVector(3, 2, 1), yields.Calculate(sim, city.Id).Yields);
        city.Buildings.Add("grid:free_bonus");
        Assert.Equal(new YieldVector(6, 4, 2), yields.Calculate(sim, city.Id).Yields);
        city.Buildings.Add("grid:no_bonus");
        Assert.Equal(new YieldVector(3, 2, 1), yields.Calculate(sim, city.Id).Yields);
        city.Buildings.Remove("grid:no_bonus");
        Assert.Equal(new YieldVector(6, 4, 2), yields.Calculate(sim, city.Id).Yields);
        city.Buildings.Remove("grid:free_bonus");
        Assert.Equal(new YieldVector(3, 2, 1), yields.Calculate(sim, city.Id).Yields);
    }

    [Fact]
    public void Area_clean_power_uses_active_team_sources_in_the_same_area_and_overrides_dirty_power()
    {
        var sim = PowerScenario(); var city = sim.State.Cities[0];
        sim.State.Players.Add(new Player { Id = 2, TeamId = 0, Civilization = sim.State.PlayerById(0).Civilization });
        sim.TeamOf(0).Members.Add(2); sim.State.RebuildIndexes();
        var source = P7ModFixture.City(sim, 70, owner: 2);
        var foreign = P7ModFixture.City(sim, 44, owner: 1);
        var distant = P7ModFixture.City(sim, 80, owner: 0);
        sim.State.PlotAt(city.Position).AreaId = 1;
        sim.State.PlotAt(source.Position).AreaId = 2;
        sim.State.PlotAt(foreign.Position).AreaId = 1;
        sim.State.PlotAt(distant.Position).AreaId = 3;
        source.Buildings.Add("grid:area"); foreign.Buildings.Add("grid:area");
        city.Buildings.Add("grid:factory"); distant.Buildings.Add("grid:factory");
        var yields = Service<ICityYieldService>(sim);
        var wellbeing = Service<ICityWellbeingService>(sim);
        Assert.True(PowerEligibility(sim, city, "grid:no_power").Success);
        Assert.Equal(new YieldVector(3, 2, 1), yields.Calculate(sim, city.Id).Yields);
        city.Buildings.Add("grid:dirty_plant");
        Assert.Equal(6, wellbeing.EnvironmentalUnhealth(sim, city.Id).Power);
        Assert.True(PowerEligibility(sim, city, "grid:no_power").Success);
        sim.State.PlotAt(source.Position).AreaId = 1;
        Assert.Contains(PowerEligibility(sim, city, "grid:no_power").Failures, f => f.Explanation == "Area already has clean power");
        Assert.Equal(2, wellbeing.EnvironmentalUnhealth(sim, city.Id).Power);
        Assert.DoesNotContain(wellbeing.Calculate(sim, city.Id).Ledger.Lines, l => l.Source == "cities:dirty_power");
        Assert.Equal(new YieldVector(6, 4, 2), yields.Calculate(sim, city.Id).Yields);
        city.Buildings.Remove("grid:dirty_plant");
        Assert.True(PowerEligibility(sim, city).Success);
        Assert.False(PowerEligibility(sim, distant).Success);
        Assert.Equal(new YieldVector(3, 2, 1), yields.Calculate(sim, distant.Id).Yields);
        sim.TeamOf(0).Techs.Add("research:writing");
        Assert.True(PowerEligibility(sim, city, "grid:no_power").Success);
        Assert.False(PowerEligibility(sim, city).Success);
        sim.TeamOf(0).Techs.Remove("research:writing");
        source.Buildings.Add("grid:replacement");
        Assert.False(PowerEligibility(sim, city).Success);
        source.Buildings.Remove("grid:replacement");
        Assert.True(PowerEligibility(sim, city).Success);
    }

    [Theory]
    [InlineData("grid:plant", 2, 0)]
    [InlineData("grid:dirty_plant", 6, 4)]
    [InlineData("grid:area", 2, 0)]
    public void Power_reaches_the_health_ledger_as_bad_health_and_dirty_power(string plant, int badHealth, int dirtyHealth)
    {
        // EnvironmentalUnhealth itself is pinned by Warming_weights_authoritative_health_sources… and by
        // Area_clean_power_uses_active_team_sources…; what only this test proves is how a plant's health
        // reaches the wellbeing ledger and the food penalty built on it.
        var sim = PowerScenario(); var city = sim.State.Cities[0]; city.Population = 8;
        var service = Service<ICityWellbeingService>(sim);
        var before = service.Calculate(sim, city.Id);
        city.Buildings.Add(plant);
        var powered = service.Calculate(sim, city.Id);
        Assert.Equal(before.Unhealthy + badHealth, powered.Unhealthy);
        Assert.Equal(before.Healthy, powered.Healthy);
        Assert.Equal(before.FoodPenalty + badHealth, powered.FoodPenalty);
        Assert.Equal(badHealth - dirtyHealth, powered.Ledger.Lines.Where(l => l.Source == "cities:power" && l.Key == "cities:health_bad").Sum(l => l.Amount));
        Assert.Equal(dirtyHealth, powered.Ledger.Lines.Where(l => l.Source == "cities:dirty_power").Sum(l => l.Amount));
        city.Buildings.Add("grid:plant");
        Assert.DoesNotContain(service.Calculate(sim, city.Id).Ledger.Lines, l => l.Source == "cities:dirty_power");
        city.Buildings.Clear();
        Assert.Equal(before.Unhealthy, service.Calculate(sim, city.Id).Unhealthy);
    }

    private static (Simulation Sim, City City) PowerAiScenario(int population, string marker)
    {
        var sim = PowerScenario(); var city = sim.State.Cities[0]; city.Population = population;
        foreach (var plot in sim.State.Plots) plot.Owner = 1;
        sim.State.PlotAt(city.Position).Owner = 0;
        city.Buildings.AddRange(["grid:ai_marker", marker]);
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:worker", Position = city.Position });
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:scout", Position = city.Position });
        return (sim, city);
    }
    private static string PowerAiChoice(Simulation sim, City city)
    {
        city.ProductionQueue.Clear();
        _ = sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
        return Assert.Single(city.ProductionQueue).ContentId;
    }

    [Theory]
    [InlineData(1, "grid:local_marker", "grid:ai_dirty", "grid:ai_factory")]
    [InlineData(8, "grid:local_marker", "grid:ai_clean", "grid:ai_factory")]
    [InlineData(1, "grid:area_marker", "grid:ai_area", "grid:ai_factory")]
    [InlineData(1, "grid:local_marker", "grid:ai_dirty", "grid:ai_powered_factory")]
    public void Ai_values_plants_that_unlock_existing_power_yields_and_prefers_clean_power_at_low_health(int population, string marker, string expected, string factory)
    {
        var (sim, city) = PowerAiScenario(population, marker);
        city.Buildings.Add(factory);
        if (population == 1) city.Buildings.Add("grid:health");
        string choice = PowerAiChoice(sim, city);
        var wellbeing = Service<ICityWellbeingService>(sim).Calculate(sim, city.Id);
        Assert.True(population == 1 ? wellbeing.Healthy - wellbeing.Unhealthy > 1 : wellbeing.Healthy - wellbeing.Unhealthy <= 0);
        long production = Service<ICityYieldService>(sim).Calculate(sim, city.Id).Yields.Production;
        Assert.True(choice == expected, $"Expected {expected}, got {choice}. Production: {production}. Health margin: {wellbeing.Healthy - wellbeing.Unhealthy}.");
    }

    [Fact]
    public void Ai_values_fuel_plants_only_when_the_resource_is_available()
    {
        var (sim, city) = PowerAiScenario(1, "grid:fuel_marker");
        city.Buildings.Add("grid:ai_factory");
        Assert.Equal("grid:ai_alternative", PowerAiChoice(sim, city));
        ConnectPowerFuel(sim);
        Assert.Equal("grid:ai_fuel", PowerAiChoice(sim, city));
    }

    [Fact]
    public void Ai_values_power_yields_on_new_consumers_only_when_the_city_has_power()
    {
        var (sim, city) = PowerAiScenario(1, "grid:yield_marker");
        Assert.Equal("grid:ai_alternative", PowerAiChoice(sim, city));
        city.Buildings.Add("grid:plant");
        Assert.Equal("grid:ai_consumer", PowerAiChoice(sim, city));
    }

    private static readonly Lazy<ContentDb> ConquestContent = new(() => BuildConquestContent());
    private static readonly Lazy<ContentDb> UnscaledConquestContent = new(() => BuildConquestContent(unscaled: true));
    private static readonly Lazy<ContentDb> AlwaysRazeConquestContent = new(() => BuildConquestContent(alwaysRaze: true));
    private static ContentDb BuildConquestContent(bool unscaled = false, bool alwaysRaze = false)
    {
        var loaded = new LoadedMods();
        var source = new ModSource { Manifest = new() { Id = "conquest", Version = "1", Dependencies = ["foundation"], References = ["cities", "foundation", "research"] },
            Root = loaded.Root, Instance = new ConquestProbeMod(loaded.Content.Get<S.RuleConstantsDef>().All.Single(), unscaled, alwaysRaze) };
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string configuration = directory.Parent!.Name;
        while (!File.Exists(Path.Combine(directory.FullName, "Civ.slnx"))) directory = directory.Parent!;
        var corporations = ModLoader.FromDirectory(Path.Combine(directory.FullName, "src", "Mods", "Corporations", "bin", configuration, "net10.0"));
        var diplomacy = ModLoader.FromDirectory(Path.Combine(directory.FullName, "src", "Mods", "Diplomacy", "bin", configuration, "net10.0"));
        return ModLoader.Build(loaded.Sources.Append(diplomacy).Append(corporations).Append(source));
    }
    private static (Simulation Sim, City City) ConquestScenario(ContentDb? content = null)
    {
        var sim = GameFactory.Scenario(content ?? ConquestContent.Value);
        GameFactory.NeutralPolitics(sim);
        sim.Settings.GameOptions["foundation:gameoption_assimilation"] = false;
        sim.State.Turn = 60;
        var city = P7ModFixture.City(sim, 39, owner: 1, population: 8);
        city.Extensions.Set(sim.Content, new CityRuleState { TurnAcquired = 10, HighestPopulation = 8 });
        sim.State.PlotAt(city.Position).Culture[0] = 100;
        sim.State.SetWar(0, 1, true);
        return (sim, city);
    }
    private static DeterministicRng CopyRng(Simulation sim)
    {
        var (a, b, c, d) = sim.Rng.State;
        return DeterministicRng.Restore(a, b, c, d);
    }
    private static City Capture(Simulation sim, City city)
    {
        Service<ICityCaptureService>(sim).Capture(sim, city.Id, 0);
        return sim.State.CityById(city.Id);
    }
    private static void ChooseConquest(Simulation sim, City city, CityConquestOption option)
    {
        Assert.True(sim.TryApply(new ResolveCityConquestCommand { Player = city.Owner, CityId = city.Id, Option = option }, out var error), error);
    }

    private static void AddAiCities(Simulation sim, int count)
    {
        for (int tile = 0; tile < count; tile++) P7ModFixture.City(sim, tile, owner: 0, population: 3);
    }

    private static void MakeAi(Simulation sim, int playerId)
    {
        var player = sim.State.PlayerById(playerId);
        sim.State.Players = sim.State.Players.Select(item => item.Id == playerId
            ? new Player
            {
                Id = item.Id, Name = item.Name, Civilization = item.Civilization, Leader = item.Leader,
                StartingPlot = item.StartingPlot, Ai = "ai:standard", TeamId = item.TeamId, Culture = item.Culture,
                Treasury = item.Treasury, Civics = item.Civics, EndedTurn = item.EndedTurn, Extensions = item.Extensions,
            }
            : item).ToList();
        sim.State.RebuildIndexes();
    }
    [Fact]
    public void Ai_annexes_a_conquest_when_its_city_count_is_below_target()
    {
        var (sim, city) = ConquestScenario();
        MakeAi(sim, 0);
        var captured = Capture(sim, city);
        var state = captured.Extensions.Get<CityRuleState>(sim.Content)!;

        Assert.False(state.IsPuppet);
        Assert.False(state.PendingConquestChoice);
    }

    [Fact]
    public void Ai_keeps_a_conquest_as_a_puppet_when_its_city_count_exceeds_target()
    {
        var (sim, city) = ConquestScenario();
        MakeAi(sim, 0);
        int target = sim.Content.Get<S.WorldSizeDef>(sim.Settings.WorldSize!).TargetNumCities;
        AddAiCities(sim, target);

        var captured = Capture(sim, city);
        var state = captured.Extensions.Get<CityRuleState>(sim.Content)!;

        Assert.True(state.IsPuppet);
        Assert.False(state.PendingConquestChoice);
    }

    [Fact]
    public void Ai_razes_a_small_unprotected_conquest_when_rules_require_it()
    {
        var (sim, city) = ConquestScenario(AlwaysRazeConquestContent.Value);
        MakeAi(sim, 0);
        city.Population = 2;
        city.Extensions.Get<CityRuleState>(sim.Content)!.HighestPopulation = 2;
        AddAiCities(sim, 5);

        Service<ICityCaptureService>(sim).Capture(sim, city.Id, 0);

        Assert.Null(sim.State.FindCity(city.Id));
        Assert.Single(sim.Events.Published.OfType<CityRazed>());
    }

    [Fact]
    public void Ai_does_not_raze_a_small_conquest_with_a_rush_class_bonus_nearby()
    {
        var (sim, city) = ConquestScenario(AlwaysRazeConquestContent.Value);
        MakeAi(sim, 0);
        city.Population = 2;
        city.Extensions.Get<CityRuleState>(sim.Content)!.HighestPopulation = 2;
        sim.State.PlotAt(city.Position).Bonus = "world:iron";
        AddAiCities(sim, 5);

        var captured = Capture(sim, city);

        Assert.NotNull(sim.State.FindCity(city.Id));
        Assert.True(captured.Extensions.Get<CityRuleState>(sim.Content)!.IsPuppet);
        Assert.Empty(sim.Events.Published.OfType<CityRazed>());
    }

    [Fact]
    public void Ai_does_not_raze_a_small_conquest_with_a_world_wonder()
    {
        var (sim, city) = ConquestScenario(AlwaysRazeConquestContent.Value);
        MakeAi(sim, 0);
        city.Population = 2;
        city.Extensions.Get<CityRuleState>(sim.Content)!.HighestPopulation = 2;
        city.Buildings.Add("conquest:wonder");
        AddAiCities(sim, 5);

        var captured = Capture(sim, city);

        Assert.NotNull(sim.State.FindCity(city.Id));
        Assert.True(captured.Extensions.Get<CityRuleState>(sim.Content)!.IsPuppet);
    }

    [Fact]
    public void Ai_annexes_mature_puppet_cities_when_the_empire_economy_is_healthy()
    {
        var sim = GameFactory.Scenario(ConquestContent.Value);
        GameFactory.NeutralPolitics(sim);
        var city = P7ModFixture.City(sim, 39, owner: 0, population: 3);
        city.Extensions.Set(sim.Content, new CityRuleState { IsPuppet = true, OccupationTurns = 0 });
        sim.State.PlayerById(0).Treasury = 10000;
        Assert.True(sim.TryApply(new SetCommerceRatesCommand { Player = 0, Rates = new(100, 0, 0, 0) }, out var error), error);
        Assert.True(Service<IPlayerEconomyService>(sim).Calculate(sim, 0).NetGold >= 0);

        sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();

        Assert.False(city.Extensions.Get<CityRuleState>(sim.Content)!.IsPuppet);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(80)]
    [InlineData(-5)]
    public void Capture_gold_uses_old_population_two_random_draws_and_clamped_acquisition_age(int age)
    {
        var (sim, city) = ConquestScenario();
        city.Extensions.Get<CityRuleState>(sim.Content)!.TurnAcquired = sim.State.Turn - age;
        var rng = CopyRng(sim);
        int expected = (20 + 8 * 10 + rng.Next(50) + rng.Next(50)) * Math.Clamp(age, 0, 50) / 50;
        long treasury = sim.State.PlayerById(0).Treasury;
        var captured = Capture(sim, city);
        var message = Assert.Single(sim.Events.Published.OfType<CityCaptured>());
        Assert.Equal(expected, message.CaptureGold);
        Assert.Equal(expected, sim.State.PlayerById(0).Treasury - treasury);
        Assert.Equal(rng.State, sim.Rng.State);
        Assert.Equal(60, captured.Extensions.Get<CityRuleState>(sim.Content)!.TurnAcquired);
        Assert.Equal(8, captured.Extensions.Get<CityRuleState>(sim.Content)!.HighestPopulation);
        Assert.Equal(7, captured.Population);
    }

    [Fact]
    public void Disabled_capture_random_ranges_and_turn_scaling_award_the_full_configured_amount_without_rng()
    {
        var (sim, city) = ConquestScenario(UnscaledConquestContent.Value);
        city.Extensions.Get<CityRuleState>(sim.Content)!.TurnAcquired = sim.State.Turn;
        var rng = sim.Rng.State;
        long treasury = sim.State.PlayerById(0).Treasury;
        Capture(sim, city);
        Assert.Equal(7 + 8 * 3, Assert.Single(sim.Events.Published.OfType<CityCaptured>()).CaptureGold);
        Assert.Equal(31, sim.State.PlayerById(0).Treasury - treasury);
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Capture_strips_capital_and_never_capture_buildings_but_preserves_the_world_wonder_identity()
    {
        var (sim, city) = ConquestScenario();
        ReplaceCivilization(sim, 0, "conquest:captor");
        city.Buildings.AddRange(["cities:palace", "conquest:never", "conquest:wonder", "conquest:always", "conquest:zero"]);
        sim.State.PlotAt(city.Position).Culture.Clear();
        sim.State.PlotAt(city.Position).Culture[1] = 100;
        var captured = Capture(sim, city);
        Assert.Equal(new[] { "conquest:always", "conquest:wonder" }, captured.Buildings);
    }

    [Theory]
    [InlineData(false, "conquest:captor", "conquest:unique")]
    [InlineData(true, "conquest:captor", "conquest:basic")]
    [InlineData(false, "conquest:disabled", null)]
    public void Capture_maps_building_classes_through_civilization_or_assimilation(bool assimilation, string civilization, string? expected)
    {
        var (sim, city) = ConquestScenario();
        ReplaceCivilization(sim, 1, "conquest:original");
        ReplaceCivilization(sim, 0, civilization);
        sim.Settings.GameOptions["foundation:gameoption_assimilation"] = assimilation;
        city.Buildings.Add("conquest:basic");
        var captured = Capture(sim, city);
        Assert.Equal(expected is null ? Array.Empty<string>() : [expected], captured.Buildings);
    }

    [Theory]
    [InlineData(60, 40, true)]
    [InlineData(50, 50, false)]
    [InlineData(40, 60, false)]
    public void Team_recapture_requires_a_unique_culture_leader_and_bypasses_zero_survival(int teamCulture, int foreignCulture, bool survives)
    {
        var (sim, city) = ConquestScenario();
        AddTeammate(sim);
        var plot = sim.State.PlotAt(city.Position); plot.Culture.Clear();
        plot.Culture[2] = teamCulture; plot.Culture[1] = foreignCulture;
        city.Buildings.Add("conquest:zero");
        Assert.Equal(survives, Capture(sim, city).Buildings.Contains("conquest:zero"));
    }

    [Fact]
    public void Building_survival_rolls_use_target_percentages_in_ordinal_id_order_and_replay_after_save()
    {
        var (sim, city) = ConquestScenario();
        city.Buildings.AddRange(["conquest:roll_b", "conquest:roll_a"]);
        var plot = sim.State.PlotAt(city.Position); plot.Culture.Clear(); plot.Culture[0] = 75; plot.Culture[1] = 100;
        city.Buildings.Add("conquest:peace"); // The occupation modifier cancels the timer after its draw.
        var rng = SurvivalStream(sim);
        var expected = new List<string> { "conquest:peace" };
        if (rng.Next(100) < 40) expected.Add("conquest:roll_a");
        if (rng.Next(100) < 60) expected.Add("conquest:roll_b");
        Assert.Equal(2, expected.Count);
        rng.Next(6);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        loaded.State.CityById(city.Id).Buildings.Reverse();
        var captured = Capture(sim, city);
        Capture(loaded, loaded.State.CityById(city.Id));
        Assert.Equal(expected, captured.Buildings);
        Assert.Equal(rng.State, sim.Rng.State);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }
    private static DeterministicRng SurvivalStream(Simulation sim)
    {
        while (true)
        {
            var rng = CopyRng(sim); rng.Next(50); rng.Next(50);
            var rolls = CopyRngAfterGold(sim);
            if ((rolls.Next(100) < 40) != (rolls.Next(100) < 60)) return rng;
            sim.Rng.NextUInt64();
        }
    }
    private static DeterministicRng CopyRngAfterGold(Simulation sim)
    {
        var rng = CopyRng(sim); rng.Next(50); rng.Next(50); return rng;
    }

    [Theory]
    [InlineData("conquest:world", 0, false)]
    [InlineData("conquest:team", 2, false)]
    [InlineData("conquest:team", 1, true)]
    [InlineData("conquest:player", 0, false)]
    [InlineData("conquest:player", 1, true)]
    [InlineData("conquest:extra", 0, true)]
    [InlineData("conquest:unlimited", 0, true)]
    public void Capture_enforces_building_class_limits_without_counting_the_transferred_source(string building, int otherOwner, bool survives)
    {
        var (sim, city) = ConquestScenario(); AddTeammate(sim);
        city.Buildings.Add(building);
        P7ModFixture.City(sim, 80, owner: otherOwner).Buildings.Add(building);
        Assert.Equal(survives, Capture(sim, city).Buildings.Contains(building));
    }

    [Theory]
    [InlineData("conquest:coast", false, false, false)]
    [InlineData("conquest:coast", true, false, true)]
    [InlineData("conquest:river", false, false, false)]
    [InlineData("conquest:river", false, true, true)]
    [InlineData("conquest:either", false, true, true)]
    [InlineData("conquest:latitude", false, false, false)]
    [InlineData("conquest:always", false, false, true)]
    public void Capture_checks_coastal_river_and_latitude_locations(string building, bool coast, bool river, bool survives)
    {
        var (sim, city) = ConquestScenario(); city.Buildings.Add(building);
        if (coast) sim.State.Plots[40].Terrain = "world:coast";
        if (river) sim.State.SetRiver(new TileId(39), 0, true);
        Assert.Equal(survives, Capture(sim, city).Buildings.Contains(building));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(40, 0, 0)]
    [InlineData(74, 0, 0)]
    [InlineData(75, 0, 0)]
    [InlineData(100, 0, 0)]
    [InlineData(0, -50, 0)]
    [InlineData(0, 0, -50)]
    [InlineData(0, -50, -50)]
    [InlineData(0, 50, 50)]
    public void Occupation_uses_team_culture_transferred_population_and_city_and_player_modifiers(int culture, int localModifier, int playerModifier)
    {
        var (sim, city) = ConquestScenario();
        AddTeammate(sim);
        var plot = sim.State.PlotAt(city.Position); plot.Culture.Clear();
        plot.Culture[2] = culture; plot.Culture[1] = 100 - culture;
        city.Buildings.Add(localModifier switch { -50 => "conquest:quick", 50 => "conquest:slow", _ => "conquest:always" });
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new OccupationProbeState { Percent = playerModifier });
        var rng = CopyRngAfterGold(sim);
        int expected = culture >= 75 ? 0 : (1 + rng.Next(6) * (100 - culture) / 100) * Math.Max(0, 100 + localModifier + playerModifier) / 100;
        var captured = Capture(sim, city);
        var state = captured.Extensions.Get<CityRuleState>(sim.Content)!;
        Assert.Equal(expected, state.OccupationTurns);
        Assert.Equal(rng.State, sim.Rng.State);
        Assert.Equal(expected > 0, GovernmentStatus.IsDisorder(sim, captured));
        if (expected == 0) return;
        Assert.Equal(0, Service<ICityGrowthService>(sim).Calculate(sim, captured.Id).Difference);
        Assert.Equal(0, Service<ICityProductionService>(sim).Calculate(sim, captured.Id, "cities:unit", "military:warrior").Production);
        Assert.Equal(0, Service<ICityMaintenanceService>(sim).Calculate(sim, captured.Id).TotalHundredths);
        ChooseConquest(sim, captured, CityConquestOption.Annex);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(expected - 1, state.OccupationTurns);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        for (int i = 1; i < expected; i++) sim.AdvanceTurn();
        Assert.False(GovernmentStatus.IsDisorder(sim, captured));
        sim.AdvanceTurn(); Assert.Equal(0, state.OccupationTurns);
    }

    [Theory]
    [InlineData(CityConquestOption.Annex)]
    [InlineData(CityConquestOption.Puppet)]
    [InlineData(CityConquestOption.Raze)]
    public void Saved_conquests_preserve_history_and_validate_each_post_load_choice(CityConquestOption option)
    {
        var (original, city) = ConquestScenario();
        city.Extensions.Get<CityRuleState>(original.Content)!.HighestPopulation = 11;
        var plot = original.State.PlotAt(city.Position);
        plot.Culture.Clear(); plot.Culture[1] = 100;
        int occupation = 1 + CopyRngAfterGold(original).Next(6); // Transferred population is seven.
        Capture(original, city);
        var sim = SaveGame.Load(original.Content, SaveGame.Save(original));
        Assert.Equal(original.ComputeHash(), sim.ComputeHash());
        var captured = sim.State.CityById(city.Id);
        var state = captured.Extensions.Get<CityRuleState>(sim.Content)!;
        Assert.Equal(60, state.TurnAcquired);
        Assert.Equal(11, state.HighestPopulation);
        Assert.Equal(occupation, state.OccupationTurns);
        Assert.True(state.IsPuppet); Assert.True(state.PendingConquestChoice);
        var before = sim.ComputeHash();
        Assert.False(sim.TryApply(new ResolveCityConquestCommand { Player = 1, CityId = city.Id, Option = option }, out _));
        Assert.False(sim.TryApply(new ResolveCityConquestCommand { Player = 0, CityId = 999, Option = option }, out _));
        Assert.False(sim.TryApply(new ResolveCityConquestCommand { Player = 0, CityId = city.Id, Option = (CityConquestOption)99 }, out _));
        Assert.Equal(before, sim.ComputeHash());
        ChooseConquest(sim, captured, option);
        ChooseConquest(original, original.State.CityById(city.Id), option);
        Assert.Equal(original.ComputeHash(), sim.ComputeHash());
        Assert.False(state.PendingConquestChoice);
        if (option == CityConquestOption.Raze)
        {
            Assert.Null(sim.State.FindCity(city.Id));
            Assert.Equal(0, Assert.Single(sim.Events.Published.OfType<CityRazed>()).Player);
            return;
        }
        Assert.Equal(option == CityConquestOption.Puppet, state.IsPuppet);
        Assert.NotNull(new ResolveCityConquestCommand { Player = 0, CityId = city.Id, Option = CityConquestOption.Puppet }.Validate(sim));
        if (state.IsPuppet) ChooseConquest(sim, captured, CityConquestOption.Annex);
        Assert.False(state.IsPuppet);
        Assert.NotNull(new ResolveCityConquestCommand { Player = 0, CityId = city.Id, Option = CityConquestOption.Annex }.Validate(sim));
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:unit", ContentId = "military:warrior" }, out var error), error);
        Assert.True(sim.TryApply(new SetCityAssignmentsCommand { Player = 0, CityId = city.Id, Automatic = true }, out error), error);
    }

    [Fact]
    public void Capital_conquest_choice_cannot_raze_the_city()
    {
        var (sim, city) = ConquestScenario(); var captured = Capture(sim, city);
        captured.Buildings.Add("cities:palace");
        var before = sim.ComputeHash();
        Assert.False(sim.TryApply(new ResolveCityConquestCommand { Player = 0, CityId = city.Id, Option = CityConquestOption.Raze }, out var error));
        Assert.Equal("Cannot raze a capital", error);
        Assert.Equal(before, sim.ComputeHash());
        ChooseConquest(sim, captured, CityConquestOption.Annex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Puppets_reject_manual_commands_and_replace_empty_or_invalid_queues_with_eligible_production(bool invalidQueue)
    {
        var (sim, city) = ConquestScenario(); var captured = Capture(sim, city);
        captured.Population = 3;
        var state = captured.Extensions.Get<CityRuleState>(sim.Content)!;
        if (invalidQueue) captured.ProductionQueue.Add(new("cities:building", "conquest:never", 10));
        AssertPuppetCommandDenials(sim, captured);
        state.ManualWork = true; captured.Worked.Clear();
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.False(state.PendingConquestChoice); Assert.True(state.IsPuppet); Assert.False(state.ManualWork);
        Assert.NotEmpty(captured.Worked);
        var order = Assert.Single(captured.ProductionQueue);
        Assert.True(Service<ICityProductionService>(sim).Validate(sim, captured.Id, order.Kind, order.ContentId).Success);
        Assert.True(order.Progress > 0);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        ChooseConquest(sim, captured, CityConquestOption.Annex);
    }
    private static void AssertPuppetCommandDenials(Simulation sim, City city)
    {
        var requests = new (GameCommand Command, string Error)[]
        {
            (new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:unit", ContentId = "military:warrior" }, "Puppet cities manage their own production"),
            (new SetCityProductionCommand { Player = 0, CityId = city.Id, RemoveIndex = 0 }, "Puppet cities manage their own production"),
            (new SetCityAssignmentsCommand { Player = 0, CityId = city.Id }, "Puppet cities manage their own citizens"),
            (new SetCityAssignmentsCommand { Player = 0, CityId = city.Id, Automatic = true }, "Puppet cities manage their own citizens"),
            (new ConscriptCityCommand { Player = 0, CityId = city.Id }, "Puppet cities cannot draft"),
            (new HurryCityCommand { Player = 0, CityId = city.Id, HurryId = "cities:hurry_population" }, "Puppet cities cannot hurry production"),
        };
        foreach (var (command, expected) in requests)
        {
            var before = sim.ComputeHash();
            Assert.False(sim.TryApply(command, out var error)); Assert.Equal(expected, error);
            Assert.Equal(before, sim.ComputeHash());
        }
    }

    [Fact]
    public void Puppet_count_costs_are_halved_locally_and_annex_restores_full_costs()
    {
        var (sim, city) = ConquestScenario(); var captured = Capture(sim, city);
        int divisor = sim.Content.Get<S.WorldSizeDef>(sim.Settings.WorldSize!).NumCitiesUnhappinessDivisor;
        for (int i = 0; i < Math.Max(2, divisor * 4); i++) P7ModFixture.City(sim, i, population: 1);
        ChooseConquest(sim, captured, CityConquestOption.Annex);
        var maintenance = Service<ICityMaintenanceService>(sim); var wellbeing = Service<ICityWellbeingService>(sim);
        var full = maintenance.Calculate(sim, captured.Id);
        long unhappy = CountUnhappy(wellbeing.Calculate(sim, captured.Id));
        Assert.True(full.CityCountHundredths > 0); Assert.True(unhappy > 0);
        captured.Extensions.Get<CityRuleState>(sim.Content)!.IsPuppet = true;
        var puppet = maintenance.Calculate(sim, captured.Id);
        Assert.Equal(full.CityCountHundredths / 2, puppet.CityCountHundredths);
        Assert.Equal(full.DistanceHundredths, puppet.DistanceHundredths);
        Assert.Equal(unhappy / 2, CountUnhappy(wellbeing.Calculate(sim, captured.Id)));
        var other = sim.State.Cities.First(c => c.Id != captured.Id);
        var otherFull = maintenance.Calculate(sim, other.Id);
        ChooseConquest(sim, captured, CityConquestOption.Annex);
        Assert.Equal(full.CityCountHundredths, maintenance.Calculate(sim, captured.Id).CityCountHundredths);
        Assert.Equal(unhappy, CountUnhappy(wellbeing.Calculate(sim, captured.Id)));
        Assert.Equal(otherFull.CityCountHundredths, maintenance.Calculate(sim, other.Id).CityCountHundredths);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capital_relocation_scores_yields_culture_and_specialists(bool raze)
    {
        var (sim, city) = ConquestScenario();
        ReplaceCivilization(sim, 1, "conquest:original");
        city.Buildings.Add("cities:palace");
        var first = P7ModFixture.City(sim, 70, owner: 1, population: 1);
        var best = P7ModFixture.City(sim, 80, owner: 1, population: 1);
        first.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        best.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        best.Culture = 40;
        best.Extensions.Set(sim.Content, new GreatPeopleCityState { SettledSpecialists = new() { ["foundation:specialist_citizen"] = 2 } });
        best.Extensions.Set(sim.Content, new ReligionCityState { Religions = new(sim.Content.Get<S.ReligionDef>().All.Take(2).Select(r => r.Id)) });
        best.Extensions.Set(sim.Content, new CorporationCityState { Corporations = new(sim.Content.Get<S.CorporationDef>().All.Take(2).Select(r => r.Id)) });
        first.Buildings.Add("conquest:benchmark");
        sim.State.PlotAt(first.Position).Culture[1] = 100;
        sim.State.PlotAt(best.Position).Culture[1] = 100;
        if (raze) Service<ICityCaptureService>(sim).Raze(sim, city.Id);
        else Capture(sim, city);
        Assert.Contains("conquest:palace", best.Buildings);
        Assert.DoesNotContain("conquest:palace", first.Buildings);
        best.Buildings.Remove("conquest:palace");
        best.Buildings.Clear(); first.Buildings.Clear(); best.Culture = 0;
        best.Extensions.Set(sim.Content, new GreatPeopleCityState());
        best.Extensions.Set(sim.Content, new ReligionCityState());
        best.Extensions.Set(sim.Content, new CorporationCityState());
        var sacrificial = P7ModFixture.City(sim, 90, owner: 1);
        Service<ICityCaptureService>(sim).Raze(sim, sacrificial.Id);
        Assert.Contains("conquest:palace", first.Buildings);
        Assert.DoesNotContain("conquest:palace", best.Buildings);
    }
    [Fact]
    public void Capital_relocation_breaks_a_score_tie_by_lowest_city_id()
    {
        var (sim, city) = ConquestScenario();
        ReplaceCivilization(sim, 1, "conquest:original");
        city.Buildings.Add("cities:palace");
        // Two identical towns: same terrain, feature and river bits, nobody working, so they score the same and only
        // the city id can decide where the palace goes.
        var sites = sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf)
            .Where(h => sim.State.CityAt(h) is null)
            .Where(h => sim.Content.Get<S.TerrainDef>(sim.State.PlotAt(h).Terrain) is { IsWater: false, Impassable: false })
            .GroupBy(h => { var p = sim.State.PlotAt(h); return (p.Terrain, p.Kind, p.Feature, p.RiverEdges); })
            .First(g => g.Count() >= 2).Take(2).ToArray();
        var lower = AddTieCity(sim, sites[0], 1);
        var higher = AddTieCity(sim, sites[1], 1);
        lower.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        higher.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        var yields = Service<ICityYieldService>(sim);
        Assert.Equal(yields.Calculate(sim, lower.Id).Yields, yields.Calculate(sim, higher.Id).Yields);
        Assert.Equal(sim.State.PlotAt(lower.Position).Culture.GetValueOrDefault(1),
            sim.State.PlotAt(higher.Position).Culture.GetValueOrDefault(1));

        Capture(sim, city);
        Assert.Contains("conquest:palace", lower.Buildings);
        Assert.DoesNotContain("conquest:palace", higher.Buildings);
    }
    private static City AddTieCity(Simulation sim, Hex position, int owner)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = position, Population = 1 };
        sim.State.AddCity(city);
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        return city;
    }
    private static long CountUnhappy(CityWellbeing wellbeing) => wellbeing.Ledger.Lines.Where(l => l.Source == "cities:city_count").Sum(l => l.Amount);
    private static void ReplaceCivilization(Simulation sim, int player, string civilization)
    {
        var old = sim.State.PlayerById(player);
        sim.State.Players[sim.State.Players.IndexOf(old)] = new Player { Id = old.Id, TeamId = old.TeamId, Civilization = civilization };
        sim.State.RebuildIndexes();
    }
    private static void AddTeammate(Simulation sim)
    {
        sim.State.Players.Add(new Player { Id = 2, TeamId = 0, Civilization = sim.State.PlayerById(0).Civilization });
        sim.TeamOf(0).Members.Add(2); sim.State.RebuildIndexes();
    }

    private sealed class ConquestProbeMod(S.RuleConstantsDef constants, bool unscaled, bool alwaysRaze) : IMod
    {
        public void Register(IModContext context)
        {
            if (unscaled || alwaysRaze)
            {
                var raw = constants.Raw;
                if (unscaled) raw = raw.SetItems(new Dictionary<string, string> { ["BASE_CAPTURE_GOLD"] = "7", ["CAPTURE_GOLD_PER_POPULATION"] = "3",
                    ["CAPTURE_GOLD_RAND1"] = "0", ["CAPTURE_GOLD_RAND2"] = "-1", ["CAPTURE_GOLD_MAX_TURNS"] = "0" });
                if (alwaysRaze) raw = raw.SetItem("PLAYER_ALWAYS_RAZES_CITIES", "1");
                context.Content<S.RuleConstantsDef>().Override(new() { Id = constants.Id, Values = constants.Values, Raw = raw });
            }
            var buildings = context.Content<S.BuildingDef>();
            buildings.Register(new() { Id = "conquest:never", DefaultBuilding = "conquest:never", Cost = -1, NeverCapture = true, CaptureSurvivalPercent = 100 });
            buildings.Register(new() { Id = "conquest:always", DefaultBuilding = "conquest:always", Cost = -1, CaptureSurvivalPercent = 100 });
            buildings.Register(new() { Id = "conquest:zero", DefaultBuilding = "conquest:zero", Cost = -1 });
            buildings.Register(new() { Id = "conquest:wonder", DefaultBuilding = "conquest:wonder", Cost = -1, Limits = new() { World = 1 }, CaptureSurvivalPercent = 100 });
            buildings.Register(new() { Id = "conquest:basic", DefaultBuilding = "conquest:basic", Cost = -1, CaptureSurvivalPercent = 0 });
            buildings.Register(new() { Id = "conquest:unique", DefaultBuilding = "conquest:basic", Cost = -1, CaptureSurvivalPercent = 100 });
            buildings.Register(new() { Id = "conquest:roll_a", DefaultBuilding = "conquest:roll_a", Cost = -1, CaptureSurvivalPercent = 40 });
            buildings.Register(new() { Id = "conquest:roll_b", DefaultBuilding = "conquest:roll_b", Cost = -1, CaptureSurvivalPercent = 60 });
            buildings.Register(new() { Id = "conquest:quick", DefaultBuilding = "conquest:quick", Cost = -1, CaptureSurvivalPercent = 100, OccupationTimeModifier = -50 });
            buildings.Register(new() { Id = "conquest:slow", DefaultBuilding = "conquest:slow", Cost = -1, CaptureSurvivalPercent = 100, OccupationTimeModifier = 50 });
            buildings.Register(new() { Id = "conquest:peace", DefaultBuilding = "conquest:peace", Cost = -1, CaptureSurvivalPercent = 100, OccupationTimeModifier = -100 });
            RegisterCaptureLimits(buildings);
            RegisterCaptureLocations(buildings);
            buildings.Register(new() { Id = "conquest:palace", DefaultBuilding = "cities:palace", Capital = true, Cost = -1 });
            buildings.Register(new() { Id = "conquest:benchmark", DefaultBuilding = "conquest:benchmark", Cost = -1, YieldChanges = new(11, 0, 0) });
            context.Content<S.CivilizationDef>().Register(new() { Id = "conquest:original", BuildingOverrides = ImmutableDictionary<string, string?>.Empty.Add("cities:palace", "conquest:palace") });
            context.Content<S.CivilizationDef>().Register(new() { Id = "conquest:captor", BuildingOverrides = ImmutableDictionary<string, string?>.Empty
                .Add("conquest:basic", "conquest:unique").Add("conquest:wonder", "conquest:unique") });
            context.Content<S.CivilizationDef>().Register(new() { Id = "conquest:disabled", BuildingOverrides = ImmutableDictionary<string, string?>.Empty.Add("conquest:basic", null) });
            context.RegisterStateComponent<OccupationProbeState>("occupation_state");
            context.RegisterService<IModifierSource>("occupation", new OccupationModifiers());
        }
        private static void RegisterCaptureLimits(ModRegistry<S.BuildingDef> buildings)
        {
            buildings.Register(new() { Id = "conquest:world", DefaultBuilding = "conquest:world", Cost = -1, CaptureSurvivalPercent = 100, Limits = new() { World = 1 } });
            buildings.Register(new() { Id = "conquest:team", DefaultBuilding = "conquest:team", Cost = -1, CaptureSurvivalPercent = 100, Limits = new() { Team = 1 } });
            buildings.Register(new() { Id = "conquest:player", DefaultBuilding = "conquest:player", Cost = -1, CaptureSurvivalPercent = 100, Limits = new() { Player = 1 } });
            buildings.Register(new() { Id = "conquest:extra", DefaultBuilding = "conquest:extra", Cost = -1, CaptureSurvivalPercent = 100, Limits = new() { Player = 1, ExtraPlayer = 1 } });
            buildings.Register(new() { Id = "conquest:unlimited", DefaultBuilding = "conquest:unlimited", Cost = -1, CaptureSurvivalPercent = 100, Limits = new() { World = 1, Team = 1, Player = 1, NoLimit = true } });
        }
        private static void RegisterCaptureLocations(ModRegistry<S.BuildingDef> buildings)
        {
            buildings.Register(new() { Id = "conquest:coast", DefaultBuilding = "conquest:coast", Cost = -1, CaptureSurvivalPercent = 100, CoastalLocation = true });
            buildings.Register(new() { Id = "conquest:river", DefaultBuilding = "conquest:river", Cost = -1, CaptureSurvivalPercent = 100, RiverLocation = true });
            buildings.Register(new() { Id = "conquest:either", DefaultBuilding = "conquest:either", Cost = -1, CaptureSurvivalPercent = 100, RiverLocation = true, CoastalLocation = true });
            buildings.Register(new() { Id = "conquest:latitude", DefaultBuilding = "conquest:latitude", Cost = -1, CaptureSurvivalPercent = 100, MinLatitude = 60 });
        }
    }
    public sealed class OccupationProbeState
    {
        public int Percent { get; set; }
    }
    private sealed class OccupationModifiers : IModifierSource
    {
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope == ModifierScope.Player)
                sink.Add("conquest:player", "cities:occupation_time_percent", context.Sim.State.PlayerById(context.Player).Extensions.Get<OccupationProbeState>(context.Sim.Content)?.Percent ?? 0);
        }
    }

    private sealed class PowerProbeMod : IMod
    {
        public void Register(IModContext context)
        {
            var buildings = context.Content<S.BuildingDef>();
            buildings.Register(new() { Id = "grid:plant", DefaultBuilding = "grid:plant", Cost = -1, ProducesPower = true, ObsoleteTech = "research:writing" });
            buildings.Register(new() { Id = "grid:dirty_plant", DefaultBuilding = "grid:dirty_plant", Cost = -1, ProducesPower = true, DirtyPower = true });
            buildings.Register(new() { Id = "grid:fuel_plant", DefaultBuilding = "grid:fuel_plant", Cost = -1, PowerBonus = "world:copper", DirtyPower = true, ObsoleteTech = "research:writing" });
            buildings.Register(new() { Id = "grid:area", DefaultBuilding = "grid:area", Cost = -1, AreaCleanPower = true, ObsoleteTech = "research:writing" });
            buildings.Register(new() { Id = "grid:no_power", DefaultBuilding = "grid:no_power", Cost = 1, NoPower = true });
            buildings.Register(new() { Id = "grid:consumer", DefaultBuilding = "grid:consumer", Cost = 1, RequiresPower = true, YieldChanges = new(0, 13, 0), Health = 3 });
            buildings.Register(new() { Id = "grid:factory", DefaultBuilding = "grid:factory", Cost = -1, PowerYieldModifiers = new(100, 100, 100) });
            buildings.Register(new() { Id = "grid:replacement", DefaultBuilding = "grid:replacement", Cost = -1,
                ReplaceBuildings = ImmutableDictionary<string, bool>.Empty.Add("grid:plant", true).Add("grid:fuel_plant", true).Add("grid:area", true) });
            buildings.Register(new() { Id = "grid:free_source", DefaultBuilding = "grid:free_source", Cost = 1,
                FreeBuilding = "grid:plant", ObsoleteTech = "research:writing" });
            buildings.Register(new() { Id = "grid:grant_blocker", DefaultBuilding = "grid:grant_blocker", Cost = -1,
                ReplaceBuildings = ImmutableDictionary<string, bool>.Empty.Add("grid:free_source", true).Add("cities:pyramid", true) });
            buildings.Register(new() { Id = "grid:workshop", DefaultBuilding = "grid:workshop", Cost = -1,
                CaptureSurvivalPercent = 100, YieldChanges = new(0, 2, 0) });
            buildings.Register(new() { Id = "grid:modern_workshop", DefaultBuilding = "grid:modern_workshop", Cost = 1, YieldChanges = new(0, 11, 0),
                Requirements = new Requirement.Leaf("building", "grid:workshop"),
                ReplaceBuildings = ImmutableDictionary<string, bool>.Empty.Add("grid:workshop", true) });
            context.Content<S.CivilizationDef>().Register(new() { Id = "grid:grant_civilization",
                BuildingOverrides = ImmutableDictionary<string, string?>.Empty.Add("cities:granary", "grid:plant") });
            buildings.Register(new() { Id = "grid:free_bonus", DefaultBuilding = "grid:free_bonus", Cost = -1, FreeBonus = "world:copper", FreeBonusCount = 1 });
            buildings.Register(new() { Id = "grid:no_bonus", DefaultBuilding = "grid:no_bonus", Cost = -1, NoBonus = "world:copper" });
            buildings.Register(new() { Id = "grid:powered_bonus", DefaultBuilding = "grid:powered_bonus", Cost = -1, RequiresPower = true, FreeBonus = "world:copper", FreeBonusCount = 1 });
            buildings.Register(new() { Id = "grid:ai_factory", DefaultBuilding = "grid:ai_factory", Cost = -1, PowerYieldModifiers = new(0, 1000, 0) });
            buildings.Register(new() { Id = "grid:ai_powered_factory", DefaultBuilding = "grid:ai_powered_factory", Cost = -1, RequiresPower = true, PowerYieldModifiers = new(0, 1000, 0) });
            buildings.Register(new() { Id = "grid:ai_marker", DefaultBuilding = "grid:ai_marker", Cost = -1 });
            buildings.Register(new() { Id = "grid:local_marker", DefaultBuilding = "grid:local_marker", Cost = -1 });
            buildings.Register(new() { Id = "grid:area_marker", DefaultBuilding = "grid:area_marker", Cost = -1 });
            buildings.Register(new() { Id = "grid:fuel_marker", DefaultBuilding = "grid:fuel_marker", Cost = -1 });
            buildings.Register(new() { Id = "grid:yield_marker", DefaultBuilding = "grid:yield_marker", Cost = -1 });
            buildings.Register(new() { Id = "grid:ai_alternative", DefaultBuilding = "grid:ai_alternative", Cost = 1,
                YieldChanges = new(0, 4, 0), Requirements = new Requirement.Leaf("building", "grid:ai_marker") });
            buildings.Register(new() { Id = "grid:ai_dirty", DefaultBuilding = "grid:ai_dirty", Cost = 1, ProducesPower = true, DirtyPower = true,
                Requirements = new Requirement.Leaf("building", "grid:local_marker") });
            buildings.Register(new() { Id = "grid:health", DefaultBuilding = "grid:health", Cost = -1, Health = 20 });
            buildings.Register(new() { Id = "grid:ai_clean", DefaultBuilding = "grid:ai_clean", Cost = 5, ProducesPower = true,
                Requirements = new Requirement.Leaf("building", "grid:local_marker") });
            buildings.Register(new() { Id = "grid:ai_area", DefaultBuilding = "grid:ai_area", Cost = 1, AreaCleanPower = true,
                Requirements = new Requirement.Leaf("building", "grid:area_marker") });
            buildings.Register(new() { Id = "grid:ai_fuel", DefaultBuilding = "grid:ai_fuel", Cost = 1, PowerBonus = "world:copper", DirtyPower = true,
                Requirements = new Requirement.Leaf("building", "grid:fuel_marker") });
            buildings.Register(new() { Id = "grid:ai_consumer", DefaultBuilding = "grid:ai_consumer", Cost = 1, RequiresPower = true, PowerYieldModifiers = new(0, 3000, 0),
                Requirements = new Requirement.Leaf("building", "grid:yield_marker") });
        }
    }

    private sealed class ProbeProjectKind : IProductionKind
    {
        public string Id => "probe:project";
        public string Label => "Projects";
        public bool IsContinuous => false;
        public IEnumerable<Civ.Content.ContentDef> Candidates(ContentDb content) => content.Get<S.ProjectDef>().All;
        public Civ.Content.ContentDef Definition(ContentDb content, string id) => content.Get<S.ProjectDef>(id);
        public long BaseCost(Civ.Content.ContentDef definition) => ((S.ProjectDef)definition).Cost;
        public bool IsVisible(RequirementResult eligibility) => true;
        public RequirementResult Validate(Simulation sim, int city, string id) => new(true, []);
        public CityProductionResult Calculate(Simulation sim, int city, string id) => throw new NotSupportedException();
    }
    private sealed class ProbeMod(S.RuleConstantsDef constants, int? buildingGoldPercent = null, int extraCultureLevels = 0) : IMod
    {
        public void Register(IModContext context)
        {
            if (buildingGoldPercent is { } percent)
                context.Content<S.RuleConstantsDef>().Override(new() { Id = constants.Id, Name = constants.Name, Era = constants.Era, Help = constants.Help, Pedia = constants.Pedia, Strategy = constants.Strategy, Icon = constants.Icon, Unsupported = constants.Unsupported, Values = constants.Values, Raw = constants.Raw.SetItem("MAXED_BUILDING_GOLD_PERCENT", percent.ToString()) });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:wonder", Name = "Wonder", DefaultBuilding = "probe:wonder", Cost = 1, Limits = new() { World = 1 } });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:enhanced_building", Name = "Enhanced Building", DefaultBuilding = "probe:enhanced_building", Cost = 1 });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:obsolete_building", Name = "Obsolete Building", DefaultBuilding = "probe:obsolete_building", Cost = 1, ObsoleteTech = "research:pottery" });
            context.Content<S.UnitDef>().Register(new() { Id = "probe:colonist", Name = "Colonist", DefaultUnit = "probe:colonist", Cost = 10, FoodProduction = true, CanFoundCity = true });
            context.Content<S.ProjectDef>().Register(new() { Id = "probe:project", Name = "Project", Cost = 1 });
            context.RegisterService<IProductionKind>("probe_project", new ProbeProjectKind());
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:basic", Name = "Basic", DefaultBuilding = "probe:basic", Cost = 1,
                YieldChanges = new(0, 2, 0), ObsoleteSafeCommerceChanges = new(0, 0, 3, 0), HurryAngerModifier = -50, ObsoleteTech = "research:pottery" });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:unique_basic", Name = "Unique Basic", DefaultBuilding = "probe:basic", Cost = 1,
                YieldChanges = new(0, 7, 0), ObsoleteSafeCommerceChanges = new(0, 0, 5, 0), HurryAngerModifier = -50 });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:upgrade", Name = "Upgrade", DefaultBuilding = "probe:upgrade", Cost = 1,
                YieldChanges = new(0, 11, 0), ReplaceBuildings = ImmutableDictionary<string, bool>.Empty.Add("probe:basic", true), ObsoleteTech = "research:writing" });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:extension", Name = "Extension", DefaultBuilding = "probe:extension", Cost = 1,
                YieldChanges = new(0, 13, 0), ExtendsBuildingClass = "probe:basic" });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:free_source", DefaultBuilding = "probe:free_source", Cost = 1,
                FreeBuilding = "probe:basic", ObsoleteTech = "research:writing" });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:shared_source", DefaultBuilding = "probe:shared_source", Cost = 1,
                FreeBuilding = "probe:basic", TeamShare = true, ObsoleteTech = "research:writing" });
            context.Content<S.CivilizationDef>().Register(new() { Id = "probe:plain" });
            context.Content<S.CivilizationDef>().Register(new() { Id = "probe:civilization",
                BuildingOverrides = ImmutableDictionary<string, string?>.Empty.Add("probe:basic", "probe:unique_basic") });
            context.Content<S.CivilizationDef>().Register(new() { Id = "probe:disabled_civilization",
                BuildingOverrides = ImmutableDictionary<string, string?>.Empty.Add("probe:basic", null) });
            context.Content<S.CivicOptionDef>().Register(new() { Id = "probe:option" });
            context.Content<S.CivicDef>().Register(new() { Id = "probe:whipping", Option = "probe:option",
                Hurrys = ImmutableDictionary<string, bool>.Empty.Add("cities:hurry_population", true) });
            context.Content<S.UnitDef>().Register(new() { Id = "probe:conscript", Name = "Conscript", DefaultUnit = "probe:conscript", Cost = 180, Conscription = 999, Moves = 1 });
            context.Content<S.TerrainDef>().Register(new() { Id = "probe:culture_barrier", Impassable = true });
            context.Content<S.FeatureDef>().Register(new() { Id = "probe:culture_discount", CultureDistance = -10 });
            context.RegisterService<IModifierSource>("effects", new Effects(extraCultureLevels));
        }
    }
    private sealed class Effects(int extraCultureLevels) : IModifierSource
    {
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope == ModifierScope.Unit) sink.Add("probe:training", "military:initial_experience", 3);
            if (context.Scope != ModifierScope.City) return;
            if (extraCultureLevels > 0) sink.Add("probe:culture", "cities:extra_culture_level", extraCultureLevels);
            sink.Add("probe:training", "cities:experience", 5);
            sink.Add("probe:item", "cities:building_class_production_percent:probe:enhanced_building", 50);
            sink.AddYield("probe:flat", new(4, 3, 4)); sink.AddYield("probe:percent", new(0, 50, 0), true);
            sink.AddCommerce("probe:direct", new(0, 0, 2, 0)); sink.AddCommerce("probe:percent", new(0, 25, 0, 0), true);
        }
    }
}
