using Civ.Modding;

namespace Civ.Client;

/// <summary>Finds mod folders (each with a mod.json) under a mods directory.</summary>
public static class ModDiscovery
{
    /// <summary>Environment variable that overrides every other mods location.</summary>
    public const string ModsEnvironmentVariable = "CIV_MODS";

    static ModDiscovery()
    {
        // A host that loads this library into its own load context (Godot) leaves the default context without Civ.Core,
        // which mods resolve through. Hand them the host's copy so mod and host share one set of types.
        var shared = typeof(ModLoader).Assembly;
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) => name.Name == shared.GetName().Name ? shared : null;
    }

    public static IReadOnlyList<ModSource> LoadMods(string modsRoot)
    {
        var result = new List<ModSource>();
        if (!Directory.Exists(modsRoot)) return result;
        foreach (var dir in Directory.GetDirectories(modsRoot).OrderBy(d => d, StringComparer.Ordinal))
            if (File.Exists(Path.Combine(dir, "mod.json")))
                result.Add(ModLoader.FromDirectory(dir));
        return result;
    }

    /// <summary>The first folder that contains at least one mod: the environment override, then the given candidates in order.</summary>
    public static string? FindModsRoot(IEnumerable<string> candidates)
    {
        var all = new[] { Environment.GetEnvironmentVariable(ModsEnvironmentVariable) ?? "" }.Concat(candidates);
        return all.Where(p => p.Length > 0 && Directory.Exists(p))
            .FirstOrDefault(p => Directory.GetDirectories(p).Any(d => File.Exists(Path.Combine(d, "mod.json"))));
    }
}
