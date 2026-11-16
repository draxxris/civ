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
    public void What_a_peer_sends_just_before_it_disposes_arrives_before_it_leaves()
    {
        // Disposing used to cancel the writers at once, so a last report or chat line sent before quitting was lost.
        string line = new('x', 1000);
        for (int round = 0; round < 10; round++)
        {
            var host = TcpTransport.Host(0);
            var guest = Join(host);
            var atHost = Inbox(host);
            var atGuest = Inbox(guest);
            var hostSaw = new List<int>();
            var guestSaw = new List<int>();
            host.PeerLeft += hostSaw.Add;
            guest.PeerLeft += guestSaw.Add;
            // Both sides heartbeat and talk, so each holds unread frames from the other when one of them closes.
            guest.Broadcast(NetMessage.OfText(NetMessageKind.Chat, 1, "hello"));
            host.Broadcast(NetMessage.OfText(NetMessageKind.Chat, 0, "hello"));
            Until(() => atHost.Count == 1 && atGuest.Count == 1, host, guest);
            atHost.Clear();
            atGuest.Clear();

            var (sender, receiver, inbox, left, peer) = round % 2 == 0 ? (guest, host, atHost, hostSaw, 1) : (host, guest, atGuest, guestSaw, 0);
            for (int i = 0; i < 200; i++) sender.Broadcast(NetMessage.OfText(NetMessageKind.Chat, peer, $"{i} {line}"));
            sender.Dispose();
            Until(() => left.Contains(peer), receiver);

            Assert.Equal(Enumerable.Range(0, 200).Select(i => $"{i} {line}"), inbox.Select(m => m.Text));
            receiver.Dispose();
        }
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

    /// <summary>A host that welcomes one client as peer 1, then says nothing: no heartbeat, no messages.</summary>
    private static (TcpListener Listener, Task<TcpClient> Accepted) QuietHost()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        async Task<TcpClient> Welcome()
        {
            var client = await listener.AcceptTcpClientAsync();
            var body = System.Text.Encoding.UTF8.GetBytes("{\"Type\":0,\"Peer\":1,\"Message\":null}");
            var frame = new byte[4 + body.Length];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(frame, body.Length);
            body.CopyTo(frame, 4);
            await client.GetStream().WriteAsync(frame);
            return client;
        }
        return (listener, Welcome());
    }

    [Fact]
    public async Task A_short_connect_timeout_does_not_shorten_how_long_the_host_may_be_silent()
    {
        // The two-process smoke check joined with a 5 second connect timeout, which also became the link's silence
        // limit; the guest then dropped the host while both were still building the world.
        var (listener, accepted) = QuietHost();
        try
        {
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            using var guest = await TcpTransport.JoinAsync("localhost", port, connectTimeout: TimeSpan.FromSeconds(1));
            using var host = await accepted;
            var left = new List<int>();
            guest.PeerLeft += left.Add;

            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(3)) { guest.Poll(); await Task.Delay(20); }

            Assert.Empty(left);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task A_joined_peer_drops_a_host_silent_past_its_silence_timeout()
    {
        var (listener, accepted) = QuietHost();
        try
        {
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            using var guest = await TcpTransport.JoinAsync("localhost", port, silenceTimeout: TimeSpan.FromSeconds(2));
            using var host = await accepted;
            var left = new List<int>();
            guest.PeerLeft += left.Add;

            Until(() => left.Contains(0), guest);
        }
        finally { listener.Stop(); }
    }

    /// <summary>A game thread's context, as Godot installs one: continuations posted to it run only when the thread
    /// pumps it, between frames.</summary>
    private sealed class FrameContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback, object?)> _posted = new();
        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));
        public void Pump()
        {
            while (_posted.TryDequeue(out var item)) item.Item1(item.Item2);
        }
    }

    [Fact]
    public void A_busy_game_thread_does_not_stall_the_links_it_opened()
    {
        // The smoke check hosted and joined from the game thread, whose context every loop then resumed on: while the
        // thread built the world, nothing was read, written or heartbeated, and the guest dropped the host.
        var previous = SynchronizationContext.Current;
        var frames = new FrameContext();
        SynchronizationContext.SetSynchronizationContext(frames);
        try
        {
            var silence = TimeSpan.FromSeconds(2);
            using var host = TcpTransport.Host(0, silence);
            var joining = TcpTransport.JoinAsync("localhost", host.Port, silenceTimeout: silence);
            var clock = Stopwatch.StartNew();
            while (!joining.IsCompleted && clock.Elapsed < Patience) { frames.Pump(); Thread.Sleep(2); }
#pragma warning disable xUnit1031 // This test is the game thread: it blocks and pumps, it never awaits.
            using var guest = joining.GetAwaiter().GetResult();
#pragma warning restore xUnit1031
            var left = new List<int>();
            host.PeerLeft += left.Add;
            guest.PeerLeft += left.Add;
            var atHost = Inbox(host);

            // The game thread is busy past the silence limit and never pumps its context, only the transports.
            clock.Restart();
            while (clock.Elapsed < silence * 2) { host.Poll(); guest.Poll(); Thread.Sleep(20); }
            guest.Broadcast(NetMessage.OfText(NetMessageKind.Chat, 1, "still here"));
            Until(() => atHost.Count == 1, host, guest);
            frames.Pump();
            host.Poll(); guest.Poll();

            Assert.Empty(left);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public void A_header_announcing_a_huge_frame_costs_only_the_bytes_that_arrive()
    {
        using var host = TcpTransport.Host(0);
        var left = new List<int>();
        host.PeerLeft += left.Add;
        long before = GC.GetTotalAllocatedBytes(true);
        using (var stranger = new TcpClient())
        {
            stranger.Connect("localhost", host.Port);
            var stream = stranger.GetStream();
            stream.Write([0x1F, 0xFF, 0xFF, 0xFF]); // a frame of about 512 MB
            stream.Write(new byte[1024]);           // of which only this arrives
        }

        Until(() => left.Contains(1), host);

        // The old reader allocated the whole announced frame up front.
        Assert.True(GC.GetTotalAllocatedBytes(true) - before < 128L * 1024 * 1024);
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
