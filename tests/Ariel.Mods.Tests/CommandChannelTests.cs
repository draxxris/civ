using Ariel.Client;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A session drives a networked game the same way as a local one: everything goes through its channel, and
/// whatever the channel delivers, from any player, is what the session reacts to.</summary>
public sealed class CommandChannelTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    /// <summary>Stands in for lockstep: commands queue until the test delivers them, as the network would.</summary>
    private sealed class QueuedChannel : ICommandChannel
    {
        private readonly List<GameCommand> _ready = [];
        public List<GameCommand> Sent { get; } = [];
        public bool PlaysAbsentHumans => false;

        public Submission Submit(Simulation sim, GameCommand command)
        {
            if (command.Validate(sim) is { } error) return Submission.Rejected(error);
            Sent.Add(command);
            return Submission.Queued;
        }

        /// <summary>Deliver what this peer sent, then commands from other peers.</summary>
        public void Deliver(params GameCommand[] remote)
        {
            _ready.AddRange(Sent);
            Sent.Clear();
            _ready.AddRange(remote);
        }

        public bool Pump(Simulation sim)
        {
            if (_ready.Count == 0) return false;
            foreach (var command in _ready) sim.TryApply(command, out _);
            _ready.Clear();
            return true;
        }
    }

    private (GameSession Session, QueuedChannel Channel) Networked(int seat = 0)
    {
        var channel = new QueuedChannel();
        return (new GameSession(GameFactory.Create(mods.Content, 3), seat, channel), channel);
    }

    private static Unit Warrior(GameSession s) => s.Sim.State.Units.First(u => u.Owner == s.LocalPlayer && !s.DefOf(u).CanFoundCity);

    private static Hex FarLandTarget(GameSession s, Unit unit)
    {
        var grid = s.Sim.State.Grid;
        var path = s.Sim.Content.Services.Get<IPathfindingService>().Single();
        foreach (var tile in grid.Range(grid.TileAt(unit.Position), 7).OrderByDescending(t => grid.Distance(grid.TileAt(unit.Position), t)))
            if (path.FindPath(s.Sim, unit.Id, grid.HexOf(tile)) is { } p && p.Steps.Count >= 5) return grid.HexOf(tile);
        throw new InvalidOperationException("no far land tile");
    }

    [Fact]
    public void A_queued_command_changes_the_world_only_when_the_channel_delivers_it()
    {
        var (s, channel) = Networked();
        var warrior = Warrior(s);
        var start = warrior.Position;
        s.SelectUnit(warrior.Id);
        int world = s.WorldRevision;

        s.RightClick(FarLandTarget(s, warrior));

        Assert.Equal("", s.Message);
        Assert.Equal(start, warrior.Position);
        Assert.Equal(world, s.WorldRevision);
        Assert.Single(channel.Sent);

        s.Pump();
        Assert.Equal(start, warrior.Position); // Nothing delivered yet.

        channel.Deliver();
        s.Pump();
        Assert.NotEqual(start, warrior.Position);
        Assert.Equal(world + 1, s.WorldRevision);
        Assert.True(s.HasGoto(warrior.Id));
    }

    [Fact]
    public void A_rejected_command_is_reported_and_never_sent()
    {
        var (s, channel) = Networked();

        Assert.False(s.Submit(new MoveUnitPathCommand { Player = s.LocalPlayer, Unit = 999_999, Path = [] }));

        Assert.StartsWith("Can't do that:", s.Message);
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void Ending_the_turn_online_leaves_other_humans_to_their_own_peers()
    {
        var (s, channel) = Networked();
        int turn = s.Sim.State.Turn;

        s.EndTurn();

        var sent = Assert.Single(channel.Sent);
        Assert.Equal(s.LocalPlayer, Assert.IsType<EndTurnCommand>(sent).Player);
        channel.Deliver();
        s.Pump();
        Assert.Equal(turn, s.Sim.State.Turn);
        Assert.False(s.Sim.State.PlayerById(1).EndedTurn);
    }

    [Fact]
    public void A_turn_another_peer_completes_continues_standing_orders_through_the_channel()
    {
        var (s, channel) = Networked();
        var warrior = Warrior(s);
        s.SelectUnit(warrior.Id);
        s.RightClick(FarLandTarget(s, warrior));
        channel.Deliver();
        s.Pump();
        var stopped = warrior.Position;
        int turn = s.Sim.State.Turn;

        s.EndTurn();
        channel.Deliver(new EndTurnCommand { Player = 1 }); // The other human's peer ends last.
        s.Pump();

        Assert.Equal(turn + 1, s.Sim.State.Turn);
        Assert.Equal(stopped, warrior.Position); // The next leg is a command too, so it waits for delivery.
        var walk = Assert.IsType<MoveUnitPathCommand>(Assert.Single(channel.Sent));
        Assert.Equal(warrior.Id, walk.Unit);
        channel.Deliver();
        s.Pump();
        Assert.NotEqual(stopped, warrior.Position);
    }

    [Fact]
    public void A_session_controls_whichever_seat_it_was_given()
    {
        var (s, _) = Networked(seat: 1);

        Assert.Equal(1, s.Me.Id);
        Assert.Equal(1, s.Unit?.Owner);
        var other = s.Sim.State.Units.First(u => u.Owner == 0);
        s.SelectUnit(other.Id);
        Assert.Equal(1, s.Unit?.Owner);
        Assert.All(s.Sim.State.Units.Where(u => u.Owner == 0), u => Assert.False(s.HasGoto(u.Id)));
    }

    [Fact]
    public void A_session_refuses_a_seat_the_game_does_not_have()
    {
        var sim = GameFactory.Create(mods.Content, 3);
        Assert.Throws<ArgumentException>(() => new GameSession(sim, 7));
    }
}
