using System.Diagnostics;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Tests;
using Ariel.Rules;

var root = AppContext.BaseDirectory;
if (args.FirstOrDefault() == "--ai-eval")
{
    Environment.ExitCode = Ariel.Benchmarks.AiEvaluation.EvaluationCli.Run(args.Skip(1).ToArray(), root);
    return;
}
if (args.Contains("--world"))
{
    StartupBenchmarks.RunWorld(root);
    return;
}
if (args.Contains("--quick") || args.Contains("--startup"))
{
    StartupBenchmarks.Run(root, sweep: args.Contains("--startup"));
    return;
}
if (args.Contains("--borders"))
{
    BorderBenchmarks.Run(ModLoader.Build(GameContent.Staged(root).Reverse()));
    return;
}
if (args.Contains("--gigantic"))
{
    GiganticTurnBenchmarks.Run(ModLoader.Build(GameContent.Staged(root).Reverse()));
    return;
}
if (args.Contains("--hash"))
{
    HashBenchmarks.Run(ModLoader.Build(GameContent.Staged(root).Reverse()));
    return;
}
if (args.Contains("--allocations"))
{
    AllocationProfile.Run(ModLoader.Build(GameContent.Staged(root).Reverse()));
    return;
}
if (args.Contains("--clone"))
{
    CloneBenchmarks.Run(ModLoader.Build(GameContent.Staged(root).Reverse()));
    return;
}
if (args.Contains("--terrain"))
{
    TerrainBenchmarks.Run(new Ariel.Client.GameCatalog(GameContent.Staged(root)));
    return;
}
var sources = GameContent.Staged(root);
var content = ModLoader.Build(sources.Reverse());
Console.WriteLine("Operational benchmarks: single samples, no timing thresholds. Content loading is excluded.");

foreach (ulong seed in new ulong[] { 1, 42, 987654 })
{
    var clock = Stopwatch.StartNew();
    var world = GameFactory.Create(content, seed);
    clock.Stop();
    Console.WriteLine($"World generation/setup seed={seed}: {clock.Elapsed.TotalMilliseconds:F2} ms; plots={world.State.Plots.Length}, players={world.State.Players.Count}");
}

var cities = GameFactory.Scenario(content);
cities.State.Map = new(60, 36, true);
cities.State.Plots = Enumerable.Range(0, 2160).Select(_ => new Plot { Terrain = "world:grass", Owner = 0 }).ToArray();
cities.State.RebuildIndexes();
for (int i = 0; i < 50; i++)
    cities.State.AddCity(new City { Id = cities.State.NextCityId++, Owner = 0, Culture = 1,
        Position = cities.State.Grid.HexOf(cities.State.Grid.TileAt(3 + i % 10 * 6, 3 + i / 10 * 6)) });
var cityClock = Stopwatch.StartNew();
cities.AdvanceTurn();
cityClock.Stop();
Console.WriteLine($"Fifty-city turn: {cityClock.Elapsed.TotalMilliseconds:F2} ms; cities={cities.State.Cities.Count}, turn={cities.State.Turn}");

var setupClock = Stopwatch.StartNew();
var ai = GameFactory.CreateAi(content, seed: 9, players: 8, world: "foundation:worldsize_standard");
for (int i = 0; i < 99; i++) ai.AdvanceTurn();
// Save/load isolates AI decisions from turn systems, as in the original timed test.
var decisions = SaveGame.Load(content, SaveGame.Save(ai));
foreach (var player in decisions.State.Players.Where(p => p.Ai is not null)) player.EndedTurn = false;
setupClock.Stop();
Console.WriteLine($"Eight-player preparation (generation, 99 turns, save/load): {setupClock.Elapsed.TotalMilliseconds:F2} ms");
var aiClock = Stopwatch.StartNew();
decisions.RunAi();
aiClock.Stop();
Console.WriteLine($"Eight-player AI decisions at turn {decisions.State.Turn}: {aiClock.Elapsed.TotalMilliseconds:F2} ms; cities={decisions.State.Cities.Count}, units={decisions.State.Units.Count}");
