using System.Diagnostics;
using Civ;
using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;

/// <summary>Focused startup and interactive workloads, without multi-turn AI preparation.</summary>
internal static class StartupBenchmarks
{
    public static void Run(string root, bool sweep)
    {
        Console.WriteLine("Startup benchmarks: first load, then one warmup and median of five samples; no timing thresholds.");
        var catalog = MeasureOnce("Catalog first load (discovery, assemblies, content, choices)", () => GameCatalog.Load(root));
        Console.WriteLine($"Content hash: {catalog.Content.ContentHash}");
        MeasureCatalogs(catalog);
        foreach (string size in sweep ? new[] { "standard", "gigantic" } : new[] { "standard" })
            foreach (ulong seed in sweep ? new ulong[] { 1, 42, 987654 } : new ulong[] { 42 })
                RunGame(catalog, size, seed);
    }

    public static void RunWorld(string root)
    {
        Console.WriteLine("World benchmarks: one catalog load, then one warmup and five samples; no timing thresholds.");
        var catalog = GameCatalog.Load(root);
        Console.WriteLine($"Content hash: {catalog.Content.ContentHash}");
        foreach (string size in new[] { "standard", "gigantic" })
            foreach (ulong seed in new ulong[] { 1, 42, 987654 })
                RunWorldGame(catalog, size, seed);
    }

    private static void MeasureCatalogs(GameCatalog catalog)
    {
        // Preserve the source order, manifests, roots and registration entry points. Only hide preparation support.
        var serial = catalog.Mods.Select(mod => new ModSource
        {
            Manifest = mod.Manifest, Root = mod.Root, Instance = new SerialMod(mod.Instance)
        }).ToArray();
        IReadOnlyList<ModSource>[] sources = [serial, catalog.Mods];
        string[] names = ["Catalog serial", "Catalog preparation"];
        double[][] times = [new double[5], new double[5]];
        long[][] bytes = [new long[5], new long[5]];
        string expected = catalog.Content.ContentHash;
        Console.WriteLine("Paired catalog builds: loaded assemblies, fresh content; one warmup each, five pairs, alternating first variant.");
        foreach (var mods in sources) AssertCatalogHash(new GameCatalog(mods), expected);
        for (int sample = 0; sample < 5; sample++)
            for (int offset = 0; offset < 2; offset++)
            {
                int variant = (sample + offset) % 2;
                long allocated = GC.GetTotalAllocatedBytes(true);
                var clock = Stopwatch.StartNew();
                var rebuilt = new GameCatalog(sources[variant]);
                clock.Stop();
                bytes[variant][sample] = GC.GetTotalAllocatedBytes(true) - allocated;
                times[variant][sample] = clock.Elapsed.TotalMilliseconds;
                AssertCatalogHash(rebuilt, expected);
            }
        for (int variant = 0; variant < sources.Length; variant++)
            PrintSamples(names[variant], times[variant], bytes[variant]);
        Console.WriteLine($"Paired catalog content hash (both warmups and all samples): {expected}");
    }

    private static void AssertCatalogHash(GameCatalog catalog, string expected)
    {
        if (!StringComparer.Ordinal.Equals(catalog.Content.ContentHash, expected))
            throw new InvalidOperationException("Serial and preparation catalogs must match the first-load content hash");
    }

    private sealed class SerialMod(IMod original) : IMod
    {
        public void Register(IModContext context) => original.Register(context);
    }

    private static void RunGame(GameCatalog catalog, string size, ulong seed)
    {
        var (options, settings, map) = CreateWorldSetup(catalog, size, seed);
        Console.WriteLine($"\n{size}: {map.Width}x{map.Height}; seed={seed}; players={settings.PlayerCount}");
        var session = MeasureWorld(catalog, options, settings, map);
        // Mutating workload: prepare a fresh session outside every timed founding sample.
        session = Measure("Found city", () =>
        {
            var settler = session.Sim.State.Units.First(u => u.Owner == GameSession.Human
                && session.Sim.Content.Get<Civ.Content.Schema.UnitDef>(u.Type).CanFoundCity);
            if (!session.Submit(new FoundCityWithSettlerCommand { Player = GameSession.Human, Settler = settler.Id }))
                throw new InvalidOperationException(session.Message);
            return session;
        }, s => s.Sim.ComputeHash().ToString("X16"), prepare: () => session = GameSession.Start(catalog, options));
        var city = session.Sim.State.Cities.First(c => c.Owner == GameSession.Human);
        Measure("DescribeCity", () => session.DescribeCity(city) ?? throw new InvalidOperationException("Missing city"));
        var choices = MeasureProductionChoices(session, city);
        Console.WriteLine($"Founded hash: {session.Sim.ComputeHash():X16}; production choices={choices.Count}");
    }

    private static void RunWorldGame(GameCatalog catalog, string size, ulong seed)
    {
        var (options, settings, map) = CreateWorldSetup(catalog, size, seed);
        Console.WriteLine($"\n{size}: {map.Width}x{map.Height}; seed={seed}; players={settings.PlayerCount}");
        MeasureWorld(catalog, options, settings, map);
    }

    private static GameSession MeasureWorld(GameCatalog catalog, NewGameOptions options, SimulationSettings settings, MapSpec map)
    {
        var generated = Measure("World generation only", () => Generate(catalog.Content, settings, map), GeneratedHash);
        Console.WriteLine($"Generated hash (plots + RNG): {GeneratedHash(generated)}");
        var session = Measure("GameSession.Start (includes initial opponent AI)", () => GameSession.Start(catalog, options),
            s => s.Sim.ComputeHash().ToString("X16"));
        Console.WriteLine($"Session hash (state + RNG): {session.Sim.ComputeHash():X16}");
        return session;
    }

    private static (NewGameOptions Options, SimulationSettings Settings, MapSpec Map) CreateWorldSetup(
        GameCatalog catalog, string size, ulong seed)
    {
        var options = catalog.DefaultOptions() with
        {
            WorldSize = "foundation:worldsize_" + size,
            Seed = seed,
            Opponents = 3
        };
        var settings = options.ToSettings(catalog);
        var map = catalog.Content.Services.Get<INewGameSetup>().Single().ResolveMap(catalog.Content, settings);
        return (options, settings, map);
    }

    private static IReadOnlyList<ProductionChoice> MeasureProductionChoices(GameSession session, City city)
    {
        var save = SaveGame.Save(session.Sim);
        ulong expectedHash = session.Sim.ComputeHash();
        var coldSession = session;
        var cold = Measure("ProductionChoices cold eligibility (all kinds)",
            () => coldSession.ProductionChoices(coldSession.Sim.State.CityById(city.Id)),
            choices => ProductionFingerprint(choices, coldSession, expectedHash),
            prepare: () => coldSession = new GameSession(SaveGame.Load(session.Sim.Content, save)));
        var warm = Measure("ProductionChoices warm eligibility (all kinds)", () => session.ProductionChoices(city),
            choices => ProductionFingerprint(choices, session, expectedHash));
        if (CanonicalJson.Digest(cold) != CanonicalJson.Digest(warm))
            throw new InvalidOperationException("Cold and warm production choices differ");
        return warm;
    }

    private static string ProductionFingerprint(IReadOnlyList<ProductionChoice> choices, GameSession session, ulong expectedHash)
    {
        if (session.Sim.ComputeHash() != expectedHash)
            throw new InvalidOperationException("Production choices changed simulation state or RNG");
        return CanonicalJson.Digest(choices);
    }

    private static (Plot[] Plots, ulong[] Rng) Generate(ContentDb content, SimulationSettings settings, MapSpec map)
    {
        var plots = Enumerable.Range(0, map.Width * map.Height).Select(_ => new Plot()).ToArray();
        var rng = new DeterministicRng(settings.Seed);
        content.MapGenerators.Get(settings.MapGenerator).Generate(map, plots, rng, content, settings.PlayerCount);
        var (a, b, c, d) = rng.State;
        return (plots, new[] { a, b, c, d });
    }

    private static string GeneratedHash((Plot[] Plots, ulong[] Rng) generated) =>
        CanonicalJson.Digest(new { generated.Plots, generated.Rng });

    private static T MeasureOnce<T>(string name, Func<T> action)
    {
        long allocated = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        var value = action();
        clock.Stop();
        Console.WriteLine($"{name}: {clock.Elapsed.TotalMilliseconds:F2} ms; {(GC.GetTotalAllocatedBytes(true) - allocated) / 1048576.0:F2} MiB allocated");
        return value;
    }

    private static T Measure<T>(string name, Func<T> action, Func<T, string>? fingerprint = null, Action? prepare = null)
    {
        prepare?.Invoke();
        var value = action();
        string? expected = fingerprint?.Invoke(value);
        var times = new double[5];
        var cpuTimes = new double[5];
        var bytes = new long[5];
        using var process = Process.GetCurrentProcess();
        for (int i = 0; i < times.Length; i++)
        {
            prepare?.Invoke();
            process.Refresh();
            TimeSpan cpuStart = process.TotalProcessorTime;
            long allocated = GC.GetTotalAllocatedBytes(true);
            var clock = Stopwatch.StartNew();
            value = action();
            clock.Stop();
            bytes[i] = GC.GetTotalAllocatedBytes(true) - allocated;
            process.Refresh();
            cpuTimes[i] = (process.TotalProcessorTime - cpuStart).TotalMilliseconds;
            times[i] = clock.Elapsed.TotalMilliseconds;
            if (fingerprint is not null && fingerprint(value) != expected) throw new InvalidOperationException($"{name}: output changed");
        }
        PrintSamples(name, times, bytes, cpuTimes);
        return value;
    }

    private static void PrintSamples(string name, double[] times, long[] bytes, double[]? cpuTimes = null)
    {
        Array.Sort(times);
        Array.Sort(bytes);
        string cpu = "";
        if (cpuTimes is not null)
        {
            Array.Sort(cpuTimes);
            cpu = $"; process CPU {cpuTimes[2]:F2} ms (min {cpuTimes[0]:F2}, max {cpuTimes[^1]:F2})";
        }
        Console.WriteLine($"{name}: {times[2]:F2} ms wall (min {times[0]:F2}, max {times[^1]:F2}); "
            + $"{bytes[2] / 1048576.0:F2} MiB allocated (min {bytes[0] / 1048576.0:F2}, max {bytes[^1] / 1048576.0:F2}){cpu}");
    }
}
