using System.Collections.Immutable;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class WorkerRepairTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static readonly Lazy<ContentDb> ProbeContent = new(() =>
    {
        var loaded = new LoadedMods();
        var source = new ModSource { Root = loaded.Root, Manifest = new() { Id = "repair_probe", Version = "1", References = ["world", "military"] }, Instance = new Probe() };
        return ModLoader.Build(loaded.Sources.Append(source));
    });
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static void Decide(Simulation sim) => sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
    private static City Home(Simulation sim)
    {
        GameFactory.NeutralPolitics(sim);
        foreach (var plot in sim.State.Plots) plot.Owner = 1;
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Population = 1, Position = sim.State.Grid.HexOf(new(39)) };
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        sim.State.AddCity(city); sim.State.Plots[39].Owner = 0;
        sim.State.Plots[40].Owner = 0;
        city.Worked.Add(sim.State.Grid.HexOf(new(40)));
        return city;
    }
    private static Unit Worker(Simulation sim, string type = "military:worker")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = type, Position = sim.State.Grid.HexOf(new(40)) };
        sim.State.AddUnit(unit); Service<IUnitService>(sim).Initialize(sim, unit.Id);
        Service<IVisibilityService>(sim).Recompute(sim);
        return unit;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void Ai_continues_an_unfinished_paid_road_without_paying_the_initial_cost_again(long funds)
    {
        var sim = GameFactory.Scenario(mods.Content); var home = Home(sim);
        var plot = sim.State.Plots[40];
        plot.Improvement = "world:improvement_improvement_farm"; plot.Bonus = "world:wheat";
        sim.TeamOf(0).Techs.UnionWith(["research:the_wheel", "research:agriculture"]);
        sim.State.PlayerById(0).Treasury = funds;
        var worker = Worker(sim);
        var builds = Service<IWorkerBuildService>(sim);
        Assert.Null(builds.Preview(sim, 0, worker.Id, new(40), "world:build_build_road").Error);
        Decide(sim);
        Assert.Equal(funds - 2, sim.State.PlayerById(0).Treasury);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, loaded })
        {
            var work = (IPlayerTurnSystem)game.Content.TurnSystems.Get("world:workers");
            Assert.Equal(320, builds.RequiredWork(game, new(40), "world:build_build_road"));
            for (int turn = 0; turn < 4; turn++)
            {
                if (turn > 0)
                {
                    game.State.UnitById(worker.Id).MovesLeft = Service<IUnitService>(game).MaximumMoves(game, worker.Id);
                    long before = game.State.PlayerById(0).Treasury;
                    Decide(game);
                    Assert.Equal(before, game.State.PlayerById(0).Treasury);
                    Assert.Equal(game.State.Grid.HexOf(new(40)), game.State.UnitById(worker.Id).Position);
                }
                work.Run(game, 0);
                if (turn < 3)
                {
                    Assert.Null(game.State.Plots[40].Route);
                    Assert.Equal(100 * (turn + 1), game.State.Plots[40].BuildProgress["world:build_build_road"]);
                }
            }
            Assert.Equal("world:route_route_road", game.State.Plots[40].Route);
            Assert.Empty(game.State.Plots[40].BuildProgress);
            Assert.Equal(1, Service<IResourceNetwork>(game).ConnectedCount(game, 0, home.Id, "world:wheat"));
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Worker_replaces_a_nonconnecting_improvement_for_a_revealed_bonus_without_a_yield_gain(bool revealed)
    {
        var sim = GameFactory.Scenario(ProbeContent.Value); var home = Home(sim);
        var plot = sim.State.Plots[40];
        plot.Improvement = "repair_probe:broken"; plot.Bonus = "repair_probe:bonus";
        plot.Route = "world:route_route_road";
        if (revealed) sim.TeamOf(0).Techs.Add("repair_probe:reveal");
        var worker = Worker(sim, "repair_probe:worker");
        var before = Service<IPlotYieldService>(sim).Calculate(sim, 0, new(40)).Yields;
        var preview = Service<IWorkerBuildService>(sim).Preview(sim, 0, worker.Id, new(40), "repair_probe:repair");
        Assert.Null(preview.Error); Assert.Equal(before, preview.Yields);
        Assert.Equal(0, Service<IResourceNetwork>(sim).ConnectedCount(sim, 0, home.Id, "repair_probe:bonus"));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, loaded })
        {
            Decide(game);
            ((IPlayerTurnSystem)game.Content.TurnSystems.Get("world:workers")).Run(game, 0);
            Assert.Equal(revealed ? "repair_probe:connector" : "repair_probe:broken", game.State.Plots[40].Improvement);
            Assert.Equal(revealed ? 1 : 0, Service<IResourceNetwork>(game).ConnectedCount(game, 0, home.Id, "repair_probe:bonus"));
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    private sealed class Probe : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.TechDef>().Register(new() { Id = "repair_probe:reveal" });
            context.Content<S.BonusDef>().Register(new() { Id = "repair_probe:bonus", RevealTech = "repair_probe:reveal" });
            context.Content<S.ImprovementDef>().Register(new() { Id = "repair_probe:broken" });
            context.Content<S.ImprovementDef>().Register(new() { Id = "repair_probe:connector", BonusEffects = ImmutableDictionary<string, S.ImprovementBonus>.Empty.Add("repair_probe:bonus", new() { MakesValid = true, Connects = true }) });
            context.Content<S.BuildDef>().Register(new() { Id = "repair_probe:repair", Time = 50, ImprovementType = "repair_probe:connector" });
            context.Content<S.UnitDef>().Register(new() { Id = "repair_probe:worker", DefaultUnit = "repair_probe:worker", Domain = "military:domain_land", Moves = 2, WorkRate = 100, Builds = ImmutableDictionary<string, bool>.Empty.Add("repair_probe:repair", true) });
        }
    }
}
