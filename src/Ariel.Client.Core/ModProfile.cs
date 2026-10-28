using System.Text.Json;

namespace Ariel.Client;

/// <summary>Explicit mod choices, stored by manifest ID. Required providers are resolved at each startup.</summary>
public sealed record ModProfile
{
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; init; } = 1;
    [System.Text.Json.Serialization.JsonRequired]
    public string[] EnabledModIds { get; init; } = [];

    /// <summary>A missing file means the original all-mod default. A broken file must not silently enable code.</summary>
    public static ModProfile? Load(string path)
    {
        string json;
        try { json = File.ReadAllText(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        var profile = JsonSerializer.Deserialize<ModProfile>(json)
            ?? throw new InvalidDataException("The mod profile is empty. Reset the selection or repair the profile.");
        if (profile.Version != 1 || profile.EnabledModIds == null || profile.EnabledModIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("The mod profile has an unsupported version or invalid IDs. Reset the selection or repair the profile.");
        return profile;
    }

    public static IReadOnlyList<string> InitialSelection(IReadOnlyList<InstalledMod> installed, ModProfile? profile) =>
        profile?.EnabledModIds ?? installed.Select(m => m.Manifest.Id).Distinct(StringComparer.Ordinal).ToArray();

    public static void Save(string path, IEnumerable<string> enabledIds)
    {
        var profile = new ModProfile { EnabledModIds = enabledIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
