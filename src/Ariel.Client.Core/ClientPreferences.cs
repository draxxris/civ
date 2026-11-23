using System.Text.Json;
using System.Text.Json.Serialization;
using Ariel.Game;

namespace Ariel.Client;

/// <summary>Choices about how this computer runs the game, kept in the user's data folder. They are not game options:
/// they never enter a save, the state hash or a lobby, so peers of one networked game may differ and no choice here
/// can change what a game does, only how fast it runs.</summary>
public sealed record ClientPreferences
{
    /// <summary>The setup screen's label for <see cref="ParallelAiPlanning"/>.</summary>
    public const string ParallelAiPlanningLabel = "Parallel AI planning (experimental)";
    /// <summary>The setup screen's help for <see cref="ParallelAiPlanning"/>.</summary>
    public const string ParallelAiPlanningHelp =
        "Can be faster on a machine with many cores, but is usually slower. It never changes the outcome of a game. "
        + "Applies to games you start or load afterwards, on this computer only.";

    /// <summary>Whether AI players plan in speculative parallel waves. Off by default.</summary>
    public bool ParallelAiPlanning { get; init; }

    /// <summary>The wave width this preference asks of a simulation: one (strictly in turn) when off.</summary>
    [JsonIgnore]
    public int AiWaveLimit => Simulation.AiWaveLimitFor(ParallelAiPlanning, Environment.ProcessorCount);

    /// <summary>The saved preferences. A missing or damaged file gives the defaults: unlike the mod profile, nothing
    /// here can grant anything, and a preference must never keep the game from starting.</summary>
    public static ClientPreferences Load(string path)
    {
        try { return JsonSerializer.Deserialize<ClientPreferences>(File.ReadAllText(path)) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
