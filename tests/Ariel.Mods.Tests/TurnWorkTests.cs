using Ariel.Client;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Turn work deferred off the command that ends a turn, run on a copy and taken back: the game must play out
/// exactly as when the work runs in place.</summary>
public sealed class TurnWorkTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Ai = "ai:standard";

    private Simulation Game(TurnMode mode, bool defer = false) => new(mods.Content, new SimulationSettings
    {
        Seed = 9, WorldSize = "foundation:worldsize_duel", MapGenerator = "world:continents",
        GameSpeed = "foundation:gamespeed_normal", Handicap = "foundation:handicap_noble",
        StartingEra = "foundation:era_ancient", PlayerCount = 3,
        Leaders = ["peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar", "peoples:leader_bismarck"],
        PlayerAis = [null, Ai, Ai],
        TurnMode = mode,
    }) { DeferTurnWork = defer };

    private static void End(Simulation sim, int player = 0) =>
        Assert.True(sim.TrySubmit(new EndTurnCommand { Player = player }, out var error), error);

    private static void RunOnAnotherThread(Simulation sim)
    {
        var work = sim.BeginTurnWork();
        Task.Run(work.Run).Wait();
        sim.CompleteTurnWork(work);
    }

    [Theory]
    [InlineData(TurnMode.Simultaneous)]
    [InlineData(TurnMode.Dynamic)]
    [InlineData(TurnMode.Sequential)]
    public void Work_run_on_a_copy_plays_the_turns_exactly_as_in_place(TurnMode mode)
    {
        var inPlace = Game(mode);
        var deferred = Game(mode, defer: true);
        for (int turn = 0; turn < 6; turn++)
        {
            End(inPlace);
            End(deferred);
            Assert.True(deferred.HasPendingTurnWork);
            Assert.Equal(inPlace.State.Turn - 1, deferred.State.Turn); // Nothing moved yet.
            RunOnAnotherThread(deferred);
            Assert.False(deferred.HasPendingTurnWork);
            Assert.Equal(inPlace.State.Turn, deferred.State.Turn);
            Assert.Equal(inPlace.ComputeHash(), deferred.ComputeHash());
            Assert.Equal(inPlace.Events.Published.Count, deferred.Events.Published.Count);
            Assert.Equal(inPlace.Events.PreviousTurn.Count, deferred.Events.PreviousTurn.Count);
        }
    }

    [Fact]
    public void A_command_after_pending_work_applies_after_that_work()
    {
        var inPlace = Game(TurnMode.Dynamic);
        var deferred = Game(TurnMode.Dynamic, defer: true);
        End(inPlace);
        End(deferred);
        Assert.True(deferred.HasPendingTurnWork);
        var handBack = new SetPlayerControlCommand { Player = 0, Target = 2, Ai = null };
        Assert.True(inPlace.TrySubmit(handBack, out var error), error);
        Assert.True(deferred.TrySubmit(handBack, out error), error);
        Assert.Equal(inPlace.ComputeHash(), deferred.ComputeHash());
    }

    [Fact]
    public void A_save_made_while_work_waits_carries_the_work()
    {
        var inPlace = Game(TurnMode.Simultaneous);
        var deferred = Game(TurnMode.Simultaneous, defer: true);
        End(inPlace);
        End(deferred);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(deferred));
        Assert.True(loaded.HasPendingTurnWork);
        loaded.RunPendingTurnWork();
        Assert.Equal(inPlace.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Work_cannot_overwrite_a_command_applied_after_it_began()
    {
        var sim = Game(TurnMode.Simultaneous, defer: true);
        End(sim);
        var work = sim.BeginTurnWork();
        work.Run();
        sim.RunPendingTurnWork();
        Assert.Throws<InvalidOperationException>(() => sim.CompleteTurnWork(work));
    }

    [Fact]
    public void A_background_session_shows_the_old_turn_until_the_others_finish()
    {
        var reference = Game(TurnMode.Simultaneous);
        var session = new GameSession(Game(TurnMode.Simultaneous), backgroundTurns: true);
        var steps = new List<StepPlayback>();
        for (int turn = 0; turn < 4; turn++)
        {
            int before = session.Sim.State.Turn;
            End(reference);
            session.EndTurn();
            Assert.True(session.TurnInProgress);
            Assert.Equal(before, session.Sim.State.Turn);
            Assert.Equal("", session.WaitingFor());
            Assert.False(session.Submit(new EndTurnCommand { Player = 0 }));

            session.WaitForOtherPlayers();
            Assert.False(session.TurnInProgress);
            Assert.Equal(before + 1, session.Sim.State.Turn);
            Assert.Equal(reference.ComputeHash(), session.Sim.ComputeHash());
            Assert.Null(session.WaitingFor());
            while (session.DequeuePlayback() is { } item) if (item is StepPlayback step) steps.Add(step);
        }
        Assert.Contains(steps, s => s.Owner != session.LocalPlayer);
        Assert.All(steps, s => Assert.Equal(1, session.Sim.State.Grid.Distance(s.From, s.To)));
    }

    [Fact]
    public void A_walk_replays_each_step_from_where_the_unit_stood()
    {
        var session = new GameSession(Game(TurnMode.Simultaneous));
        var unit = session.Sim.State.Units.First(u => u.Owner == 0 && session.DefOf(u).Strength > 0);
        var start = unit.Position;
        session.SelectUnit(unit.Id);
        var grid = session.Sim.State.Grid;
        var target = grid.Neighbors(grid.TileAt(start)).Select(grid.HexOf).First(h => session.PreviewAt(h) is { Path.Count: 1, Attack: null });
        while (session.DequeuePlayback() is not null) { }

        session.RightClick(target);

        var step = Assert.IsType<StepPlayback>(session.DequeuePlayback());
        Assert.Equal((unit.Id, unit.Type, 0, start, target), (step.UnitId, step.Type, step.Owner, step.From, step.To));
        Assert.Null(session.DequeuePlayback());
    }
}
