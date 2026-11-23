using Ariel.Content;
using Ariel.Game;
using Xunit;

namespace Ariel.Tests;

public class SimulationProfilerTests
{
    private sealed class CountedId : IHasId
    {
        public int Reads;
        public string Id { get { Reads++; return "test:counted"; } }
    }

    [Fact]
    public void Without_a_profiler_a_scope_reads_no_id_and_allocates_nothing()
    {
        SimulationProfiler? none = null;
        var subject = new CountedId();
        using (none.Measure(ProfileKind.TurnSystem, subject)) { }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            using var scope = none.Measure(ProfileKind.TurnSystem, subject);
            Assert.False(scope.Active);
            using (none.Measure(ProfileKind.Kernel, "kernel:test")) { }
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, subject.Reads);
    }

    [Fact]
    public void An_unprofiled_game_records_nothing_and_plays_as_a_profiled_one()
    {
        var plain = TestGame.Sim(seed: 3);
        var profiled = TestGame.Sim(seed: 3);
        profiled.Profiler = new SimulationProfiler();
        for (int i = 0; i < 4; i++) { plain.AdvanceTurn(); profiled.AdvanceTurn(); }
        Assert.Null(plain.Profiler);
        Assert.Equal(plain.ComputeHash(), profiled.ComputeHash());
    }

    [Fact]
    public void A_profiled_game_counts_every_turn_system_once_per_turn_and_samples_each_turn()
    {
        var sim = TestGame.Sim(seed: 3);
        var profiler = sim.Profiler = new SimulationProfiler();
        const int turns = 3;
        for (int i = 0; i < turns; i++)
        {
            sim.AdvanceTurn();
            profiler.RecordTurn(sim);
        }

        var systems = profiler.Entries().Where(e => e.Kind == ProfileKind.TurnSystem).ToDictionary(e => e.Id, e => e.Calls);
        Assert.Equal(sim.Content.TurnSystems.All.Select(s => s.Id).Order(StringComparer.Ordinal), systems.Keys.Order(StringComparer.Ordinal));
        Assert.All(systems.Values, calls => Assert.Equal(turns, calls));
        Assert.Equal(Enumerable.Range(sim.State.Turn - turns + 1, turns), profiler.Turns.Select(t => t.Turn));
        Assert.All(profiler.Turns, t => Assert.True(t.WallMs >= t.SystemsMs, $"turn {t.Turn}: systems outlast the turn"));
    }

    [Fact]
    public void Entries_are_sorted_by_cost_and_spans_add_up_by_kind_and_id()
    {
        var profiler = new SimulationProfiler();
        profiler.Add(ProfileKind.AiPlanner, "test:cheap", 10);
        profiler.Add(ProfileKind.AiPlanner, "test:dear", 1_000);
        profiler.Add(ProfileKind.AiPlanner, "test:cheap", 15);
        profiler.Add(ProfileKind.TurnSystem, "test:cheap", 500);
        Parallel.For(0, 100, _ => profiler.Add(ProfileKind.AiSeam, "test:parallel", 2));

        var entries = profiler.Entries();
        Assert.Equal(["test:dear", "test:cheap", "test:parallel", "test:cheap"], entries.Select(e => e.Id));
        Assert.Equal([ProfileKind.AiPlanner, ProfileKind.TurnSystem, ProfileKind.AiSeam, ProfileKind.AiPlanner], entries.Select(e => e.Kind));
        Assert.Equal(2, entries[^1].Calls);
        Assert.Equal(100, entries[2].Calls);
        Assert.Contains("test:dear", profiler.Report());
    }

    [Fact]
    public void A_scope_counts_the_bytes_its_thread_allocates()
    {
        var profiler = new SimulationProfiler();
        byte[] kept;
        using (profiler.Measure(ProfileKind.Kernel, "test:alloc")) kept = new byte[10_000];
        var entry = Assert.Single(profiler.Entries());
        Assert.InRange(entry.AllocatedBytes, kept.Length, kept.Length + 1_000);
        Assert.Equal(1, entry.Calls);
    }

    [Fact]
    public void Write_produces_the_report_and_the_per_turn_tables()
    {
        var sim = TestGame.Sim(seed: 3);
        var profiler = sim.Profiler = new SimulationProfiler();
        sim.AdvanceTurn();
        profiler.RecordTurn(sim);
        var dir = Path.Combine(Path.GetTempPath(), "ariel-profiler-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            profiler.Write(dir);
            Assert.StartsWith("turn,wall_ms,", File.ReadAllText(Path.Combine(dir, "turns.csv")));
            Assert.Equal(2, File.ReadAllLines(Path.Combine(dir, "turns.csv")).Length);
            Assert.Contains("TurnSystem,base:research,", File.ReadAllText(Path.Combine(dir, "turn-entries.csv")));
            Assert.Contains("TurnSystem,base:research,1,", File.ReadAllText(Path.Combine(dir, "totals.csv")));
            Assert.Contains("base:research", File.ReadAllText(Path.Combine(dir, "report.txt")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
