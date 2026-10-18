using System.Collections.Concurrent;
using Civ.BaseGame;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;

namespace Civ.Tests;

public sealed class AiSpeculationTests
{
    [Fact]
    public void Clone_preserves_exact_hash_and_isolates_mutable_state_and_indexes()
    {
        var source = Game(2, (_, _) => []);
        var state = source.State;
        var plot = state.Plots[0];
        plot.BuildProgress["road"] = 7; plot.Culture[0] = 11;
        plot.Revealed.Add(0); plot.Visible.Add(0);
        plot.RevealedSnapshots[0] = new("base:grassland", PlotKind.Hills, false, null, null, null, null, 0);
        state.PlayerById(0).Civics["rule"] = "old";
        state.TeamOf(0).Research["base:writing"] = 17;
        state.UnitById(1).Promotions.Add("old");
        var city = new City { Id = state.NextCityId++, Owner = 0, Position = new(5, 5),
            Buildings = ["old"], Worked = [new(6, 5)], Specialists = new() { ["old"] = 1 },
            ProductionQueue = [new("unit", "base:warrior", 3)] };
        state.AddCity(city);
        foreach (var extensions in Extensions(state))
            extensions.Set(source.Content, new Memory { Samples = [3, 7], Values = new() { ["old"] = 9 } });
        ulong original = source.ComputeHash();
        var clone = Copy(source);
        Assert.Equal(original, clone.ComputeHash());

        var copy = clone.State;
        copy.Plots[0].Terrain = "base:plains";
        copy.Plots[0].BuildProgress["road"] = 99; copy.Plots[0].Culture[0] = 99;
        copy.Plots[0].Revealed.Add(65); copy.Plots[0].Visible.Clear(); copy.Plots[0].RevealedSnapshots.Clear();
        copy.PlayerById(0).Treasury = 99; copy.PlayerById(0).Civics["rule"] = "new";
        copy.TeamOf(0).Techs.Add("new"); copy.TeamOf(0).Research["base:writing"] = 99;
        copy.TeamOf(0).Members.Add(99); copy.SetWar(0, 1, true);
        copy.CityById(city.Id).Buildings.Add("new"); copy.CityById(city.Id).Worked.Clear();
        copy.CityById(city.Id).Specialists["old"] = 99; copy.CityById(city.Id).ProductionQueue.Clear();
        copy.UnitById(1).Health = 42; copy.UnitById(1).Promotions.Add("new");
        copy.MoveUnit(1, new(8, 5));
        foreach (var extensions in Extensions(copy))
        {
            var memory = extensions.Get<Memory>(source.Content)!;
            memory.Samples.Add(99); memory.Values["old"] = 99;
        }
        copy.RemoveCity(city.Id);
        copy.AddCity(new() { Id = city.Id, Owner = 1, Position = new(9, 5) });

        Assert.Equal(original, source.ComputeHash());
        Assert.NotEqual(original, clone.ComputeHash());
        Assert.Equal(7, plot.BuildProgress["road"]); Assert.Equal(11, plot.Culture[0]);
        Assert.False(plot.Revealed.Contains(65)); Assert.True(plot.Visible.Contains(0));
        Assert.Single(plot.RevealedSnapshots);
        Assert.Equal("old", state.PlayerById(0).Civics["rule"]);
        Assert.Equal(17, state.TeamOf(0).Research["base:writing"]); Assert.Empty(state.TeamOf(0).Wars);
        Assert.Equal(new[] { "old" }, city.Buildings); Assert.Single(city.Worked); Assert.Single(city.ProductionQueue);
        Assert.Equal(1, city.Specialists["old"]); Assert.Equal(100, state.UnitById(1).Health);
        Assert.Equal(new[] { "old" }, state.UnitById(1).Promotions);
        Assert.Same(state.UnitById(1), Assert.Single(state.UnitsAt(new(2, 2))));
        Assert.Empty(copy.UnitsAt(new(2, 2)));
        Assert.Same(copy.UnitById(1), Assert.Single(copy.UnitsAt(new(8, 5))));
        Assert.Same(city, state.CityAt(new(5, 5))); Assert.Null(copy.CityAt(new(5, 5)));
        Assert.Same(copy.CityById(city.Id), copy.CityAt(new(9, 5)));
        foreach (var extensions in Extensions(state))
        {
            var memory = extensions.Get<Memory>(source.Content)!;
            Assert.Equal(new[] { 3, 7 }, memory.Samples); Assert.Equal(9, memory.Values["old"]);
        }
        state.MoveUnit(1, new(3, 2));
        Assert.Equal(new Hex(8, 5), copy.UnitById(1).Position);
    }

    [Fact]
    public void Peaceful_stationary_border_cities_and_garrisons_allow_one_wave_for_distant_moves()
    {
        var calls = new ConcurrentBag<int>();
        var sim = Game(2, (game, player) =>
        {
            calls.Add(player);
            return [Move(game, player)];
        });
        for (int player = 0; player < 2; player++)
        {
            var position = new Hex(10 + player * 3, 7);
            sim.State.AddCity(new() { Id = sim.State.NextCityId++, Owner = player, Position = position });
            sim.State.AddUnit(new() { Id = sim.State.NextUnitId++, Owner = player, Type = "base:warrior", Position = position });
            sim.State.PlotAt(position).Owner = player;
        }
        var sequential = Copy(sim); sequential.AiWaveLimit = 1;
        var events = Observe(sim);
        sim.RunAi();
        Assert.Equal(new[] { 0, 1 }, calls.Order());
        Assert.Equal(new[] { SpeculationOutcome.Direct, SpeculationOutcome.Committed }, events.Select(e => e.Outcome));
        Assert.All(events, e => Assert.Equal(1, e.Wave));
        Assert.All(sim.State.Teams, t => Assert.Empty(t.Wars));
        Assert.Equal(3, sim.State.Grid.Distance(sim.State.Cities[0].Position, sim.State.Cities[1].Position));
        Assert.True(sim.State.Grid.Distance(sim.State.UnitById(1).Position, sim.State.UnitById(2).Position) > 5);
        sequential.RunAi(); Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
    }

    [Fact]
    public void Nearby_active_moves_relaunch_all_remaining_players_concurrently_from_post_commit_state()
    {
        using var rendezvous = new CountdownEvent(3);
        var snapshots = new ConcurrentBag<(int Player, Hex First, Hex Second)>();
        bool synchronize = true;
        var sim = Game(4, (game, player) =>
        {
            var first = game.State.UnitById(1).Position;
            var second = game.State.UnitById(2).Position;
            snapshots.Add((player, first, second));
            if (synchronize && player > 0 && first == new Hex(3, 2))
            {
                rendezvous.Signal();
                Assert.True(rendezvous.Wait(TimeSpan.FromSeconds(20)), "The relaunched players did not plan concurrently");
            }
            return [Move(game, player)];
        });
        sim.State.MoveUnit(2, new(6, 2));
        var sequential = Copy(sim); sequential.AiWaveLimit = 1;
        var events = Observe(sim);
        sim.RunAi();
        Assert.Equal(0, rendezvous.CurrentCount);
        Assert.Equal(new[] { (1, 0, SpeculationOutcome.Direct), (1, 1, SpeculationOutcome.Conflict),
            (2, 1, SpeculationOutcome.Direct), (2, 2, SpeculationOutcome.Committed), (2, 3, SpeculationOutcome.Committed) },
            events.Select(e => (e.Wave, e.Player, e.Outcome)));
        Assert.Equal("footprint", events[1].Reason);
        Assert.Single(snapshots, s => s.Player == 0);
        foreach (int player in new[] { 1, 2, 3 })
        {
            var turns = snapshots.Where(s => s.Player == player).ToArray();
            Assert.Equal(2, turns.Length);
            Assert.Contains(turns, s => s.First == new Hex(2, 2));
            Assert.Contains(turns, s => s.First == new Hex(3, 2) && s.Second == new Hex(6, 2));
        }
        Assert.All(sim.State.Players, p => Assert.True(p.EndedTurn));
        synchronize = false;
        sequential.RunAi(); Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
    }

    [Theory]
    [InlineData("Fortify")]
    [InlineData("FortifyTurns")]
    [InlineData("Blockade")]
    [InlineData("Health")]
    [InlineData("Experience")]
    [InlineData("Moves")]
    [InlineData("Promotion")]
    [InlineData("Create")]
    [InlineData("Delete")]
    [InlineData("DirectFortify")]
    [InlineData("DirectCreate")]
    [InlineData("DirectDelete")]
    public void Unit_mutations_without_hex_targets_invalidate_nearby_plans(string change)
    {
        var sim = Game(2, (game, player) =>
        {
            if (player == 0)
            {
                var command = new MutateUnit { Player = 0, Change = change };
                if (change.StartsWith("Direct", StringComparison.Ordinal)) { command.Apply(game); return []; }
                return [command];
            }
            bool changed = Changed(game, change);
            var unit = game.State.UnitById(2);
            return [new MoveUnitCommand { Player = player, UnitId = unit.Id, To = unit.Position.Neighbor(changed ? 0 : 1) }];
        });
        sim.State.MoveUnit(2, new(6, 2));
        var sequential = Copy(sim); sequential.AiWaveLimit = 1;
        var events = Observe(sim);
        sim.RunAi(); sequential.RunAi();
        Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
        var conflict = Assert.Single(events, e => e.Outcome == SpeculationOutcome.Conflict);
        Assert.Equal(change is "Health" or "Experience" or "Promotion" or "Create" or "Delete" or "DirectCreate" or "DirectDelete"
            ? "entity aggregates" : "footprint", conflict.Reason);
        Assert.Equal(SpeculationOutcome.Direct, events[^1].Outcome);
        Assert.Equal(2, events[^1].Wave);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Distant_unit_creation_conflicts_even_when_the_later_plan_does_not_allocate_an_id(bool laterAllocates)
    {
        var sim = Game(2, (game, player) => player == 0 || laterAllocates
            ? [new MutateUnit { Player = player, Change = "Create" }]
            : [Move(game, player)]);
        var sequential = Copy(sim); sequential.AiWaveLimit = 1;
        var events = Observe(sim);
        sim.RunAi(); sequential.RunAi();
        Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
        Assert.Equal(SpeculationOutcome.Conflict, events[1].Outcome);
        Assert.Equal(laterAllocates ? "unit id" : "entity aggregates", events[1].Reason);
        Assert.Equal(3, events.Count);
        Assert.Equal(laterAllocates ? new[] { 1, 2, 3, 4 } : new[] { 1, 2, 3 }, sim.State.Units.Select(u => u.Id));
    }

    [Fact]
    public void Distant_unit_creation_changing_a_globally_read_aggregate_matches_sequential()
    {
        var sim = Game(2, (game, player) => player == 0
            ? [new MutateUnit { Player = 0, Change = "Create" }]
            : [new MoveUnitCommand { Player = 1, UnitId = 2,
                To = game.State.UnitById(2).Position.Neighbor(game.State.Units.Count(u => u.Owner == 0) > 1 ? 0 : 1) }]);
        AssertGlobalReplan(sim);
    }

    [Theory]
    [InlineData("RemoveUnit")]
    [InlineData("UnitOwner")]
    [InlineData("UnitType")]
    [InlineData("Health")]
    [InlineData("Experience")]
    [InlineData("Promotion")]
    [InlineData("CreateCity")]
    [InlineData("RemoveCity")]
    [InlineData("CityOwner")]
    [InlineData("Population")]
    public void Distant_entity_changes_affecting_global_reads_match_sequential(string change)
    {
        static long Read(GameState state) => state.Units.Where(u => u.Owner == 0)
            .Sum(u => u.Health + u.Experience + u.Promotions.Count + (u.Type == "base:warrior" ? 1 : 0))
            + state.Cities.Where(c => c.Owner == 0).Sum(c => c.Population * 1000L);
        long initial = 0;
        var sim = Game(2, (game, player) => player == 0
            ? [new ChangeAggregate { Change = change }]
            : [new MoveUnitCommand { Player = 1, UnitId = 2,
                To = game.State.UnitById(2).Position.Neighbor(Read(game.State) != initial ? 0 : 1) }]);
        sim.State.Players.Add(new() { Id = 2, TeamId = 2 });
        sim.State.Teams.Add(new() { Id = 2, Members = [2] });
        sim.State.RebuildIndexes();
        sim.State.AddCity(new() { Id = sim.State.NextCityId++, Owner = 0, Position = new(3, 5), Population = 3 });
        initial = Read(sim.State);
        AssertGlobalReplan(sim);
        Assert.NotEqual(initial, Read(sim.State));
    }

    private static void AssertGlobalReplan(Simulation sim)
    {
        var sequential = Copy(sim); sequential.AiWaveLimit = 1;
        var events = Observe(sim);
        sim.RunAi(); sequential.RunAi();
        Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
        Assert.Equal("entity aggregates", Assert.Single(events, e => e.Outcome == SpeculationOutcome.Conflict).Reason);
        Assert.Equal(2, events[^1].Wave);
        Assert.Equal(SpeculationOutcome.Direct, events[^1].Outcome);
    }

    private sealed class ChangeAggregate : GameCommand
    {
        public string Change { get; init; } = "";
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim)
        {
            var state = sim.State;
            if (Change is "Health" or "Experience" or "Promotion")
            {
                new MutateUnit { Change = Change }.Apply(sim); return;
            }
            if (Change is "UnitOwner" or "UnitType")
            {
                var old = state.UnitById(1);
                state.RemoveUnit(1);
                state.AddUnit(new() { Id = old.Id, Owner = Change == "UnitOwner" ? 2 : 0,
                    Type = Change == "UnitType" ? "base:settler" : old.Type, Position = old.Position });
                return;
            }
            ChangeCityOrRemoveUnit(state);
        }
        private void ChangeCityOrRemoveUnit(GameState state)
        {
            switch (Change)
            {
                case "RemoveUnit": state.RemoveUnit(1); break;
                case "CreateCity": state.AddCity(new() { Id = state.NextCityId++, Owner = 0, Position = new(5, 5) }); break;
                case "RemoveCity": state.RemoveCity(1); break;
                case "Population": state.CityById(1).Population++; break;
                case "CityOwner":
                    var city = state.CityById(1); state.RemoveCity(1);
                    state.AddCity(new() { Id = city.Id, Owner = 2, Position = city.Position, Population = city.Population }); break;
                default: throw new ArgumentOutOfRangeException(nameof(Change));
            }
        }
    }

    private static List<SpeculationEvent> Observe(Simulation sim)
    {
        var events = new List<SpeculationEvent>(); sim.SpeculationObserver = events.Add; return events;
    }

    private static MoveUnitCommand Move(Simulation sim, int player)
    {
        var unit = sim.State.UnitById(player + 1);
        return new() { Player = player, UnitId = unit.Id, To = unit.Position.Neighbor(0) };
    }

    private static Simulation Game(int players, Func<Simulation, int, IEnumerable<GameCommand>> decide)
    {
        var probe = new Probe(decide);
        var content = ModLoader.Build([
            ModLoader.FromAssembly(typeof(TestRulesetMod).Assembly, AppContext.BaseDirectory),
            new ModSource { Manifest = new() { Id = "wave_probe", Dependencies = ["base"] },
                Instance = probe, Root = AppContext.BaseDirectory },
        ]);
        var state = new GameState { Map = new(80, 12),
            Plots = Enumerable.Range(0, 960).Select(_ => new Plot { Terrain = "base:grassland" }).ToArray() };
        for (int player = 0; player < players; player++)
        {
            state.Players.Add(new() { Id = player, TeamId = player, Ai = probe.Id });
            state.Teams.Add(new() { Id = player, Members = [player] });
            var unit = new Unit { Id = state.NextUnitId++, Owner = player, Type = "base:warrior", Position = new(2 + player * 20, 2), MovesLeft = 2 };
            unit.Extensions.Set(content, new MilitaryUnitState());
            state.Units.Add(unit);
        }
        state.RebuildIndexes();
        return new(content, new() { Seed = 71, PlayerCount = players, Map = state.Map }, state, new(71));
    }

    private static Simulation Copy(Simulation sim)
    {
        var (a, b, c, d) = sim.Rng.State;
        return new(sim.Content, sim.Settings, sim.State.Clone(), DeterministicRng.Restore(a, b, c, d));
    }

    private static IEnumerable<ExtensionState> Extensions(GameState state) =>
        new[] { state.Extensions }.Concat(state.Players.Select(p => p.Extensions)).Concat(state.Teams.Select(t => t.Extensions))
            .Concat(state.Cities.Select(c => c.Extensions)).Concat(state.Units.Select(u => u.Extensions));

    public sealed class Memory
    {
        public List<int> Samples { get; set; } = [];
        public SortedDictionary<string, int> Values { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Probe(Func<Simulation, int, IEnumerable<GameCommand>> decide) : IMod, IPlayerAI
    {
        public string Id => "wave_probe:ai";
        public void Register(IModContext context)
        {
            context.PlayerAIs.Register(this);
            context.RegisterStateComponent<Memory>("memory");
            context.RegisterStateComponent<MilitaryUnitState>("unit_state");
        }
        public IEnumerable<GameCommand> Decide(Simulation sim, int playerId) => decide(sim, playerId);
    }

    // No Hex properties: the footprint must follow entity state, not only command targets.
    private sealed class MutateUnit : GameCommand
    {
        public string Change { get; init; } = "";
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim)
        {
            var unit = sim.State.UnitById(Player + 1);
            var military = unit.Extensions.Get<MilitaryUnitState>(sim.Content)!;
            switch (Change)
            {
                case "Fortify": case "DirectFortify": military.Activity = UnitActivity.Fortify; break;
                case "Blockade": military.Activity = UnitActivity.Blockade; break;
                case "FortifyTurns": military.FortifyTurns = 3; break;
                case "Health": unit.Health = 50; break;
                case "Experience": unit.Experience = 7; break;
                case "Moves": unit.MovesLeft = 0; break;
                case "Promotion": unit.Promotions.Add("trained"); break;
                case "Delete": case "DirectDelete": sim.State.RemoveUnit(unit.Id); break;
                case "Create": case "DirectCreate": sim.State.AddUnit(new() { Id = sim.State.NextUnitId++, Owner = Player, Type = unit.Type, Position = unit.Position }); break;
                default: throw new ArgumentOutOfRangeException(nameof(Change));
            }
        }
    }

    private static bool Changed(Simulation sim, string change)
    {
        var unit = sim.State.FindUnit(1);
        var military = unit?.Extensions.Get<MilitaryUnitState>(sim.Content);
        return change switch
        {
            "Fortify" or "DirectFortify" => military!.Activity == UnitActivity.Fortify,
            "Blockade" => military!.Activity == UnitActivity.Blockade,
            "FortifyTurns" => military!.FortifyTurns == 3,
            "Health" => unit!.Health == 50,
            "Experience" => unit!.Experience == 7,
            "Moves" => unit!.MovesLeft == 0,
            "Promotion" => unit!.Promotions.Contains("trained"),
            "Delete" or "DirectDelete" => unit is null,
            "Create" or "DirectCreate" => sim.State.Units.Any(u => u.Id > 2),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
    }
}
