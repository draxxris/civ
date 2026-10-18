using System.Collections.Immutable;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using S = Civ.Content.Schema;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class ResearchEconomyTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    // Reuse frozen content and saved empty scenarios, never mutable game state.
    private static readonly Lazy<string> StandardScenario = new(() => SaveGame.Save(GameFactory.Scenario(new LoadedMods().Content)));
    private static readonly Lazy<(ContentDb Content, string Save)> ProbeScenario = new(CreateProbeScenario);
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static (ContentDb, string) CreateProbeScenario()
    {
        var loaded = new LoadedMods();
        var source = new ModSource { Manifest = new() { Id = "probe", Version = "1", Dependencies = ["foundation"],
            References = ["foundation", "research", "cities", "government", "economy", "military"] }, Root = loaded.Root, Instance = new ProbeMod() };
        var content = ModLoader.Build(loaded.Sources.Append(source));
        return (content, SaveGame.Save(GameFactory.Scenario(content)));
    }
    private Simulation Scenario(bool probe = false)
    {
        var sim = probe ? SaveGame.Load(ProbeScenario.Value.Content, ProbeScenario.Value.Save)
            : SaveGame.Load(mods.Content, StandardScenario.Value);
        // Isolate Economy/Research equations from political modifiers. Integration has its own fixture.
        GameFactory.NeutralPolitics(sim);
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(39)), Name = "Test" };
        sim.State.AddCity(city);
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        sim.State.PlayerById(0).Treasury = 1000;
        if (probe) sim.State.PlayerById(0).Civics["probe:option"] = "probe:civic";
        return sim;
    }
    [Theory]
    [InlineData("research:agriculture", "normal", "noble", "standard", 20)]
    [InlineData("research:bronze_working", "normal", "noble", "standard", 64)]
    [InlineData("research:absolutism", "normal", "noble", "standard", 1633)]
    [InlineData("research:pottery", "quick", "chieftain", "tiny", 11)]
    [InlineData("research:bronze_working", "marathon", "deity", "huge", 318)]
    public void Tech_cost_keeps_each_source_truncation(string tech, string speed, string handicap, string world, long expected)
    {
        var sim = new Simulation(mods.Content, new SimulationSettings { Seed = 17, PlayerCount = 1,
            WorldSize = "foundation:worldsize_" + world, GameSpeed = "foundation:gamespeed_" + speed,
            Handicap = "foundation:handicap_" + handicap, StartingEra = "foundation:era_ancient",
            MapGenerator = "world:continents", Leaders = ["peoples:leader_julius_caesar"] });
        var before = sim.ComputeHash();
        var result = Service<IResearchService>(sim).CalculateCost(sim, 0, tech);
        Assert.Equal(expected, result.Cost);
        Assert.Equal(expected, result.Ledger.Total("research:cost"));
        Assert.Equal(before, sim.ComputeHash());
    }
    [Fact]
    public void All_prerequisites_and_one_alternative_are_required_with_reasons()
    {
        var sim = Scenario(); var team = sim.TeamOf(0);
        var command = new SetTeamResearchCommand { Player = 0, Tech = "research:pottery" };
        Assert.False(sim.TryApply(command, out var reason));
        Assert.Contains("research:stone_tools", reason);
        Assert.Contains("research:agriculture", reason);
        team.Techs.Add("research:agriculture");
        Assert.False(sim.TryApply(command, out reason));
        Assert.Contains("research:stone_tools", reason);
        team.Techs.Add("research:stone_tools");
        Assert.True(sim.TryApply(command, out reason), reason);
        team.Techs.Remove("research:agriculture"); team.Techs.Add("research:fishing");
        Assert.True(sim.TryApply(command, out reason), reason);
        team.Techs.Add("research:pottery");
        Assert.False(sim.TryApply(command, out reason)); Assert.Contains("already known", reason);
    }
    [Fact]
    public void Overflow_normalizes_known_team_bonus_and_queue_progress_survives_save()
    {
        var sim = Scenario(); var team = sim.TeamOf(0); var service = Service<IResearchService>(sim);
        sim.TeamOf(1).Techs.Add("research:agriculture");
        Assert.Equal(100, service.RateModifier(sim, 0, "research:agriculture")); // No contact, no exposure.
        team.Extensions.Set(sim.Content, new ResearchTeamState { MetTeams = [1] });
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "research:agriculture", Queue = ["research:fishing"] }, out var error), error);
        Assert.Equal(125, service.RateModifier(sim, 0, "research:agriculture"));
        service.Contribute(sim, 0, 21); // 21 * 125 / 100 = 26; cost 20; normalized overflow 6 * 100 / 125 = 4.
        Assert.Contains("research:agriculture", team.Techs);
        Assert.Contains(sim.Events.Published, e => e is TechDiscovered { Team: 0, Tech: "research:agriculture" });
        Assert.Equal(4, team.Extensions.Get<ResearchTeamState>(sim.Content)!.Overflow);
        Assert.Equal("research:fishing", team.Researching);
        service.Contribute(sim, 0, 1);
        Assert.Equal(5, team.ResearchProgress);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "research:hunting" }, out _));
        service.Contribute(sim, 0, 2);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "research:fishing" }, out _));
        Assert.Equal(5, team.ResearchProgress); Assert.Equal(2, team.Research["research:hunting"]);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }
    [Fact]
    public void Shared_team_contributions_do_not_duplicate_discoveries_and_future_tech_repeats()
    {
        var sim = Scenario(true); var service = Service<IResearchService>(sim);
        // Foundation's team setup is covered elsewhere; this scenario adds the second member to an existing team.
        sim.State.Teams[1].Members.Clear();
        var original = sim.State.Players[1];
        sim.State.Players[1] = new Player { Id = 1, TeamId = 0, Civilization = original.Civilization };
        sim.TeamOf(0).Members.Add(1); sim.State.RebuildIndexes();
        Assert.Equal(30, service.CalculateCost(sim, 0, "research:agriculture").Cost);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "research:agriculture" }, out _));
        service.Contribute(sim, 0, 15); service.Contribute(sim, 1, 15);
        Assert.Single(sim.Events.Published.OfType<TechDiscovered>());
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "probe:future", Queue = ["probe:future"] }, out _));
        service.Contribute(sim, 0, 1); service.Contribute(sim, 1, 1);
        Assert.Equal(2, sim.TeamOf(0).Extensions.Get<ResearchTeamState>(sim.Content)!.Completions["probe:future"]);
        Assert.Equal("foundation:era_future", sim.State.PlayerById(0).Extensions.Get<CityPlayerState>(sim.Content)!.CurrentEra);
    }
    [Fact]
    public void First_discovery_rewards_are_unique_and_disabled_technology_is_rejected()
    {
        var sim = Scenario(true); var service = Service<IResearchService>(sim);
        Assert.False(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "probe:disabled" }, out var error));
        Assert.Contains("disabled", error);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "probe:reward" }, out _));
        service.Contribute(sim, 0, 1);
        Assert.Single(sim.State.Units, u => u.Type == "military:scientist");
        Assert.Equal(1, sim.TeamOf(0).Extensions.Get<ResearchTeamState>(sim.Content)!.FreeTechs);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "research:agriculture", FreeTech = true }, out _));
        Assert.Contains("research:agriculture", sim.TeamOf(0).Techs);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 1, Tech = "probe:reward" }, out _));
        service.Contribute(sim, 1, 1);
        Assert.Equal(0, sim.TeamOf(1).Extensions.Get<ResearchTeamState>(sim.Content)!.FreeTechs);
        Assert.Single(sim.State.Units, u => u.Type == "military:scientist");
    }
    [Fact]
    public void Slider_split_direct_commerce_and_negative_modifier_order_have_hundredth_ledgers()
    {
        var sim = Scenario(true); var service = Service<IPlayerEconomyService>(sim);
        Assert.True(sim.TryApply(new SetCommerceRatesCommand { Player = 0, Rates = new(35, 35, 25, 5) }, out var error), error);
        var before = sim.ComputeHash(); var result = service.CalculateCity(sim, 1);
        // Five raw commerce: 175 research + 1 direct hundredth, then 133% => 234.
        // Gold: 175 - 200 direct, scaled inversely by 200% => -12 (toward zero).
        Assert.Equal(new CommerceVector(-12, 234, 281, 25), result.Hundredths);
        Assert.Equal(175, result.Ledger.Total("economy:research_split_hundredths"));
        Assert.Equal(1, result.Ledger.Total("economy:research_direct_hundredths"));
        Assert.Equal(133, result.Ledger.Total("economy:research_factor_percent"));
        Assert.Equal(before, sim.ComputeHash());
        Assert.False(sim.TryApply(new SetCommerceRatesCommand { Player = 0, Rates = new(34, 36, 25, 5) }, out error));
        Assert.Contains("steps", error); Assert.Equal(before, sim.ComputeHash());
        Assert.False(sim.TryApply(new SetCommerceRatesCommand { Player = 0, Rates = new(10, 90, 0, 0) }, out error));
        Assert.Contains("maximum", error);
        Assert.False(sim.TryApply(new SetCommerceRatesCommand { Player = 0, Rates = new(50, 60, 0, 0) }, out error));
        Assert.Contains("sum", error);
    }
    [Fact]
    public void Commerce_culture_hook_and_disorder_cover_whole_final_rate()
    {
        var sim = Scenario(true); var service = Service<IPlayerEconomyService>(sim);
        service.SetRates(sim, 0, new(50, 0, 50, 0));
        sim.AdvanceTurn();
        Assert.Equal(4, sim.State.Cities[0].Culture); // (250 split + 100 direct) * 125% = 437 hundredths.
        Assert.Equal(37, sim.State.Cities[0].Extensions.Get<CityRuleState>(sim.Content)!.CultureRemainder);
        sim.State.Cities[0].Extensions.Get<CityRuleState>(sim.Content)!.Disorder = true;
        Assert.Equal(default, service.CalculateCity(sim, 1).Hundredths);
    }
    [Fact]
    public void Expense_ledger_charges_maintenance_civics_units_supply_and_compounded_inflation()
    {
        var sim = Scenario(true); var city = sim.State.Cities[0]; city.Population = 10; city.Buildings.Add("cities:palace");
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(44)), Population = 10 });
        for (int i = 0; i < 20; i++) sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = sim.State.Grid.HexOf(new TileId(0)) });
        sim.State.Plots[0].Owner = null;
        // Inflation 25%: civic base (12 * 50/100 + 1*100/100) = 7;
        // 7*125/100=8; *80/100=6; *115/100=6. Civic upkeep is inflated only here, not again by the aggregate.
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new EconomyPlayerState { AccruedCostRatioTimes10000 = 12500 });
        var result = Service<IPlayerEconomyService>(sim).Calculate(sim, 0);
        Assert.Equal(18, result.Ledger.Total("economy:free_units"));
        Assert.Equal(2, result.Ledger.Total("economy:paid_units"));
        Assert.Equal(1, result.Ledger.Total("economy:unit_upkeep")); // 2 * 50/100 * 115/100.
        Assert.Equal(8, result.Ledger.Total("economy:unit_supply")); // (20-6) * 50/100 * 115/100.
        Assert.Equal(6, result.Ledger.Total("economy:civic_upkeep"));
        Assert.Equal(1938, result.Ledger.Total("economy:maintenance_hundredths")); // Capital 25 + other 1888+25.
        Assert.Equal(19, result.Ledger.Total("economy:maintenance"));
        Assert.Equal(28, result.Ledger.Total("economy:pre_inflated")); // Maintenance 19 + units 1 + supply 8.
        Assert.Equal(25, result.Ledger.Total("economy:inflation_percent"));
        Assert.Equal(7, result.Ledger.Total("economy:inflation")); // 28*125/100 - 28.
        Assert.Equal(41, result.Expenses); // 35 inflated aggregate + 6 civic upkeep.
    }
    [Fact]
    public void Inflation_updates_after_gold_and_imported_profile_disables_it()
    {
        var sim = Scenario(true); sim.State.Turn = 231;
        // Ten effective turns. Per-turn=12*85/100=10; ratio=10000+100+90*100/20000=10100.
        sim.AdvanceTurn();
        Assert.Equal(10100, sim.State.PlayerById(0).Extensions.Get<EconomyPlayerState>(sim.Content)!.AccruedCostRatioTimes10000);
        Assert.Equal(1, Service<IPlayerEconomyService>(sim).Calculate(sim, 0).Ledger.Total("economy:inflation_percent"));
        var shipped = Scenario(); shipped.State.Turn = 400; shipped.AdvanceTurn();
        Assert.Equal(0, Service<IPlayerEconomyService>(shipped).Calculate(shipped, 0).Ledger.Total("economy:inflation_percent"));
    }
    [Fact]
    public void Bankruptcy_moves_science_to_gold_before_research_and_records_unpaid_costs()
    {
        var sim = Scenario(); var player = sim.State.PlayerById(0); player.Treasury = 0;
        for (int i = 0; i < 40; i++) sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = sim.State.Cities[0].Position });
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = "research:agriculture" }, out _));
        sim.AdvanceTurn();
        var state = player.Extensions.Get<EconomyPlayerState>(sim.Content)!;
        Assert.Equal(new CommerceVector(100, 0, 0, 0), state.Rates);
        Assert.Equal(0, player.Treasury); Assert.True(state.UnpaidGold > 0);
        Assert.Equal(1, sim.TeamOf(0).ResearchProgress); // Base research remains, but slider science is gone.
    }
    [Fact]
    public void Process_queue_converts_production_after_commerce_modifiers_without_eating_overflow()
    {
        var sim = Scenario(true); var city = sim.State.Cities[0]; var service = Service<IPlayerEconomyService>(sim);
        var command = new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "economy:process", ContentId = "economy:process_research" };
        Assert.False(sim.TryApply(command, out var error)); Assert.Contains("research:alphabet", error);
        sim.TeamOf(0).Techs.Add("research:alphabet"); Assert.True(sim.TryApply(command, out error), error);
        city.Extensions.Get<CityRuleState>(sim.Content)!.Overflow = 12;
        var production = Service<ICityProductionService>(sim).Calculate(sim, 1, "economy:process", "economy:process_research");
        Assert.Equal(0, production.Cost); Assert.Equal(3, production.Production);
        service.SetRates(sim, 0, new(50, 50, 0, 0));
        var result = service.CalculateCity(sim, 1);
        Assert.Equal(150, result.Ledger.Total("economy:research_process_hundredths")); // 3 modified production * 50.
        Assert.Equal(483, result.Hundredths.Research); // (250 + 1) * 133/100 = 333, then process 150.
        sim.AdvanceTurn();
        Assert.Equal(12, city.Extensions.Get<CityRuleState>(sim.Content)!.Overflow);
        Assert.Equal(0, Assert.Single(city.ProductionQueue).Progress);
        Assert.Equal(4, sim.State.PlayerById(0).Extensions.Get<EconomyPlayerState>(sim.Content)!.SettledCommerceHundredths.Research / 100);
    }
    [Fact]
    public void Gold_hurry_uses_new_item_surcharge_and_whip_uses_scaled_population_and_anger()
    {
        var sim = Scenario(true); var city = sim.State.Cities[0]; var service = Service<ICityHurryService>(sim);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:building", ContentId = "probe:building" }, out _));
        var hurry = service.Calculate(sim, 1, "cities:hurry_gold");
        Assert.Equal(17, Service<ICityProductionService>(sim).Calculate(sim, 1, "cities:building", "probe:building").Cost); // 10*176/100*100/100.
        Assert.Equal(78, hurry.Gold); // ceil(17*150/100)*3.
        Assert.Equal(150, hurry.Ledger.Total("economy:hurry_factor_percent"));
        sim.State.PlayerById(0).Treasury = 77;
        Assert.False(sim.TryApply(new HurryCityCommand { Player = 0, CityId = 1, HurryId = "cities:hurry_gold" }, out var error)); Assert.Contains("gold", error);
        sim.State.PlayerById(0).Treasury = 78;
        Assert.True(sim.TryApply(new HurryCityCommand { Player = 0, CityId = 1, HurryId = "cities:hurry_gold" }, out error), error);
        Assert.Equal(0, sim.State.PlayerById(0).Treasury); Assert.Equal(17, city.ProductionQueue[0].Progress);
        sim.AdvanceTurn(); Assert.Contains("probe:building", city.Buildings);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:building", ContentId = "probe:whip" }, out _));
        city.Population = 20; city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = 1 };
        // Happiness is not modelled here: give the city 18 workers, as if it had grown content, so the whip must release them.
        var center = sim.State.Grid.TileAt(city.Position);
        city.Worked = Service<ICityWorkService>(sim).WorkArea(sim, 1).Where(t => t != center).Take(18).Select(t => sim.State.Grid.HexOf(t)).ToList();
        hurry = service.Calculate(sim, 1, "cities:hurry_population");
        Assert.Equal(3, hurry.Population); // Cost 528, left 527. ceil(527²/790)=352; per citizen 3000/19=157.
        Assert.Equal(706, hurry.Production); // 3*157*150/100 = 706.
        Assert.True(sim.TryApply(new HurryCityCommand { Player = 0, CityId = 1, HurryId = "cities:hurry_population" }, out error), error);
        Assert.Equal(17, city.Population);
        Assert.True(city.Worked.Count + city.Specialists.Values.Sum() <= city.Population);
        Assert.Equal(20, city.Extensions.Get<EconomyCityState>(sim.Content)!.HurryAngerTurns);
        Assert.Contains(Service<ICityWellbeingService>(sim).Calculate(sim, 1).Ledger.Lines, l => l.Source == "economy:hurry" && l.Amount == 1);
    }
    [Fact]
    [Trait("Category", "E2E")]
    public void Full_mod_smoke_runs_one_hundred_scripted_turns_and_resumes_mid_research()
    {
        var sim = GameFactory.Create(mods.Content, 19);
        foreach (var settler in sim.State.Units.Where(u => mods.Content.Get<S.UnitDef>(u.Type).CanFoundCity).ToArray())
            Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = settler.Owner, Settler = settler.Id }, out var error), error);
        Simulation? loaded = null;
        for (int turn = 0; turn < 100; turn++)
        {
            foreach (var player in sim.State.Players)
            {
                if (sim.TeamOf(player.Id).Researching is not null) continue;
                var tech = mods.Content.Get<S.TechDef>().All.FirstOrDefault(t => Service<IResearchService>(sim).Validate(sim, player.Id, t.Id).Success);
                if (tech is null) continue;
                var command = new SetTeamResearchCommand { Player = player.Id, Tech = tech.Id };
                Assert.True(sim.TryApply(command, out _));
                if (loaded is not null) Assert.True(loaded.TryApply(command, out _));
            }
            sim.AdvanceTurn(); loaded?.AdvanceTurn();
            if (turn == 39) { loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash()); }
        }
        Assert.Equal(101, sim.State.Turn);
        Assert.Equal(sim.ComputeHash(), loaded!.ComputeHash());
        Assert.All(sim.State.Teams, t => Assert.True(t.Extensions.Get<ResearchTeamState>(sim.Content)!.Completions.Count > 0));
    }
    private sealed class ProbeMod : IMod
    {
        public void Register(IModContext context)
        {
            var original = context.Content<S.RuleConstantsDef>().Get("foundation:rule_constants");
            context.Content<S.RuleConstantsDef>().Override(new() { Id = original.Id, Name = original.Name, Values = original.Values,
                Raw = original.Raw.SetItem("DISABLE_INFLATION", "0").SetItem("INFLATION_RATE_MOMENTUM", "1") });
            context.Content<S.TechDef>().Register(new() { Id = "probe:future", Cost = 1, Repeat = true, Era = "foundation:era_future" });
            context.Content<S.TechDef>().Register(new() { Id = "probe:reward", Cost = 1, FirstFreeTechs = 1, FirstFreeUnit = "military:scientist" });
            context.Content<S.TechDef>().Register(new() { Id = "probe:disabled", Cost = 1, Disable = true });
            context.Content<S.UpkeepDef>().Register(new() { Id = "probe:upkeep", PopulationPercent = 50, CityPercent = 100 });
            context.Content<S.CivicOptionDef>().Register(new() { Id = "probe:option" });
            context.Content<S.CivicDef>().Register(new() { Id = "probe:civic", Option = "probe:option", Upkeep = "probe:upkeep",
                Hurrys = new Dictionary<string, bool> { ["cities:hurry_gold"] = true, ["cities:hurry_population"] = true }.ToImmutableDictionary() });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:building", DefaultBuilding = "probe:building", Cost = 10 });
            context.Content<S.BuildingDef>().Register(new() { Id = "probe:whip", DefaultBuilding = "probe:whip", Cost = 300 });
            context.RegisterService<IModifierSource>("effects", new Effects());
        }
    }
    private sealed class Effects : IModifierSource
    {
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope == ModifierScope.Player) sink.Add("probe:limits", "economy:max_rate:research", -20);
            if (context.Scope != ModifierScope.City) return;
            sink.AddYield("probe:flat", new(0, 0, 4)); sink.AddYield("probe:production", new(0, 50, 0), true);
            sink.AddCommerce("probe:direct", new(-2, 0, 1, 0));
            sink.Add("probe:fraction", "cities:direct_research_hundredths", 1);
            sink.AddCommerce("probe:modifier", new(100, 33, 25, 0), true);
        }
    }
}
