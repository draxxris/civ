using System.Diagnostics;
using System.Net.Sockets;
using Ariel.BaseGame;
using Ariel.Game;
using Ariel.Net;
using Xunit;

namespace Ariel.Tests;

/// <summary>The TCP star over loopback: relaying, ordering, peer changes, liveness, and lockstep on top.</summary>
public sealed class TcpTransportTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>Poll every transport until the condition holds, or fail after <see cref="Patience"/>.</summary>
    private static void Until(Func<bool> done, params ITransport[] transports)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            if (clock.Elapsed > Patience) throw new TimeoutException("condition not met over loopback");
            foreach (var t in transports) t.Poll();
            Thread.Sleep(2);
        }
    }

    private static TcpTransport Join(TcpTransport host) => TcpTransport.JoinAsync("localhost", host.Port).GetAwaiter().GetResult();

    private static List<NetMessage> Inbox(ITransport t)
    {
        var list = new List<NetMessage>();
        t.Received += list.Add;
        return list;
    }

    [Fact]
    public void The_host_assigns_ids_and_relays_broadcasts_in_order_with_the_true_sender()
    {
        using var host = TcpTransport.Host(0);
        using var one = Join(host);
        using var two = Join(host);
        Assert.Equal((0, 1, 2), (host.LocalPeer, one.LocalPeer, two.LocalPeer));
        var atHost = Inbox(host);
        var atTwo = Inbox(two);

        for (int i = 0; i < 200; i++) one.Broadcast(NetMessage.OfText(NetMessageKind.Chat, peer: 7, $"line {i}"));
        Until(() => atHost.Count == 200 && atTwo.Count == 200, host, one, two);

        Assert.Equal(Enumerable.Range(0, 200).Select(i => $"line {i}"), atTwo.Select(m => m.Text));
        Assert.All(atHost.Concat(atTwo), m => Assert.Equal(1, m.Peer)); // a claimed sender is overwritten
    }

    [Fact]
    public void A_direct_message_reaches_only_its_recipient()
    {
        using var host = TcpTransport.Host(0);
        using var one = Join(host);
        using var two = Join(host);
        var atHost = Inbox(host);
        var atOne = Inbox(one);
        var atTwo = Inbox(two);

        two.SendTo(1, NetMessage.OfText(NetMessageKind.Chat, 2, "to one"));
        host.SendTo(2, NetMessage.OfText(NetMessageKind.Snapshot, 0, "to two"));
        one.SendTo(0, NetMessage.OfText(NetMessageKind.Lobby, 1, "to host"));
        Until(() => atOne.Count == 1 && atTwo.Count == 1 && atHost.Count == 1, host, one, two);
        Thread.Sleep(50);
        foreach (var t in new ITransport[] { host, one, two }) t.Poll();

        Assert.Equal("to one", Assert.Single(atOne).Text);
        Assert.Equal("to two", Assert.Single(atTwo).Text);
        Assert.Equal("to host", Assert.Single(atHost).Text);
    }

    [Fact]
    public void Peers_hear_about_each_other_joining_and_leaving()
    {
        using var host = TcpTransport.Host(0);
        var hostJoined = new List<int>();
        var hostLeft = new List<int>();
        host.PeerJoined += hostJoined.Add;
        host.PeerLeft += hostLeft.Add;
        using var one = Join(host);
        var oneJoined = new List<int>();
        var oneLeft = new List<int>();
        one.PeerJoined += oneJoined.Add;
        one.PeerLeft += oneLeft.Add;
        var two = Join(host);
        Until(() => hostJoined.Count == 2 && oneJoined.Contains(2), host, one);

        two.Dispose();
        Until(() => hostLeft.Contains(2) && oneLeft.Contains(2), host, one);

        host.Dispose();
        Until(() => oneLeft.Contains(0), one);
    }

    [Fact]
    public void A_peer_that_goes_silent_is_dropped_after_the_timeout()
    {
        using var host = TcpTransport.Host(0, TimeSpan.FromSeconds(2));
        var left = new List<int>();
        host.PeerLeft += left.Add;
        using var mute = new TcpClient();
        mute.Connect("localhost", host.Port); // connects but never sends a heartbeat

        Until(() => left.Contains(1), host);
    }

    [Fact]
    public void A_lockstep_game_over_tcp_stays_in_sync()
    {
        using var hostLink = TcpTransport.Host(0);
        using var clientLink = Join(hostLink);
        var simA = TestGame.Sim();
        var simB = TestGame.Sim();
        using var a = new LockstepSession(simA, new CommandCodec(simA.Content), hostLink, [0, 1], 0);
        using var b = new LockstepSession(simB, new CommandCodec(simB.Content), clientLink, [0, 1], 1);
        bool desync = false;
        a.Desync += _ => desync = true;
        b.Desync += _ => desync = true;

        a.Enqueue(new FoundCityCommand { Player = 0, Position = TestGame.FindLand(simA) });
        b.Enqueue(new EndTurnCommand { Player = 1 });
        a.Enqueue(new EndTurnCommand { Player = 0 });
        var clock = Stopwatch.StartNew();
        while (simA.State.Turn < 2 || simB.State.Turn < 2 || a.Tick < 10 || b.Tick < 10)
        {
            if (clock.Elapsed > Patience) throw new TimeoutException($"ticks {a.Tick}/{b.Tick}, turns {simA.State.Turn}/{simB.State.Turn}");
            a.Pump();
            b.Pump();
            Thread.Sleep(1);
        }

        Assert.False(desync);
        Assert.Single(simB.State.Cities);
        Assert.Equal(simA.ComputeHash(), simB.ComputeHash());
    }
}
