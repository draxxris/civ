using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace Ariel.Modding;

/// <summary>Marks an assembly as the published API of a mod: the types other mods and the client compile against to
/// use that mod's services. The loader shares exactly one copy of a marked assembly across the process.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ModApiAttribute(string provider) : Attribute
{
    /// <summary>Id of the mod that implements this API.</summary>
    public string Provider { get; } = provider;
}

/// <summary>The process-wide set of mod API assemblies. Every mod load context, the host and the tests see one
/// instance of each, so a service registered under an API interface by its provider is found by every consumer.
/// <para>Resolution, by simple assembly name, first match wins:</para>
/// <list type="number">
/// <item>An API already in the set.</item>
/// <item>The host's copy: the context that loaded <c>Ariel.Core</c> resolves the name (a client or a test compiled
/// against the API has it in its own output). Not the default context under Godot.</item>
/// <item>The copy in a mod folder, loaded once into a shared API context. A provider publishes the copy its manifest
/// names (<see cref="ModManifest.Api"/>) before any mod's code runs; a consumer whose provider is absent carries its
/// own copy, recognised by <see cref="ModApiAttribute"/>.</item>
/// </list>
/// A file without the marker is never shared: a mod's private dependencies stay in its own load context.</summary>
public static class ModApis
{
    private static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.Ordinal);
    private static readonly ApiLoadContext Context = new();

    /// <summary>The shared copy of an API assembly already loaded, or null.</summary>
    public static Assembly? Find(AssemblyName name)
    {
        lock (Loaded) return name.Name is { } simple && Loaded.TryGetValue(simple, out var assembly) ? assembly : null;
    }

    /// <summary>Make the API a provider's manifest declares part of the process before any mod's code runs.</summary>
    public static Assembly Publish(string dir, ModManifest manifest)
    {
        if (manifest.Api.Length == 0) throw new ArgumentException($"Mod '{manifest.Id}' publishes no API", nameof(manifest));
        if (Path.GetFileName(manifest.Api) != manifest.Api)
            throw new InvalidDataException($"Mod '{manifest.Id}': api '{manifest.Api}' must be a file name in the mod folder");
        string path = Path.GetFullPath(Path.Combine(dir, manifest.Api));
        if (!File.Exists(path)) throw new FileNotFoundException($"Mod '{manifest.Id}' names API '{manifest.Api}', which is not in '{dir}'", path);
        string? provider = ProviderOf(path);
        if (provider != manifest.Id)
            throw new InvalidDataException($"Mod '{manifest.Id}': '{manifest.Api}' must carry [assembly: ModApi(\"{manifest.Id}\")]" +
                (provider == null ? "" : $", found ModApi(\"{provider}\")"));
        return Load(AssemblyName.GetAssemblyName(path), path);
    }

    /// <summary>The shared copy of <paramref name="name"/> when the file a load context found for it is an API
    /// assembly; null when it is a private dependency of that context.</summary>
    internal static Assembly? Resolve(AssemblyName name, string path) =>
        Find(name) ?? (ProviderOf(path) == null ? null : Load(name, path));

    private static Assembly Load(AssemblyName name, string path)
    {
        string simple = name.Name ?? throw new ArgumentException("Assembly name has no simple name", nameof(name));
        lock (Loaded)
        {
            if (Loaded.TryGetValue(simple, out var known)) return known;
            var assembly = HostCopy(simple) ?? Context.Load(path);
            Loaded.Add(simple, assembly);
            return assembly;
        }
    }

    /// <summary>The host's own copy, when the host was built against this API.</summary>
    private static Assembly? HostCopy(string simple)
    {
        var host = AssemblyLoadContext.GetLoadContext(typeof(ModApis).Assembly) ?? AssemblyLoadContext.Default;
        foreach (var loaded in host.Assemblies)
            if (loaded.GetName().Name == simple) return loaded;
        try { return host.LoadFromAssemblyName(new AssemblyName(simple)); }
        catch (FileNotFoundException) { return null; }
    }

    /// <summary>The provider id of <see cref="ModApiAttribute"/> on the assembly at <paramref name="path"/>, read from
    /// metadata without loading it; null when the file is not marked.</summary>
    internal static string? ProviderOf(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) return null;
        var metadata = pe.GetMetadataReader();
        if (!metadata.IsAssembly) return null;
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (!metadata.StringComparer.Equals(type.Name, nameof(ModApiAttribute))
                || !metadata.StringComparer.Equals(type.Namespace, typeof(ModApiAttribute).Namespace!)) continue;
            var value = metadata.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() != 1) return null;
            return value.ReadSerializedString();
        }
        return null;
    }

    /// <summary>Holds the API copies the host does not have. An API's own references resolve to the shared mod API
    /// (<c>Ariel.Core</c>, <c>Ariel.Rules</c>), to other APIs, or to an API file beside one already loaded.</summary>
    private sealed class ApiLoadContext() : AssemblyLoadContext("Ariel mod APIs", isCollectible: false)
    {
        private readonly List<string> _folders = [];

        public Assembly Load(string path)
        {
            lock (_folders)
            {
                string folder = Path.GetDirectoryName(path)!;
                if (!_folders.Contains(folder)) _folders.Add(folder);
            }
            return LoadFromAssemblyPath(path);
        }

        protected override Assembly? Load(AssemblyName name)
        {
            if (ModLoader.ResolveShared(name) is { } shared) return shared;
            string[] folders;
            lock (_folders) folders = [.. _folders];
            foreach (string folder in folders)
            {
                string candidate = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(candidate) && Resolve(name, candidate) is { } api) return api;
            }
            return null;
        }
    }
}
