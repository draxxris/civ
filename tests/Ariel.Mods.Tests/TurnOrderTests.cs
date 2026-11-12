using Ariel.Client;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Turn segments: who moves when within a turn, under each turn mode, with humans and AI mixed.</summary>
public sealed class TurnOrderTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Ai = "ai:standard";

    /// <summary>A duel-size game with the given seats; null is a human.</summary>
    private Simulation Game(TurnMode mode, params string?[] seats) => new(mods.Content, new SimulationSettings
    {
        Seed = 5, WorldSize = "foundation:worldsize_duel", MapGenerator = "world:continents",
        GameSpeed = "foundation:gamespeed_normal", Handicap = "foundation:handicap_noble",
        StartingEra = "foundation:era_ancient", PlayerCount = seats.Length,
        Leaders = Enumerable.Range(0, seats.Length).Select(i => new[]
        {
            "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar",
            "peoples:leader_bismarck", "peoples:leader_gandhi",
        }[i % 4]).ToArray(),
        PlayerAis = seats,
        TurnMode = mode,
    });

    private static void End(Simulation sim, int player) =>
        Assert.True(sim.TrySubmit(new EndTurnCommand { Player = player }, out var error), error);

    private static string? Rejection(Simulation sim, int player)
    {
        sim.TrySubmit(new EndTurnCommand { Player = player }, out var error);
        return error;
    }

    [Fact]
    public void Plans_group_players_by_mode_and_leave_out_players_that_already_ended()
    {
        var state = Game(TurnMode.Simultaneous, null, null, null, null).State;
        state.SetWar(0, 1, true);
        state.SetWar(2, 1, true);
        foreach (var p in state.Players) p.EndedTurn = false;
        state.Players.Single(p => p.Id == 3).EndedTurn = true; // as barbarians are at turn start

        Assert.Equal([[0, 1, 2]], TurnOrder.Plan(state, TurnMode.Simultaneous));
        Assert.Equal([[0], [1], [2]], TurnOrder.Plan(state, TurnMode.Sequential));
        Assert.Equal([[0, 2], [1]], TurnOrder.Plan(state, TurnMode.Dynamic));
    }

    [Fact]
    public void Dynamic_plans_keep_teammates_together()
    {
        var state = Game(TurnMode.Simultaneous, null, null, null).State;
        state.SetWar(0, 1, true);
        foreach (var p in state.Players) p.EndedTurn = false;
        state.PlayerById(2).TeamId = 1;

        Assert.Equal([[0], [1, 2]], TurnOrder.Plan(state, TurnMode.Dynamic));
    }

    [Fact]
    public void Simultaneous_turns_run_every_AI_first_then_wait_for_all_humans()
    {
        var sim = Game(TurnMode.Simultaneous, null, Ai, null);

        Assert.True(sim.State.PlayerById(1).EndedTurn);
        Assert.True(sim.MayAct(0));
        Assert.True(sim.MayAct(2));
        End(sim, 0);
        Assert.Equal(1, sim.State.Turn);
        End(sim, 2);
        Assert.Equal(2, sim.State.Turn);
        Assert.True(sim.State.PlayerById(1).EndedTurn);
    }

    [Fact]
    public void Sequential_turns_hand_the_move_from_player_to_player_and_run_AI_in_its_place()
    {
        var sim = Game(TurnMode.Sequential, null, Ai, null, Ai);
        Assert.Equal([[0], [1], [2], [3]], sim.State.TurnOrder.Segments);
        Assert.Equal(0, sim.State.TurnOrder.Active);
        Assert.False(sim.State.PlayerById(1).EndedTurn); // AI waits for its own segment
        Assert.Equal("Not your turn", Rejection(sim, 2));

        End(sim, 0);
        Assert.True(sim.State.PlayerById(1).EndedTurn);
        Assert.Equal(2, sim.State.TurnOrder.Active);
        Assert.False(sim.MayAct(0));
        Assert.True(sim.MayAct(2));

        End(sim, 2);
        Assert.Equal(2, sim.State.Turn);
        Assert.Equal(0, sim.State.TurnOrder.Active);
        Assert.True(sim.MayAct(0));
    }

    [Fact]
    public void Dynamic_turns_separate_players_at_war_from_the_next_turn_on()
    {
        var sim = Game(TurnMode.Dynamic, null, null, Ai);
        Assert.Equal([[0, 1, 2]], sim.State.TurnOrder.Segments);

        sim.State.SetWar(0, 1, true); // declared mid-turn: this turn's order stands
        Assert.True(sim.MayAct(1));
        End(sim, 0);
        End(sim, 1);

        Assert.Equal(2, sim.State.Turn);
        Assert.Equal([[0, 2], [1]], sim.State.TurnOrder.Segments);
        Assert.True(sim.State.PlayerById(2).EndedTurn); // at peace with player 0, so it moved at the turn start
        Assert.Equal("Not your turn", Rejection(sim, 1));
        End(sim, 0);
        Assert.True(sim.MayAct(1));
        End(sim, 1);
        Assert.Equal(3, sim.State.Turn);
    }

    [Fact]
    public void An_all_AI_run_of_segments_never_advances_the_turn_on_its_own()
    {
        var sim = Game(TurnMode.Sequential, Ai, Ai, Ai);

        Assert.True(sim.State.TurnOrder.Finished);
        Assert.All(sim.State.Players.Where(p => p.Ai != null), p => Assert.True(p.EndedTurn));
        Assert.Equal(1, sim.State.Turn);
    }

    [Fact]
    public void Rules_issue_commands_for_any_player_whatever_the_segment()
    {
        var sim = Game(TurnMode.Sequential, null, null);

        Assert.False(sim.MayAct(1));
        Assert.True(sim.TryApply(new EndTurnCommand { Player = 1 }, out var error), error);
    }

    [Fact]
    public void A_player_that_joins_mid_turn_is_bound_by_no_segment()
    {
        var sim = Game(TurnMode.Sequential, null, null);
        sim.State.Players.Add(new Player { Id = 2, TeamId = 0 });
        sim.State.TeamById(0).Members.Add(2);
        sim.State.RebuildIndexes();

        Assert.True(sim.MayAct(2));
    }

    [Fact]
    public void The_segment_in_progress_survives_save_and_load()
    {
        var sim = Game(TurnMode.Sequential, null, Ai, null);
        End(sim, 0);

        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));

        Assert.Equal(sim.State.TurnOrder.Segments, loaded.State.TurnOrder.Segments);
        Assert.Equal(2, loaded.State.TurnOrder.Active);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal("Not your turn", Rejection(loaded, 0));
        End(loaded, 2);
        Assert.Equal(2, loaded.State.Turn);
    }

    [Fact]
    public void A_session_shows_whom_it_waits_for_once_its_segment_has_moved()
    {
        var session = new GameSession(Game(TurnMode.Sequential, null, Ai, null));
        Assert.Null(session.WaitingFor());

        session.EndTurn(); // the other human's segment has not come, so only the local player ends

        Assert.Equal(1, session.Sim.State.Turn);
        Assert.False(session.IsMyMove);
        Assert.Equal(session.Sim.State.PlayerById(2).Name, session.WaitingFor());
        Assert.StartsWith("Can't do that: Not your turn", Submitted(session));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ending_an_absent_humans_segment_runs_the_moves_that_follow_it_without_ending_the_next_turn(bool background)
    {
        var session = new GameSession(Game(TurnMode.Sequential, null, null, Ai), backgroundTurns: background);

        session.EndTurn(); // the local player's segment ends; the absent human's segment comes next
        Assert.Equal(1, session.Sim.State.Turn);
        Assert.False(session.TurnInProgress);

        session.EndTurn(); // ends the absent human's segment, which leaves the AI's moves and the rollover
        Assert.Equal(background, session.TurnInProgress);
        session.WaitForOtherPlayers();

        Assert.Equal(2, session.Sim.State.Turn);
        Assert.False(session.Sim.HasPendingTurnWork);
        Assert.True(session.IsMyMove);
        Assert.False(session.Sim.State.PlayerById(session.LocalPlayer).EndedTurn);
    }

    private static string Submitted(GameSession session)
    {
        session.Submit(new EndTurnCommand { Player = session.LocalPlayer });
        return session.Message;
    }
}
