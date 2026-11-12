using System.Diagnostics;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

/// <summary>Whole turns, AI included, on an eight-player gigantic world: where per-turn allocations and
/// whole-map work show up most. The final hash makes it a determinism check as well.</summary>
internal static class GiganticTurnBenchmarks
{
    private const int Warmup = 20, Timed = 10;

    public static void Run(ContentDb content)
    {
        var sim = GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_gigantic");
        for (int i = 0; i < Warmup; i++) sim.AdvanceTurn();
        int gen2 = GC.CollectionCount(2);
        long allocated = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < Timed; i++) sim.AdvanceTurn();
        clock.Stop();
        long bytes = GC.GetTotalAllocatedBytes(true) - allocated;
        Console.WriteLine($"Gigantic 8-player turns {Warmup + 1}-{Warmup + Timed}: {clock.Elapsed.TotalMilliseconds / Timed:F0} ms per turn, "
            + $"{bytes / Timed / (1024.0 * 1024.0):F1} MB allocated per turn, gen-2 collections={GC.CollectionCount(2) - gen2}");
        Console.WriteLine($"  turn={sim.State.Turn}, cities={sim.State.Cities.Count}, units={sim.State.Units.Count}, hash={sim.ComputeHash():X16}");
    }
}
