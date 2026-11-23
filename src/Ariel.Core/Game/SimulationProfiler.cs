using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Ariel.Game;

/// <summary>What a <see cref="SimulationProfiler"/> entry measures. Entries of one kind never nest, so the kind's sum
/// is meaningful; entries of different kinds do nest (a command inside an AI planner, a valuer inside a planner).</summary>
public enum ProfileKind
{
    /// <summary>One registered <see cref="ITurnSystem"/>; a player-interval system counts one call per player.</summary>
    TurnSystem,
    /// <summary>One AI planner for one player. Planners of different players run on several threads at once, so the
    /// kind's sum is processor time, larger than the wall time it took.</summary>
    AiPlanner,
    /// <summary>One AI valuer, wish source or similar seam a planner asks; nested inside its planner.</summary>
    AiSeam,
    /// <summary>One top-level command by type, its application with the event callbacks. AI players issue theirs on
    /// parallel forks too, and the wave commit replays them, so the kind's sum is processor time.</summary>
    Command,
    /// <summary>Kernel work around the rules: the AI waves as a whole and the speculation bookkeeping inside them.</summary>
    Kernel,
}

/// <summary>Opt-in timing of the turn pipeline: every turn system, AI planner, AI seam, top-level command and kernel
/// step by id, with cumulative time and calls, plus one sample per turn the host records. A host attaches one to
/// <see cref="Simulation.Profiler"/>; the simulation copies the reference into the forks it plans AI turns on, so
/// parallel work lands in the same profiler. Never saved or hashed, and it changes nothing the rules decide.
/// <para>Off costs nothing: with no profiler attached, <see cref="ProfilerExtensions.Measure"/> returns an empty scope
/// without reading the clock or allocating. On, each measured call costs two clock reads, two reads of the thread's
/// allocation counter and a dictionary lookup. Bytes are those the measuring thread allocated inside the span, so work
/// a span hands to other threads (a parallel scoring loop) is not in its bytes, only in its time.</para></summary>
public sealed class SimulationProfiler
{
    private sealed class Counter(ProfileKind kind, string id)
    {
        public readonly ProfileKind Kind = kind;
        public readonly string Id = id;
        public long Ticks, Calls, Bytes;
        // Totals as of the last recorded turn, for the per-turn deltas. Only the recording thread touches them.
        public long TurnTicks, TurnCalls, TurnBytes;
    }

    /// <summary>One entry's totals over the run.</summary>
    public readonly record struct Entry(ProfileKind Kind, string Id, long Calls, double Milliseconds, long AllocatedBytes);

    /// <summary>What one turn cost: wall time since the previous sample, the turn systems' and the AI waves' share of
    /// it, managed bytes allocated, time the garbage collector paused the process, gen-2 collections, and the size of
    /// the game afterwards.</summary>
    public readonly record struct TurnSample(int Turn, double WallMs, double SystemsMs, double AiMs, long AllocatedBytes,
        double GcPauseMs, int Gen2Collections, int Cities, int Units);

    private readonly ConcurrentDictionary<(ProfileKind, string), Counter> _counters = new();
    private readonly List<TurnSample> _turns = new();
    private readonly List<(int Turn, ProfileKind Kind, string Id, long Calls, double Ms, long Bytes)> _turnEntries = new();
    private long _turnStart = Stopwatch.GetTimestamp(), _turnAllocated = GC.GetTotalAllocatedBytes(), _runStart = Stopwatch.GetTimestamp();
    private int _turnGen2 = GC.CollectionCount(2);
    private TimeSpan _turnPause = GC.GetTotalPauseDuration();

    /// <summary>A profiler when <c>ARIEL_PROFILE</c> names an output directory, else null. Hosts that offer
    /// profiling read it so one switch works for every tool.</summary>
    public static SimulationProfiler? FromEnvironment(out string? directory)
    {
        directory = Environment.GetEnvironmentVariable("ARIEL_PROFILE");
        if (string.IsNullOrWhiteSpace(directory)) { directory = null; return null; }
        return new SimulationProfiler();
    }

    /// <summary>Add one measured span. Thread-safe.</summary>
    public void Add(ProfileKind kind, string id, long ticks, long calls = 1, long bytes = 0)
    {
        var counter = _counters.GetOrAdd((kind, id), static key => new Counter(key.Item1, key.Item2));
        Interlocked.Add(ref counter.Ticks, ticks);
        Interlocked.Add(ref counter.Calls, calls);
        Interlocked.Add(ref counter.Bytes, bytes);
    }

    /// <summary>Every entry, most expensive first, ties by kind and ordinal id.</summary>
    public IReadOnlyList<Entry> Entries() => _counters.Values
        .Select(c => new Entry(c.Kind, c.Id, Interlocked.Read(ref c.Calls), Ms(Interlocked.Read(ref c.Ticks)), Interlocked.Read(ref c.Bytes)))
        .OrderByDescending(e => e.Milliseconds).ThenBy(e => e.Kind).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();

    /// <summary>The per-turn samples recorded so far, oldest first.</summary>
    public IReadOnlyList<TurnSample> Turns => _turns;

    /// <summary>Close the turn that just finished: everything since the previous call (or since the profiler was made)
    /// counts toward it. Call it from the host's turn loop while no AI work runs.</summary>
    public void RecordTurn(Simulation sim)
    {
        long now = Stopwatch.GetTimestamp(), allocated = GC.GetTotalAllocatedBytes();
        int gen2 = GC.CollectionCount(2);
        var pause = GC.GetTotalPauseDuration();
        long systems = 0, ai = 0;
        foreach (var c in _counters.Values)
        {
            long ticks = Interlocked.Read(ref c.Ticks), calls = Interlocked.Read(ref c.Calls), bytes = Interlocked.Read(ref c.Bytes);
            long dt = ticks - c.TurnTicks, dc = calls - c.TurnCalls, db = bytes - c.TurnBytes;
            c.TurnTicks = ticks; c.TurnCalls = calls; c.TurnBytes = bytes;
            if (dc == 0 && dt == 0) continue;
            _turnEntries.Add((sim.State.Turn, c.Kind, c.Id, dc, Ms(dt), db));
            if (c.Kind == ProfileKind.TurnSystem) systems += dt;
            else if (c.Kind == ProfileKind.Kernel && c.Id == AiWaves) ai += dt;
        }
        _turns.Add(new TurnSample(sim.State.Turn, Ms(now - _turnStart), Ms(systems), Ms(ai), allocated - _turnAllocated,
            (pause - _turnPause).TotalMilliseconds, gen2 - _turnGen2, sim.State.Cities.Count, sim.State.Units.Count));
        _turnStart = now; _turnAllocated = allocated; _turnGen2 = gen2; _turnPause = pause;
    }

    /// <summary>The kernel entry that times whole AI waves (<see cref="Simulation.RunAi"/>), wall time.</summary>
    public const string AiWaves = "kernel:ai_turns";

    /// <summary>A readable report: the run, a per-turn curve at a few turns, and every entry sorted by cost.</summary>
    public string Report(int top = 60)
    {
        var sb = new StringBuilder();
        double wall = Ms(Stopwatch.GetTimestamp() - _runStart);
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine(inv, $"Profile: {_turns.Count} turns, {wall / 1000:F1} s since the profiler started, " +
                           $"{_turns.Sum(t => t.AllocatedBytes) / 1048576.0:F0} MiB allocated and " +
                           $"{_turns.Sum(t => t.GcPauseMs) / 1000:F1} s of collector pauses over the recorded turns");
        if (_turns.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  turn    wall ms  systems ms     ai ms  alloc MiB  gc ms  gen2  cities  units");
            foreach (var t in CurveTurns())
                sb.AppendLine(inv, $"{t.Turn,6} {t.WallMs,10:F1} {t.SystemsMs,11:F1} {t.AiMs,9:F1} {t.AllocatedBytes / 1048576.0,10:F1} {t.GcPauseMs,6:F1} {t.Gen2Collections,5} {t.Cities,7} {t.Units,6}");
        }
        sb.AppendLine();
        sb.AppendLine("Inclusive time by entry, summed over every thread it ran on. Turn systems and the kernel's wave");
        sb.AppendLine("steps run on the turn's own thread; AI planners and seams, and the commands AI players issue, also");
        sb.AppendLine("run on parallel forks, so their sums are processor time and can exceed the wall time.");
        sb.AppendLine();
        sb.AppendLine("  kind        total ms      calls    mean us   alloc MiB  id");
        foreach (var e in Entries().Take(top))
            sb.AppendLine(inv, $"  {e.Kind,-10} {e.Milliseconds,9:F0} {e.Calls,10} {e.Milliseconds * 1000 / Math.Max(1, e.Calls),10:F1} {e.AllocatedBytes / 1048576.0,11:F1}  {e.Id}");
        return sb.ToString();
    }

    // Turn 1, 10, 25, then every 25th, and the last recorded turn.
    private IEnumerable<TurnSample> CurveTurns()
    {
        int last = _turns[^1].Turn;
        return _turns.Where(t => t.Turn is 1 or 10 || t.Turn % 25 == 0 || t.Turn == last);
    }

    /// <summary>Write the report (<c>report.txt</c>), the totals (<c>totals.csv</c>), the per-turn samples
    /// (<c>turns.csv</c>) and every entry's cost per turn (<c>turn-entries.csv</c>) into <paramref name="directory"/>.</summary>
    public void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var inv = CultureInfo.InvariantCulture;
        File.WriteAllText(Path.Combine(directory, "report.txt"), Report(int.MaxValue));
        var totals = new StringBuilder("kind,id,calls,ms,allocated_bytes\n");
        foreach (var e in Entries()) totals.AppendLine(inv, $"{e.Kind},{e.Id},{e.Calls},{e.Milliseconds:F3},{e.AllocatedBytes}");
        File.WriteAllText(Path.Combine(directory, "totals.csv"), totals.ToString());
        var turns = new StringBuilder("turn,wall_ms,systems_ms,ai_ms,allocated_bytes,gc_pause_ms,gen2,cities,units\n");
        foreach (var t in _turns)
            turns.AppendLine(inv, $"{t.Turn},{t.WallMs:F3},{t.SystemsMs:F3},{t.AiMs:F3},{t.AllocatedBytes},{t.GcPauseMs:F3},{t.Gen2Collections},{t.Cities},{t.Units}");
        File.WriteAllText(Path.Combine(directory, "turns.csv"), turns.ToString());
        var entries = new StringBuilder("turn,kind,id,calls,ms,allocated_bytes\n");
        foreach (var (turn, kind, id, calls, ms, bytes) in _turnEntries) entries.AppendLine(inv, $"{turn},{kind},{id},{calls},{ms:F3},{bytes}");
        File.WriteAllText(Path.Combine(directory, "turn-entries.csv"), entries.ToString());
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}

/// <summary>One measured span; disposing it adds the time to its profiler. Empty, and free, without a profiler.</summary>
public readonly struct ProfileScope : IDisposable
{
    private readonly SimulationProfiler? _profiler;
    private readonly ProfileKind _kind;
    private readonly string? _id;
    private readonly long _start, _allocated;

    internal ProfileScope(SimulationProfiler profiler, ProfileKind kind, string id)
    {
        _profiler = profiler; _kind = kind; _id = id;
        _allocated = GC.GetAllocatedBytesForCurrentThread();
        _start = Stopwatch.GetTimestamp();
    }

    /// <summary>Whether this scope measures anything.</summary>
    public bool Active => _profiler is not null;

    public void Dispose()
    {
        if (_profiler is null) return;
        long ticks = Stopwatch.GetTimestamp() - _start;
        _profiler.Add(_kind, _id!, ticks, 1, GC.GetAllocatedBytesForCurrentThread() - _allocated);
    }
}

public static class ProfilerExtensions
{
    /// <summary>Measure a span under <paramref name="id"/>: <c>using (sim.Profiler.Measure(kind, id)) { ... }</c>.
    /// Pass an id that already exists (a system's or planner's own id); without a profiler nothing is read or made.</summary>
    public static ProfileScope Measure(this SimulationProfiler? profiler, ProfileKind kind, string id) =>
        profiler is null ? default : new ProfileScope(profiler, kind, id);

    /// <summary>Measure a span under <paramref name="subject"/>'s id, which is read only when a profiler is attached:
    /// some ids are computed properties, and the off path must not build strings.</summary>
    public static ProfileScope Measure(this SimulationProfiler? profiler, ProfileKind kind, Ariel.Content.IHasId subject) =>
        profiler is null ? default : new ProfileScope(profiler, kind, subject.Id);
}
