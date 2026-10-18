using Civ.Content;
using Civ.Game;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class ReligionRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Buddhism = "religion:buddhism";
    private static readonly Lazy<string> ScenarioSave = new(() =>
    {
        var sim = GameFactory.Scenario(new LoadedMods().Content);
        GameFactory.NeutralPolitics(sim);
        return SaveGame.Save(sim);
    });
    private Simulation Scenario() => SaveGame.Load(mods.Content, ScenarioSave.Value);
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static City City(Simulation sim, int tile, int owner = 0, int population = 1)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = population };
        sim.State.AddCity(city);
        return city;
    }
    private static void LearnFoundingTech(Simulation sim, int player, string religion = Buddhism)
    {
        var tech = sim.Content.Get<S.ReligionDef>(religion).FoundingTech!;
        // Isolate discovery from its prerequisite graph, while retaining the real research command and event path.
        sim.TeamOf(player).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Where(t => t.Id != tech).Select(t => t.Id));
        sim.TeamOf(player).Techs.Remove(tech);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = player, Tech = tech }, out var error), error);
        var research = Service<IResearchService>(sim);
        research.Contribute(sim, player, research.CalculateCost(sim, sim.TeamOf(player).Id, tech).Cost);
    }
    private static Unit Missionary(Simulation sim, City city, int owner = 0, string type = "military:buddhist_missionary")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = city.Position, MovesLeft = 60 };
        sim.State.AddUnit(unit);
        return unit;
    }
    private static void Spread(Simulation sim, City city, string religion = Buddhism, string type = "military:buddhist_missionary")
    {
        var unit = Missionary(sim, city, city.Owner, type);
        Assert.True(sim.TryApply(new SpreadReligionCommand { Player = city.Owner, Unit = unit.Id, Religion = religion }, out var error), error);
    }
    [Theory]
    [InlineData(Buddhism, 0)]
    [InlineData("religion:christianity", 1)]
    public void First_discovery_founds_once_and_holy_city_scoring_discourages_the_capital(string religionId, int missionaries)
    {
        var sim = Scenario();
        var capital = City(sim, 0); capital.Buildings.Add("cities:palace");
        var other = City(sim, 5, population: 100);
        var rival = City(sim, 60, owner: 1);
        LearnFoundingTech(sim, 0, religionId);
        var religion = Service<IReligionService>(sim);
        Assert.Equal(other.Id, religion.HolyCity(sim, religionId)); // Population beats capital's /8 score for every allowed roll.
        Assert.Contains(religionId, religion.Present(sim, other.Id));
        Assert.Empty(religion.Present(sim, capital.Id));
        LearnFoundingTech(sim, 1, religionId);
        Assert.Equal(other.Id, religion.HolyCity(sim, religionId));
        Assert.Empty(religion.Present(sim, rival.Id));
        Assert.Single(sim.Events.Published.OfType<ReligionFounded>());
        string missionary = sim.Content.Get<S.ReligionDef>(religionId).Missionary!;
        Assert.Equal(missionaries, sim.State.Units.Count(u => u.Type == missionary));
        Assert.All(sim.State.Units.Where(u => u.Type == missionary), u =>
        {
            Assert.Equal(0, u.Owner); Assert.Equal(other.Position, u.Position); Assert.True(u.MovesLeft > 0);
        });
    }
    [Fact]
    public void Discovery_without_a_city_reserves_the_first_team_until_it_founds_a_city()
    {
        var sim = Scenario(); LearnFoundingTech(sim, 0);
        var rival = City(sim, 60, owner: 1); LearnFoundingTech(sim, 1);
        Assert.Null(Service<IReligionService>(sim).HolyCity(sim, Buddhism));
        var city = City(sim, 0);
        sim.Events.Publish(new CityFounded(city.Id, 0, city.Position));
        Assert.Equal(city.Id, Service<IReligionService>(sim).HolyCity(sim, Buddhism));
        Assert.DoesNotContain(Buddhism, Service<IReligionService>(sim).Present(sim, rival.Id));
    }
    [Fact]
    public void Natural_spread_uses_connected_influence_and_continues_identically_after_a_save()
    {
        var sim = Scenario(); var source = City(sim, 0); LearnFoundingTech(sim, 0);
        source.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine"]);
        var connected = City(sim, 1); var isolated = City(sim, 8);
        sim.State.Turn = 101; // Outside the new-religion window, before old-religion attenuation.
        var religion = Service<IReligionService>(sim);
        Assert.Equal(new ReligionSpreadOdds(10, 1100), religion.NaturalSpreadOdds(sim, connected.Id, Buddhism)); // 245/6 +1, then /4. Hex diameter is nine.
        Assert.Equal(0, religion.NaturalSpreadOdds(sim, isolated.Id, Buddhism).Threshold);
        var before = sim.ComputeHash();
        _ = religion.NaturalSpreadOdds(sim, connected.Id, Buddhism);
        Assert.Equal(before, sim.ComputeHash()); // Preview neither draws RNG nor writes extension state.
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        var phase = sim.Content.TurnSystems.All.Single(s => s.Id == "religion:city_turns");
        // Keep age fixed while sampling enough low-probability attempts to avoid dependence on the map's RNG offset.
        for (int i = 0; i < 1100 && religion.Present(sim, connected.Id).Count == 0; i++)
        {
            phase.Run(sim); phase.Run(loaded);
            Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        }
        Assert.Contains(Buddhism, religion.Present(sim, connected.Id));
        Assert.Empty(religion.Present(sim, isolated.Id));
    }
    [Fact]
    public void Missionary_commands_consume_the_unit_and_enforce_foreign_civic_and_ownership_gates()
    {
        var sim = Scenario(); var domestic = City(sim, 0); var foreign = City(sim, 40, 1);
        sim.State.PlayerById(1).Civics["government:civicoption_religion"] = "government:intolerant";
        var unit = Missionary(sim, foreign);
        var command = new SpreadReligionCommand { Player = 0, Unit = unit.Id, Religion = Buddhism };
        var hash = sim.ComputeHash();
        Assert.False(sim.TryApply(command, out var error)); Assert.Contains("prohibit", error);
        Assert.Equal(hash, sim.ComputeHash());
        sim.State.MoveUnit(unit.Id, domestic.Position);
        Assert.False(sim.TryApply(new SpreadReligionCommand { Player = 1, Unit = unit.Id, Religion = Buddhism }, out _));
        Assert.Equal(100, Service<IReligionService>(sim).MissionaryChance(sim, unit.Id, Buddhism));
        Assert.True(sim.TryApply(command, out error), error);
        Assert.Null(sim.State.FindUnit(unit.Id));
        Assert.Contains(Buddhism, Service<IReligionService>(sim).Present(sim, domestic.Id));
        Assert.Single(sim.Events.Published.OfType<MissionaryAttempt>(), e => e.Success);
    }
    [Fact]
    public void Conversion_updates_happiness_production_and_commerce_ledgers_and_saves_anarchy()
    {
        var sim = Scenario(); var holy = City(sim, 0); LearnFoundingTech(sim, 0);
        var foreign = City(sim, 40, 1); Spread(sim, foreign);
        holy.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine", "cities:sistine_chapel"]);
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        var religion = Service<IReligionService>(sim);
        Assert.True(sim.TryApply(new SetStateReligionCommand { Player = 0, Religion = Buddhism }, out var error), error);
        Assert.Equal(Buddhism, religion.StateReligion(sim, 0));
        Assert.True(GovernmentStatus.IsAnarchy(sim, 0));
        Assert.Equal(default, Service<ICityYieldService>(sim).Calculate(sim, holy.Id).Yields);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.False(GovernmentStatus.IsAnarchy(sim, 0));
        var effects = ModifierSink.Collect(new(sim, ModifierScope.City, 0, holy)).Ledger;
        Assert.Equal(2, effects.Lines.Single(l => l.Source == "cities:buddhist_temple" && l.Key == "cities:happiness").Amount);
        Assert.Equal(1, effects.Lines.Single(l => l.Source == Buddhism && l.Key == "cities:happiness").Amount);
        Assert.Equal(2, effects.Lines.Single(l => l.Source == "cities:buddhist_shrine:shrine" && l.Key == "commerce.flat.gold").Amount);
        Assert.Equal(4, effects.Lines.Single(l => l.Source == "cities:buddhist_shrine:shrine" && l.Key == "commerce.flat.culture").Amount);
        Assert.Equal(5, effects.Lines.Single(l => l.Source == "cities:buddhist_temple:state_religion" && l.Key == "commerce.flat.culture").Amount);
        Assert.Equal(15, effects.Total("cities:religion_building_production_percent"));
        Assert.Equal(15, Service<ICityProductionService>(sim).Calculate(sim, holy.Id, "cities:building", "cities:granary").ModifierPercent - 100);
        Assert.False(sim.TryApply(new SetStateReligionCommand { Player = 0 }, out error)); Assert.Contains("cooldown", error);
    }
    [Fact]
    public void Building_eligibility_distinguishes_presence_state_religion_and_holy_city()
    {
        var sim = Scenario(); var holy = City(sim, 0); LearnFoundingTech(sim, 0);
        var city = City(sim, 5);
        var production = Service<ICityProductionService>(sim);
        Assert.False(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_temple").Success);
        Spread(sim, city);
        Assert.True(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_temple").Success);
        var prophet = Missionary(sim, city, type: "military:prophet");
        var shrine = new BuildShrineCommand { Player = 0, Unit = prophet.Id, Building = "cities:buddhist_shrine" };
        Assert.False(sim.TryApply(shrine, out var shrineError)); Assert.Contains("holy city", shrineError);
        sim.State.MoveUnit(prophet.Id, holy.Position);
        Assert.True(sim.TryApply(shrine, out shrineError), shrineError);
        Assert.Contains("cities:buddhist_shrine", holy.Buildings);
        Assert.Null(sim.State.FindUnit(prophet.Id));
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        Spread(sim, city, "religion:hinduism", "military:hindu_missionary");
        Assert.True(sim.TryApply(new SetStateReligionCommand { Player = 0, Religion = "religion:hinduism" }, out var error), error);
        Assert.False(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_monastery").Success);
        Assert.True(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_temple").Success); // Temple exemption.
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:free_church";
        Assert.True(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_monastery").Success);
        sim.State.PlayerById(0).Civics.Clear();
        Assert.Null(Service<IReligionService>(sim).StateReligion(sim, 0));
        Assert.Equal("religion:hinduism", sim.State.PlayerById(0).Extensions.Get<ReligionPlayerState>(sim.Content)!.LastStateReligion);
    }
    [Fact]
    public void Old_religion_spread_odds_scale_down_smoothly_instead_of_truncating_to_zero()
    {
        var sim = Scenario(); var source = City(sim, 0); LearnFoundingTech(sim, 0);
        source.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine"]);
        var connected = City(sim, 1);
        var religion = Service<IReligionService>(sim);
        int founded = sim.State.Turn;
        // Base threshold is 41 for this pair. Range 1100 implies 110% growth, so the old-age line is 250 * 110 / 100 = 275.
        // Threshold is then 41 * 275 / age, and the reported odds divide that by four.
        sim.State.Turn = founded + 101;
        Assert.Equal(10, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
        sim.State.Turn = founded + 300;
        Assert.Equal(9, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
        sim.State.Turn = founded + 400;
        Assert.Equal(7, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
        sim.State.Turn = founded + 550;
        Assert.Equal(5, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
    }
    [Fact]
    public void State_religion_buildings_need_an_active_state_religion_even_when_they_name_no_religion()
    {
        var sim = Scenario(); City(sim, 0); LearnFoundingTech(sim, 0);
        var city = City(sim, 5); Spread(sim, city);
        var religion = Service<IReligionService>(sim);
        var palace = sim.Content.Get<S.BuildingDef>("cities:apostolic_palace");
        Assert.NotNull(religion.ValidateBuilding(sim, city, palace));
        Assert.Null(religion.ValidateBuilding(sim, city, sim.Content.Get<S.BuildingDef>("cities:buddhist_temple"))); // Temple exemption.
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        Assert.True(sim.TryApply(new SetStateReligionCommand { Player = 0, Religion = Buddhism }, out var error), error);
        Assert.Null(religion.ValidateBuilding(sim, city, palace));
        Assert.Equal("Requires matching state religion: religion:islam",
            religion.ValidateBuilding(sim, city, sim.Content.Get<S.BuildingDef>("cities:alhambra")));
    }
}
