using Ariel.Client;
using Ariel.Game;
using Ariel.Net;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Two front ends, one per human, playing the full ruleset through lockstep.</summary>
public sealed class LockstepChannelTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private sealed record Peer(GameSession Session, LockstepSession Lockstep, LockstepChannel Channel, ITransport Link);

    private (Peer A, Peer B) Pair(TurnMode mode = TurnMode.Simultaneous, int inputDelay = 2)
    {
        var hub = new InProcessHub();
        Peer Make(int seat, ITransport link)
        {
            var sim = new Simulation(mods.Content, GameFactory.Settings(3) with { TurnMode = mode });
            var lockstep = new LockstepSession(sim, new CommandCodec(sim.Content), link, [0, 1], seat, inputDelay);
            var channel = new LockstepChannel(lockstep);
            return new Peer(new GameSession(sim, seat, channel), lockstep, channel, link);
        }
        return (Make(0, hub.CreateEndpoint()), Make(1, hub.CreateEndpoint()));
    }

    private static void Run(Peer a, Peer b, Func<bool> until)
    {
        for (int i = 0; i < 400 && !until(); i++) { a.Session.Pump(); b.Session.Pump(); }
        Assert.True(until(), "lockstep did not get there");
    }

    private static Unit Warrior(GameSession s) => s.Sim.State.Units.First(u => u.Owner == s.LocalPlayer && !s.DefOf(u).CanFoundCity);

    [Fact]
    public void Both_peers_see_each_others_moves_and_advance_the_turn_together()
    {
        var (a, b) = Pair();
        bool desync = false;
        a.Lockstep.Desync += _ => desync = true;
        b.Lockstep.Desync += _ => desync = true;
        var settler = a.Session.Sim.State.Units.First(u => u.Owner == 0 && a.Session.DefOf(u).CanFoundCity);
        a.Session.SelectUnit(settler.Id);
        a.Session.RunAction("found");
        Assert.Empty(a.Session.Sim.State.Cities); // queued, not applied

        Run(a, b, () => a.Session.Sim.State.Cities.Count == 1 && b.Session.Sim.State.Cities.Count == 1);

        a.Session.EndTurn();
        Run(a, b, () => a.Session.Sim.State.PlayerById(0).EndedTurn && b.Session.Sim.State.PlayerById(0).EndedTurn);
        Assert.Equal(1, a.Session.Sim.State.Turn);
        Assert.Equal(b.Session.Sim.State.PlayerById(1).Name, a.Session.WaitingFor());
        b.Session.EndTurn();
        Run(a, b, () => a.Session.Sim.State.Turn == 2 && b.Session.Sim.State.Turn == 2);

        Assert.False(desync);
        Assert.Equal(a.Session.Sim.ComputeHash(), b.Session.Sim.ComputeHash());
        Assert.Null(a.Session.WaitingFor());
        Assert.Contains(a.Session.Notifications, n => n.Turn == 1);
    }

    [Fact]
    public void A_second_end_turn_sent_before_the_first_applied_does_not_end_the_next_turn()
    {
        var (a, b) = Pair();
        b.Session.EndTurn();
        Run(a, b, () => a.Session.Sim.State.PlayerById(1).EndedTurn && b.Session.Sim.State.PlayerById(1).EndedTurn);

        // Both presses go out before either applies; the first ends the turn and the turn rolls over at once.
        a.Session.EndTurn();
        a.Session.EndTurn();
        Run(a, b, () => a.Session.Sim.State.Turn == 2 && b.Session.Sim.State.Turn == 2);
        for (int i = 0; i < 20; i++) { a.Session.Pump(); b.Session.Pump(); }

        Assert.False(a.Session.Sim.State.PlayerById(0).EndedTurn);
        Assert.False(b.Session.Sim.State.PlayerById(0).EndedTurn);
        Assert.Equal(a.Session.Sim.ComputeHash(), b.Session.Sim.ComputeHash());
    }

    [Fact]
    public void A_peer_that_loses_the_host_runs_the_ticks_it_holds_before_it_stops()
    {
        // A long input delay lets the host run well ahead of a guest that is slow to pump.
        var (a, b) = Pair(inputDelay: 6);
        Run(a, b, () => a.Lockstep.Tick > 3 && b.Lockstep.Tick > 3);
        b.Link.PeerLeft += _ => b.Channel.FailAfterHeldTicks("The host left the game.");

        // The host runs ahead to every tick the guest has sent, then ends its turn; the guest runs one tick a frame,
        // just enough for the host to apply it, so it stays a whole input delay behind.
        a.Session.Pump();
        a.Session.EndTurn();
        for (int i = 0; i < 100 && !a.Session.Sim.State.PlayerById(0).EndedTurn; i++) { a.Session.Pump(); b.Lockstep.Pump(maxTicks: 1); }
        Assert.True(a.Session.Sim.State.PlayerById(0).EndedTurn);
        Assert.False(b.Session.Sim.State.PlayerById(0).EndedTurn);
        a.Link.Dispose(); // its batches for the ticks it ran went out first

        for (int i = 0; i < 100 && b.Session.ConnectionProblem == null; i++) b.Session.Pump();

        Assert.Equal("The host left the game.", b.Session.ConnectionProblem);
        Assert.True(b.Session.Sim.State.PlayerById(0).EndedTurn);
        Assert.Equal(a.Session.Sim.ComputeHash(), b.Session.Sim.ComputeHash());
    }

    [Fact]
    public void Commands_are_refused_once_the_host_is_gone()
    {
        var (a, b) = Pair();
        Run(a, b, () => a.Lockstep.Tick > 3 && b.Lockstep.Tick > 3);
        b.Channel.FailAfterHeldTicks("The host left the game.");

        // The ticks still held may run, but nothing new can join them.
        Assert.False(b.Session.Submit(new EndTurnCommand { Player = 1 }));
        Assert.Contains("The host left the game.", b.Session.Message);
    }

    [Fact]
    public void An_idle_peer_does_not_rebuild_the_world_each_frame()
    {
        var (a, b) = Pair();
        Run(a, b, () => a.Lockstep.Tick > 20);
        int world = a.Session.WorldRevision;

        for (int i = 0; i < 50; i++) { a.Session.Pump(); b.Session.Pump(); }

        Assert.True(a.Lockstep.Tick > 40);
        Assert.Equal(world, a.Session.WorldRevision);
    }

    [Fact]
    public void Sequential_mode_refuses_moves_out_of_turn_before_they_are_sent()
    {
        var (a, b) = Pair(TurnMode.Sequential);
        Assert.False(b.Session.IsMyMove);
        var warrior = Warrior(b.Session);

        Assert.False(b.Session.Submit(new SetUnitActivityCommand { Player = 1, Unit = warrior.Id, Activity = UnitActivity.Sleep }));
        Assert.Equal("Can't do that: Not your turn", b.Session.Message);

        a.Session.EndTurn();
        Run(a, b, () => b.Session.IsMyMove);
        Assert.True(b.Session.Submit(new SetUnitActivityCommand { Player = 1, Unit = warrior.Id, Activity = UnitActivity.Sleep }));
        Run(a, b, () => a.Session.ActivityOf(a.Session.Sim.State.FindUnit(warrior.Id)!) == UnitActivity.Sleep);
    }
}
