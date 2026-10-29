using System.Text.Json;
using Ariel.Content;

namespace Ariel.Game;

/// <summary>Versioned JSON save codec. Loading never regenerates maps, publishes events, or reruns AI.</summary>
public static class SaveGame
{
    /// <summary>Supported schema version; incompatible versions require an explicit migration.</summary>
    public const int SchemaVersion = 3;
    /// <summary>Serialize setup, loaded mod versions, content identity, state and RNG words.</summary>
    public static string Save(Simulation sim)
    {
        var (a, b, c, d) = sim.Rng.State;
        return CanonicalJson.Serialize(new Snapshot(SchemaVersion, sim.Content.Mods.ToArray(), sim.Content.ContentHash, sim.Settings, [a, b, c, d], sim.State));
    }
    /// <summary>Load using frozen content. Force bypasses mod/content mismatch only, not unknown state types or invalid data.</summary>
    public static Simulation Load(ContentDb content, string json, bool force = false)
    {
        var options = new JsonSerializerOptions(CanonicalJson.Options);
        options.Converters.Insert(0, new ExtensionStateConverter(content.StateComponents));
        var save = JsonSerializer.Deserialize<Snapshot>(json, options) ?? throw new InvalidDataException("Empty save");
        if (save.SchemaVersion != SchemaVersion) throw new InvalidDataException($"Unsupported save schema {save.SchemaVersion}; expected {SchemaVersion}");
        if (!force && !MatchesContent(content, save))
            throw new InvalidDataException($"Save content mismatch: expected {content.ContentHash}, got {save.ContentHash}");
        if (save.Rng.Length != 4) throw new InvalidDataException("Save must contain four RNG words");
        // Normalization also rejects floats before accepting any mod state.
        _ = CanonicalJson.Serialize(save.State);
        save.State.RebuildIndexes();
        var rng = DeterministicRng.Restore(save.Rng[0], save.Rng[1], save.Rng[2], save.Rng[3]);
        var sim = new Simulation(content, save.Settings, save.State, rng);
        sim.SanitizeMap();
        return sim;
    }
    /// <summary>A save matches frozen content under the current digest, or under the pre-rename digest that the
    /// same content produced before the project rename changed type names. Any other difference is a mismatch.</summary>
    private static bool MatchesContent(ContentDb content, Snapshot save) =>
        save.Mods.SequenceEqual(content.Mods)
        && (save.ContentHash == content.ContentHash || save.ContentHash == content.LegacyContentHash);

    private sealed record Snapshot(int SchemaVersion, LoadedMod[] Mods, string ContentHash, SimulationSettings Settings, ulong[] Rng, GameState State);
}
