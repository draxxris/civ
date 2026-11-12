using System.Diagnostics;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

/// <summary>The lockstep state hash: time, allocation and gen-2 collections per <see cref="Simulation.ComputeHash"/>,
/// on a fresh gigantic world and on a standard eight-player game played to turn 100.</summary>
internal static class HashBenchmarks
{
    private const int Samples = 100;

    public static void Run(ContentDb content)
    {
        Measure("Gigantic world at turn 1", GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_gigantic"));
        var sim = GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_standard");
        for (int i = 0; i < 99; i++) sim.AdvanceTurn();
        Measure("Standard world at turn 100", sim);
    }

    private static void Measure(string label, Simulation sim)
    {
        ulong hash = sim.ComputeHash(); // warm the serializer and the canonical buffers
        Console.WriteLine($"{label}: plots={sim.State.Plots.Length}, cities={sim.State.Cities.Count}, units={sim.State.Units.Count}, "
            + $"canonical size={CanonicalJson.Serialize(new { sim.State }).Length / 1024} KB, hash={hash:X16}");
        int gen2 = GC.CollectionCount(2);
        long allocated = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < Samples; i++)
            if (sim.ComputeHash() != hash) throw new InvalidOperationException("The state hash changed between samples");
        clock.Stop();
        long bytes = GC.GetTotalAllocatedBytes(true) - allocated;
        Console.WriteLine($"  ComputeHash x{Samples}: {clock.Elapsed.TotalMilliseconds / Samples:F2} ms each, "
            + $"{bytes / Samples / 1024.0:F0} KB allocated each, gen-2 collections={GC.CollectionCount(2) - gen2}");
    }
}
