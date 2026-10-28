using System.Text;
using System.Text.Json;
using Ariel.Modding;

namespace Ariel.Client;

/// <summary>Finds manifest-only mod installations before the host selects executable code.</summary>
public static class ModDiscovery
{
    /// <summary>Optional mods root with the highest per-ID priority.</summary>
    public const string ModsEnvironmentVariable = "ARIEL_MODS";

    static ModDiscovery()
    {
        // A host that loads this library into its own load context (Godot) leaves the default context without Ariel.Core,
        // which mods resolve through. Hand them the host's copy so mod and host share one set of types.
        var shared = typeof(ModLoader).Assembly;
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) => name.Name == shared.GetName().Name ? shared : null;
    }

    /// <summary>Reads manifests only. Invalid folders remain diagnostics, not executable mods.</summary>
    public static ModDiscoveryResult Discover(string modsRoot)
    {
        var errors = new List<string>();
        var mods = ReadRoot(modsRoot, "Directory", errors).Where(m => m.Error == null).ToArray();
        return new(mods, errors);
    }

    /// <summary>
    /// Merges IDs in priority order: environment, user, then bundled roots in caller order.
    /// Within each root, ordinal folder order breaks ties. A winner replaces the entire losing installation.
    /// Invalid manifests reserve every readable ID so a lower-priority copy cannot execute instead.
    /// </summary>
    public static ModDiscoveryResult DiscoverMerged(IEnumerable<string> bundledRoots, string? userModsRoot, string? environmentModsRoot)
    {
        var locations = new[] { new ModOrigin(environmentModsRoot ?? "", ModsEnvironmentVariable), new ModOrigin(userModsRoot ?? "", "User-installed") }
            .Concat(bundledRoots.Select((root, index) => new ModOrigin(root, $"Bundled {index + 1}")));
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var candidates = new List<InstalledMod>();
        var errors = new List<string>();
        foreach (var location in locations)
        {
            if (string.IsNullOrWhiteSpace(location.Root) || !Directory.Exists(location.Root)) continue;
            if (!seen.Add(CanonicalRoot(location.Root))) continue;
            candidates.AddRange(ReadRoot(location.Root, location.Source, errors));
        }
        var winners = candidates.GroupBy(m => m.Manifest.Id, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.First() with { Overridden = g.Skip(1).Select(m => new ModOrigin(m.Root, m.Source)).ToArray() }).ToArray();
        return new(winners, errors);
    }

    private static string CanonicalRoot(string root)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        // Restart at the filesystem root after each link, including links in a target's ancestors.
        // Bound even expanding cycles, whose paths never repeat. No recursive traversal is needed.
        for (int links = 0; links <= 64; links++)
        {
            if (!seen.Add(path)) throw new IOException($"Mod root contains a link cycle: '{root}'.");
            string? target = ResolveFirstRootLink(path);
            if (target == null) return path;
            path = target;
        }
        throw new IOException($"Mod root exceeds the limit of 64 directory links: '{root}'.");
    }

    private static string? ResolveFirstRootLink(string path)
    {
        string current = Path.GetPathRoot(path)!;
        var parts = path[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            current = Path.Combine(current, part);
            var target = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: false);
            if (target != null)
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(target.FullName, Path.GetRelativePath(current, path))));
        }
        return null;
    }

    private static IReadOnlyList<InstalledMod> ReadRoot(string root, string source, List<string> errors)
    {
        var mods = new List<InstalledMod>();
        if (!Directory.Exists(root)) return mods;
        foreach (var dir in Directory.GetDirectories(root).Order(StringComparer.Ordinal))
        {
            if (!File.Exists(Path.Combine(dir, "mod.json"))) continue;
            mods.AddRange(ReadCandidate(Path.GetFullPath(dir), source, errors));
        }
        return mods;
    }

    private static IReadOnlyList<InstalledMod> ReadCandidate(string dir, string source, List<string> errors)
    {
        IReadOnlyList<string> ids = [];
        try
        {
            string json = File.ReadAllText(Path.Combine(dir, "mod.json"));
            ids = ReadIdentities(json);
            if (ids.Count > 1) throw new InvalidDataException("The manifest declares conflicting IDs.");
            return [new(ReadManifest(json), dir) { Source = source }];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            string message = $"Invalid manifest '{Path.Combine(dir, "mod.json")}': {error.Message}";
            errors.Add(message);
            return ids.Select(id => new InstalledMod(new() { Id = id, Name = id }, dir) { Source = source, Error = message }).ToArray();
        }
    }

    private static IReadOnlyList<string> ReadIdentities(string json)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1) continue;
                if (!string.Equals(reader.GetString(), "id", StringComparison.OrdinalIgnoreCase)) continue;
                if (reader.Read() && reader.TokenType == JsonTokenType.String && !string.IsNullOrWhiteSpace(reader.GetString()))
                    ids.Add(reader.GetString()!);
            }
        }
        catch (JsonException) { /* A readable ID before malformed metadata still reserves that identity. */ }
        return ids.Order(StringComparer.Ordinal).ToArray();
    }

    private static ModManifest ReadManifest(string json)
    {
        var manifest = JsonSerializer.Deserialize<ModManifest>(json, ModLoader.Json)
            ?? throw new InvalidDataException("The manifest is empty.");
        string[] requiredFields = [manifest.Id, manifest.Name, manifest.Version, manifest.Assembly];
        if (requiredFields.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("The manifest needs an id, name, version, and assembly.");
        ValidateRequirements(manifest.Dependencies);
        ValidateRequirements(manifest.References);
        return manifest;
    }

    private static void ValidateRequirements(string[]? ids)
    {
        if (ids == null || ids.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Dependency and reference IDs must be nonempty strings.");
    }

    /// <summary>Compatibility path: no profile means all installed mods.</summary>
    public static IReadOnlyList<ModSource> LoadMods(string modsRoot)
    {
        var discovery = Discover(modsRoot);
        if (discovery.Errors.Count > 0) throw new InvalidDataException(string.Join("\n", discovery.Errors));
        return LoadMods(discovery.Mods, discovery.Mods.Select(m => m.Manifest.Id));
    }

    /// <summary>Checks the complete selection before loading any assembly. Registration order belongs to ModLoader.</summary>
    public static IReadOnlyList<ModSource> LoadMods(IReadOnlyList<InstalledMod> installed, IEnumerable<string> enabledIds)
    {
        var selection = ModSelection.Resolve(installed, enabledIds);
        if (!selection.IsValid) throw new InvalidOperationException(string.Join("\n", selection.Errors));
        // Finish the entire preflight before loading code. Use these manifests rather than reading
        // them again during loading. DLLs and sidecars can still change; this is not a filesystem lock.
        var validated = selection.Mods.Select(RevalidateManifest).ToArray();
        return selection.Mods.Select((m, index) => ModLoader.FromDirectory(m.Root, validated[index])).ToArray();
    }

    private static ModManifest RevalidateManifest(InstalledMod mod)
    {
        var errors = new List<string>();
        var current = ReadCandidate(mod.Root, mod.Source, errors);
        if (errors.Count > 0 || current.Count != 1 ||
            JsonSerializer.Serialize(current[0].Manifest, ModLoader.Json) != JsonSerializer.Serialize(mod.Manifest, ModLoader.Json))
            throw new InvalidOperationException($"Manifest '{Path.Combine(mod.Root, "mod.json")}' changed or cannot be validated. " +
                $"Restart the client to refresh the mod selection. {string.Join("\n", errors)}");
        return current[0].Manifest;
    }

    /// <summary>The first folder that contains at least one mod: the environment override, then the given candidates in order.</summary>
    public static string? FindModsRoot(IEnumerable<string> candidates)
    {
        var all = new[] { Environment.GetEnvironmentVariable(ModsEnvironmentVariable) ?? "" }.Concat(candidates);
        return all.Where(p => p.Length > 0 && Directory.Exists(p))
            .FirstOrDefault(p => Directory.GetDirectories(p).Any(d => File.Exists(Path.Combine(d, "mod.json"))));
    }
}
