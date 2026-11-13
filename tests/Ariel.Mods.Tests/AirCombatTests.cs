using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.AirCombat;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Air missions and interception are the AirCombat mod's: with it aircraft fly from the unit panel and the
/// computer player, without it the base game refuses to train aircraft and no unit stands on intercept.</summary>
public sealed class AirCombatTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static readonly Lazy<ContentDb> WithoutAirCombat = new(() =>
        ModLoader.Build(new LoadedMods().Sources.Where(s => s.Manifest.Id != "air_combat")));

    [Fact]
    public void The_mod_registers_missions_interception_orders_and_the_air_planner()
    {
        var content = mods.Content;
        Assert.NotNull(content.Rules().AirOperations);
        Assert.NotNull(content.Services.Find<IAirMissionService>());
        Assert.True(content.CommandTypes.Contains("air_combat:air_mission"));
        var planner = Assert.Single(content.Services.Get<Ariel.Ai.IAiPlanner>(), p => p.Id.StartsWith("air_combat:", StringComparison.Ordinal));
        Assert.Equal(Ariel.Ai.AiPlannerOrder.Air, planner.Order);

        var (sim, fighter, _) = Airfield(content);
        Assert.Null(new SetUnitActivityCommand { Player = 0, Unit = fighter.Id, Activity = UnitActivity.Intercept }.Validate(sim));
        var session = new GameSession(sim); session.SelectUnit(fighter.Id);
        var recon = session.DescribeUnit(fighter)!.Actions.First(a => a.Command is AirMissionCommand { Kind: AirMissionKind.Recon });
        session.RunAction(recon.Id);
        Assert.Equal("", session.Message);
        Assert.Contains(sim.Events.Published.OfType<AirMissionResolved>(), e => e.Report.Kind == AirMissionKind.Recon);
    }

    [Fact]
    public void Without_the_mod_aircraft_cannot_be_trained_or_intercept_and_the_ai_still_plays()
    {
        var content = WithoutAirCombat.Value;
        Assert.Null(content.Rules().AirOperations);
        Assert.Null(content.Services.Find<IAirMissionService>());
        Assert.False(content.CommandTypes.Contains("air_combat:air_mission"));
        Assert.DoesNotContain(content.Services.Get<Ariel.Ai.IAiPlanner>(), p => p.Order == Ariel.Ai.AiPlannerOrder.Air);

        var (sim, fighter, city) = Airfield(content);
        Assert.Equal("Unit cannot intercept",
            new SetUnitActivityCommand { Player = 0, Unit = fighter.Id, Activity = UnitActivity.Intercept }.Validate(sim));
        var production = sim.Content.Services.Get<ICityProductionService>().Single();
        Assert.Contains(production.Validate(sim, city.Id, "cities:unit", "military:fighter").Failures,
            f => f.Explanation == "Air operations are unavailable");
        var session = new GameSession(sim); session.SelectUnit(fighter.Id);
        Assert.DoesNotContain(session.DescribeUnit(fighter)!.Actions, a => a.Id.StartsWith("air_mission:", StringComparison.Ordinal));

        var game = GameFactory.CreateAi(content, seed: 5, players: 2);
        for (int turn = 0; turn < 30; turn++) game.AdvanceTurn();
        foreach (var player in game.State.Players.Where(p => p.Ai == "ai:standard"))
            Assert.True(game.State.Cities.Count(c => c.Owner == player.Id) >= 1, $"player {player.Id} has no city");
        Assert.DoesNotContain(game.State.Units, u => game.Content.Get<Ariel.Content.Schema.UnitDef>(u.Type).Domain == "military:domain_air");
    }

    /// <summary>A fighter at full health in a city of player 0.</summary>
    private static (Simulation Sim, Unit Fighter, City City) Airfield(ContentDb content)
    {
        var sim = GameFactory.Scenario(content);
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new(40)), Population = 3 };
        sim.State.AddCity(city);
        var fighter = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:fighter", Position = city.Position, MovesLeft = 60 };
        sim.State.AddUnit(fighter);
        return (sim, fighter, city);
    }
}
