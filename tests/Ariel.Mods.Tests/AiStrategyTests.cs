using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Xunit;
using CivicDef = Ariel.Content.Schema.CivicDef;
using TechDef = Ariel.Content.Schema.TechDef;
using static Ariel.Mods.Tests.AiBindings;

namespace Ariel.Mods.Tests;

/// <summary>Expansion, production, research and civic choices that weigh the empire's situation.</summary>
public sealed class AiStrategyTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void A_capital_at_its_happy_cap_may_found_a_second_city_before_the_turn_gate()
    {
        var sim = Scenario(new(20, 12, true), game =>
        {
            game.State.Turn = 40;
            game.State.PlayerById(0).Treasury = 500;
            AddCity(game, 0, 4, 4).Population = 3;
            AddUnit(game, 0, "military:worker", 4, 4);
            AddUnit(game, 0, "military:warrior", 4, 4);
            AddUnit(game, 0, "military:warrior", 4, 4);
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var sites = NewAi(sim, "SitePlanner", ctx);
        Assert.False((bool)Call(sites, "WantsMoreCities", 0, 0)!);

        sim.State.Cities[0].Population = 14;
        Call(ctx, "Refresh");
        var view = Call(ctx, "ViewOf", sim.State.Cities[0])!;
        Assert.True(Property<int>(view, "HappyMargin") <= 0);
        Assert.True((bool)Call(sites, "WantsMoreCities", 0, 0)!);
    }

    [Fact]
    public void Land_near_a_rival_is_claimed_early_and_avoided_late()
    {
        var sim = Scenario(new(20, 12, true), game => { game.State.Turn = 50; AddCity(game, 0, 4, 4); });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var sites = NewAi(sim, "SitePlanner", ctx);
        int Value(int distance) => (int)Call(sites, "ContestedValue", distance)!;
        Assert.True(Value(2) > Value(6));
        Assert.True(Value(6) > 0);
        Assert.Equal(0, Value(8));
        sim.State.Turn = 200;
        Assert.True(Value(2) < 0);
    }

    [Fact]
    public void Slow_builds_cost_more_in_weak_cities_only()
    {
        var sim = Scenario(new(8, 8, true), game => AddCity(game, 0, 4, 4));
        var city = sim.State.Cities[0];
        object View(long production) => NewAi(sim, "CityView", city, new YieldVector(2, production, 2), 0, 0, 0, 0);
        long Cost(int cost, long production) =>
            (long)AiType(sim, "CityPlanner").GetMethod("BuildCost")!.Invoke(null, [cost, View(production)])!;
        Assert.Equal(160, Cost(100, 10));
        Assert.Equal(160, Cost(100, 7));
        // 50 turns: 35 turns past the limit add 35/15 of the cost.
        Assert.Equal(100 + 60 + 100 * 35 / 15, Cost(100, 2));
    }

    [Fact]
    public void Gold_hurries_threatened_defenders_before_settlers_and_ordinary_builds()
    {
        var sim = Scenario(new(12, 8, true), game =>
        {
            foreach (int column in new[] { 2, 6, 10 }) AddCity(game, 0, column, 4);
            game.State.Cities[0].ProductionQueue.Add(new("cities:unit", "military:warrior"));
            game.State.Cities[1].ProductionQueue.Add(new("cities:unit", "military:settler"));
            game.State.Cities[2].ProductionQueue.Add(new("cities:building", "cities:elder_council"));
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var army = NewAi(sim, "ArmyPlan", ctx);
        var cities = NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), army);
        var hurry = NewAi(sim, "HurryPlanner", ctx, cities);
        int Urgency(City city, int threat) =>
            (int)Call(hurry, "Urgency", NewAi(sim, "CityView", city, new YieldVector(2, 2, 2), 0, 0, threat, 0))!;
        Assert.Equal(3, Urgency(sim.State.Cities[0], 4));
        Assert.Equal(0, Urgency(sim.State.Cities[0], 0));
        Assert.Equal(2, Urgency(sim.State.Cities[1], 0));
        Assert.Equal(0, Urgency(sim.State.Cities[2], 4));
    }

    [Fact]
    public void Wartime_values_cheap_armies_and_low_weariness()
    {
        var sim = Scenario(new(8, 8, true), game => AddCity(game, 0, 4, 4));
        var warValue = AiType(sim, "CivicPlanner").GetMethod("WarValue")!;
        var militarist = new CivicDef { Id = "test:militarist", BaseFreeMilitaryUnits = 2, WarWearinessModifier = -50 };
        var pacifist = new CivicDef { Id = "test:pacifist", WarWearinessModifier = 50 };
        Assert.Equal(2 * 4 + 50 / 5, (int)warValue.Invoke(null, [militarist, 1])!);
        Assert.True((int)warValue.Invoke(null, [pacifist, 1])! < 0);
    }

    [Fact]
    public void Research_goals_only_raise_techs_that_can_be_chosen_now()
    {
        var sim = Scenario(new(12, 8, true), game => AddCity(game, 0, 4, 4));
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var research = sim.Rules().Research;
        var selectable = sim.Content.Get<TechDef>().All.Where(t => !t.Disable && research.ValidateSelection(sim, 0, t.Id).Success)
            .Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(selectable);
        var planner = NewAi(sim, "ResearchPlanner", ctx);
        var goals = (Dictionary<string, long>)Call(planner, "GoalScores", research, new HashSet<string>(StringComparer.Ordinal), selectable)!;
        Assert.NotEmpty(goals);
        Assert.All(goals, goal => Assert.Contains(goal.Key, selectable));
        Assert.All(goals.Values, score => Assert.True(score > 0));
    }

    private Simulation Scenario(MapSpec map, Action<Simulation> configure) => Simulation.CreateScenario(mods.Content,
        GameFactory.Settings(71), map, Enumerable.Range(0, map.Width * map.Height)
            .Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
        {
            configure(game);
            foreach (var plot in game.State.Plots) plot.Revealed.Add(game.TeamOf(0).Id);
        });

    private static City AddCity(Simulation sim, int owner, int column, int row)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = HexGrid.FromOffset(column, row), Population = 3 };
        sim.State.AddCity(city);
        sim.State.PlotAt(city.Position).Owner = owner;
        return city;
    }

    private static Unit AddUnit(Simulation sim, int owner, string type, int column, int row)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = HexGrid.FromOffset(column, row) };
        sim.State.AddUnit(unit);
        sim.Rules().Units.Initialize(sim, unit.Id);
        return unit;
    }
}
