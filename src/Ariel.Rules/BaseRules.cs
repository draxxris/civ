using Ariel.Modding;

namespace Ariel.Rules;

/// <summary>The rules modules of the base game, linked into the host instead of loaded as mod assemblies.
/// A module keeps its own content namespace, manifest and sidecar folder, and registers through the same
/// <see cref="IModContext"/> as a mod, so ids, load order and content identity do not depend on where code lives.</summary>
public static class BaseRules
{
    private sealed record Module(string Id, string Folder, Func<IMod> Create);

    private static readonly Module[] Modules =
    [
        new("cities", "Cities", () => new Cities.CitiesModule()),
        new("economy", "Economy", () => new Economy.EconomyModule()),
        new("foundation", "Foundation", () => new Foundation.FoundationModule()),
        new("government", "Government", () => new Government.GovernmentModule()),
        new("military", "Military", () => new Military.MilitaryModule()),
        new("peoples", "Peoples", () => new Peoples.PeoplesModule()),
        new("relations", "Relations", () => new Relations.RelationsModule()),
        new("religion", "Religion", () => new Religion.ReligionModule()),
        new("research", "Research", () => new Research.ResearchModule()),
        new("world", "World", () => new World.WorldModule()),
    ];

    /// <summary>Module ids in ordinal order. A mod may depend on them; none can be disabled or replaced.</summary>
    public static IReadOnlyList<string> Ids { get; } = Modules.Select(m => m.Id).ToArray();

    /// <summary>Whether an id names a base module.</summary>
    public static bool IsBase(string id) => Array.Exists(Modules, m => m.Id == id);

    /// <summary>Fresh module instances with the manifests and sidecars staged under <paramref name="rulesRoot"/>.</summary>
    public static IReadOnlyList<ModSource> Sources(string rulesRoot)
    {
        var sources = new ModSource[Modules.Length];
        for (int i = 0; i < Modules.Length; i++)
            sources[i] = Linked(rulesRoot, Modules[i].Id, Modules[i].Folder, Modules[i].Create());
        return sources;
    }

    /// <summary>A module linked into the host, whose manifest and sidecars are staged under
    /// <c><paramref name="rulesRoot"/>/<paramref name="folder"/></c> and must declare <paramref name="id"/>.</summary>
    public static ModSource Linked(string rulesRoot, string id, string folder, IMod instance)
    {
        string root = Path.Combine(rulesRoot, folder);
        var manifest = ModLoader.ReadManifest(root);
        if (manifest.Id != id)
            throw new InvalidDataException($"{root}/mod.json declares '{manifest.Id}', expected base module '{id}'");
        return new ModSource { Manifest = manifest, Instance = instance, Root = root };
    }
}
