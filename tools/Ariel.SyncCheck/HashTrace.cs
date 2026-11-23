using System.Globalization;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

namespace Ariel.SyncCheck;

/// <summary>
/// Plays an all-AI game and prints the state hash after every turn, one "turn hash" line each. Lockstep needs every peer
/// to compute the same hashes, so two runs of the same game must print the same lines whatever the machine: thread
/// count, timing of the parallel AI waves, process-local string hashing. With <c>--reload-every N</c> the run also saves
/// and reloads its game every N turns, which must not change the hashes either; a rejoining peer starts from a save.
/// With <c>--background</c> every turn rolls over as a front end runs it: as turn work on a copy of the game, on another
/// thread, taken back afterwards. With <c>--waves N</c> (<c>max</c> for no limit) the AI players plan in speculative waves
/// of up to N players instead of strictly in turn, which must not change the hashes either. With <c>--save PATH</c> the run writes the final game as a save, for inspecting
/// what the AIs did. With <c>--load PATH</c> the run continues that save instead of starting a new game (seed, players,
/// world and era then come from the save), for measuring a late game without replaying it. With <c>--profile DIR</c> (or <c>ARIEL_PROFILE=DIR</c>) it times every turn system, AI planner,
/// AI seam, command and kernel step, prints the table sorted by cost to stderr and writes it with per-turn CSVs to DIR.
/// </summary>
internal static class HashTrace
{
    public static int Run(string[] args, ContentDb content)
    {
        ulong seed = ulong.Parse(Option(args, "--seed") ?? "1", CultureInfo.InvariantCulture);
        int players = int.Parse(Option(args, "--players") ?? "6", CultureInfo.InvariantCulture);
        int turns = int.Parse(Option(args, "--turns") ?? "100", CultureInfo.InvariantCulture);
        int reload = int.Parse(Option(args, "--reload-every") ?? "0", CultureInfo.InvariantCulture);
        string world = Option(args, "--world") ?? "foundation:worldsize_small";
        string era = Option(args, "--era") ?? "foundation:era_ancient";
        var mode = Enum.Parse<TurnMode>(Option(args, "--turn-mode") ?? nameof(TurnMode.Simultaneous), ignoreCase: true);
        bool background = args.Contains("--background");
        int waves = Option(args, "--waves") is { } w ? w == "max" ? int.MaxValue : int.Parse(w, CultureInfo.InvariantCulture) : 1;
        string? save = Option(args, "--save");
        string? load = Option(args, "--load");
        var profiler = SimulationProfiler.FromEnvironment(out string? profileDir);
        if (Option(args, "--profile") is { } dir) (profiler, profileDir) = (profiler ?? new SimulationProfiler(), dir);

        var sim = load is null ? GameFactory.CreateAi(content, seed, players, world, mode, era) : SaveGame.Load(content, File.ReadAllText(load));
        sim.Profiler = profiler; sim.AiWaveLimit = waves;
        string start = load is null ? $"seed={seed} players={players} era={era} turn-mode={mode}" : $"load={load} turn={sim.State.Turn}";
        Console.Error.WriteLine($"hash-trace {start} turns={turns} reload-every={reload} background={background} waves={waves} " +
                                $"processors={Environment.ProcessorCount} content={content.ContentHash}");
        Console.WriteLine($"{sim.State.Turn} {sim.ComputeHash():x16}");
        for (int i = 1; i <= turns; i++)
        {
            if (background) Background(sim);
            else sim.AdvanceTurn();
            if (reload > 0 && i % reload == 0)
            {
                sim = SaveGame.Load(content, SaveGame.Save(sim));
                sim.Profiler = profiler; sim.AiWaveLimit = waves;
            }
            ulong hash;
            using (profiler.Measure(ProfileKind.Kernel, "kernel:state_hash")) hash = sim.ComputeHash();
            Console.WriteLine($"{sim.State.Turn} {hash:x16}");
            profiler?.RecordTurn(sim);
        }
        if (profiler is not null)
        {
            profiler.Write(profileDir!);
            Console.Error.WriteLine(profiler.Report());
            Console.Error.WriteLine($"profile written to {Path.GetFullPath(profileDir!)}");
        }
        Console.Error.WriteLine($"final turn {sim.State.Turn} hash {sim.ComputeHash():x16}");
        if (save is not null) File.WriteAllText(save, SaveGame.Save(sim));
        return 0;
    }

    /// <summary>Every AI has ended, so the pending work is the rollover and the next turn's moves.</summary>
    private static void Background(Simulation sim)
    {
        sim.DeferTurnWork = true;
        var work = sim.BeginTurnWork();
        Task.Run(work.Run).Wait();
        sim.CompleteTurnWork(work);
    }

    private static string? Option(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
