using System.Diagnostics;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

/// <summary>The borders phase (every city spreads culture and resolves plot ownership) on a standard eight-player
/// game played to turn 100. Both hashes must stay unchanged under a pure optimization.</summary>
internal static class BorderBenchmarks
{
    private const int Samples = 10;

    public static void Run(ContentDb content)
    {
        var sim = GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_standard");
        for (int i = 0; i < 99; i++) sim.AdvanceTurn();
        Console.WriteLine($"Standard 8-player game at turn {sim.State.Turn}: cities={sim.State.Cities.Count}, hash={sim.ComputeHash():X16}");
        string save = SaveGame.Save(sim);
        var borders = content.TurnSystems.All.Single(s => s.Id == "cities:borders");

        ulong? after = null;
        double milliseconds = 0;
        long bytes = 0;
        for (int i = 0; i < Samples; i++)
        {
            var copy = SaveGame.Load(content, save);
            long allocated = GC.GetTotalAllocatedBytes(true);
            var clock = Stopwatch.StartNew();
            borders.Run(copy);
            clock.Stop();
            bytes += GC.GetTotalAllocatedBytes(true) - allocated;
            if (i > 0) milliseconds += clock.Elapsed.TotalMilliseconds; // the first run warms up
            ulong hash = copy.ComputeHash();
            if (after is { } expected && expected != hash) throw new InvalidOperationException("The borders phase is not deterministic");
            after = hash;
        }
        Console.WriteLine($"  borders phase: {milliseconds / (Samples - 1):F2} ms, {bytes / Samples / (1024.0 * 1024.0):F1} MB allocated, hash after={after:X16}");
    }
}
