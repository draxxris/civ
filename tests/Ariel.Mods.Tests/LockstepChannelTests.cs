using Ariel.Client;
using Ariel.Game;
using Ariel.Net;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Two front ends, one per human, playing the full ruleset through lockstep.</summary>
public sealed class LockstepChannelTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private sealed record Peer(GameSession Session, LockstepSession Lockstep);

    private (Peer A, Peer B) Pair(TurnMode mode = TurnMode.Simultaneous)
    {
        var hub = new InProcessHub();
        Peer Make(int seat, ITransport link)
        {
            var sim = new Simulation(mods.Content, GameFactory.Settings(3) with { TurnMode = mode });
            var lockstep = new LockstepSession(sim, new CommandCodec(sim.Content), link, [0, 1], seat);
            return new Peer(new GameSession(sim, seat, new LockstepChannel(lockstep)), lockstep);
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
