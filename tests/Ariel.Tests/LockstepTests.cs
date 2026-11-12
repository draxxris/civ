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
}
