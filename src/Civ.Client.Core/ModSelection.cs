using Civ.Modding;

namespace Civ.Client;

/// <summary>A manifest and its location, with no assembly or mod instance.</summary>
public sealed record InstalledMod(ModManifest Manifest, string Root)
{
    public string Source { get; init; } = "Directory";
    public IReadOnlyList<ModOrigin> Overridden { get; init; } = [];
    /// <summary>An invalid winning manifest reserves its ID but cannot enter a load selection.</summary>
    public string? Error { get; init; }
}

public sealed record ModOrigin(string Root, string Source);

public sealed record ModDiscoveryResult(IReadOnlyList<InstalledMod> Mods, IReadOnlyList<string> Errors);

/// <summary>Explicit choices plus their required providers. Content references do not impose load order.</summary>
public sealed class ModSelection
{
    public IReadOnlyList<InstalledMod> Mods { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredBy { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    private ModSelection(IReadOnlyList<InstalledMod> mods, Dictionary<string, HashSet<string>> reasons, IEnumerable<string> errors)
    {
        Mods = mods;
        RequiredBy = reasons.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value.Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        Errors = errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static ModSelection Resolve(IReadOnlyList<InstalledMod> installed, IEnumerable<string> enabledIds)
    {
        var available = installed.GroupBy(m => m.Manifest.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var selected = new Dictionary<string, InstalledMod>(StringComparer.Ordinal);
        var reasons = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var errors = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(enabledIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        while (pending.TryDequeue(out var id))
        {
            if (!visited.Add(id)) continue;
            if (!available.TryGetValue(id, out var candidates))
            {
                string reason = reasons.TryGetValue(id, out var parents) ? $" Required by: {string.Join(", ", parents.Order(StringComparer.Ordinal))}." : "";
                errors.Add($"Mod '{id}' is not installed.{reason} Install it or disable the mods that require it.");
                continue;
            }
            if (candidates.Length != 1)
            {
                errors.Add($"Duplicate mod id '{id}'. Remove the duplicate folders or disable this mod.");
                continue;
            }
            var mod = candidates[0];
            if (mod.Error != null)
            {
                errors.Add($"Mod '{id}' is blocked. {mod.Error} Repair this manifest or disable the mod. Lower-priority copies cannot replace it.");
                continue;
            }
            selected.Add(id, mod);
            IncludeRequirements(mod, reasons, pending);
        }
        FindCycles(selected, errors);
        return new(selected.Values.OrderBy(m => m.Manifest.Id, StringComparer.Ordinal).ToArray(), reasons, errors);
    }

    private static void IncludeRequirements(InstalledMod mod, Dictionary<string, HashSet<string>> reasons, Queue<string> pending)
    {
        foreach (var id in mod.Manifest.Dependencies.Concat(mod.Manifest.References).Distinct(StringComparer.Ordinal))
        {
            if (!reasons.TryGetValue(id, out var parents)) reasons[id] = parents = new(StringComparer.Ordinal);
            parents.Add(mod.Manifest.Id);
            pending.Enqueue(id);
        }
    }

    private static void FindCycles(Dictionary<string, InstalledMod> selected, List<string> errors)
    {
        // Iterative elimination avoids recursion limits on large mod sets. References can be cyclic.
        var pending = new Dictionary<string, InstalledMod>(selected, StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var ready = pending.Values.Where(m => m.Manifest.Dependencies.All(d => !selected.ContainsKey(d) || done.Contains(d))).ToArray();
            if (ready.Length == 0)
            {
                errors.Add($"Dependency cycle blocks: {string.Join(", ", pending.Keys.Order(StringComparer.Ordinal))}. Disable these mods or correct their manifests.");
                return;
            }
            foreach (var mod in ready) { pending.Remove(mod.Manifest.Id); done.Add(mod.Manifest.Id); }
        }
    }
}
