using Ariel.BaseGame;
using Ariel.Game;
using Ariel.Net;
using Xunit;

namespace Ariel.Tests;

public class LockstepTests
{
    private static (LockstepSession a, LockstepSession b, Simulation simA, Simulation simB) Pair()
    {
        var hub = new InProcessHub();
        var simA = TestGame.Sim(); var simB = TestGame.Sim();
        var a = new LockstepSession(simA, new CommandCodec(simA.Content), hub.CreateEndpoint(), [0, 1], 0);
        var b = new LockstepSession(simB, new CommandCodec(simB.Content), hub.CreateEndpoint(), [0, 1], 1);
        return (a, b, simA, simB);
    }

    // Lockstep never goes idle on its own (empty batches keep ticks flowing), so pump a bounded number of rounds.
    private static void PumpAll(LockstepSession a, LockstepSession b, int rounds = 8)
    {
        for (int i = 0; i < rounds; i++) { a.Pump(); b.Pump(); }
    }

    [Fact]
    public void TwoPeersStayInSyncAndConverge()
    {
        var (a, b, simA, simB) = Pair();
        bool desync = false;
        a.Desync += _ => desync = true;
        b.Desync += _ => desync = true;

        var land = TestGame.FindLand(simA);
        a.Enqueue(new FoundCityCommand { Player = 0, Position = land });
        b.Enqueue(new SetResearchCommand { Player = 1, Tech = "base:mining" });
        PumpAll(a, b);
        a.Enqueue(new EndTurnCommand { Player = 0 });
        b.Enqueue(new EndTurnCommand { Player = 1 });
        PumpAll(a, b, 20);

        Assert.False(desync);
        Assert.Single(simA.State.Cities);
        Assert.Equal(2, simA.State.Turn);
        Assert.Equal(simA.ComputeHash(), simB.ComputeHash());
        Assert.True(a.Tick > 2);
    }

    [Fact]
    public void ContentMismatchFailsHandshakeBeforeAnyTickExecutes()
    {
        var hub = new InProcessHub();
        var simA = TestGame.Sim();
        var changed = Ariel.Modding.ModLoader.FromAssembly(
            new Ariel.Modding.ModManifest { Id = "base", Version = "different" },
            typeof(TestRulesetMod).Assembly, AppContext.BaseDirectory);
        var simB = new Simulation(Ariel.Modding.ModLoader.Build([changed]), new SimulationSettings());
        var a = new LockstepSession(simA, new CommandCodec(simA.Content), hub.CreateEndpoint(), [0, 1], 0);
        var b = new LockstepSession(simB, new CommandCodec(simB.Content), hub.CreateEndpoint(), [0, 1], 1);
        a.Pump();
        Assert.Contains("content mismatch", Assert.Throws<InvalidDataException>(() => b.Pump()).Message);
        Assert.Throws<InvalidDataException>(() => a.Pump());
        Assert.Equal(0, a.Tick);
        Assert.Equal(0, b.Tick);
    }

    [Fact]
    public void DivergentStateRaisesDesync()
    {
        var (a, b, simA, _) = Pair();
        bool desync = false;
        a.Desync += _ => desync = true;
        simA.State.Turn = 99; // simulate a nondeterminism bug
        a.Enqueue(new EndTurnCommand { Player = 0 }); // only ticks that apply commands are hashed
        PumpAll(a, b, 10);
        Assert.True(desync);
    }

    [Theory]
    [InlineData("{\"type\":\"no:such\",\"data\":{}}")]
    [InlineData("{ not json")]
    [InlineData("null")]
    public void AnUndecodableCommandIsRefusedAndNeverReplaysTheCommandsBeforeIt(string bad)
    {
        var hub = new InProcessHub();
        var sim = TestGame.Sim();
        var a = new LockstepSession(sim, new CommandCodec(sim.Content), hub.CreateEndpoint(), [0, 1], 0);
        a.Deliver(new NetMessage(NetMessageKind.Handshake, 0, 1, [], 0, sim.Content.ContentHash));
        a.Enqueue(new FoundCityCommand { Player = 0, Position = TestGame.FindLand(sim) });
        a.Pump(); // runs the agreed empty ticks and submits the city for tick 2
        Assert.Equal(2, a.Tick);
        // Peer 1's batch for tick 2 applies after peer 0's city and cannot be decoded; a null batch follows.
        a.Deliver(new NetMessage(NetMessageKind.Commands, 2, 1, [bad], 0));
        a.Deliver(new NetMessage(NetMessageKind.Commands, 3, 1, null!, 0));

        a.Pump(maxTicks: 1);
        long applied = a.Applied;
        a.Pump(maxTicks: 1);

        Assert.Equal(4, a.Tick);
        Assert.Equal(applied, a.Applied);
        Assert.Single(sim.State.Cities);
    }

    /// <summary>Peer 0's session, with peer 1 played by hand through raw messages.</summary>
    private static (LockstepSession a, Simulation sim, CommandCodec codec) WithScriptedPeer()
    {
        var hub = new InProcessHub();
        var sim = TestGame.Sim();
        var codec = new CommandCodec(sim.Content);
        var a = new LockstepSession(sim, codec, hub.CreateEndpoint(), [0, 1], 0);
        a.Deliver(new NetMessage(NetMessageKind.Handshake, 0, 1, [], 0, sim.Content.ContentHash));
        a.Pump(); // runs the agreed empty ticks; tick 2 waits for peer 1
        Assert.Equal(2, a.Tick);
        return (a, sim, codec);
    }

    [Fact]
    public void A_peers_first_batch_for_a_tick_stands_and_a_different_second_one_is_a_fault()
    {
        var (a, sim, codec) = WithScriptedPeer();
        var land = TestGame.FindLand(sim);
        string found = codec.Encode(new FoundCityCommand { Player = 1, Position = land });
        a.Deliver(new NetMessage(NetMessageKind.Commands, 2, 1, [], 0));
        a.Deliver(new NetMessage(NetMessageKind.Commands, 2, 1, [found], 0));
        a.Deliver(new NetMessage(NetMessageKind.Commands, 2, 1, [], 0)); // an identical repeat is harmless

        a.Pump(maxTicks: 1);

        Assert.Equal(3, a.Tick);
        Assert.Empty(sim.State.Cities);
        Assert.Contains("second, different batch for tick 2", a.LastFault);
    }

    [Fact]
    public void A_relayed_batch_claiming_to_be_the_local_peers_is_ignored()
    {
        var (a, sim, codec) = WithScriptedPeer();
        string found = codec.Encode(new FoundCityCommand { Player = 0, Position = TestGame.FindLand(sim) });
        a.Deliver(new NetMessage(NetMessageKind.Commands, 4, 0, [found], 0)); // before peer 0 submits tick 4 itself
        for (int t = 2; t <= 4; t++) a.Deliver(new NetMessage(NetMessageKind.Commands, t, 1, [], 0));

        a.Pump();

        Assert.Equal(5, a.Tick);
        Assert.Empty(sim.State.Cities);
    }

    [Fact]
    public void A_far_future_batch_cannot_keep_a_departed_peer_a_member()
    {
        var (a, _, _) = WithScriptedPeer();
        a.Deliver(new NetMessage(NetMessageKind.Commands, 2, 1, [], 0));
        a.Deliver(new NetMessage(NetMessageKind.Commands, int.MaxValue - 1, 1, [], 0));
        Assert.Equal(2, a.LastBatchTick(1));
        Assert.Contains("beyond", a.LastFault);

        a.RemovePeer(1, a.LastBatchTick(1));
        a.Pump(maxTicks: 10); // alone, the session would run ticks forever: bound it

        Assert.Equal(12, a.Tick);
    }

    [Fact]
    public void A_late_handshake_and_a_revised_hash_cannot_stop_or_desync_the_game()
    {
        var (a, sim, codec) = WithScriptedPeer();
        bool desync = false;
        a.Desync += _ => desync = true;
        a.Deliver(new NetMessage(NetMessageKind.Handshake, 0, 1, [], 0, "some other content"));
        a.Enqueue(new SetResearchCommand { Player = 0, Tech = "base:mining" }); // goes out for tick 4
        for (int t = 2; t <= 4; t++) a.Deliver(new NetMessage(NetMessageKind.Commands, t, 1, [], 0));
        a.Pump(maxTicks: 3);
        Assert.Equal(5, a.Tick);
        var copy = SaveGame.Load(sim.Content, SaveGame.Save(sim));

        a.Deliver(new NetMessage(NetMessageKind.Hash, 4, 1, [], copy.ComputeHash()));
        a.Deliver(new NetMessage(NetMessageKind.Hash, 4, 1, [], 12345));

        Assert.False(desync);
    }
}
