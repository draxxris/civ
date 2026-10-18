using System.Text.Json;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;

namespace Civ.Mods.Tests;

// Authored state only: no generation, startup AI, AdvanceTurn, or multi-turn simulation systems.
public sealed class AiIntentTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Theory]
    [InlineData(0, 33)]
    [InlineData(1, 32)]
    public void Strategic_reviews_are_staggered_bounded_and_saved_on_both_sides_of_the_boundary(int player, int due)
    {
        var sim = Strategy();
        Review(sim, player);
        Assert.Equal(30, Goal(sim, player, "Civic").GetProperty("EvaluatedTurn").GetInt32());
        Assert.Equal(due, Goal(sim, player, "Civic").GetProperty("ReviewTurn").GetInt32());
        var rng = sim.Rng.State;
        sim.State.Turn = due - 1;
        Review(sim, player);
        Assert.Equal(30, Goal(sim, player, "Civic").GetProperty("EvaluatedTurn").GetInt32());
        var before = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.State.Turn = before.State.Turn = due;
        Review(sim, player); Review(before, player);
        Assert.Equal(due, Goal(sim, player, "Civic").GetProperty("EvaluatedTurn").GetInt32());
        Assert.Equal(due, Campaign(sim, player).GetProperty("EvaluatedTurn").GetInt32());
        Assert.Equal(sim.ComputeHash(), before.ComputeHash());
        var after = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.State.Turn++; before.State.Turn++; after.State.Turn++;
        Review(sim, player); Review(before, player); Review(after, player);
        Assert.Equal(sim.ComputeHash(), before.ComputeHash());
        Assert.Equal(sim.ComputeHash(), after.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Theory]
    [InlineData("war")]
    [InlineData("peace")]
    [InlineData("acquire")]
    [InlineData("lose")]
    [InlineData("technology")]
    [InlineData("emergency")]
    public void Relevant_changes_reconsider_both_strategic_goals_immediately(string change)
    {
        var sim = Strategy();
        if (change == "war") sim.State.SetWar(0, 1, false);
        Review(sim);
        sim.State.Turn = 31;
        switch (change)
        {
            case "war": sim.State.SetWar(0, 1, true); break;
            case "peace": sim.State.SetWar(0, 1, false); break;
            case "acquire": TransferCity(sim, 3, 0); break;
            case "lose": TransferCity(sim, 2, 1); break;
            case "technology": sim.TeamOf(0).Techs.Add("research:agriculture"); break;
            case "emergency": sim.State.PlayerById(0).Treasury = 0; break;
        }
        Review(sim);
        Assert.Equal(31, Goal(sim, 0, "Civic").GetProperty("EvaluatedTurn").GetInt32());
        Assert.Equal(31, Campaign(sim).GetProperty("EvaluatedTurn").GetInt32());
        if (change == "peace") Assert.Equal(JsonValueKind.Null, Campaign(sim).GetProperty("City").ValueKind);
    }

    [Theory]
    [InlineData("destroy")]
    [InlineData("capture")]
    [InlineData("hidden")]
    public void Invalid_campaign_target_is_replaced_in_the_same_pass(string change)
    {
        var sim = Strategy();
        Review(sim);
        Assert.Equal(3, Campaign(sim).GetProperty("City").GetInt32());
        sim.State.Turn++;
        switch (change)
        {
            case "destroy": sim.State.RemoveCity(3); break;
            case "capture": TransferCity(sim, 3, 0); break;
            case "hidden": sim.State.PlotAt(sim.State.CityById(3).Position).Revealed.Clear(); break;
        }
        Review(sim);
        Assert.Equal(4, Campaign(sim).GetProperty("City").GetInt32());
        Assert.Equal(31, Campaign(sim).GetProperty("EvaluatedTurn").GetInt32());
    }

    [Theory]
    [InlineData("cooldown")]
    [InlineData("anarchy")]
    [InlineData("golden_age")]
    [InlineData("building")]
    public void Civic_readiness_and_new_anarchy_capabilities_invalidate_immediately(string change)
    {
        var sim = Strategy();
        Review(sim);
        sim.State.Turn++;
        var timers = sim.State.PlayerById(0).Extensions.Get<GovernmentPlayerState>(sim.Content)!;
        switch (change)
        {
            case "cooldown": timers.RevolutionTimer = 0; break;
            case "anarchy": timers.AnarchyTurns = 1; break;
            case "golden_age": sim.State.PlayerById(0).Extensions.Set(sim.Content, new CityPlayerState { GoldenAge = true }); break;
            case "building": sim.State.CityById(1).Buildings.Add("cities:palace"); break;
        }
        Review(sim);
        Assert.Equal(31, Goal(sim, 0, "Civic").GetProperty("EvaluatedTurn").GetInt32());
        if (change == "anarchy") Assert.Empty(sim.State.PlayerById(0).Civics);
    }

    [Fact]
    public void Civic_adoption_and_its_post_adoption_goal_replay_and_load_without_immediate_reconsideration()
    {
        var sim = Strategy();
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new GovernmentPlayerState());
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new CityPlayerState { GoldenAge = true });
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<Civ.Content.Schema.TechDef>().All.Select(t => t.Id));
        var fork = sim.ForkSpeculative();
        Apply(fork, "ReviewCivicGoalCommand");
        Assert.NotEmpty(fork.State.PlayerById(0).Civics);
        var codec = new CommandCodec(sim.Content);
        foreach (var (command, _, _) in fork._recordedCommands!)
            Assert.True(sim.TryApply(codec.Decode(codec.Encode(command)), out var error), error);
        Assert.Equal(fork.ComputeHash(), sim.ComputeHash());
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.State.Turn++; loaded.State.Turn++;
        Apply(sim, "ReviewCivicGoalCommand"); Apply(loaded, "ReviewCivicGoalCommand");
        Assert.Equal(30, Goal(sim, 0, "Civic").GetProperty("EvaluatedTurn").GetInt32());
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Worker_keeps_a_valid_destination_despite_a_new_nearer_job_and_finishes_travelling(bool reload)
    {
        var sim = Workers();
        Plan(sim, "WorkerPlanner");
        var first = Mission(sim, 1);
        int destination = first.GetProperty("Target").GetProperty("Value").GetInt32();
        Assert.Equal(Tile(sim, 7, 2).Value, destination);
        Assert.Equal(HexGrid.FromOffset(2, 2), sim.State.UnitById(1).Position);
        var copy = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        if (reload) sim = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, copy })
        {
            FarmTile(game, 3, 2);
            game.State.Turn++;
            game.State.UnitById(1).MovesLeft = 240;
            Plan(game, "WorkerPlanner");
            Assert.Equal(HexGrid.FromOffset(4, 2), game.State.UnitById(1).Position);
            Assert.Equal(first.GetRawText(), Mission(game, 1).GetRawText());
        }
        Assert.Equal(sim.ComputeHash(), copy.ComputeHash());
        for (int i = 0; i < 2; i++)
        {
            sim.State.Turn++;
            sim.State.UnitById(1).MovesLeft = 240;
            Plan(sim, "WorkerPlanner");
        }
        Assert.Equal(sim.State.Grid.HexOf(new(destination)), sim.State.UnitById(1).Position);
        Assert.True(sim.Content.Services.Get<IWorkerBuildService>().Single().IsWorking(sim, 1));
        Assert.False(HasMission(sim, 1));
    }

    [Fact]
    public void Settler_continues_a_distant_site_across_load_and_founds_instead_of_replanning()
    {
        var sim = Settlers();
        Plan(sim, "SettlerPlanner");
        var first = Mission(sim, 1);
        var target = new TileId(first.GetProperty("Target").GetProperty("Value").GetInt32());
        Assert.Equal(Tile(sim, 9, 2), target);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var rng = sim.Rng.State;
        for (int turn = 31; turn <= 35; turn++)
        {
            foreach (var game in new[] { sim, loaded })
            {
                game.State.Turn = turn;
                var settler = game.State.FindUnit(1);
                if (settler is null) continue;
                settler.MovesLeft = 120;
                int distance = game.State.Grid.Distance(settler.Position, game.State.Grid.HexOf(target));
                Plan(game, "SettlerPlanner");
                if (game.State.FindUnit(1) is { } moved)
                {
                    Assert.True(game.State.Grid.Distance(moved.Position, game.State.Grid.HexOf(target)) < distance);
                    Assert.Equal(first.GetRawText(), Mission(game, 1).GetRawText());
                }
            }
            Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        }
        Assert.Null(sim.State.FindUnit(1));
        Assert.Equal(0, sim.State.CityAt(sim.State.Grid.HexOf(target))!.Owner);
        Assert.False(HasMission(sim, 1));
        Assert.Equal(rng, sim.Rng.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Civilian_threat_overrides_retained_travel_immediately(bool settler)
    {
        var sim = settler ? Settlers() : Workers();
        string planner = settler ? "SettlerPlanner" : "WorkerPlanner";
        Plan(sim, planner);
        var unit = sim.State.UnitById(1);
        var home = sim.State.CityById(1).Position;
        // Move the worker away from the city so an emergency retreat has an observable step.
        if (!settler) sim.State.MoveUnit(1, HexGrid.FromOffset(4, 2));
        var before = unit.Position;
        int distance = sim.State.Grid.Distance(before, home);
        sim.State.SetWar(0, 1, true);
        AddUnit(sim, 1, "military:warrior", unit.Position.Neighbor(0));
        sim.State.Turn++;
        unit.MovesLeft = 120;
        Plan(sim, planner);
        Assert.False(HasMission(sim, 1));
        Assert.True(sim.State.Grid.Distance(unit.Position, home) < distance);
    }

    [Theory]
    [InlineData(false, "foreign")]
    [InlineData(false, "water")]
    [InlineData(false, "complete")]
    [InlineData(false, "target_threat")]
    [InlineData(false, "expired")]
    [InlineData(true, "foreign")]
    [InlineData(true, "water")]
    [InlineData(true, "city")]
    [InlineData(true, "target_threat")]
    [InlineData(true, "expired")]
    public void Invalid_destinations_are_replaced_without_an_idle_pass(bool settler, string change)
    {
        var sim = settler ? Settlers(twoSites: true) : Workers();
        string command = settler ? "PrepareSettlerMissionsCommand" : "PrepareWorkerMissionsCommand";
        Apply(sim, command);
        var old = Mission(sim, 1);
        var tile = new TileId(old.GetProperty("Target").GetProperty("Value").GetInt32());
        sim.State.Turn = change == "expired" ? 42 : 31;
        switch (change)
        {
            case "foreign": sim.State.Plots[tile.Value].Owner = 1; break;
            case "water": sim.State.Plots[tile.Value].Terrain = "world:coast"; break;
            case "complete": sim.State.Plots[tile.Value].Improvement = "world:improvement_improvement_farm"; break;
            case "city": AddCity(sim, 1, sim.State.Grid.HexOf(tile)); break;
            case "target_threat":
                sim.State.SetWar(0, 1, true);
                AddUnit(sim, 1, "military:warrior", sim.State.Grid.HexOf(tile)); break;
        }
        var before = sim.State.UnitById(1).Position;
        Plan(sim, settler ? "SettlerPlanner" : "WorkerPlanner");
        Assert.NotEqual(before, sim.State.UnitById(1).Position);
        var fresh = Mission(sim, 1);
        Assert.Equal(sim.State.Turn, fresh.GetProperty("CreatedTurn").GetInt32());
        if (change != "expired") Assert.NotEqual(tile.Value, fresh.GetProperty("Target").GetProperty("Value").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Blocked_routes_recompute_or_choose_a_reachable_destination_in_the_same_pass(bool settler)
    {
        var sim = settler ? Settlers(twoSites: true) : Workers();
        string command = settler ? "PrepareSettlerMissionsCommand" : "PrepareWorkerMissionsCommand";
        Apply(sim, command);
        var tile = new TileId(Mission(sim, 1).GetProperty("Target").GetProperty("Value").GetInt32());
        foreach (var neighbor in sim.State.Grid.Neighbors(tile)) sim.State.Plots[neighbor.Value].Kind = PlotKind.Peak;
        sim.State.Turn++;
        var before = sim.State.UnitById(1).Position;
        Plan(sim, settler ? "SettlerPlanner" : "WorkerPlanner");
        Assert.NotEqual(before, sim.State.UnitById(1).Position);
        Assert.NotEqual(tile.Value, Mission(sim, 1).GetProperty("Target").GetProperty("Value").GetInt32());
    }

    [Fact]
    public void Blocked_mission_recovers_even_when_the_first_candidate_batch_is_unreachable()
    {
        var sim = Workers();
        var distant = sim.State.CityById(1);
        foreach (var tile in sim.Content.Services.Get<ICityWorkService>().Single().WorkArea(sim, distant.Id))
        {
            if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is not null) continue;
            sim.State.Plots[tile.Value].Owner = 0;
            sim.State.SetRiver(tile, 0, true);
            sim.State.Plots[tile.Value].BuildProgress["world:build_build_farm"] = 1;
            distant.Worked.Add(sim.State.Grid.HexOf(tile));
        }
        var home = AddCity(sim, 0, HexGrid.FromOffset(1, 2));
        FarmTile(sim, 2, 2);
        home.Worked.Add(HexGrid.FromOffset(2, 2));
        Apply(sim, "PrepareWorkerMissionsCommand");
        Assert.True(Mission(sim, 1).GetProperty("Target").GetProperty("Value").GetInt32() % sim.State.Map.Width > 4);
        foreach (int row in Enumerable.Range(0, sim.State.Map.Height)) sim.State.Plots[Tile(sim, 4, row).Value].Owner = 1;
        sim.State.Turn++;
        Plan(sim, "WorkerPlanner");
        Assert.Equal(HexGrid.FromOffset(2, 2), sim.State.UnitById(1).Position);
        Assert.True(sim.Content.Services.Get<IWorkerBuildService>().Single().IsWorking(sim, 1));
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("zero_health")]
    [InlineData("captured")]
    [InlineData("role")]
    [InlineData("domain")]
    [InlineData("transport")]
    public void Missions_for_unavailable_units_are_pruned_through_commands(string change)
    {
        var sim = Workers();
        Apply(sim, "PrepareWorkerMissionsCommand");
        var worker = sim.State.UnitById(1);
        switch (change)
        {
            case "dead": sim.State.RemoveUnit(1); break;
            case "zero_health": worker.Health = 0; break;
            case "captured": ReplaceUnit(sim, worker, 1, worker.Type); break;
            case "role": ReplaceUnit(sim, worker, 0, "military:warrior"); break;
            case "domain": ReplaceUnit(sim, worker, 0, "military:galley"); break;
            case "transport": worker.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId = 99; break;
        }
        Apply(sim, "PruneAiIntentCommand");
        Assert.False(HasMission(sim, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exclusive_destinations_are_reserved_in_unit_id_order(bool settler)
    {
        var sim = settler ? Settlers(twoSites: true) : Workers();
        AddUnit(sim, 0, sim.State.UnitById(1).Type, sim.State.UnitById(1).Position);
        sim.State.Units.Reverse(); // List insertion order must not determine the reservation winner.
        Apply(sim, settler ? "PrepareSettlerMissionsCommand" : "PrepareWorkerMissionsCommand");
        var first = Mission(sim, 1).GetProperty("Target").GetProperty("Value").GetInt32();
        var second = Mission(sim, 2).GetProperty("Target").GetProperty("Value").GetInt32();
        Assert.NotEqual(first, second);
        if (settler) Assert.True(sim.State.Grid.Distance(new TileId(first), new TileId(second)) > 3);
        else Assert.Equal(Tile(sim, 7, 2).Value, first);
        var saved = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Apply(saved, settler ? "PrepareSettlerMissionsCommand" : "PrepareWorkerMissionsCommand");
        Assert.Equal(sim.ComputeHash(), saved.ComputeHash());
    }

    [Fact]
    public void Active_worker_build_reserves_its_tile_before_lower_id_workers_choose_destinations()
    {
        var sim = Workers();
        var builder = AddUnit(sim, 0, "military:worker", HexGrid.FromOffset(7, 2));
        Assert.True(sim.TryApply(new StartBuildCommand { Player = 0, Unit = builder.Id, Build = "world:build_build_farm" }, out _));
        Apply(sim, "PrepareWorkerMissionsCommand");
        Assert.Equal(Tile(sim, 9, 4).Value, Mission(sim, 1).GetProperty("Target").GetProperty("Value").GetInt32());
        Assert.False(HasMission(sim, builder.Id));
    }

    [Fact]
    public void Missing_component_loads_empty_and_clones_and_resets_use_the_normal_state_contract()
    {
        var sim = Workers();
        string legacy = SaveGame.Save(sim);
        Assert.False(Intent(sim).HasValue);
        var loaded = SaveGame.Load(sim.Content, legacy);
        Assert.False(Intent(loaded).HasValue);
        Apply(loaded, "PrepareWorkerMissionsCommand");
        Assert.True(HasMission(loaded, 1));
        Review(loaded);
        ulong withMission = loaded.ComputeHash();
        var clone = loaded.ForkSpeculative();
        Assert.Equal(withMission, clone.ComputeHash());
        Apply(clone, "ForgetUnitMissionCommand", unit: 1);
        Assert.False(HasMission(clone, 1));
        Assert.True(HasMission(loaded, 1));
        Assert.NotEqual(withMission, clone.ComputeHash());
        Assert.Equal(withMission, SaveGame.Load(loaded.Content, SaveGame.Save(loaded)).ComputeHash());
        loaded.State.RemoveUnit(1); loaded.State.RemoveCity(1);
        Apply(loaded, "PruneAiIntentCommand");
        Assert.False(Intent(loaded).HasValue);
    }

    [Fact]
    public void Changed_intent_rejects_a_serialized_command_without_mutating_authoritative_state()
    {
        var sim = Workers();
        var fork = sim.ForkSpeculative();
        Apply(fork, "PrepareWorkerMissionsCommand");
        var command = Assert.Single(fork._recordedCommands!).Command;
        var codec = new CommandCodec(sim.Content);
        sim.State.Plots[Tile(sim, 7, 2).Value].Improvement = "world:improvement_improvement_farm";
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        Assert.False(sim.TryApply(codec.Decode(codec.Encode(command)), out var error));
        Assert.Equal("AI intent changed", error);
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
        Assert.False(Intent(sim).HasValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_mission_cleanup_replays_and_loads_after_the_unit_arrives(bool settler)
    {
        var sim = settler ? Settlers() : Workers();
        Apply(sim, settler ? "PrepareSettlerMissionsCommand" : "PrepareWorkerMissionsCommand");
        var tile = new TileId(Mission(sim, 1).GetProperty("Target").GetProperty("Value").GetInt32());
        sim.State.MoveUnit(1, sim.State.Grid.HexOf(tile).Neighbor(3));
        sim.State.UnitById(1).MovesLeft = 120;
        var fork = sim.ForkSpeculative();
        Plan(fork, settler ? "SettlerPlanner" : "WorkerPlanner");
        Assert.False(HasMission(fork, 1));
        var codec = new CommandCodec(sim.Content);
        foreach (var (command, _, _) in fork._recordedCommands!)
            Assert.True(sim.TryApply(codec.Decode(codec.Encode(command)), out var error), error);
        Assert.Equal(fork.ComputeHash(), sim.ComputeHash());
        Assert.Equal(sim.ComputeHash(), SaveGame.Load(sim.Content, SaveGame.Save(sim)).ComputeHash());
        if (settler) Assert.Equal(0, sim.State.CityAt(sim.State.Grid.HexOf(tile))!.Owner);
        else Assert.True(sim.Content.Services.Get<IWorkerBuildService>().Single().IsWorking(sim, 1));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Intent_creation_continuation_and_cleanup_replay_top_level_commands_and_survive_load(bool reload, bool settler)
    {
        var direct = settler ? Settlers() : Workers();
        if (reload) direct = SaveGame.Load(direct.Content, SaveGame.Save(direct));
        for (int pass = 0; pass < 3; pass++)
        {
            if (pass > 0) direct.State.Turn++;
            if (pass == 2) direct.State.RemoveUnit(1);
            else direct.State.UnitById(1).MovesLeft = 120;
            var fork = direct.ForkSpeculative();
            Review(fork);
            Plan(fork, settler ? "SettlerPlanner" : "WorkerPlanner");
            Apply(fork, "PruneAiIntentCommand");
            Assert.NotEmpty(fork._recordedCommands!);
            var codec = new CommandCodec(direct.Content);
            foreach (var (command, _, _) in fork._recordedCommands!)
                Assert.True(direct.TryApply(codec.Decode(codec.Encode(command)), out var error), error);
            Assert.Equal(fork.ComputeHash(), direct.ComputeHash());
            Assert.Equal(direct.ComputeHash(), SaveGame.Load(direct.Content, SaveGame.Save(direct)).ComputeHash());
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Real_speculative_waves_commit_intent_and_reject_changed_remote_decisions(bool changeRemoteTarget, bool transient)
    {
        var probe = new IntentProbe(changeRemoteTarget, transient);
        var source = new ModSource { Manifest = new() { Id = "intent_probe", Dependencies = ["ai"] },
            Instance = probe, Root = AppContext.BaseDirectory };
        var content = ModLoader.Build(mods.Sources.Append(source));
        var sim = Workers(content, players: 2, width: 64);
        // The first player is distant and stationary. The second plans the actual worker destination.
        ReplaceUnit(sim, sim.State.UnitById(1), 1, "military:worker");

        var state = sim.State;
        state.Players = state.Players.Select(p => new Player { Id = p.Id, TeamId = p.TeamId, Ai = probe.Id,
            Treasury = p.Treasury, Extensions = p.Extensions }).ToList();
        state.RebuildIndexes();
        AddUnit(sim, 0, "military:warrior", HexGrid.FromOffset(55, 2));
        TransferCity(sim, 1, 1);
        FarmTile(sim, 7, 2, owner: 1); FarmTile(sim, 9, 4, owner: 1);
        if (transient)
        {
            sim.State.MoveUnit(1, HexGrid.FromOffset(7, 2));
            var distant = AddCity(sim, 1, HexGrid.FromOffset(16, 2));
            var tile = Tile(sim, 17, 2);
            FarmTile(sim, 17, 2, owner: 1);
            distant.Worked.Add(sim.State.Grid.HexOf(tile));
        }
        var sequential = SaveGame.Load(content, SaveGame.Save(sim)); sequential.AiWaveLimit = 1;
        var events = new List<SpeculationEvent>(); sim.SpeculationObserver = events.Add;
        sim.RunAi(); sequential.RunAi();
        Assert.Equal(sequential.ComputeHash(), sim.ComputeHash());
        Assert.True(HasMission(sim, 1, player: 1));
        Assert.Equal(sim.ComputeHash(), SaveGame.Load(content, SaveGame.Save(sim)).ComputeHash());
        if (changeRemoteTarget)
        {
            Assert.Contains(events, e => e.Outcome == SpeculationOutcome.Conflict && e.Reason == "replay rejected: AI intent changed");
            Assert.Equal(Tile(sim, transient ? 17 : 9, transient ? 2 : 4).Value,
                Mission(sim, 1, player: 1).GetProperty("Target").GetProperty("Value").GetInt32());
        }
        else Assert.Contains(events, e => e.Player == 1 && e.Outcome == SpeculationOutcome.Committed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retained_distant_campaign_does_not_delay_a_small_field_army_attack_on_a_nearby_enemy(bool destroyTargets)
    {
        var sim = Strategy();
        Review(sim);
        int retained = Campaign(sim).GetProperty("City").GetInt32();
        if (destroyTargets) { sim.State.RemoveCity(3); sim.State.RemoveCity(4); }
        sim.State.Turn++;
        for (int i = 0; i < 4; i++)
        {
            AddUnit(sim, 0, "military:warrior", sim.State.CityById(1).Position);
            AddUnit(sim, 0, "military:warrior", sim.State.CityById(2).Position);
        }
        var attacker = AddUnit(sim, 0, "military:javelineer", HexGrid.FromOffset(8, 2));
        var enemy = AddUnit(sim, 1, "military:warrior", HexGrid.FromOffset(9, 2));
        enemy.Health = 10;
        sim.State.PlotAt(enemy.Position).Owner = 0;
        sim.State.PlotAt(enemy.Position).Visible.Add(0);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var army = NewAi(sim, "ArmyPlan", ctx);
        var cities = NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), army);
        cities.GetType().GetMethod("Plan")!.Invoke(cities, null);
        var attack = NewAi(sim, "AttackPlanner", ctx, NewAi(sim, "UnitOrders", ctx));
        var military = NewAi(sim, "MilitaryPlanner", ctx, army, cities, attack);
        military.GetType().GetMethod("Plan")!.Invoke(military, null);
        Assert.Contains(sim.Content.Services.Get<ICombatService>().Single().History(sim), r => r.Attacker == attacker.Id);
        if (destroyTargets) Assert.Equal(JsonValueKind.Null, Campaign(sim).GetProperty("City").ValueKind);
        else Assert.Equal(retained, Campaign(sim).GetProperty("City").GetInt32());
        Assert.Equal(destroyTargets ? 31 : 30, Campaign(sim).GetProperty("EvaluatedTurn").GetInt32());
    }

    private Simulation Strategy()
    {
        var sim = Empty(mods.Content, new(24, 8));
        AddCity(sim, 0, HexGrid.FromOffset(2, 2)); AddCity(sim, 0, HexGrid.FromOffset(2, 6));
        AddCity(sim, 1, HexGrid.FromOffset(12, 2)); AddCity(sim, 1, HexGrid.FromOffset(20, 6));
        sim.State.SetWar(0, 1, true);
        return sim;
    }

    private Simulation Workers(ContentDb? content = null, int players = 2, int width = 16)
    {
        var sim = Empty(content ?? mods.Content, new(width, 5), players);
        var home = AddCity(sim, 0, HexGrid.FromOffset(8, 2));
        FarmTile(sim, 7, 2); FarmTile(sim, 9, 4);
        home.Worked = [HexGrid.FromOffset(7, 2), HexGrid.FromOffset(9, 4)];
        sim.TeamOf(0).Techs.Add("research:agriculture");
        if (players > 1) sim.TeamOf(1).Techs.Add("research:agriculture");
        AddUnit(sim, 0, "military:worker", HexGrid.FromOffset(0, 2));
        return sim;
    }

    private Simulation Settlers(bool twoSites = false)
    {
        var sim = Empty(mods.Content, new(16, 11));
        AddCity(sim, 0, HexGrid.FromOffset(4, 2));
        foreach (var plot in sim.State.Plots) plot.Revealed.Clear();
        sim.State.Plots[Tile(sim, 9, 2).Value].Revealed.Add(0);
        if (twoSites) sim.State.Plots[Tile(sim, 9, 7).Value].Revealed.Add(0);
        var unit = AddUnit(sim, 0, "military:settler", HexGrid.FromOffset(5, 2));
        unit.MovesLeft = 120;
        return sim;
    }

    private static Simulation Empty(ContentDb content, MapSpec map, int players = 2)
    {
        var state = new GameState { Map = map, Turn = 30,
            Plots = Enumerable.Range(0, map.Width * map.Height).Select(_ => new Plot { Terrain = "world:grass" }).ToArray() };
        for (int p = 0; p < players; p++)
        {
            var player = new Player { Id = p, TeamId = p, Treasury = 1000,
                Civilization = content.Get<Civ.Content.Schema.CivilizationDef>().All.First().Id };
            player.Extensions.Set(content, new GovernmentPlayerState { RevolutionTimer = 1 });
            state.Players.Add(player); state.Teams.Add(new() { Id = p, Members = [p] });
            foreach (var plot in state.Plots) plot.Revealed.Add(p);
        }
        state.RebuildIndexes();
        return new(content, (GameFactory.Settings(71) with { PlayerCount = players, Map = map }).Resolve(content), state, new(71));
    }

    private static void FarmTile(Simulation sim, int column, int row, int owner = 0)
    {
        var tile = Tile(sim, column, row);
        sim.State.Plots[tile.Value].Owner = owner;
        sim.State.SetRiver(tile, 0, true);
        sim.State.CityById(1).Worked.Add(sim.State.Grid.HexOf(tile));
    }

    private static City AddCity(Simulation sim, int owner, Hex position)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = position, Population = 3 };
        sim.State.AddCity(city); sim.State.PlotAt(position).Owner = owner;
        return city;
    }

    private static Unit AddUnit(Simulation sim, int owner, string type, Hex position)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = position };
        sim.State.AddUnit(unit); sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        return unit;
    }

    private static void TransferCity(Simulation sim, int id, int owner)
    {
        var old = sim.State.CityById(id);
        sim.State.RemoveCity(id);
        sim.State.AddCity(new() { Id = id, Owner = owner, Position = old.Position, Population = old.Population, Worked = old.Worked });
        sim.State.PlotAt(old.Position).Owner = owner;
    }

    private static void ReplaceUnit(Simulation sim, Unit unit, int owner, string type)
    {
        sim.State.RemoveUnit(unit.Id);
        var replacement = new Unit { Id = unit.Id, Owner = owner, Type = type, Position = unit.Position };
        sim.State.AddUnit(replacement); sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
    }

    private static void Review(Simulation sim, int player = 0)
    {
        Apply(sim, "ReviewCivicGoalCommand", player);
        Apply(sim, "ReviewCampaignGoalsCommand", player);
    }

    private static void Apply(Simulation sim, string name, int player = 0, int unit = 0)
    {
        var command = (GameCommand)NewAi(sim, name);
        command.GetType().GetProperty("Player")!.SetValue(command, player);
        command.GetType().GetProperty("Unit")?.SetValue(command, unit);
        command.GetType().GetProperty("Areas")?.SetValue(command, new[] { 0 });
        Assert.True(sim.TryApply(command, out var error), error);
    }

    private static void Plan(Simulation sim, string planner, int player = 0)
    {
        var ctx = NewAi(sim, "AiContext", sim, player);
        object target = planner == "SettlerPlanner"
            ? NewAi(sim, planner, ctx, NewAi(sim, "SitePlanner", ctx))
            : NewAi(sim, planner, ctx);
        target.GetType().GetMethod("Plan")!.Invoke(target, null);
    }

    private static object NewAi(Simulation sim, string name, params object[] args) => Activator.CreateInstance(
        sim.Content.PlayerAIs.Get("ai:standard").GetType().Assembly.GetType($"Civ.Mods.Ai.{name}")!, args)!;
    private static TileId Tile(Simulation sim, int column, int row) => sim.State.Grid.TileAt(HexGrid.FromOffset(column, row));
    private static JsonElement? Intent(Simulation sim, int player = 0) =>
        JsonSerializer.SerializeToElement(sim.State.PlayerById(player).Extensions, CanonicalJson.Options)
            .TryGetProperty("ai:player_state", out var result) ? result : null;
    private static JsonElement Goal(Simulation sim, int player, string name) => Intent(sim, player)!.Value.GetProperty(name);
    private static JsonElement Campaign(Simulation sim, int player = 0) => Goal(sim, player, "Campaigns").GetProperty("0");
    private static JsonElement Mission(Simulation sim, int unit, int player = 0) => Goal(sim, player, "Missions").GetProperty(unit.ToString());
    private static bool HasMission(Simulation sim, int unit, int player = 0) =>
        Intent(sim, player)?.GetProperty("Missions").TryGetProperty(unit.ToString(), out _) == true;

    private sealed class IntentProbe(bool changeRemoteTarget, bool transient) : IMod, IPlayerAI
    {
        public string Id => "intent_probe:ai";
        public void Register(IModContext context) => context.PlayerAIs.Register(this);
        public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
        {
            if (playerId == 0)
            {
                if (transient) sim.State.Plots[Tile(sim, 17, 2).Value].BuildProgress["world:build_build_farm"] = 1;
                else if (changeRemoteTarget) sim.State.Plots[Tile(sim, 7, 2).Value].Improvement = "world:improvement_improvement_farm";
                return [];
            }
            Review(sim, playerId);
            if (changeRemoteTarget && !transient) Apply(sim, "PrepareWorkerMissionsCommand", playerId);
            else Plan(sim, "WorkerPlanner", playerId);
            return [];
        }
    }
}
