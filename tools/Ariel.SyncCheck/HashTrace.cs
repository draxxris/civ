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
        var mode = Enum.Parse<TurnMode>(Option(args, "--turn-mode") ?? nameof(TurnMode.Simultaneous), ignoreCase: true);

        var sim = GameFactory.CreateAi(content, seed, players, world, mode);
        Console.Error.WriteLine($"hash-trace seed={seed} players={players} turns={turns} turn-mode={mode} reload-every={reload} " +
                                $"processors={Environment.ProcessorCount} content={content.ContentHash}");
        Console.WriteLine($"{sim.State.Turn} {sim.ComputeHash():x16}");
        for (int i = 1; i <= turns; i++)
        {
            sim.AdvanceTurn();
            if (reload > 0 && i % reload == 0) sim = SaveGame.Load(content, SaveGame.Save(sim));
            Console.WriteLine($"{sim.State.Turn} {sim.ComputeHash():x16}");
        }
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
