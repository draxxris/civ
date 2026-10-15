using Civ.Content;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class GovernmentPeoplesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Caesar = "peoples:leader_julius_caesar", Godwinson = "peoples:leader_godwinson", Hus = "peoples:leader_hus";
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private Simulation Create(string leader, string speed = "normal", int[]? playerTeams = null, string world = "standard")
    {
        var sim = new Simulation(mods.Content, new SimulationSettings
        {
            Seed = 5, WorldSize = "foundation:worldsize_" + world, MapGenerator = "world:continents",
            GameSpeed = "foundation:gamespeed_" + speed, Handicap = "foundation:handicap_noble",
            StartingEra = "foundation:era_ancient", PlayerCount = 2, Leaders = [leader, Caesar], PlayerTeams = playerTeams ?? [],
        });
        sim.TeamOf(0).Techs.UnionWith(["research:code_of_laws", "research:monarchy", "research:slavery"]);
        return sim;
    }
    private static City AddCity(Simulation sim, int owner = 0)
    {
        var tile = sim.State.Players[owner].StartingPlot!.Value;
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(tile), Name = "T" };
        sim.State.AddCity(city);
        return city;
    }
    [Fact]
    public void Players_start_with_one_civic_per_option_from_civilization_or_option_default()
    {
        var sim = Create(Caesar);
        Assert.Equal(8, sim.State.Players[0].Civics.Count);
        Assert.Equal("government:barter", sim.State.Players[0].Civics["government:civicoption_economy"]);
    }
    [Theory]
    [InlineData(Caesar, "normal", 2)]     // (1 base + 1 civic) * 200% speed * 50% era.
    [InlineData(Caesar, "marathon", 4)]   // (1 + 1) * 400% * 50%.
    [InlineData(Hus, "marathon", 1)]      // The spiritual trait caps anarchy at one turn.
    public void Single_switch_anarchy_scales_with_speed_and_is_capped_by_trait(string leader, string speed, int turns)
    {
        var sim = Create(leader, speed);
        var result = Service<IGovernmentService>(sim).Calculate(sim, 0, ["government:despotism"]);
        Assert.Equal(turns, result.AnarchyTurns);
        Assert.Equal(5 + turns, result.CooldownTurns);
    }
    [Fact]
    public void Revolution_with_two_civics_sums_civic_lengths_once()
    {
        var sim = Create(Caesar);
        var result = Service<IGovernmentService>(sim).Calculate(sim, 0, ["government:despotism", "government:slavery"]);
        Assert.Equal(3, result.AnarchyTurns); // (1 + 1 + 1) * 200% * 50%.
    }
    [Fact]
    public void Requirements_and_negative_civic_gates_are_checked_against_the_final_cabinet()
    {
        var sim = Create(Caesar);
        Assert.False(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = ["government:republic"] }, out var error));
        Assert.Contains("research:democracy", error, StringComparison.Ordinal);
        sim.State.Players[0].Civics["government:civicoption_government"] = "government:despotism";
        sim.TeamOf(0).Techs.UnionWith(["research:liberalism"]);
        Assert.False(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = ["government:parliament"] }, out error));
        Assert.Contains("government:parliament", error); // The retained despotism forbids it.
    }
    [Fact]
    public void Switch_sets_anarchy_blocks_research_and_yields_and_survives_saves()
    {
        var sim = Create(Caesar); var city = AddCity(sim);
        var before = Service<ICityYieldService>(sim).Calculate(sim, city.Id).Yields;
        Assert.True(before.Production > 0);
        Assert.True(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = ["government:despotism"] }, out var error), error);
        Assert.Equal("government:despotism", sim.State.Players[0].Civics["government:civicoption_government"]);
        Assert.True(GovernmentStatus.IsAnarchy(sim, 0));
        Assert.Equal(default, Service<ICityYieldService>(sim).Calculate(sim, city.Id).Yields);
        Assert.False(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = ["government:slavery"] }, out error));
        Assert.Contains("anarchy", error);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.True(GovernmentStatus.IsAnarchy(sim, 0)); // Two turns of anarchy at normal speed.
        sim.AdvanceTurn();
        Assert.False(GovernmentStatus.IsAnarchy(sim, 0));
    }
    [Fact]
    public void Republic_switch_passes_through_anarchy_then_raises_city_production()
    {
        var sim = Create(Caesar); var city = AddCity(sim);
        sim.TeamOf(0).Techs.UnionWith(["research:democracy"]);
        city.Population = 3; Service<ICityWorkService>(sim).Assign(sim, city.Id);
        long before = Service<ICityYieldService>(sim).Calculate(sim, city.Id).Yields.Production;
        Assert.True(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = ["government:republic"] }, out var error), error);
        Assert.Equal("government:republic", sim.State.Players[0].Civics["government:civicoption_government"]);
        Assert.True(GovernmentStatus.IsAnarchy(sim, 0));
        Assert.Equal(0, Service<ICityYieldService>(sim).Calculate(sim, city.Id).Yields.Production);
        for (int turn = 0; turn < 5 && GovernmentStatus.IsAnarchy(sim, 0); turn++) sim.AdvanceTurn();
        Assert.False(GovernmentStatus.IsAnarchy(sim, 0));
        var after = Service<ICityYieldService>(sim).Calculate(sim, city.Id);
        Assert.Equal(25, after.Ledger.Total("yield.percent.production")); // Republic: +25% production.
        Assert.True(after.Yields.Production > before, $"production {before} -> {after.Yields.Production}");
    }
    [Fact]
    public void Civic_modifiers_reach_city_ledgers_with_hand_checked_numbers()
    {
        var sim = Create(Caesar); var city = AddCity(sim);
        sim.State.Players[0].Civics["government:civicoption_government"] = "government:republic"; // +25% production, +25% commerce.
        var ledger = Service<ICityYieldService>(sim).Calculate(sim, city.Id).Ledger;
        Assert.Equal(25, ledger.Total("yield.percent.production"));
        Assert.Equal(25, ledger.Total("yield.percent.commerce"));
        var player = ModifierSink.Collect(new(sim, ModifierScope.Player, 0)).Ledger;
        Assert.Equal(250, player.Total("cities:count_maintenance_percent"));
        Assert.Equal(250, player.Total("cities:distance_maintenance_percent"));
        sim.State.Players[0].Civics["government:civicoption_society"] = "government:tribal";
        Assert.Equal(50, ModifierSink.Collect(new(sim, ModifierScope.Player, 0)).Ledger.Total("cities:growth_percent"));
    }
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void Foreign_civic_pressure_affects_non_adopters_not_adopters(bool sameTeam, long expectedAnger)
    {
        var sim = Create(Caesar, playerTeams: sameTeam ? [0, 0] : [0, 1]);
        var nonAdopter = AddCity(sim, 0);
        var adopter = AddCity(sim, 1);
        nonAdopter.Population = adopter.Population = 10;
        const string category = "government:civicoption_military";
        sim.State.Players[0].Civics[category] = "government:standing_army";
        sim.State.Players[1].Civics[category] = "government:mutually_assured_destruction";
        var wellbeing = Service<ICityWellbeingService>(sim);
        var pressure = wellbeing.Calculate(sim, nonAdopter.Id).Ledger.Lines
            .Where(l => l.Source == "government:civic_anger").ToArray();
        if (expectedAnger == 0) Assert.Empty(pressure);
        else Assert.Equal(new LedgerLine("government:civic_anger", "cities:happiness_bad", expectedAnger), Assert.Single(pressure));
        Assert.DoesNotContain(wellbeing.Calculate(sim, adopter.Id).Ledger.Lines, l => l.Source == "government:civic_anger");
    }
    [Fact]
    public void Trait_modifiers_reach_city_and_player_ledgers()
    {
        var sim = Create("peoples:leader_pericles"); var city = AddCity(sim); // Scientific and philosophical.
        var yields = Service<ICityYieldService>(sim).Calculate(sim, city.Id);
        long Own(string key) => yields.Ledger.Lines.Where(l => l.Source == "peoples:trait_scientific" && l.Key == key).Sum(l => l.Amount);
        Assert.Equal(10, Own("commerce.percent.research"));
        Assert.Equal(1, Own("commerce.flat.research"));
        Assert.Equal(100, yields.Ledger.Lines.Where(l => l.Source == "peoples:trait_philosophical" && l.Key == "cities:great_people_percent").Sum(l => l.Amount));
        var org = Create(Caesar); // Imperialist and organized.
        Assert.Equal(-50, ModifierSink.Collect(new(org, ModifierScope.Player, 0)).Ledger.Total("economy:upkeep_percent"));
        var general = ModifierSink.Collect(new(org, ModifierScope.Player, 0)).Ledger.Lines.Where(l => l.Key == "military:general_rate_percent");
        Assert.Equal(100, general.Single(l => l.Source == "peoples:trait_imperialist").Amount);
        Assert.Equal(-100, general.Single(l => l.Source == "government:bandits").Amount); // Starting civic.
    }
    [Fact]
    public void Civic_gated_buildings_and_unique_units_follow_active_civics_and_civilization()
    {
        var sim = Create(Godwinson); var city = AddCity(sim);
        sim.TeamOf(0).Techs.UnionWith(["research:democracy", "research:bronze_working", "research:iron_working"]);
        var production = Service<ICityProductionService>(sim);
        Assert.Contains(production.Validate(sim, city.Id, "cities:building", "cities:agora").Failures, f => f.Explanation.Contains("government:republic"));
        sim.State.Players[0].Civics["government:civicoption_government"] = "government:republic";
        Assert.DoesNotContain(production.Validate(sim, city.Id, "cities:building", "cities:agora").Failures, f => f.Explanation.Contains("government:republic"));
        Assert.Contains(production.Validate(sim, city.Id, "cities:unit", "military:swordsman").Failures, f => f.Explanation.Contains("replacement"));
        Assert.DoesNotContain(production.Validate(sim, city.Id, "cities:unit", "military:anglo_saxon_huscarl").Failures, f => f.Explanation.Contains("replacement"));
    }
    [Theory]
    [InlineData("duel", 2)]
    [InlineData("tiny", 3)]
    [InlineData("standard", 5)]
    [InlineData("huge", 7)]
    public void Civic_draft_quota_scales_by_world_size_with_integer_truncation(string world, int maximum)
    {
        var sim = Create(Caesar, world: world); var city = AddCity(sim);
        sim.TeamOf(0).Techs.Add("research:archery");
        sim.State.PlayerById(0).Civics["government:civicoption_military"] = "government:conscription1";
        var result = Assert.IsType<ConscriptResult>(Service<ICityConscriptService>(sim).Calculate(sim, city.Id));
        Assert.Equal(maximum, result.MaxPlayerDrafts); // Base quota four, scaled by -50%, -25%, +25%, +75%.
        Assert.Equal(maximum, result.Ledger.Total("cities:max_conscript"));
    }
    [Fact]
    public void Leader_personality_is_exposed_through_core_query()
    {
        var sim = Create(Godwinson); var peoples = Service<IPeoplesService>(sim);
        Assert.Equal(Godwinson, peoples.Leader(sim, 0)!.Id);
        Assert.Equal(["peoples:trait_financial", "peoples:trait_protective"], peoples.Traits(sim, 0).Select(t => t.Id));
        Assert.NotEmpty(peoples.Leader(sim, 0)!.Flavors);
    }
    [Fact]
    public void Headless_150_turn_game_switches_civics_and_replays_from_saves()
    {
        var sim = Create(Caesar);
        foreach (var player in sim.State.Players) AddCity(sim, player.Id);
        sim.TeamOf(1).Techs.UnionWith(["research:code_of_laws", "research:monarchy"]);
        string[] choices = ["government:despotism", "government:hereditary_rule"];
        int switches = 0;
        for (int turn = 1; turn <= 150; turn++)
        {
            if (turn % 40 == 5)
                foreach (int p in new[] { 0, 1 })
                    if (sim.TryApply(new SetCivicsCommand { Player = p, Civics = [choices[switches % 2]] }, out _)) switches++;
            sim.AdvanceTurn();
        }
        Assert.True(switches >= 2);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(151, sim.State.Turn);
    }
}
