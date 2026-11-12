using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

/// <summary>What a turn allocates, by type: five whole turns of a standard eight-player game from turn 100, with the
/// runtime's allocation samples (one about every 100 KB a thread allocates) summed per type. It names the types to
/// look for; it cannot name the code that allocates them.</summary>
internal static class AllocationProfile
{
    private const int Turns = 5, Shown = 40;

    public static void Run(ContentDb content)
    {
        var sim = GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_standard");
        for (int i = 0; i < 99; i++) sim.AdvanceTurn();
        using var samples = new Samples();
        samples.Recording = true;
        long allocated = GC.GetTotalAllocatedBytes(true);
        for (int i = 0; i < Turns; i++) sim.AdvanceTurn();
        long total = GC.GetTotalAllocatedBytes(true) - allocated;
        samples.Recording = false;
        Console.WriteLine($"Standard 8-player turns 100-{99 + Turns}: {total / Turns / 1048576.0:F0} MiB allocated per turn; largest types:");
        foreach (var (type, bytes) in samples.Bytes.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(Shown))
            Console.WriteLine($"{bytes / Turns / 1048576.0,8:F1} MiB  {type}");
    }

    /// <summary>Sums the runtime's GCAllocationTick events by the type that crossed the sampling threshold.</summary>
    private sealed class Samples : EventListener
    {
        private const EventKeywords Gc = (EventKeywords)0x1;
        public ConcurrentDictionary<string, long> Bytes { get; } = new(StringComparer.Ordinal);
        public volatile bool Recording;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, EventLevel.Verbose, Gc);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!Recording || e.EventName?.StartsWith("GCAllocationTick", StringComparison.Ordinal) != true) return;
            if (e.PayloadNames is not { } names || e.Payload is not { } payload) return;
            int type = names.IndexOf("TypeName"), amount = names.IndexOf("AllocationAmount64");
            if (type < 0 || amount < 0) return;
            long bytes = Convert.ToInt64(payload[amount]);
            Bytes.AddOrUpdate((string)payload[type]!, bytes, (_, sum) => sum + bytes);
        }
    }
}
