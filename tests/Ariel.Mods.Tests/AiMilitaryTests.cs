using Ariel.Content;
using Ariel.Game;
using Xunit;
using static Ariel.Mods.Tests.AiBindings;
using PromotionDef = Ariel.Content.Schema.PromotionDef;
using UnitDef = Ariel.Content.Schema.UnitDef;

namespace Ariel.Mods.Tests;

/// <summary>Attack sizing, healing, escorts, promotions, upgrades, war choice and peace in the computer player.</summary>
public sealed class AiMilitaryTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Attacks_launch_on_expected_wins_not_head_count()
    {
        var sim = Scenario(game =>
        {
            AddCity(game, 0, 2, 4);
            AddCity(game, 1, 10, 4);
            for (int i = 0; i < 2; i++) AddUnit(game, 1, "military:musketman", 10, 4);
        });
        AtWar(sim);
        foreach (var plot in sim.State.Plots) plot.Visible.Add(sim.TeamOf(0).Id);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var attack = NewAi(sim, "AttackPlanner", ctx, NewAi(sim, "UnitOrders", ctx));
        var city = sim.State.Cities.Single(c => c.Owner == 1);
        // Four warriors were enough under the old head-count rule (defenders + 2), but cannot beat two musketmen.
        var warriors = Enumerable.Range(0, 4).Select(_ => AddUnit(sim, 0, "military:warrior", 6, 4)).ToList();
        Call(ctx, "Refresh");
        Assert.False((bool)Call(attack, "Ready", warriors, city)!);

        var strong = Enumerable.Range(0, 6).Select(_ => AddUnit(sim, 0, "military:rifleman", 6, 4)).ToList();
        Call(ctx, "Refresh");
        Assert.True((bool)Call(attack, "Ready", strong, city)!);
    }

    [Fact]
    public void Wounded_soldiers_near_enemies_fall_back_to_a_city()
    {
        var sim = Scenario(game =>
        {
            game.State.Turn = 30;
            AddCity(game, 0, 2, 4);
            for (int i = 0; i < 3; i++) AddUnit(game, 0, "military:warrior", 2, 4);
            AddCity(game, 1, 14, 4);
        });
        AtWar(sim);
        var wounded = AddUnit(sim, 0, "military:swordsman", 6, 4);
        wounded.Health = 40;
        AddUnit(sim, 1, "military:axeman", 7, 4);
        int before = sim.State.Grid.Distance(wounded.Position, sim.State.Cities[0].Position);
        Military(sim);
        Assert.True(sim.State.Grid.Distance(wounded.Position, sim.State.Cities[0].Position) < before);
    }

    [Fact]
    public void A_soldier_joins_a_settler_that_left_the_city()
    {
        var sim = Scenario(game =>
        {
            game.State.Turn = 30;
            AddCity(game, 0, 2, 4);
            for (int i = 0; i < 3; i++) AddUnit(game, 0, "military:warrior", 2, 4);
        });
        var settler = AddUnit(sim, 0, "military:settler", 5, 4);
        var guard = AddUnit(sim, 0, "military:warrior", 4, 4);
        Military(sim);
        Assert.Equal(settler.Position, guard.Position);

        // With the escort on its tile, a lone warrior nearby no longer turns the settler back.
        AtWar(sim);
        AddUnit(sim, 1, "military:warrior", 7, 4);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var planner = NewAi(sim, "SettlerPlanner", ctx, NewAi(sim, "SitePlanner", ctx));
        Assert.True((int)Call(planner, "EscortStrength", settler)! >= (int)Call(ctx, "ThreatAround", settler.Position, 3)!);
    }

    [Fact]
    public void Promotions_suit_the_unit_role()
    {
        var sim = Scenario(_ => { });
        var value = AiType(sim, "MilitaryPlanner").GetMethods().Single(m => m.Name == "Value" && m.GetParameters().Length == 2);
        var roles = AiType(sim, "PromotionRole");
        int Value(string promotion, string role) =>
            (int)value.Invoke(null, [sim.Content.Get<PromotionDef>(promotion), Enum.Parse(roles, role)])!;
        Assert.True(Value("military:city_garrison1", "Defender") > Value("military:combat1", "Defender"));
        Assert.True(Value("military:city_raider1", "Attacker") > Value("military:city_garrison1", "Attacker"));
        Assert.True(Value("military:barrage1", "Siege") > Value("military:city_garrison1", "Siege"));
    }

    [Fact]
    public void Upgrades_rank_by_strength_gained_per_gold()
    {
        var sim = Scenario(_ => { });
        var value = AiType(sim, "MilitaryPlanner").GetMethod("UpgradeValue")!;
        long? Value(string from, string to, long price) =>
            (long?)value.Invoke(null, [sim.Content.Get<UnitDef>(from), sim.Content.Get<UnitDef>(to), price]);
        Assert.Null(Value("military:warrior", "military:warrior", 10));
        Assert.True(Value("military:warrior", "military:axeman", 60) > Value("military:warrior", "military:archer", 40));
        Assert.True(Value("military:warrior", "military:axeman", 60) > Value("military:warrior", "military:axeman", 90));
    }

    [Fact]
    public void Peace_follows_how_the_war_is_going()
    {
        var sim = Scenario(_ => { });
        var wants = AiType(sim, "WarPlanner").GetMethod("WantsPeace")!;
        bool Wants(long mine, long theirs, int score, int length) => (bool)wants.Invoke(null, [mine, theirs, score, length])!;
        Assert.False(Wants(100, 100, 0, 20));
        Assert.True(Wants(80, 100, 0, 20));
        Assert.True(Wants(100, 100, -200, 20));
        Assert.True(Wants(100, 100, 50, 45));
        Assert.False(Wants(100, 100, 400, 60));
        Assert.True(Wants(100, 100, 200, 80));
    }

    [Fact]
    public void War_targets_the_richer_of_two_weak_neighbours()
    {
        var sim = Scenario(game =>
        {
            AddCity(game, 0, 7, 4);
            AddCity(game, 1, 2, 4);
            AddCity(game, 2, 12, 4).Population = 10;
            for (int i = 0; i < 15; i++) AddUnit(game, 0, "military:swordsman", 7, 4);
        }, players: 3);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var war = NewAi(sim, "WarPlanner", ctx, NewAi(sim, "ArmyPlan", ctx));
        Assert.Equal(2, (int?)Call(war, "ChooseTarget"));
    }

    [Fact]
    public void Foreign_troops_massed_beside_a_city_raise_its_garrison()
    {
        var sim = Scenario(game => { game.State.Turn = 30; AddCity(game, 0, 4, 4); AddCity(game, 1, 12, 4); });
        foreach (var plot in sim.State.Plots) plot.Visible.Add(sim.TeamOf(0).Id);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var army = NewAi(sim, "ArmyPlan", ctx);
        int Want() => (int)Call(army, "DesiredGarrison", Call(ctx, "ViewOf", sim.State.Cities[0])!)!;
        int calm = Want();
        for (int i = 0; i < 4; i++) AddUnit(sim, 1, "military:swordsman", 5, 4);
        Call(ctx, "Refresh");
        Assert.True(Want() > calm);
    }

    [Fact]
    public void Air_units_bomb_only_cities_an_army_is_marching_on()
    {
        var sim = Scenario(game =>
        {
            AddCity(game, 0, 2, 4);
            AddCity(game, 1, 9, 4);
            AddCity(game, 1, 13, 1);
        });
        AtWar(sim);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var air = NewAi(sim, "AirPlanner", ctx);
        IEnumerable<City> Targets() => ((IEnumerable<City>)Call(air, "CampaignCities")!).ToList();
        Assert.Empty(Targets());
        AddUnit(sim, 0, "military:swordsman", 7, 4);
        Call(ctx, "Refresh");
        Assert.Equal([sim.State.Cities[1].Id], Targets().Select(c => c.Id));
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void A_prepared_war_is_declared_from_the_border_and_its_capture_is_scored()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var own = new City { Id = 1, Owner = 0, Position = sim.State.Grid.HexOf(new(39)), Population = 4 };
        var enemy = new City { Id = 2, Owner = 1, Position = own.Position.Neighbor(0).Neighbor(0).Neighbor(0).Neighbor(0).Neighbor(0), Population = 3 };
        enemy.Extensions.Set(sim.Content, new CityRuleState { HighestPopulation = enemy.Population });
        sim.State.AddCity(own); sim.State.AddCity(enemy); sim.State.NextCityId = 3;
        sim.State.PlotAt(enemy.Position).Owner = 1;
        sim.State.PlotAt(enemy.Position).Revealed.Add(sim.TeamOf(0).Id);
        sim.TeamOf(0).Techs.UnionWith(["research:woodworking", "research:siege_warfare"]);
        for (int i = 0; i < 15; i++)
            AddUnitAt(sim, 0, i < 12 ? "military:javelineer" : "military:catapult", own.Position);
        AddUnitAt(sim, 1, "military:warrior", enemy.Position);
        sim.State.Turn = 50;
        int? prepared = null, declared = null;
        for (int i = 0; i < 100 && sim.State.CityById(enemy.Id).Owner != 0; i++)
        {
            sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
            if (prepared is null && Component(sim, sim.State.PlayerById(0).Extensions, "AiPlayerState") is { } state
                && state.GetType().GetProperty("War")!.GetValue(state) is not null) prepared = sim.State.Turn;
            if (declared is null && sim.TeamOf(0).Wars.Contains(sim.TeamOf(1).Id)) declared = sim.State.Turn;
            sim.AdvanceTurn();
        }
        Assert.NotNull(prepared);
        Assert.NotNull(declared);
        Assert.True(declared >= prepared + 5, $"prepared {prepared}, declared {declared}");
        Assert.Equal(0, sim.State.CityById(enemy.Id).Owner);
        var record = Component(sim, sim.TeamOf(0).Extensions, "AiTeamState")!;
        var score = (SortedDictionary<int, int>)record.GetType().GetProperty("WarScore")!.GetValue(record)!;
        Assert.True(score.GetValueOrDefault(sim.TeamOf(1).Id) > 0);
    }

    private static object? Component(Simulation sim, object extensions, string type) => extensions.GetType().GetMethod("Get")!
        .MakeGenericMethod(AiType(sim, type)).Invoke(extensions, [sim.Content]);

    private static void Military(Simulation sim)
    {
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var army = NewAi(sim, "ArmyPlan", ctx);
        var cities = NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), army);
        Call(NewAi(sim, "MilitaryPlanner", ctx, army, cities, NewAi(sim, "AttackPlanner", ctx, NewAi(sim, "UnitOrders", ctx))), "Plan");
    }

    private static void AtWar(Simulation sim) => sim.State.SetWar(sim.TeamOf(0).Id, sim.TeamOf(1).Id, true);

    private Simulation Scenario(Action<Simulation> configure, int players = 2)
    {
        var leaders = new[] { "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar", "peoples:leader_bismarck" }.Take(players).ToArray();
        var map = new MapSpec(16, 8, true);
        return Simulation.CreateScenario(mods.Content, GameFactory.Settings(71, leaders), map,
            Enumerable.Range(0, map.Width * map.Height).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                GameFactory.NeutralPolitics(game);
                configure(game);
                foreach (var plot in game.State.Plots) plot.Revealed.Add(game.TeamOf(0).Id);
            });
    }

    private static City AddCity(Simulation sim, int owner, int column, int row)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = HexGrid.FromOffset(column, row), Population = 3 };
        sim.State.AddCity(city);
        sim.State.PlotAt(city.Position).Owner = owner;
        return city;
    }

    private static Unit AddUnit(Simulation sim, int owner, string type, int column, int row) =>
        AddUnitAt(sim, owner, type, HexGrid.FromOffset(column, row));

    private static Unit AddUnitAt(Simulation sim, int owner, string type, Hex position)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = position };
        sim.State.AddUnit(unit);
        sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        return unit;
    }
}
