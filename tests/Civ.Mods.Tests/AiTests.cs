using System.Collections.Concurrent;
using Civ.Content;
using Civ.Modding;
using Civ.Game;
using Xunit;
using Xunit.Abstractions;

namespace Civ.Mods.Tests;

public sealed class AiTests(LoadedMods mods, ITestOutputHelper output) : IClassFixture<LoadedMods>
{
    [Fact]
    [Trait("Category", "E2E")]
    public void Four_computer_players_develop_and_fight_over_200_turns()
    {
        var sim = GameFactory.CreateAi(mods.Content, seed: 17);
        int combat = 0;
        var founded = new int[4];
        var completed = new int[4];
        var owners = new Dictionary<int, int>();
        for (int turn = 0; turn < 200; turn++)
        {
            sim.AdvanceTurn();
            combat += sim.Events.PreviousTurn.OfType<CombatResolved>().Count();
            CountDevelopment(sim.Events.PreviousTurn, owners, founded, completed);
            if (turn % 25 == 24) Metrics(sim, $"turn {sim.State.Turn}");
        }
        combat += sim.Events.Published.OfType<CombatResolved>().Count();
        output.WriteLine($"Cities founded per AI: {string.Join(", ", founded)}");
        output.WriteLine($"Buildings completed per AI: {string.Join(", ", completed)}");
        output.WriteLine($"Combats: {combat}");
        foreach (var player in sim.State.Players.Where(p => p.Ai == "ai:standard"))
        {
            Assert.True(founded[player.Id] >= 1, $"player {player.Id}: founded no city");
            Assert.True(sim.TeamOf(player.Id).Techs.Count >= 15, $"player {player.Id}: {sim.TeamOf(player.Id).Techs.Count} technologies");
            Assert.True(completed[player.Id] > 0, $"player {player.Id} completed no buildings");
        }
        Assert.True(sim.State.Cities.Count(c => c.Owner is >= 0 and < 4) >= 4, "Fewer than four civilization cities survived");
        Assert.True(sim.State.Cities.Where(c => c.Owner is >= 0 and < 4).Select(c => c.Owner).Distinct().Count() >= 3,
            "Fewer than three civilizations survived");
        Assert.True(founded.Sum() > founded.Length, "No AI founded a city beyond its initial capital");
        Assert.True(combat > 0, "No combat occurred");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Identical_games_and_a_midgame_save_have_equal_hashes_at_every_turn()
    {
        var a = GameFactory.CreateAi(mods.Content, seed: 5);
        var b = GameFactory.CreateAi(mods.Content, seed: 5);
        Simulation? saved = null;
        for (int turn = 0; turn <= 150; turn++)
        {
            Assert.Equal(a.ComputeHash(), b.ComputeHash());
            if (turn == 75) saved = SaveGame.Load(mods.Content, SaveGame.Save(a));
            if (saved is not null) Assert.Equal(a.ComputeHash(), saved.ComputeHash());
            if (turn == 150) break;
            a.AdvanceTurn(); b.AdvanceTurn(); saved?.AdvanceTurn();
        }
        Metrics(a, "determinism turn 151");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Speculative_games_and_midrun_save_match_sequential_hashes_for_twenty_turns()
    {
        var a = GameFactory.CreateAi(mods.Content, seed: 23);
        var b = GameFactory.CreateAi(mods.Content, seed: 23);
        var sequential = SaveGame.Load(mods.Content, SaveGame.Save(a));
        sequential.AiWaveLimit = 1;
        for (int turn = 0; turn <= 20; turn++)
        {
            Assert.Equal(sequential.ComputeHash(), a.ComputeHash());
            Assert.Equal(a.ComputeHash(), b.ComputeHash());
            if (turn == 10) b = SaveGame.Load(mods.Content, SaveGame.Save(b));
            Assert.Equal(a.ComputeHash(), b.ComputeHash());
            if (turn == 20) break;
            a.AdvanceTurn(); b.AdvanceTurn(); sequential.AdvanceTurn();
        }
        Assert.All(a.State.Players.Take(4), p => Assert.NotNull(p.Ai));
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Speculative_and_sequential_turns_match_across_the_war_planning_threshold()
    {
        var sim = Simulation.CreateScenario(mods.Content,
            GameFactory.Settings(71) with { PlayerAis = ["ai:standard", "ai:standard"] }, new(40, 12),
            Enumerable.Range(0, 480).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                game.State.Turn = 44;
                GameFactory.NeutralPolitics(game);
                foreach (int owner in new[] { 0, 1 })
                {
                    var city = new City { Id = game.State.NextCityId++, Owner = owner, Position = new(5 + owner * 12, 5) };
                    game.State.AddCity(city); game.State.PlotAt(city.Position).Owner = owner;
                    for (int i = 0; i < (owner == 0 ? 12 : 1); i++)
                    {
                        var unit = new Unit { Id = game.State.NextUnitId++, Owner = owner,
                            Type = owner == 0 ? "military:javelineer" : "military:warrior", Position = city.Position };
                        game.State.AddUnit(unit); Service<IUnitService>(game).Initialize(game, unit.Id);
                    }
                }
                foreach (var plot in game.State.Plots) { plot.Revealed.Add(0); plot.Revealed.Add(1); }
            });
        var sequential = SaveGame.Load(mods.Content, SaveGame.Save(sim)); sequential.AiWaveLimit = 1;
        Assert.Empty(sim.TeamOf(0).Wars);
        bool declared = false;
        for (int i = 0; i < 20; i++)
        {
            sim.AdvanceTurn(); sequential.AdvanceTurn();
            Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
            declared |= sim.TeamOf(0).Wars.Contains(1);
            if (i == 9) sim = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        }
        Assert.True(declared, "The fixture must exercise war target selection and the declaration roll after turn 45");
    }

    [Fact]
    public void Peaceful_settlers_contesting_one_site_replan_the_second_founding_like_sequential_turns()
    {
        var probe = new SettlementProbe();
        var content = ModLoader.Build(mods.Sources.Append(new ModSource
        {
            Manifest = new() { Id = "settlement_probe", Dependencies = ["cities", "military"] },
            Instance = probe, Root = mods.Root,
        }));
        var events = new List<SpeculationEvent>();
        Simulation Create(int width) => Simulation.CreateScenario(content,
            GameFactory.Settings(71) with { PlayerAis = [probe.Id, probe.Id] }, new(20, 12),
            Enumerable.Range(0, 240).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                game.AiWaveLimit = width;
                game.SpeculationObserver = events.Add;
                foreach (int owner in new[] { 0, 1 })
                {
                    var unit = new Unit { Id = game.State.NextUnitId++, Owner = owner,
                        Type = "military:settler", Position = new(7, 5) };
                    game.State.AddUnit(unit); Service<IUnitService>(game).Initialize(game, unit.Id);
                }
            });
        var sim = Create(int.MaxValue);
        Assert.Contains(probe.Decisions, d => d == (1, true));
        Assert.Contains(probe.Decisions, d => d == (1, false));
        var conflict = Assert.Single(events, e => e.Outcome == SpeculationOutcome.Conflict);
        Assert.Equal(1, conflict.Player); Assert.Equal("city id", conflict.Reason);
        Assert.Contains(events, e => e.Player == 1 && e.Wave == 2 && e.Outcome == SpeculationOutcome.Direct);
        var city = Assert.Single(sim.State.Cities);
        Assert.Equal(0, city.Owner); Assert.Equal(new Hex(7, 5), city.Position);
        Assert.Equal(UnitActivity.Sleep, sim.State.UnitById(2).Extensions.Get<MilitaryUnitState>(content)!.Activity);
        Assert.All(sim.State.Teams, t => Assert.Empty(t.Wars));
        var sequential = Create(1);
        Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
    }

    [Fact]
    public void Concurrent_players_do_not_start_nested_candidate_scoring_tasks()
    {
        var probe = new ScoringProbe();
        var content = ModLoader.Build(mods.Sources.Append(new ModSource
        {
            Manifest = new() { Id = "scoring_probe", Dependencies = ["ai", "cities"] }, Instance = probe, Root = mods.Root,
        }));
        var sim = Simulation.CreateScenario(content,
            GameFactory.Settings(71) with { PlayerAis = [probe.Id, probe.Id] }, new(60, 12),
            Enumerable.Range(0, 720).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                game.State.Turn = 50;
                GameFactory.NeutralPolitics(game);
                foreach (int owner in new[] { 0, 1 })
                {
                    var city = new City { Id = game.State.NextCityId++, Owner = owner, Position = new(4 + owner * 30, 5) };
                    game.State.AddCity(city); game.State.PlotAt(city.Position).Owner = owner;
                    for (int i = 0; i < 4; i++)
                    {
                        var unit = new Unit { Id = game.State.NextUnitId++, Owner = owner, Type = "military:warrior", Position = city.Position };
                        game.State.AddUnit(unit); Service<IUnitService>(game).Initialize(game, unit.Id);
                    }
                }
            });
        Assert.True(probe.Checked > 0, "The fixture must reach real building candidate scoring");
        Assert.Empty(probe.NestedTasks);
        Assert.All(sim.State.Players.Take(2), p => Assert.True(p.EndedTurn));
    }

    private sealed class ScoringProbe : IMod, IPlayerAI, IRequirementEvaluator
    {
        private readonly AsyncLocal<int?> _parent = new();
        private int _decisions, _checked;
        public int Checked => _checked;
        public ConcurrentBag<int?> NestedTasks { get; } = new();
        public string Id => "scoring_probe:ai";
        public string Kind => "scoring-task";
        public void Register(IModContext context)
        {
            context.PlayerAIs.Register(this);
            context.RegisterService<IRequirementEvaluator>("scoring", this);
            for (int i = 0; i < 16; i++) context.Content<Civ.Content.Schema.BuildingDef>().Register(new()
            {
                Id = $"scoring_probe:building{i}", DefaultBuilding = $"scoring_probe:building{i}", Cost = 40,
                Requirements = new Requirement.Leaf(Kind, "probe"),
            });
        }
        public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
        {
            // Only the initial wave participates; a conflict can later run one player outside a wave.
            _parent.Value = Interlocked.Increment(ref _decisions) <= 2 ? Task.CurrentId : null;
            try { return sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, playerId).ToArray(); }
            finally { _parent.Value = null; }
        }
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
        {
            if (_parent.Value is { } parent)
            {
                Interlocked.Increment(ref _checked);
                if (Task.CurrentId != parent) NestedTasks.Add(Task.CurrentId);
            }
            return null;
        }
    }

    private sealed class SettlementProbe : IMod, IPlayerAI
    {
        public string Id => "settlement_probe:ai";
        public ConcurrentBag<(int Player, bool CanFound)> Decisions { get; } = new();
        public void Register(IModContext context) => context.PlayerAIs.Register(this);
        public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
        {
            var settler = sim.State.Units.Single(u => u.Owner == playerId);
            var found = new FoundCityWithSettlerCommand { Player = playerId, Settler = settler.Id };
            bool canFound = found.Validate(sim) is null;
            Decisions.Add((playerId, canFound));
            return canFound ? [found] : [new SetUnitActivityCommand { Player = playerId, Unit = settler.Id, Activity = UnitActivity.Sleep }];
        }
    }

    [Fact]
    public void Strong_army_declares_war_and_captures_a_weak_neighbour()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var own = new City { Id = 1, Owner = 0, Position = sim.State.Grid.HexOf(new(39)), Population = 4 };
        var enemy = new City { Id = 2, Owner = 1, Position = own.Position.Neighbor(0).Neighbor(0).Neighbor(0).Neighbor(0).Neighbor(0), Population = 3 };
        // Preserve its growth history if the isolated enemy starves before the army arrives.
        enemy.Extensions.Set(sim.Content, new CityRuleState { HighestPopulation = enemy.Population });
        sim.State.AddCity(own); sim.State.AddCity(enemy); sim.State.NextCityId = 3;
        sim.State.PlotAt(enemy.Position).Owner = 1;
        sim.State.PlotAt(enemy.Position).Revealed.Add(sim.TeamOf(0).Id);
        sim.TeamOf(0).Techs.UnionWith(["research:woodworking", "research:siege_warfare"]);
        var units = sim.Content.Services.Get<IUnitService>().Single();
        for (int i = 0; i < 15; i++)
        {
            var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Position = own.Position,
                Type = i < 12 ? "military:javelineer" : "military:catapult" };
            sim.State.AddUnit(unit); units.Initialize(sim, unit.Id);
        }
        var defender = new Unit { Id = sim.State.NextUnitId++, Owner = 1, Position = enemy.Position, Type = "military:warrior" };
        sim.State.AddUnit(defender); units.Initialize(sim, defender.Id);
        sim.State.Turn = 50;
        bool declared = false; int combats = 0;
        for (int i = 0; i < 100 && sim.State.CityById(enemy.Id).Owner != 0; i++)
        {
            sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
            declared |= sim.TeamOf(0).Wars.Contains(sim.TeamOf(1).Id);
            combats += sim.Content.Services.Get<ICombatService>().Single().History(sim).Count;
            sim.AdvanceTurn();
        }
        Assert.True(declared, "The stronger army never declared war");
        Assert.Equal(0, sim.State.CityById(enemy.Id).Owner);
        Assert.True(combats > 0);
    }

    [Theory]
    [InlineData("water", 100, true)]
    [InlineData("unimproved-land", 100, false)]
    [InlineData("improved-land", 100, true)]
    [InlineData("water", 0, false)]
    public void Route_upgrade_waits_only_for_unimproved_worker_improvable_land(string workedTile, long treasury, bool upgrades)
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim);
        foreach (var plot in sim.State.Plots) plot.Owner = 1;
        var start = sim.State.Grid.HexOf(new TileId(39));
        var home = City(sim, 39);
        var routeHex = start.Neighbor(0);
        var end = routeHex.Neighbor(0);
        Assert.True(sim.State.Grid.TryTile(routeHex, out var routeTile));
        Assert.True(sim.State.Grid.TryTile(end, out var endTile));
        City(sim, endTile.Value);
        foreach (var route in new[] { new TileId(39), routeTile, endTile })
        {
            var plot = sim.State.Plots[route.Value];
            plot.Owner = 0; plot.Route = "world:route_route_road"; plot.Revealed.Add(sim.TeamOf(0).Id);
        }
        var target = sim.State.Plots[routeTile.Value];
        target.Improvement = "world:improvement_improvement_farm";
        home.Worked.Add(sim.State.Grid.HexOf(routeTile));
        var blocker = new TileId(50);
        var workedPlot = sim.State.Plots[blocker.Value];
        workedPlot.Owner = 0;
        if (workedTile == "water") workedPlot.Terrain = "world:coast";
        if (workedTile == "improved-land") workedPlot.Improvement = "world:improvement_improvement_farm";
        home.Worked.Add(sim.State.Grid.HexOf(blocker));
        home.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        sim.TeamOf(0).Techs.Add("research:railroad");
        sim.State.PlayerById(0).Treasury = treasury;
        var worker = Add(sim, "military:worker", sim.State.Grid.HexOf(routeTile));
        var preview = Service<IWorkerBuildService>(sim).Preview(sim, 0, worker.Id, routeTile, "world:build_build_railroad");
        Assert.Equal(treasury >= 10 ? null : "Not enough gold", preview.Error);

        Decide(sim);

        Assert.Equal(treasury - (upgrades ? 10 : 0), sim.State.PlayerById(0).Treasury);
        sim.AdvanceTurn();
        Assert.Equal(upgrades, sim.State.Plots[routeTile.Value].Route == "world:route_route_railroad"
            || sim.State.Plots[routeTile.Value].BuildProgress.ContainsKey("world:build_build_railroad"));
    }

    [Fact]
    public void Worker_repairs_ruins_with_a_farm_on_a_worked_freshwater_tile()
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim);
        var city = City(sim, 39);
        var tile = new TileId(40);
        var plot = sim.State.Plots[tile.Value];
        plot.Owner = 0;
        plot.Improvement = "world:city_ruins";
        sim.State.SetRiver(tile, 0, true);
        city.Worked.Add(sim.State.Grid.HexOf(tile));
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        sim.TeamOf(0).Techs.Add("research:agriculture");
        var worker = Add(sim, "military:worker", sim.State.Grid.HexOf(tile));

        Decide(sim);

        sim.AdvanceTurn();
        Assert.True(plot.Improvement == "world:improvement_improvement_farm"
            || plot.BuildProgress.ContainsKey("world:build_build_farm"));
    }

    [Fact]
    public void Production_emphasis_still_feeds_the_city_at_the_content_consumption_rate()
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        var center = sim.State.Grid.TileAt(3, 3);
        var city = new City { Id = 1, Owner = 0, Population = 3, Position = sim.State.Grid.HexOf(center) };
        sim.State.AddCity(city); sim.State.NextCityId = 2;
        foreach (var tile in sim.State.Grid.Range(center, 2)) sim.State.Plots[tile.Value].Feature = "world:forest";
        foreach (var tile in sim.State.Grid.Neighbors(center).Take(2)) sim.State.Plots[tile.Value].Kind = PlotKind.Hills;
        var food = sim.State.Grid.Neighbors(center).Last();
        sim.State.Plots[food.Value].Feature = "world:flood_plains";
        sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
        var growth = sim.Content.Services.Get<ICityGrowthService>().Single().Calculate(sim, city.Id);
        Assert.True(growth.Difference >= 0, $"Production assignments leave {growth.Difference} food per turn");
        Assert.True(sim.Content.Services.Get<ICityWorkService>().Single().Validate(sim, city.Id, city.Worked, city.Specialists) is null);
    }

    [Fact]
    public void Rich_player_uses_the_highest_research_rate_that_pays_recurring_costs()
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        var city = new City { Id = 1, Owner = 0, Population = 3, Position = sim.State.Grid.HexOf(new(39)), Buildings = ["cities:palace"] };
        sim.State.AddCity(city); sim.State.NextCityId = 2;
        sim.State.PlayerById(0).Treasury = 10000;
        // Noble grants fourteen free units here. Exceed that allowance so the sliders must fund upkeep.
        for (int i = 0; i < 20; i++) sim.State.AddUnit(new Unit
        {
            Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = city.Position,
        });
        sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
        var economy = sim.Content.Services.Get<IPlayerEconomyService>().Single();
        var rates = economy.Rates(sim, 0);
        Assert.True(economy.Calculate(sim, 0).NetGold >= 0);
        Assert.True(rates.Gold > 0, "Fixture must have recurring costs that require gold commerce");
        Assert.True(sim.TryApply(new SetCommerceRatesCommand
        {
            Player = 0, Rates = new(rates.Gold - 5, rates.Research + 5, 0, 0),
        }, out var error), error);
        Assert.True(economy.Calculate(sim, 0).NetGold < 0, "The AI left an affordable five-point research increase unused");
    }

    [Fact]
    public void War_commands_roundtrip_and_rejected_early_peace_does_not_change_state()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var codec = new CommandCodec(mods.Content);
        var war = codec.Decode("{\"type\":\"ai:declare_war\",\"data\":{\"player\":0,\"target\":1}}");
        Assert.True(sim.TryApply(codec.Decode(codec.Encode(war)), out var error), error);
        Assert.Contains(1, sim.TeamOf(0).Wars); Assert.Contains(0, sim.TeamOf(1).Wars);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        var peace = codec.Decode("{\"type\":\"ai:make_peace\",\"data\":{\"player\":0,\"target\":1}}");
        ulong before = sim.ComputeHash();
        Assert.False(sim.TryApply(peace, out _));
        Assert.Equal(before, sim.ComputeHash());
        Assert.Equal(before, loaded.ComputeHash());
        sim.State.Turn += 12; loaded.State.Turn += 12;
        Assert.True(sim.TryApply(peace, out error), error);
        Assert.True(loaded.TryApply(peace, out error), error);
        Assert.Empty(sim.TeamOf(0).Wars); Assert.Empty(sim.TeamOf(1).Wars);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Peace_treaty_blocks_redeclaring_war_for_ten_turns_and_survives_save_load()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var codec = new CommandCodec(mods.Content);
        var war = codec.Decode("{\"type\":\"ai:declare_war\",\"data\":{\"player\":0,\"target\":1}}");
        var peace = codec.Decode("{\"type\":\"ai:make_peace\",\"data\":{\"player\":0,\"target\":1}}");
        Assert.True(sim.TryApply(war, out var error), error);
        sim.State.Turn += 12; // Minimum war duration before a treaty.
        Assert.True(sim.TryApply(peace, out error), error);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        ulong before = sim.ComputeHash();
        Assert.False(sim.TryApply(war, out error)); Assert.Contains("peace treaty", error);
        Assert.Equal(before, sim.ComputeHash());
        Assert.False(loaded.TryApply(war, out var loadedError)); Assert.Equal(error, loadedError);
        sim.State.Turn += 9; loaded.State.Turn += 9; // Nine turns after peace is still inside the cooldown.
        Assert.False(sim.TryApply(war, out _)); Assert.False(loaded.TryApply(war, out _));
        sim.State.Turn += 1; loaded.State.Turn += 1;
        Assert.True(sim.TryApply(war, out error), error);
        Assert.True(loaded.TryApply(war, out loadedError), loadedError);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(5, false)]
    public void Loaded_transport_delivers_an_overseas_assault(int departureX, bool captures)
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var hex = sim.State.Grid.HexOf(tile);
            var plot = sim.State.Plots[tile.Value];
            int x = tile.Value % 12;
            plot.Terrain = x is >= 4 and < 8 ? "world:coast" : "world:grass";
            plot.AreaId = x < 4 ? 0 : x < 8 ? 1 : 2;
            plot.Owner = x < 4 ? 0 : x >= 8 ? 1 : null;
            plot.Revealed.Add(0); plot.Visible.Add(0);
        }
        var home = new City { Id = 1, Owner = 0, Position = sim.State.Grid.HexOf(sim.State.Grid.TileAt(3, 3)), Population = 3 };
        var target = new City { Id = 2, Owner = 1, Position = sim.State.Grid.HexOf(sim.State.Grid.TileAt(8, 3)), Population = 3 };
        sim.State.AddCity(home); sim.State.AddCity(target); sim.State.NextCityId = 3;
        sim.State.SetWar(0, 1, true);
        sim.State.Turn = 30;
        var ship = new Unit { Id = 1, Owner = 0, Type = "military:galley", Position = sim.State.Grid.HexOf(sim.State.Grid.TileAt(departureX, 3)) };
        var departure = ship.Position;
        var cargo = new Unit { Id = 2, Owner = 0, Type = "military:warrior", Position = ship.Position };
        sim.State.AddUnit(ship); sim.State.AddUnit(cargo); sim.State.NextUnitId = 3;
        var units = sim.Content.Services.Get<IUnitService>().Single();
        units.Initialize(sim, ship.Id); units.Initialize(sim, cargo.Id);
        Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = cargo.Id, Transport = ship.Id }, out var error), error);
        sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
        Assert.Null(cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        if (captures)
        {
            Assert.Equal(0, sim.State.CityById(target.Id).Owner); Assert.Equal(target.Position, cargo.Position);
        }
        else
        {
            Assert.NotEqual(departure, ship.Position); Assert.Equal(1, sim.State.Grid.Distance(target.Position, cargo.Position));
            Assert.Equal(2, sim.State.PlotAt(cargo.Position).AreaId);
        }
    }

    [Fact]
    public void Idle_fighter_provides_interception_cover()
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        var city = new City { Id = 1, Owner = 0, Position = sim.State.Grid.HexOf(new(39)) };
        sim.State.AddCity(city); sim.State.NextCityId = 2;
        var fighter = new Unit { Id = 1, Owner = 0, Type = "military:fighter", Position = city.Position };
        sim.State.AddUnit(fighter); sim.State.NextUnitId = 2;
        sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, fighter.Id);
        sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
        Assert.Equal(UnitActivity.Intercept, fighter.Extensions.Get<MilitaryUnitState>(sim.Content)!.Activity);
    }

    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static City City(Simulation sim, int tile = 39, int owner = 0, int population = 3)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new(tile)), Population = population };
        sim.State.AddCity(city); sim.State.PlotAt(city.Position).Owner = owner; return city;
    }
    private static Unit Add(Simulation sim, string type, Hex position, int owner = 0)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = position };
        sim.State.AddUnit(unit); Service<IUnitService>(sim).Initialize(sim, unit.Id); Service<IVisibilityService>(sim).Recompute(sim); return unit;
    }
    private static void Decide(Simulation sim) => sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();

    [Fact]
    public void Religious_ai_adopts_a_faith_then_sends_missionaries_and_purges_minorities()
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim);
        var home = City(sim); var target = City(sim, 41);
        home.Extensions.Set(sim.Content, new ReligionCityState { Religions = ["religion:buddhism"] });
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:intolerant";
        Decide(sim);
        Assert.Equal("religion:buddhism", Service<IReligionService>(sim).StateReligion(sim, 0));
        sim.AdvanceTurn(); // Finish conversion anarchy before religious unit orders.
        var missionary = Add(sim, "military:buddhist_missionary", target.Position);
        Decide(sim);
        Assert.Null(sim.State.FindUnit(missionary.Id)); Assert.Contains("religion:buddhism", Service<IReligionService>(sim).Present(sim, target.Id));
        target.Extensions.Get<ReligionCityState>(sim.Content)!.Religions.Add("religion:hinduism");
        var inquisitor = Add(sim, "military:inquisitor", target.Position);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Decide(sim); Decide(loaded);
        Assert.Null(sim.State.FindUnit(inquisitor.Id));
        Assert.Contains(sim.Events.Published.OfType<InquisitionAttempt>(), e => e.Unit == inquisitor.Id && e.City == target.Id);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void General_ai_attaches_to_the_strongest_available_soldier_at_war()
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim); var home = City(sim);
        var general = Add(sim, "military:great_general", home.Position);
        var weak = Add(sim, "military:warrior", home.Position); var strong = Add(sim, "military:javelineer", home.Position);
        sim.State.SetWar(0, 1, true);
        Decide(sim);
        Assert.Null(sim.State.FindUnit(general.Id));
        Assert.Equal("military:great_general", strong.Extensions.Get<MilitaryUnitState>(sim.Content)!.AttachedLeaderUnitType);
        Assert.Null(weak.Extensions.Get<MilitaryUnitState>(sim.Content)!.AttachedLeaderUnitType);
        Assert.True(weak.Experience > 0); Assert.True(strong.Experience > 0);
    }

    [Fact]
    public void Merchant_ai_executes_a_profitable_foreign_trade_mission()
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim);
        var home = City(sim); home.Buildings.Add("cities:palace"); var foreign = City(sim, 40, 1);
        var merchant = Add(sim, "military:merchant", foreign.Position);
        long before = sim.State.PlayerById(0).Treasury;
        Decide(sim);
        Assert.Null(sim.State.FindUnit(merchant.Id));
        Assert.True(sim.State.PlayerById(0).Treasury > before + sim.Content.Get<Civ.Content.Schema.UnitDef>(merchant.Type).BaseTrade);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scientist_ai_selects_a_bulb_or_golden_age_from_empire_value(bool largeEmpire)
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim);
        var home = City(sim, population: largeEmpire ? 100 : 1);
        home.Buildings.AddRange(sim.Content.Get<Civ.Content.Schema.UnitDef>("military:scientist").ConstructibleBuildings.Keys);
        // Special buildings are already present; compare immediate alternatives.
        var scientist = Add(sim, "military:scientist", home.Position);
        if (!largeEmpire) sim.State.PlayerById(0).Extensions.Set(sim.Content, new GreatPeoplePlayerState { GoldenAgeTurns = 2 });
        sim.State.Turn = 100;
        var techs = sim.TeamOf(0).Techs.ToHashSet();
        Decide(sim);
        Assert.Null(sim.State.FindUnit(scientist.Id));
        if (largeEmpire) Assert.True(Service<IGreatPeopleService>(sim).GoldenAgeTurnsRemaining(sim, 0) > 0);
        else Assert.Contains(sim.Events.Published.OfType<TechDiscovered>(), e => !techs.Contains(e.Tech));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Combined_arms_ai_uses_support_fire_before_melee(bool defendedCity)
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim); City(sim, 37);
        var target = sim.State.Grid.HexOf(new(40));
        if (defendedCity) { var city = City(sim, 40, 1); city.Buildings.Add("cities:walls"); }
        var siege = Add(sim, "military:catapult", sim.State.Grid.HexOf(new(39)));
        var plane = Add(sim, "military:bomber", sim.State.Grid.HexOf(new(38)));
        var defender = Add(sim, "military:warrior", target, 1);
        for (int i = 0; i < 5; i++) Add(sim, "military:javelineer", siege.Position);
        sim.State.SetWar(0, 1, true); sim.State.Turn = 30;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Decide(sim); Decide(loaded);
        Assert.True(siege.Extensions.Get<MilitaryUnitState>(sim.Content)!.Attacked);
        Assert.Equal(0, plane.MovesLeft);
        var events = sim.Events.Published.ToList();
        Assert.Contains(events.OfType<AirMissionResolved>(), e => e.Report.Kind == (defendedCity ? AirMissionKind.AirBomb : AirMissionKind.AirStrike));
        if (defendedCity) Assert.Contains(events.OfType<CityBombarded>(), e => e.Unit == siege.Id);
        else
        {
            var reports = Service<ICombatService>(sim).History(sim);
            Assert.Equal(siege.Id, reports[0].Attacker); Assert.Single(reports[0].Rounds);
            Assert.Equal(100, reports[0].Rounds[0].AttackerHealth);
            Assert.Equal(sim.State.Grid.HexOf(new(39)), siege.Position);
            Assert.Contains(reports.Skip(1), r => r.Attacker != siege.Id);
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    private static void CountDevelopment(IEnumerable<DomainEvent> events, Dictionary<int, int> owners, int[] founded, int[] completed)
    {
        foreach (var message in events)
        {
            switch (message)
            {
                case CityFounded city:
                    owners[city.City] = city.Player;
                    if (city.Player < founded.Length) founded[city.Player]++;
                    break;
                case CityCaptured city:
                    owners[city.City] = city.Player;
                    break;
                case BuildingCompleted building:
                    if (owners.TryGetValue(building.City, out var owner) && owner < completed.Length) completed[owner]++;
                    break;
            }
        }
    }

    private void Metrics(Simulation sim, string label)
    {
        foreach (var p in sim.State.Players.Where(p => p.Ai == "ai:standard"))
            output.WriteLine($"{label} player {p.Id}: cities={sim.State.Cities.Count(c => c.Owner == p.Id)} " +
                $"techs={sim.TeamOf(p.Id).Techs.Count} units={sim.State.Units.Count(u => u.Owner == p.Id)} " +
                $"buildings={sim.State.Cities.Where(c => c.Owner == p.Id).Sum(c => c.Buildings.Count)} " +
                $"wars={sim.TeamOf(p.Id).Wars.Count} gold={p.Treasury}");
    }
}
