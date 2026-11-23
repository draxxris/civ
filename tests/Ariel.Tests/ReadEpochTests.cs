using Ariel.Game;
using Xunit;

namespace Ariel.Tests;

public sealed class ReadEpochTests
{
    private sealed class Probe : GameCommand
    {
        public bool Reject { get; init; }
        public bool Write { get; init; }
        public long SeenEpoch { get; private set; } = -1;
        public long OpenedInside { get; private set; } = -1;
        public override string? Validate(Simulation sim)
        {
            SeenEpoch = sim.ReadEpoch;
            return Reject ? "no" : null;
        }
        public override void Apply(Simulation sim)
        {
            using (sim.OpenReadEpoch()) OpenedInside = sim.ReadEpoch;
            if (Write) sim.State.Turn++;
        }
    }

    [Fact]
    public void Commands_end_the_open_epoch_and_no_epoch_opens_inside_one()
    {
        var sim = TestGame.Sim(5);
        Assert.Equal(0, sim.ReadEpoch);
        var first = sim.OpenReadEpoch();
        long a = sim.ReadEpoch;
        Assert.NotEqual(0, a);
        var probe = new Probe { Player = 0 };
        Assert.True(sim.TryApply(probe, out _));
        Assert.Equal(0, probe.SeenEpoch);
        Assert.Equal(0, probe.OpenedInside);
        Assert.Equal(0, sim.ReadEpoch);
        using (sim.OpenReadEpoch())
        {
            long b = sim.ReadEpoch;
            Assert.True(b > a, "an ended epoch's value is never reused");
            first.Dispose(); // A stale handle leaves the newer epoch open.
            Assert.Equal(b, sim.ReadEpoch);
        }
        Assert.Equal(0, sim.ReadEpoch);
        _ = sim.OpenReadEpoch();
        Assert.False(sim.TryApply(new Probe { Player = 0, Reject = true }, out _));
        Assert.Equal(0, sim.ReadEpoch);
        _ = sim.OpenReadEpoch();
        sim.AdvanceTurn();
        Assert.Equal(0, sim.ReadEpoch);
    }

    [Fact]
    public void Verification_catches_a_write_that_bypassed_the_kernel_and_a_rejection_that_wrote()
    {
        var sim = TestGame.Sim(5);
        sim.VerifyReadEpochs = true;
        using (sim.OpenReadEpoch()) _ = sim.Rng.Next(10); // The RNG stream is not part of the promise.
        Assert.True(sim.TryApply(new Probe { Player = 0, Write = true }, out _));
        var epoch = sim.OpenReadEpoch();
        sim.State.Turn++;
        Assert.Throws<InvalidOperationException>(epoch.Dispose);
        _ = sim.OpenReadEpoch();
        sim.State.Turn++;
        Assert.Throws<InvalidOperationException>(() => sim.TryApply(new Probe { Player = 0 }, out _));
        Assert.Throws<InvalidOperationException>(() => sim.TryApply(new WritingRejection { Player = 0 }, out _));
    }

    private sealed class WritingRejection : GameCommand
    {
        public override string? Validate(Simulation sim) { sim.State.Turn++; return "no"; }
        public override void Apply(Simulation sim) { }
    }
}
