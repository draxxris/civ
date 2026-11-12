using System.Text.Json;
using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Validate;

/// <summary>Report the height and cliff rules a save file or a debug scene breaks. The game itself loads a map exactly as
/// it is written, so this is where an author finds out that a height disagrees with its ground or that a cliff stands on
/// an edge the contract does not allow.</summary>
public static class Program
{
    /// <summary>Longest list printed for one file; a generator bug must not produce an unreadable report.</summary>
    private const int MaxLines = 50;

    private const string Usage = """
        usage: mise run validate -- <file> [more files...] [options]

        A save file and a debug scene are told apart by their JSON. Options:
          --save         treat every file as a save, whatever its shape
          --scene        treat every file as a debug scene
          --map <id>     lay a scene over a generated map: a generator id or name, or "default"
          --size <id>    world size for that map (smallest by area by default); --size alone generates too
        Exit code 0 means every file follows the contract, 1 means something was reported.
        """;

    public static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(Usage);
            return 0;
        }
        if (!TryParse(args, out var options, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }
        if (options.Files.Count == 0)
        {
            Console.Error.WriteLine("No file to validate.");
            Console.Error.WriteLine(Usage);
            return 2;
        }
        GameCatalog catalog;
        try
        {
            catalog = new GameCatalog(Discover());
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"mods do not load: {e.Message}");
            return 2;
        }
        var geometry = catalog.Content.Rules().MapGeometry;
        int bad = 0;
        foreach (var file in options.Files)
        {
            try
            {
                bool save = options.Save || (!options.Scene && IsSave(file));
                if (save && options.Generated)
                {
                    Console.Error.WriteLine($"{file}: a save carries its own map, so --map and --size do not apply");
                    return 2;
                }
                var sim = save ? LoadSave(catalog, file) : LoadScene(catalog, file, options);
                var problems = geometry.Report(sim);
                if (problems.Count == 0)
                {
                    Console.WriteLine($"{file}: ok");
                    continue;
                }
                bad++;
                foreach (var line in problems.Take(MaxLines)) Console.WriteLine($"{file}: {line}");
                if (problems.Count > MaxLines) Console.WriteLine($"{file}: ... and {problems.Count - MaxLines} more");
            }
            catch (Exception e)
            {
                bad++;
                Console.Error.WriteLine($"{file}: {e.Message}");
            }
        }
        Console.WriteLine(bad == 0
            ? $"validated {options.Files.Count} file(s): every height and cliff follows the contract"
            : $"validated {options.Files.Count} file(s): {bad} with problems");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>The base modules and mods staged beside this tool by the build.</summary>
    private static IReadOnlyList<ModSource> Discover() => Ariel.Rules.GameContent.Staged(AppContext.BaseDirectory);

    /// <summary>A save carries its schema version and the snapshot the game writes.</summary>
    private static bool IsSave(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        return doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("SchemaVersion", out _)
            && doc.RootElement.TryGetProperty("State", out _);
    }

    /// <summary>How many owners a scene mentions, counting a city, a unit and a plot owner alike.</summary>
    private static int Owners(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        int least = 1;
        Count(doc.RootElement, ref least);
        return least;

        static void Count(JsonElement element, ref int least)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.NameEquals("owner") && property.Value.ValueKind == JsonValueKind.Number)
                            least = Math.Max(least, property.Value.GetInt32() + 1);
                        Count(property.Value, ref least);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray()) Count(item, ref least);
                    break;
            }
        }
    }

    private static Simulation LoadSave(GameCatalog catalog, string file) =>
        SaveGame.Load(catalog.Content, File.ReadAllText(file));

    /// <summary>A scene is laid over the world the game would give it: a map of the scene's own size, or a generated map
    /// when <c>--world</c> names one.</summary>
    private static Simulation LoadScene(GameCatalog catalog, string file, Options options)
    {
        // A scene world has one player, exactly as the game gives it; over a generated map the scene needs a player per
        // owner it mentions.
        int players = options.Generated ? Math.Clamp(Owners(file), 1, catalog.Leaders.Count) : 1;
        var settings = new SimulationSettings
        {
            Seed = 1,
            PlayerCount = players,
            Leaders = catalog.Leaders.Take(players).Select(l => l.Id).ToArray(),
            WorldSize = Size(catalog, options.Size),
            MapGenerator = Generator(catalog, options.Map),
        };
        if (!options.Generated)
        {
            var scene = DebugScenario.Frame(file)
                ?? throw new InvalidDataException("a scene without a position needs --map to say what map it sits on");
            settings = settings with { MapOverride = new MapSpec(scene.Width, scene.Height) };
        }
        var sim = new Simulation(catalog.Content, settings);
        DebugScenario.Apply(new GameSession(sim), file, sceneWorld: !options.Generated);
        return sim;
    }

    private static string Size(GameCatalog catalog, string? wanted)
    {
        if (wanted is null) return catalog.WorldSizes[0].Id;
        var choice = catalog.WorldSizes.FirstOrDefault(s => s.Id == wanted)
            ?? catalog.WorldSizes.FirstOrDefault(s => s.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"unknown world size '{wanted}'");
        return choice.Id;
    }

    private static string Generator(GameCatalog catalog, string? wanted)
    {
        if (wanted is null or "default") return catalog.MapGenerators[0].Id;
        var choice = catalog.MapGenerators.FirstOrDefault(g => g.Id == wanted)
            ?? catalog.MapGenerators.FirstOrDefault(g => g.Id[(g.Id.IndexOf(':') + 1)..] == wanted)
            ?? catalog.MapGenerators.FirstOrDefault(g => g.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"unknown map generator '{wanted}'");
        return choice.Id;
    }

    /// <summary>Files and flags. <paramref name="Map"/> is the generator to lay a scene over; null keeps the scene's own
    /// world, and an empty string means that generator but the ruleset default.</summary>
    private sealed record Options(List<string> Files, bool Save, bool Scene, string? Map = null, string? Size = null)
    {
        public bool Generated => Map is not null || Size is not null;
    }

    private static bool TryParse(string[] args, out Options options, out string? error)
    {
        var files = new List<string>();
        bool save = false, scene = false;
        string? map = null, size = null;
        error = null;
        for (int i = 0; i < args.Length && error is null; i++)
        {
            switch (args[i])
            {
                case "--save": save = true; break;
                case "--scene": scene = true; break;
                case "--map" when i + 1 < args.Length && !args[i + 1].StartsWith('-'):
                    map = args[++i];
                    break;
                case "--map":
                    error = "--map needs a generator id or name, or \"default\"";
                    break;
                case "--size" when i + 1 < args.Length: size = args[++i]; break;
                case "--size":
                    error = "--size needs a world size id or name";
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal)) error = $"unknown option '{args[i]}'";
                    else files.Add(args[i]);
                    break;
            }
        }
        if (save && scene) error = "--save and --scene exclude each other";
        if ((map is not null || size is not null) && save)
            error = "--map and --size lay a scene over a generated map, so they cannot take --save";
        options = new(files, save, scene, map, size);
        return error is null;
    }
}
