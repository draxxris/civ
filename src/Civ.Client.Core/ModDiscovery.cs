using Civ.Modding;

namespace Civ.Client;

/// <summary>Finds mod folders (each with a mod.json) and visual resource packs (.pck) under a mods directory.</summary>
public static class ModDiscovery
{
    public static IReadOnlyList<ModSource> LoadMods(string modsRoot)
    {
        var result = new List<ModSource>();
        if (!Directory.Exists(modsRoot)) return result;
        foreach (var dir in Directory.GetDirectories(modsRoot).OrderBy(d => d, StringComparer.Ordinal))
            if (File.Exists(Path.Combine(dir, "mod.json")))
                result.Add(ModLoader.FromDirectory(dir));
        return result;
    }

    /// <summary>Resource packs mods ship for custom visuals (sprites, models, scenes as data). Loaded by the engine, never by code.</summary>
    public static IReadOnlyList<string> FindPacks(string modsRoot) =>
        Directory.Exists(modsRoot)
            ? Directory.GetFiles(modsRoot, "*.pck", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToList()
            : [];
}
