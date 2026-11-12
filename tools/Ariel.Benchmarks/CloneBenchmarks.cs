using System.Diagnostics;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

/// <summary>The state copy behind every speculative AI fork: time and allocation per <see cref="GameState.Clone"/>
/// on a standard eight-player game played to turn 100, the share spent copying extension components through their
/// JSON contract, and both against one whole turn.</summary>
internal static class CloneBenchmarks
{
    private const int Samples = 50;

    public static void Run(ContentDb content)
    {
        var sim = GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_standard");
        for (int i = 0; i < 99; i++) sim.AdvanceTurn();
        var state = sim.State;
        Console.WriteLine($"Standard world at turn 100: plots={state.Plots.Length}, players={state.Players.Count}, "
            + $"cities={state.Cities.Count}, units={state.Units.Count}");
        Extensions(state);
        var (cloneMs, cloneKb) = Measure("GameState.Clone", () => state.Clone());
        var (extensionMs, extensionKb) = Measure("  extension components only", () => Extensions(state));
        Console.WriteLine($"  extension share: {extensionMs / cloneMs:P0} of time, {extensionKb / cloneKb:P0} of allocation");

        long allocated = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        const int turns = 5;
        for (int i = 0; i < turns; i++) sim.AdvanceTurn();
        clock.Stop();
        double turnMs = clock.Elapsed.TotalMilliseconds / turns;
        double turnKb = (GC.GetTotalAllocatedBytes(true) - allocated) / turns / 1024.0;
        Console.WriteLine($"AdvanceTurn x{turns} (turns 100-104): {turnMs:F1} ms each, {turnKb / 1024:F1} MiB allocated each");
        Console.WriteLine($"  one clone is {cloneMs / turnMs:P2} of a turn; its extension copies are {extensionMs / turnMs:P2}");
    }

    /// <summary>Copy every extension set a state clone copies, and nothing else.</summary>
    private static int Extensions(GameState state)
    {
        int count = 1;
        state.Extensions.Clone();
        foreach (var player in state.Players) { player.Extensions.Clone(); count++; }
        foreach (var team in state.Teams) { team.Extensions.Clone(); count++; }
        foreach (var city in state.Cities) { city.Extensions.Clone(); count++; }
        foreach (var unit in state.Units) { unit.Extensions.Clone(); count++; }
        return count;
    }

    private static (double Ms, double Kb) Measure<T>(string label, Func<T> action)
    {
        action();
        long allocated = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < Samples; i++) action();
        clock.Stop();
        double ms = clock.Elapsed.TotalMilliseconds / Samples;
        double kb = (GC.GetTotalAllocatedBytes(true) - allocated) / Samples / 1024.0;
        Console.WriteLine($"{label} x{Samples}: {ms:F2} ms each, {kb:F0} KB allocated each");
        return (ms, kb);
    }
}
