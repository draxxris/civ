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

    [Fact]
    public void A_collection_inside_an_epoch_hands_out_independent_copies_of_the_same_sums()
    {
        var sim = TestGame.Sim(5);
        var context = new Ariel.Content.ModifierContext(sim, Ariel.Content.ModifierScope.City, 0);
        var outside = Ariel.Content.ModifierSink.Collect(context);
        using (sim.OpenReadEpoch())
        {
            var first = Ariel.Content.ModifierSink.Collect(context);
            first.Add("test", "test:extra", 5);
            var second = Ariel.Content.ModifierSink.Collect(context);
            Assert.NotSame(first, second);
            Assert.Equal(0, second.Total("test:extra"));
            Assert.Equal(outside.Ledger.Lines, second.Ledger.Lines);
            Assert.Equal(outside.FlatYield, second.FlatYield);
            Assert.Equal(outside.FlatCommerce, second.FlatCommerce);
        }
    }

    [Fact]
    public void An_epoch_memo_keeps_answers_only_within_one_epoch_of_one_simulation()
    {
        var sim = TestGame.Sim(5);
        var other = TestGame.Sim(5);
        var memo = new ReadEpochMemo<int, int>();
        int made = 0;
        int Ask(Simulation s, int key) => memo.GetOrAdd(s, key, 10, (_, k, offset) => { made++; return k + offset; });
        Assert.Equal(11, Ask(sim, 1)); Assert.Equal(11, Ask(sim, 1));
        Assert.Equal(2, made); // Outside an epoch every ask computes.
        Assert.False(memo.TryGet(sim, 1, out _));
        memo.Set(sim, 1, 99);
        Assert.False(memo.TryGet(sim, 1, out _));
        using (sim.OpenReadEpoch())
        {
            made = 0;
            Assert.Equal(11, Ask(sim, 1)); Assert.Equal(11, Ask(sim, 1)); Assert.Equal(12, Ask(sim, 2));
            Assert.Equal(2, made);
            Assert.True(memo.TryGet(sim, 2, out int kept)); Assert.Equal(12, kept);
            using (other.OpenReadEpoch())
            {
                Assert.False(memo.TryGet(other, 1, out _)); // Another simulation never sees this one's answers.
                Assert.Equal(11, Ask(other, 1));
            }
        }
        using (sim.OpenReadEpoch())
        {
            Assert.False(memo.TryGet(sim, 1, out _)); // A new epoch starts empty.
            memo.Set(sim, 1, 7);
            Assert.Equal(7, Ask(sim, 1));
            sim.AdvanceTurn();
            Assert.Equal(11, Ask(sim, 1)); // The rollover ended the epoch.
        }
    }

    private sealed class SpanProbe : GameCommand
    {
        public long Inside { get; private set; } = -1;
        public long After { get; private set; } = -1;
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim)
        {
            using (sim.OpenReadSpan()) Inside = sim.ReadEpoch;
            After = sim.ReadEpoch;
        }
    }

    // A dedicated thread: Task.Wait may run a task that has not started yet inline, on the waiting thread.
    private static long OnAnotherThread(Func<long> read)
    {
        long seen = -1;
        var thread = new Thread(() => seen = read());
        thread.Start();
        thread.Join();
        return seen;
    }

    [Fact]
    public void A_span_belongs_to_its_thread_opens_inside_a_command_and_ends_before_any_write()
    {
        var sim = TestGame.Sim(5);
        long seenElsewhere = -1;
        using (var span = sim.OpenReadSpan())
        {
            long s = sim.ReadEpoch;
            Assert.NotEqual(0, s);
            using (sim.OpenReadSpan()) Assert.Equal(s, sim.ReadEpoch); // A nested span reads under the open one.
            seenElsewhere = OnAnotherThread(() => sim.ReadEpoch);
            Assert.Equal(0, seenElsewhere); // Other threads do not see it.
            var probe = new SpanProbe { Player = 0 };
            Assert.True(sim.TryApply(probe, out _));
            Assert.True(probe.Inside > s);
            Assert.Equal(0, probe.After);
            Assert.Equal(0, sim.ReadEpoch); // The command ended the span before it validated.
        }
        using (sim.OpenReadEpoch())
        {
            long e = sim.ReadEpoch;
            using (sim.OpenReadSpan()) Assert.Equal(e, sim.ReadEpoch); // An open epoch already shares more.
            seenElsewhere = OnAnotherThread(() => sim.ReadEpoch);
            Assert.Equal(e, seenElsewhere);
        }
        var stale = sim.OpenReadSpan();
        sim.AdvanceTurn();
        Assert.Equal(0, sim.ReadEpoch);
        stale.Dispose();
        sim.VerifyReadEpochs = true;
        var written = sim.OpenReadSpan();
        sim.State.Turn++;
        Assert.Throws<InvalidOperationException>(written.Dispose);
    }

    private sealed class WritingRejection : GameCommand
    {
        public override string? Validate(Simulation sim) { sim.State.Turn++; return "no"; }
        public override void Apply(Simulation sim) { }
    }
}
