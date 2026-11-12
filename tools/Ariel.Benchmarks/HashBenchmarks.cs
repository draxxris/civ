using System.Diagnostics;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

/// <summary>The lockstep state hash on a played-out eight-player game: time, allocation and gen-2
/// collections per <see cref="Simulation.ComputeHash"/>.</summary>
internal static class HashBenchmarks
{
    private const int Samples = 100;

    public static void Run(ContentDb content)
    {
        var sim = GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_standard");
        for (int i = 0; i < 99; i++) sim.AdvanceTurn();
        ulong hash = sim.ComputeHash(); // warm the serializer and the canonical buffers
        Console.WriteLine($"State at turn {sim.State.Turn}: cities={sim.State.Cities.Count}, units={sim.State.Units.Count}, hash={hash:X16}");

        int gen2 = GC.CollectionCount(2);
        long allocated = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < Samples; i++)
            if (sim.ComputeHash() != hash) throw new InvalidOperationException("The state hash changed between samples");
        clock.Stop();
        long bytes = GC.GetTotalAllocatedBytes(true) - allocated;
        Console.WriteLine($"ComputeHash x{Samples}: {clock.Elapsed.TotalMilliseconds / Samples:F2} ms each, "
            + $"{bytes / Samples / 1024.0:F0} KB allocated each, gen-2 collections={GC.CollectionCount(2) - gen2}");
    }
}
