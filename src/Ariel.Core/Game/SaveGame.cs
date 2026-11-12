using System.Text.Json;
using System.Text.Json.Serialization;
using Ariel.Content;

namespace Ariel.Game;

/// <summary>Versioned JSON save codec. Loading never regenerates maps, publishes events, or reruns AI.
/// Loading is strict: a member the current types do not declare, such as a field a newer or older build wrote,
/// rejects the whole save rather than being dropped, so a game never continues from state it only partly read.</summary>
public static class SaveGame
{
    /// <summary>Settings decoding, as strict as save loading: a peer on another build fails loudly instead of
    /// starting from settings that silently lost a member.</summary>
    private static readonly JsonSerializerOptions StrictSettings = new(CanonicalJson.Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Supported schema version; incompatible versions require an explicit migration.</summary>
    public const int SchemaVersion = 5;
    /// <summary>Serialize setup, loaded mod versions, content identity, state and RNG words.</summary>
    public static string Save(Simulation sim)
    {
        var (a, b, c, d) = sim.Rng.State;
        return CanonicalJson.Serialize(new Snapshot(SchemaVersion, sim.Content.Mods.ToArray(), sim.Content.ContentHash, sim.Settings, [a, b, c, d], sim.State));
    }
    /// <summary>Load using frozen content. Force bypasses mod/content mismatch only, not unknown state types or invalid
    /// data. Every way a save can be malformed, from bad JSON to duplicate ids, surfaces as one
    /// <see cref="InvalidDataException"/> carrying the original failure, so callers have a single type to catch.</summary>
    public static Simulation Load(ContentDb content, string json, bool force = false)
    {
        try { return LoadUnchecked(content, json, force); }
        catch (Exception e) when (e is not InvalidDataException && IsMalformedData(e))
        {
            throw new InvalidDataException($"Corrupt save: {e.Message}", e);
        }
    }

    private static Simulation LoadUnchecked(ContentDb content, string json, bool force)
    {
        var options = new JsonSerializerOptions(CanonicalJson.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Insert(0, new ExtensionStateConverter(content.StateComponents));
        var save = JsonSerializer.Deserialize<Snapshot>(json, options) ?? throw new InvalidDataException("Empty save");
        if (save.SchemaVersion != SchemaVersion) throw new InvalidDataException($"Unsupported save schema {save.SchemaVersion}; expected {SchemaVersion}");
        if (!force && !MatchesContent(content, save))
            throw new InvalidDataException($"Save content mismatch: expected {content.ContentHash}, got {save.ContentHash}");
        if (save.Rng.Length != 4) throw new InvalidDataException("Save must contain four RNG words");
        // Rejects floats - in practice a hand-edited save - before accepting any mod state, without paying for
        // a second canonical write of the whole document.
        CanonicalJson.ValidateIntegers(json);
        save.State.RebuildIndexes();
        var rng = DeterministicRng.Restore(save.Rng[0], save.Rng[1], save.Rng[2], save.Rng[3]);
        return new Simulation(content, save.Settings, save.State, rng);
    }

    /// <summary>Failures a malformed document raises while it is read or indexed: a bad token, a null where a list
    /// belongs, a duplicate key, an out-of-range mask or an id at the top of the range.</summary>
    private static bool IsMalformedData(Exception e) => e is JsonException or ArgumentException or OverflowException
        or NullReferenceException or KeyNotFoundException or InvalidOperationException or FormatException
        or NotSupportedException or IndexOutOfRangeException;

    /// <summary>Settings as canonical JSON, so a multiplayer host can hand every peer the exact setup it resolved.</summary>
    public static string SaveSettings(SimulationSettings settings) => CanonicalJson.Serialize(settings);
    /// <summary>Read settings written by <see cref="SaveSettings"/>.</summary>
    public static SimulationSettings LoadSettings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SimulationSettings>(json, StrictSettings) ?? throw new InvalidDataException("Empty settings");
        }
        catch (JsonException e) { throw new InvalidDataException($"Corrupt settings: {e.Message}", e); }
    }
    /// <summary>A save matches frozen content under the current digest, or under the pre-rename digest that the
    /// same content produced before the project rename changed type names. Any other difference is a mismatch.</summary>
    private static bool MatchesContent(ContentDb content, Snapshot save) =>
        save.Mods.SequenceEqual(content.Mods)
        && (save.ContentHash == content.ContentHash || save.ContentHash == content.LegacyContentHash);

    private sealed record Snapshot(int SchemaVersion, LoadedMod[] Mods, string ContentHash, SimulationSettings Settings, ulong[] Rng, GameState State);
}
