using System.Collections;
using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;
using Xunit;
using static Civ.Mods.Tests.AiBindings;

using UnitDef = Civ.Content.Schema.UnitDef;

namespace Civ.Mods.Tests;

public sealed class AiCalculationTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    // The recorded constant this used to assert was a whole-state FNV hash; the same test already compares the
    // decisions structurally, so the real contract is that the sequential, in-wave and reloaded runs agree.
    [Fact]
    public void City_production_decisions_hash_and_rng_agree_across_sequential_wave_and_reload()
    {
        var hashes = new List<ulong>();
        foreach (var (inWave, reload) in new[] { (false, false), (true, false), (false, true) })
        {
            var sim = Scenario(new(24, 10, true), game =>
            {
                game.State.Turn = 60;
                GameFactory.NeutralPolitics(game);
                foreach (int column in new[] { 2, 10, 18 })
                {
                    var city = AddCity(game, 0, column, 4);
                    city.ProductionQueue.Add(new("economy:process", "economy:process_wealth"));
                    foreach (var tile in game.State.Grid.Range(game.State.Grid.TileAt(city.Position), 2))
                        game.State.Plots[tile.Value].Owner = 0;
                    for (int i = 0; i < 3; i++) AddUnit(game, 0, "military:warrior", column, 4);
                }
                AddUnit(game, 0, "military:worker", 2, 4);
            });
            if (reload) sim = SaveGame.Load(mods.Content, SaveGame.Save(sim));
            sim.IsParallelAiTurn = inWave;
            var rng = sim.Rng.State;
            var log = new PlannerLog();
            sim.CommandObserver = log;
            var ctx = NewAi(sim, "AiContext", sim, 0);
            var army = NewAi(sim, "ArmyPlan", ctx);
            Call(NewAi(sim, "CitizenPlanner", ctx, army), "Plan");
            Call(NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), army), "Plan");
            Assert.Equal(new[] { "1:cities:unit/military:worker", "2:cities:unit/military:worker",
                    "3:cities:building/cities:elder_council" },
                sim.State.Cities.SelectMany(c => c.ProductionQueue.Select(q => $"{c.Id}:{q.Kind}/{q.ContentId}")));
            Assert.Equal(new[] { "produce:1:cities:unit/military:worker", "produce:2:cities:unit/military:worker",
                "produce:3:cities:building/cities:elder_council" }, log.Commands);
            Assert.Equal(rng, sim.Rng.State);
            hashes.Add(sim.ComputeHash());
        }
        Assert.All(hashes, h => Assert.Equal(hashes[0], h));
    }



    [Fact]
    public void Military_orders_agree_across_sequential_wave_and_reload()
    {
        var hashes = new List<ulong>();
        foreach (var (inWave, reload) in new[] { (false, false), (true, false), (false, true) })
        {
            var sim = Scenario(new(12, 8, true), game =>
            {
                game.State.Turn = 60;
                GameFactory.NeutralPolitics(game);
                var city = AddCity(game, 0, 6, 3);
                city.ProductionQueue.Add(new("economy:process", "economy:process_wealth"));
                AddUnit(game, 0, "military:warrior", 5, 3);
                AddUnit(game, 0, "military:warrior", 7, 3);
                AddUnit(game, 0, "military:warrior", 6, 6);
                var blocked = AddUnit(game, 0, "military:warrior", 0, 0);
                foreach (var tile in game.State.Grid.Neighbors(game.State.Grid.TileAt(blocked.Position)))
                    game.State.Plots[tile.Value].Kind = PlotKind.Peak;
            });
            if (reload) sim = SaveGame.Load(mods.Content, SaveGame.Save(sim));
            sim.IsParallelAiTurn = inWave;
            var rng = sim.Rng.State;
            var log = new MovementLog();
            sim.CommandObserver = log;
            var ctx = NewAi(sim, "AiContext", sim, 0);
            var army = NewAi(sim, "ArmyPlan", ctx);
            var cities = NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), army);
            Call(cities, "Plan");
            var attack = NewAi(sim, "AttackPlanner", ctx, NewAi(sim, "UnitOrders", ctx));
            Call(NewAi(sim, "MilitaryPlanner", ctx, army, cities, attack), "Plan");
            Assert.Equal(new[] { 1, 2, 3 }, log.Units);
            Assert.Equal(new[] { 1, 2 }, sim.State.UnitsAt(sim.State.CityById(1).Position).Select(u => u.Id));
            Assert.Equal(rng, sim.Rng.State);
            // No turns advance here, so the variants must agree on the whole-state hash instead of a recorded constant.
            hashes.Add(sim.ComputeHash());
        }
        Assert.All(hashes, h => Assert.Equal(hashes[0], h));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Path_queries_preserve_steps_costs_and_state_at_blocked_wrap_seams(bool wrap, bool blocked)
    {
        var sim = Scenario(new(6, 1, wrap), game =>
        {
            AddUnit(game, 0, "military:worker", 0, 0);
            game.State.Plots[2].Kind = PlotKind.Hills;
            if (blocked) game.State.Plots[3].Kind = PlotKind.Peak;
        });
        var unit = sim.State.UnitById(1);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var query = Method<Func<Unit, Hex, bool, UnitPath?>>(ctx, "FindPath");
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        // First access includes simultaneous identical queries and normalized aliases of the same destination.
        Parallel.For(0, 24, i =>
        {
            var target = new Hex(5 + (wrap ? (i % 3 - 1) * 6 : 0), 0);
            var path = query(unit, target, false);
            if (!wrap && blocked) { Assert.Null(path); return; }
            Assert.NotNull(path);
            Assert.Equal((wrap ? new[] { 5 } : new[] { 1, 2, 3, 4, 5 }).Select(t => new Hex(t, 0)), path.Steps);
            Assert.Equal(wrap ? 120 : 720, path.Cost);
            Assert.Null(sim.Content.Services.Get<IMovementService>().Single().ValidatePath(sim, 0, unit.Id, path.Steps));
        });
        Assert.Null(query(unit, new(5, -1), false));
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Path_queries_keep_unit_specific_costs_domains_and_promotions()
    {
        var sim = Scenario(new(4, 1), game =>
        {
            AddUnit(game, 0, "military:worker", 0, 0);
            var warrior = AddUnit(game, 0, "military:warrior", 0, 0);
            warrior.Promotions.Add("military:guerilla1");
            warrior.Experience = 10;
            AddUnit(game, 0, "military:galley", 3, 0);
            game.State.Plots[1].Kind = PlotKind.Hills;
            game.State.Plots[3].Terrain = "world:coast";
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var query = Method<Func<Unit, Hex, bool, UnitPath?>>(ctx, "FindPath");
        var target = new Hex(2, 0);
        var rng = sim.Rng.State;
        Parallel.For(0, 24, i =>
        {
            var unit = sim.State.UnitById(i % 3 + 1);
            var path = query(unit, target, false);
            if (unit.Id == 3) { Assert.Null(path); return; }
            Assert.NotNull(path);
            Assert.Equal(new[] { new Hex(1, 0), target }, path.Steps);
            Assert.Equal(unit.Id == 1 ? 360 : 240, path.Cost);
        });
        Assert.True(Method<Func<GameCommand, bool>>(ctx, "Issue")(new PromoteMilitaryUnitCommand
            { Player = 0, Unit = 2, Promotion = "military:guerilla2" }));
        Assert.Equal(180, query(sim.State.UnitById(2), target, false)!.Cost);
        Assert.Equal(360, query(sim.State.UnitById(1), target, false)!.Cost);
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Path_queries_refresh_unreachable_results_after_commands_movement_and_direct_changes()
    {
        var sim = Scenario(new(7, 1), game =>
        {
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:worker", 0, 0);
            AddUnit(game, 1, "military:warrior", 2, 0);
        });
        sim.State.Plots[2].Visible.Add(sim.TeamOf(0).Id);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var query = Method<Func<Unit, Hex, bool, UnitPath?>>(ctx, "FindPath");
        var issue = Method<Func<GameCommand, bool>>(ctx, "Issue");
        var worker = sim.State.UnitById(1);
        var target = new Hex(6, 0);
        var rng = sim.Rng.State;
        Assert.Null(query(worker, target, false));
        Assert.Null(query(worker, target, false));
        Assert.True(issue(new DisbandUnitCommand { Player = 1, Unit = 2 }));
        Assert.Equal(720, query(worker, target, false)!.Cost);
        foreach (int step in new[] { 1, 2 })
        {
            Assert.True(issue(new MoveUnitPathCommand { Player = 0, Unit = 1, Path = [new(step, 0)] }));
            var path = query(worker, target, false);
            Assert.NotNull(path);
            Assert.Equal(Enumerable.Range(step + 1, 6 - step).Select(t => new Hex(t, 0)), path.Steps);
            Assert.Equal((6 - step) * 120, path.Cost);
        }
        Assert.Equal(0, worker.MovesLeft);
        Assert.False(Method<Func<Unit, Hex, bool, bool>>(ctx, "MoveToward")(worker, target, false));
        sim.State.Plots[3].Kind = PlotKind.Peak;
        Call(ctx, "Refresh");
        Assert.Null(query(worker, target, false));
        sim.State.Plots[3].Kind = PlotKind.Flat;
        foreach (var plot in sim.State.Plots) plot.Route = "world:route_route_road";
        Call(ctx, "Refresh");
        Assert.Equal(240, query(worker, target, false)!.Cost);
        Assert.Equal(rng, sim.Rng.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Path_queries_distinguish_attack_endpoints_from_ordinary_steps(bool attackFirst)
    {
        var sim = Scenario(new(5, 1), game =>
        {
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:warrior", 0, 0);
            AddUnit(game, 1, "military:settler", 3, 0);
        });
        sim.State.Plots[3].Visible.Add(sim.TeamOf(0).Id);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var query = Method<Func<Unit, Hex, bool, UnitPath?>>(ctx, "FindPath");
        var unit = sim.State.UnitById(1);
        var target = new Hex(3, 0);
        var rng = sim.Rng.State;
        foreach (bool attack in new[] { attackFirst, !attackFirst, attackFirst })
        {
            var path = query(unit, target, attack);
            if (!attack) { Assert.Null(path); continue; }
            Assert.NotNull(path);
            Assert.Equal(new[] { new Hex(1, 0), new Hex(2, 0), target }, path.Steps);
            Assert.Equal(360, path.Cost);
            Assert.Null(sim.Content.Services.Get<IMovementService>().Single().ValidatePath(sim, 0, unit.Id, path.Steps));
        }
        // An attack endpoint cannot become an intermediate step on a path beyond the hostile unit.
        Assert.Null(query(unit, new(4, 0), true));
        var state = unit.Extensions.Get<MilitaryUnitState>(sim.Content)!;
        state.Attacked = true;
        Call(ctx, "Refresh");
        Assert.Null(query(unit, target, true));
        state.Attacked = false;
        Call(ctx, "Refresh");
        Assert.True((bool)Call(NewAi(sim, "UnitOrders", ctx), "Approach", unit, target)!);
        Assert.Equal(new Hex(1, 0), unit.Position);
        Assert.Equal(1, sim.State.UnitById(2).Owner);
        Assert.Equal(rng, sim.Rng.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Path_queries_stay_isolated_across_shared_content_games_and_many_destinations(bool independentGames)
    {
        Simulation Create() => Scenario(new(40, 1), game => AddUnit(game, 0, "military:worker", 0, 0));
        var sim = Create();
        var forks = Enumerable.Range(0, 3).Select(_ => independentGames ? Create() : sim.ForkSpeculative()).ToArray();
        forks[1].State.Plots[1].Kind = PlotKind.Peak;
        foreach (var plot in forks[2].State.Plots) plot.Route = "world:route_route_road";
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        Parallel.For(0, forks.Length, i =>
        {
            var fork = forks[i];
            var before = fork.ComputeHash();
            var ctx = NewAi(fork, "AiContext", fork, 0);
            var query = Method<Func<Unit, Hex, bool, UnitPath?>>(ctx, "FindPath");
            var unit = fork.State.UnitById(1);
            // More distinct queries than retained entries, then revisit them in reverse order.
            foreach (int destination in Enumerable.Range(1, 39).Concat(Enumerable.Range(1, 39).Reverse()))
            {
                var path = query(unit, new(destination, 0), false);
                if (i == 1) { Assert.Null(path); continue; }
                Assert.NotNull(path);
                Assert.Equal(Enumerable.Range(1, destination).Select(t => new Hex(t, 0)), path.Steps);
                Assert.Equal(destination * (i == 2 ? 60 : 120), path.Cost);
            }
            Assert.Equal(before, fork.ComputeHash());
            Assert.Equal(rng, fork.Rng.State);
        });
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Existing_garrisons_keep_defender_quality_and_unit_id_order(bool inWave)
    {
        var sim = Scenario(new(12, 8, true), game =>
        {
            game.State.Turn = 60;
            GameFactory.NeutralPolitics(game);
            foreach (int column in new[] { 4, 9 })
            {
                var city = AddCity(game, 0, column, 3);
                city.ProductionQueue.Add(new("cities:unit", "military:warrior"));
            }
            AddUnit(game, 0, "military:warrior", 4, 3);
            AddUnit(game, 0, "military:javelineer", 4, 3).Health = 90;
            AddUnit(game, 0, "military:javelineer", 4, 3).Health = 90;
            AddUnit(game, 0, "military:javelineer", 4, 3);
            AddUnit(game, 0, "military:warrior", 9, 3);
            AddUnit(game, 0, "military:warrior", 9, 3);
        });
        sim.IsParallelAiTurn = inWave;
        var rng = sim.Rng.State;
        var log = new PlannerLog();
        sim.CommandObserver = log;
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var army = NewAi(sim, "ArmyPlan", ctx);
        var cities = NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), army);
        Call(cities, "Plan");
        var attack = NewAi(sim, "AttackPlanner", ctx, NewAi(sim, "UnitOrders", ctx));
        Call(NewAi(sim, "MilitaryPlanner", ctx, army, cities, attack), "Plan");
        // Reserve the healthiest strong defender, then the lower ID on the quality/health tie.
        // Reserve the second city's defenders before resting either remaining soldier.
        Assert.Equal(new[] { "activity:4:Fortify", "activity:2:Fortify", "activity:5:Fortify",
            "activity:6:Fortify", "activity:1:Fortify", "activity:3:Fortify" }, log.Commands);
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Production_fallbacks_keep_their_order_after_a_city_changes_owner_across_variants()
    {
        var hashes = new List<ulong>();
        foreach (var (inWave, reload) in new[] { (false, false), (true, false), (false, true) })
        {
            var sim = Scenario(new(8, 4, true), game =>
            {
                game.State.Turn = 60;
                GameFactory.NeutralPolitics(game);
                foreach (int column in new[] { 1, 5 })
                {
                    var city = AddCity(game, 0, column, 2);
                    city.Population = column == 1 ? 4 : 3;
                    city.ProductionQueue.Add(new("economy:process", "economy:process_wealth"));
                    for (int i = 0; i < 6; i++) AddUnit(game, 0, "military:warrior", column, 2);
                    AddUnit(game, 0, "military:worker", column, 2);
                }
            });
            if (reload) sim = SaveGame.Load(mods.Content, SaveGame.Save(sim));
            sim.IsParallelAiTurn = inWave;
            var rng = sim.Rng.State;
            // A synchronous command callback changes ownership after views were captured. The next city's
            // production service can still rate choices, but its command validator must reject every order.
            var log = new PlannerLog(transferCity: 2);
            sim.CommandObserver = log;
            var ctx = NewAi(sim, "AiContext", sim, 0);
            Call(NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), NewAi(sim, "ArmyPlan", ctx)), "Plan");
            // Recorded before candidate pruning, including duplicate process fallbacks.
            Assert.Equal(new[]
            {
                "produce:1:cities:building/cities:sacrificial_altar",
                "rejected:produce:2:cities:building/cities:sacrificial_altar",
                "rejected:produce:2:cities:building/cities:firepit",
                "rejected:produce:2:economy:process/economy:process_wealth",
                "rejected:produce:2:economy:process/economy:process_wealth",
                "rejected:produce:2:economy:process/economy:process_culture",
                "rejected:produce:2:economy:process/economy:process_idle",
            }, log.Commands);
            Assert.Equal(rng, sim.Rng.State);
            hashes.Add(sim.ComputeHash());
        }
        Assert.All(hashes, h => Assert.Equal(hashes[0], h));
    }

    [Fact]
    public void Worker_choices_keep_order_and_budget_after_starting_a_paid_build_across_variants()
    {
        var hashes = new List<ulong>();
        foreach (var (inWave, reload) in new[] { (false, false), (true, false), (false, true) })
        {
            var sim = Scenario(new(12, 6, true), game =>
            {
                game.State.Turn = 60;
                GameFactory.NeutralPolitics(game);
                game.TeamOf(0).Techs.UnionWith(["research:agriculture", "research:the_wheel", "research:railroad"]);
                game.State.PlayerById(0).Treasury = 10;
                foreach (var plot in game.State.Plots)
                {
                    plot.Owner = 0;
                    plot.Improvement = "world:improvement_improvement_farm";
                    plot.Route = "world:route_route_road";
                }
                foreach (int column in new[] { 2, 8 })
                {
                    var city = AddCity(game, 0, column, 3);
                    var worker = AddUnit(game, 0, "military:worker", column == 2 ? 3 : 7, 3);
                    city.Worked.Add(worker.Position);
                    city.Extensions.Set(game.Content, new CityRuleState { ManualWork = true });
                }
            });
            if (reload) sim = SaveGame.Load(mods.Content, SaveGame.Save(sim));
            sim.IsParallelAiTurn = inWave;
            var rng = sim.Rng.State;
            var log = new PlannerLog();
            sim.CommandObserver = log;
            Call(NewAi(sim, "WorkerPlanner", NewAi(sim, "AiContext", sim, 0)), "Plan");
            Assert.Equal(new[] { "build:1:world:build_build_railroad", "move:2:7,3" }, log.Commands);
            Assert.Equal(0, sim.State.PlayerById(0).Treasury);
            Assert.Equal(rng, sim.Rng.State);
            hashes.Add(sim.ComputeHash());
        }
        Assert.All(hashes, h => Assert.Equal(hashes[0], h));
    }

    [Fact]
    public void Unit_choices_preserve_legal_score_ties_across_mutations_and_concurrent_forks()
    {
        var content = ModLoader.Build(mods.Sources.Append(new ModSource
        {
            Manifest = new() { Id = "choice_probe", Dependencies = ["ai", "cities", "military"] },
            Instance = new ChoiceUnits(), Root = mods.Root,
        }));
        var sim = Simulation.CreateScenario(content, GameFactory.Settings(71), new(4, 1),
            Enumerable.Range(0, 4).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                GameFactory.NeutralPolitics(game);
                AddCity(game, 0, 0, 0);
                AddUnit(game, 0, "choice_probe:limited", 0, 0);
            });
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        var forks = Enumerable.Range(0, 3).Select(_ => sim.ForkSpeculative()).ToArray();
        Parallel.ForEach(forks, fork =>
        {
            var ctx = NewAi(fork, "AiContext", fork, 0);
            var options = NewAi(fork, "AiOptions", ctx);
            var role = Enum.Parse(AiType(fork, "UnitRole"), "Melee");
            UnitDef? Best(Func<UnitDef, int> score, Func<UnitDef, bool>? filter = null) =>
                (UnitDef?)Call(options, "Best", 1, role, score,
                    new Func<UnitDef, bool>(u => u.Id.StartsWith("choice_probe:", StringComparison.Ordinal) && (filter?.Invoke(u) ?? true)));
            Func<UnitDef, int> score = u => u.Id == "choice_probe:limited" ? 100 : 10;
            Parallel.For(0, 16, _ => Assert.Equal("choice_probe:alpha", Best(score)!.Id));
            Assert.Equal("choice_probe:zeta", Best(score, u => u.Id != "choice_probe:alpha")!.Id);
            Assert.Equal("choice_probe:alpha", Best(_ => int.MinValue)!.Id);
            Assert.Null(Best(score, _ => false));
            Assert.Equal(hash, fork.ComputeHash());
            Assert.True(Method<Func<GameCommand, bool>>(ctx, "Issue")(new DisbandUnitCommand { Player = 0, Unit = 1 }));
            Assert.Equal("choice_probe:limited", Best(score)!.Id);
            AddUnit(fork, 0, "choice_probe:limited", 0, 0);
            Call(ctx, "Refresh");
            Assert.Equal("choice_probe:alpha", Best(score)!.Id);
            Assert.Equal(rng, fork.Rng.State);
        });
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    private sealed class ChoiceUnits : IMod
    {
        public void Register(IModContext context)
        {
            foreach (string name in new[] { "zeta", "limited", "alpha" })
                context.Content<UnitDef>().Register(new()
                {
                    Id = $"choice_probe:{name}", DefaultUnit = $"choice_probe:{name}",
                    Domain = "military:domain_land", Cost = 10, Strength = 1,
                    Limits = new() { Player = name == "limited" ? 1 : -1 },
                });
        }
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(11, true)]
    [InlineData(2, true)]
    public void Radius_queries_match_scans_at_edges_and_wrap_seams(int width, bool wrap)
    {
        var settings = GameFactory.Settings(71, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar",
            "peoples:leader_bismarck", "peoples:leader_gandhi") with { PlayerTeams = [0, 1, 1, 0] };
        var sim = Scenario(new(width, 8, wrap), game =>
        {
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:warrior", 0, 3);
            AddUnit(game, 1, "military:warrior", width - 1, 3);
            AddUnit(game, 1, "military:settler", width - 1, 3);
            AddUnit(game, 2, "military:javelineer", 0, 5);
            AddUnit(game, 3, "military:javelineer", 0, 3);
        }, settings);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var threat = Method<Func<Hex, int, int>>(ctx, "ThreatAround");
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        // First access is concurrent, including duplicate queries against the same lazy index.
        Parallel.ForEach(sim.State.Grid.Tiles, tile =>
        {
            var position = sim.State.Grid.HexOf(tile);
            foreach (int radius in new[] { 0, 1, 2, 3, 7 })
            {
                int expected = ScanThreat(sim, position, radius);
                Assert.Equal(expected, threat(position, radius));
                Assert.Equal(expected, threat(position, radius));
                if (wrap) Assert.Equal(expected, threat(new(position.Q + width, position.R), radius));
            }
        });
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Radius_queries_follow_movement_capture_death_war_and_refresh_without_affecting_another_game()
    {
        Simulation Create() => Scenario(new(14, 8, true), game =>
        {
            game.State.Turn = 60;
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:warrior", 0, 3);
            AddUnit(game, 1, "military:warrior", 13, 3);
            AddUnit(game, 1, "military:settler", 1, 3);
        });
        var sim = Create();
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var threat = Method<Func<Hex, int, int>>(ctx, "ThreatAround");
        var issue = Method<Func<GameCommand, bool>>(ctx, "Issue");
        var position = HexGrid.FromOffset(0, 3);
        var isolated = Create();
        var isolatedThreat = Method<Func<Hex, int, int>>(NewAi(isolated, "AiContext", isolated, 0), "ThreatAround");
        int isolatedExpected = ScanThreat(isolated, position, 1);
        var isolatedHash = isolated.ComputeHash();
        var isolatedRng = isolated.Rng.State;
        void Check()
        {
            Assert.Equal(ScanThreat(sim, position, 1), threat(position, 1));
            Assert.Equal(isolatedExpected, isolatedThreat(position, 1));
            Assert.Equal(isolatedHash, isolated.ComputeHash());
            Assert.Equal(isolatedRng, isolated.Rng.State);
        }
        Check();
        Assert.True(issue(new MoveUnitPathCommand { Player = 1, Unit = 2, Path = [HexGrid.FromOffset(12, 3)] }));
        Check();
        Assert.True(issue(new MoveUnitPathCommand { Player = 0, Unit = 1, Path = [HexGrid.FromOffset(1, 3)] }));
        Assert.DoesNotContain(sim.State.Units, u => u.Id == 3 && u.Owner == 1);
        Check();
        Assert.True(issue(new DisbandUnitCommand { Player = 1, Unit = 2 }));
        Check();
        // Direct service/state changes must be followed by an explicit segment boundary.
        var enemy = AddUnit(sim, 1, "military:warrior", 13, 3);
        Call(ctx, "Refresh");
        Check();
        enemy.Health = 0;
        Call(ctx, "Refresh");
        Check();
        enemy.Health = 100;
        Call(ctx, "Refresh");
        Check();
        var peace = (GameCommand)NewAi(sim, "MakePeaceCommand");
        Set(peace, "Player", 0);
        Set(peace, "Target", 1);
        Assert.True(issue(peace));
        Check();
    }

    [Fact]
    public void Campaign_targets_follow_war_capture_and_revelation()
    {
        var sim = Scenario(new(16, 10, true), game =>
        {
            game.State.Turn = 60;
            AddCity(game, 0, 0, 3);
            AddCity(game, 1, 15, 3);
            AddCity(game, 1, 8, 6);
        });
        var hidden = sim.State.CityById(3);
        sim.State.PlotAt(hidden.Position).Revealed.Clear();
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var targets = NewAi(sim, "CampaignTargets", ctx);
        Assert.Null(Call(targets, "SelectPrimaryTargetCity", 0));
        var war = (GameCommand)NewAi(sim, "DeclareWarCommand");
        Set(war, "Target", 1);
        Assert.True(Method<Func<GameCommand, bool>>(ctx, "Issue")(war));
        Parallel.For(0, 16, _ => Assert.Equal(2, ((City)Call(targets, "SelectPrimaryTargetCity", 0)!).Id));
        sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, 2, 0);
        Call(ctx, "Refresh");
        Assert.Null(Call(targets, "SelectPrimaryTargetCity", 0));
        sim.State.PlotAt(hidden.Position).Revealed.Add(sim.TeamOf(0).Id);
        Call(ctx, "Refresh");
        Assert.Equal(3, ((City)Call(targets, "SelectPrimaryTargetCity", 0)!).Id);
    }

    [Theory]
    [InlineData(22, false)]
    [InlineData(22, true)]
    [InlineData(2, true)]
    public void Settlement_sites_match_an_independent_spacing_and_eligibility_scan(int width, bool wrap)
    {
        var sim = Scenario(new(width, 14, wrap), game =>
        {
            AddCity(game, 0, 0, 2);
            AddCity(game, 1, width - 1, 11);
            game.State.Plots[game.State.Grid.TileAt(width / 2, 6).Value].Kind = PlotKind.Hills;

        });
        // Scenario reveals all tiles after configuration; leave one genuinely unexplored candidate.
        sim.State.Plots[sim.State.Grid.TileAt(width / 2, 8).Value].Revealed.Clear();
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var sites = NewAi(sim, "SitePlanner", ctx);
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        AssertRanked(sites);
        Assert.Equal(ScanSites(sim), ReadSites(sites));
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Settlers_require_the_empire_treasury_reserve()
    {
        var sim = Scenario(new(20, 12, true), game =>
        {
            game.State.Turn = 60;
            game.State.PlayerById(0).Treasury = 49;
            AddCity(game, 0, 4, 4);
            AddUnit(game, 0, "military:worker", 4, 4);
            AddUnit(game, 0, "military:warrior", 4, 4);
            AddUnit(game, 0, "military:warrior", 4, 4);
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var sites = NewAi(sim, "SitePlanner", ctx);

        Assert.False((bool)Call(sites, "WantsMoreCities", 0, 0)!);
        sim.State.PlayerById(0).Treasury = 50;
        Assert.True((bool)Call(sites, "WantsMoreCities", 0, 0)!);
    }
    [Fact]
    public void Settlement_sites_refresh_after_two_foundings_and_a_city_capture()
    {
        var sim = Scenario(new(24, 12, true), game =>
        {
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:settler", 1, 4);
            AddUnit(game, 0, "military:settler", 10, 4);
            AddCity(game, 1, 18, 7);
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var sites = NewAi(sim, "SitePlanner", ctx);
        var issue = Method<Func<GameCommand, bool>>(ctx, "Issue");
        Assert.NotEmpty(ReadSites(sites));
        Assert.Equal(ScanSites(sim), ReadSites(sites));
        foreach (int settler in new[] { 1, 2 })
        {
            Assert.True(issue(new FoundCityWithSettlerCommand { Player = 0, Settler = settler }));
            Assert.Equal(ScanSites(sim), ReadSites(sites));
        }
        sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, 1, 0);
        Call(ctx, "Refresh");
        Assert.Equal(3, sim.State.Cities.Count(c => c.Owner == 0));
        Assert.Equal(ScanSites(sim), ReadSites(sites));
    }

    [Fact]
    public void Counts_and_city_views_follow_assignments_and_queue_changes()
    {
        var sim = Scenario(new(12, 8, true), game =>
        {
            var city = AddCity(game, 0, 4, 4);
            foreach (var tile in game.State.Grid.Range(game.State.Grid.TileAt(city.Position), 2))
                game.State.Plots[tile.Value].Owner = 0;
            AddUnit(game, 0, "military:worker", 4, 4);
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var issue = Method<Func<GameCommand, bool>>(ctx, "Issue");
        var city = sim.State.CityById(1);
        var before = Property<YieldVector>(Call(ctx, "ViewOf", city)!, "Yields");
        Assert.True(issue(new SetCityAssignmentsCommand { Player = 0, CityId = 1, Tiles = [HexGrid.FromOffset(5, 4)] }));
        var after = Property<YieldVector>(Call(ctx, "ViewOf", city)!, "Yields");
        Assert.NotEqual(before, after);
        Assert.Equal(sim.Content.Services.Get<ICityYieldService>().Single().Calculate(sim, 1).Yields, after);
        var roleType = AiType(sim, "UnitRole");
        int Count(string method, string role) => (int)Call(ctx, method, Enum.Parse(roleType, role))!;
        Assert.Equal(1, Count("CountUnits", "Worker"));
        Assert.Equal(0, Count("CountQueued", "Melee"));
        Assert.True(issue(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:unit", ContentId = "military:warrior" }));
        Assert.Equal(1, Count("CountQueued", "Melee"));
        Assert.True(issue(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:unit", ContentId = "military:worker", Append = true }));
        Assert.Equal(1, Count("CountQueued", "Worker"));
        Assert.True(issue(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:unit", ContentId = "military:worker" }));
        Assert.Equal(0, Count("CountQueued", "Melee"));
        Assert.Equal(1, Count("CountQueued", "Worker"));
        Assert.True(issue(new DisbandUnitCommand { Player = 0, Unit = 1 }));
        Assert.Equal(0, Count("CountUnits", "Worker"));
        var hash = sim.ComputeHash();
        Assert.False(issue(new SetCityProductionCommand { Player = 1, CityId = 1, Kind = "cities:unit", ContentId = "military:warrior" }));
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(0, Count("CountQueued", "Melee"));
    }

    private static int ScanThreat(Simulation sim, Hex position, int radius) => sim.State.Units.Where(u => u.Health > 0
        && u.Owner != 0 && sim.TeamOf(0).Wars.Contains(sim.TeamOf(u.Owner).Id)
        && sim.State.Grid.Distance(u.Position, position) <= radius).Sum(u => Math.Max(1, sim.Content.Get<Civ.Content.Schema.UnitDef>(u.Type).Strength));

    // Deliberately scan all plots/cities instead of using range masks or a nearest-city search.
    // These fixtures contain only grass and optional hills, without water, rivers, features or bonuses.
    // The score below exists only to reproduce the planner's "is this site worth settling" threshold; the
    // ranking itself is not compared, because ScanSiteScore copies SitePlanner.Score's weights and a copy
    // cannot prove them.
    private static (int Tile, int Area)[] ScanSites(Simulation sim)
    {
        var grid = sim.State.Grid;
        var own = sim.State.Cities.Where(c => c.Owner == 0).ToArray();
        var anchors = own.Length > 0 ? own.Select(c => c.Position) : sim.State.Units.Where(u => u.Owner == 0).Select(u => u.Position);
        int range = sim.Content.Get<RuleConstantsDef>().All.Single().Values.MinCityRange;
        var sites = new List<(int Tile, int Area)>();
        foreach (var tile in grid.Tiles)
        {
            if (!ScanEligible(sim, tile, anchors, range)) continue;
            if (ScanSiteScore(sim, tile, own) >= 100) sites.Add((tile.Value, sim.State.Plots[tile.Value].AreaId));
        }
        return sites.OrderBy(s => s.Tile).ToArray();
    }

    private static bool ScanEligible(Simulation sim, TileId tile, IEnumerable<Hex> anchors, int range)
    {
        var plot = sim.State.Plots[tile.Value];
        var grid = sim.State.Grid;
        if (!plot.Revealed.Contains(sim.TeamOf(0).Id) || plot.Owner is > 0) return false;
        if (!anchors.Any(a => grid.Distance(grid.TileAt(a), tile) <= 9)) return false;
        return !sim.State.Cities.Any(c => grid.Distance(grid.TileAt(c.Position), tile) <= range + (c.Owner == 0 ? 1 : 2));
    }

    private static int ScanSiteScore(Simulation sim, TileId tile, City[] own)
    {
        var grid = sim.State.Grid;
        int score = 0;
        foreach (var neighbor in grid.Tiles)
        {
            int distance = grid.Distance(tile, neighbor);
            var other = sim.State.Plots[neighbor.Value];
            if (distance > 3 || other.Owner is > 0) continue;
            int value = other.Kind == PlotKind.Hills ? 12 : 8;
            if (sim.State.Cities.Any(c => grid.Distance(grid.TileAt(c.Position), neighbor) <= 2)) value /= 3;
            score += distance <= 2 ? value : value / 2;
        }
        if (sim.State.Plots[tile.Value].Kind == PlotKind.Hills) score += 14;
        int nearest = own.Select(c => grid.Distance(grid.TileAt(c.Position), tile)).DefaultIfEmpty(0).Min();
        return score - Math.Max(0, nearest - 6) * 12;
    }

    // The scores themselves are not compared (ScanSiteScore copies SitePlanner.Score's weights), but the order
    // they produce is a real contract: best site first, tile id breaking ties.
    private static void AssertRanked(object sites)
    {
        var scored = ((IEnumerable)Property<object>(sites, "Sites")).Cast<object>()
            .Select(s => (Tile: Property<TileId>(s, "Tile").Value, Score: Property<int>(s, "Score"))).ToArray();
        Assert.Equal(scored.OrderByDescending(s => s.Score).ThenBy(s => s.Tile), scored);
    }

    private static (int Tile, int Area)[] ReadSites(object sites) => ((IEnumerable)Property<object>(sites, "Sites"))
        .Cast<object>().Select(s => (Property<TileId>(s, "Tile").Value, Property<int>(s, "Area")))
        .OrderBy(s => s.Item1).ToArray();

    private Simulation Scenario(MapSpec map, Action<Simulation> configure, SimulationSettings? settings = null) => Simulation.CreateScenario(mods.Content,
        settings ?? GameFactory.Settings(71), map, Enumerable.Range(0, map.Width * map.Height)
            .Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
        {
            configure(game);
            foreach (var plot in game.State.Plots) plot.Revealed.Add(game.TeamOf(0).Id);
        });

    private static City AddCity(Simulation sim, int owner, int column, int row)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner,
            Position = HexGrid.FromOffset(column, row), Population = 3 };
        sim.State.AddCity(city);
        sim.State.PlotAt(city.Position).Owner = owner;
        return city;
    }

    private static Unit AddUnit(Simulation sim, int owner, string type, int column, int row)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type,
            Position = HexGrid.FromOffset(column, row) };
        sim.State.AddUnit(unit);
        sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        return unit;
    }

    private sealed class PlannerLog(int? transferCity = null) : ICommandObserver
    {
        public List<string> Commands { get; } = [];
        public void Before(Simulation sim, GameCommand command) => Record(command, "");
        public void After(Simulation sim, GameCommand command)
        {
            if (command is not SetCityProductionCommand || transferCity is not { } city) return;
            var old = sim.State.CityById(city);
            sim.State.RemoveCity(city);
            sim.State.AddCity(new City { Id = city, Owner = 1, Position = old.Position,
                Population = old.Population, ProductionQueue = old.ProductionQueue });
            sim.State.PlotAt(old.Position).Owner = 1;
            transferCity = null;
        }
        public void Rejected(Simulation sim, GameCommand command) => Record(command, "rejected:");
        private void Record(GameCommand command, string prefix)
        {
            string? description = command switch
            {
                SetCityProductionCommand p => $"produce:{p.CityId}:{p.Kind}/{p.ContentId}",
                StartBuildCommand b => $"build:{b.Unit}:{b.Build}",
                SetUnitActivityCommand a => $"activity:{a.Unit}:{a.Activity}",
                MoveUnitPathCommand m => $"move:{m.Unit}:{string.Join(";", m.Path.Select(h => $"{h.Q},{h.R}"))}",
                _ => null,
            };
            if (description is not null) Commands.Add(prefix + description);
        }
    }

    private sealed class MovementLog : ICommandObserver
    {
        public List<int> Units { get; } = [];
        public void Before(Simulation sim, GameCommand command)
        {
            if (command is MoveUnitPathCommand move) Units.Add(move.Unit);
        }
        public void After(Simulation sim, GameCommand command) { }
        public void Rejected(Simulation sim, GameCommand command) { }
    }

}
