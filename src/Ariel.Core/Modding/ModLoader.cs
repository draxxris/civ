using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Ariel.Content;

namespace Ariel.Modding;

/// <summary>A mod instance, manifest and sidecar root ready for registration.</summary>
public sealed class ModSource
{
    /// <summary>Mod identity, version and dependencies.</summary>
    public required ModManifest Manifest { get; init; }
    /// <summary>Registration entry point.</summary>
    public required IMod Instance { get; init; }
    /// <summary>Sidecar root directory.</summary>
    public required string Root { get; init; }
}

/// <summary>Deterministic dependency ordering, isolated assembly loading and registry freezing.</summary>
public static class ModLoader
{
    /// <summary>Shared sidecar/command JSON conventions; requirement converters are attached to their types.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private static readonly List<Assembly> Shared = [typeof(ModLoader).Assembly];

    /// <summary>Make a host assembly part of the mod API: a mod that references it binds to the host's copy instead of
    /// loading its own, so both sides share one set of types. <c>Ariel.Core</c> is always shared.</summary>
    public static void Share(Assembly assembly)
    {
        lock (Shared) if (!Shared.Contains(assembly)) Shared.Add(assembly);
    }

    /// <summary>The host's copy of a shared assembly, or null when <paramref name="name"/> is not part of the mod API.</summary>
    public static Assembly? ResolveShared(AssemblyName name)
    {
        lock (Shared) return Shared.Find(a => a.GetName().Name == name.Name);
    }

    /// <summary>Load a mod folder: <c>mod.json</c> + the DLL it names + optional sidecar data.</summary>
    public static ModSource FromDirectory(string dir) => FromDirectory(dir, ReadManifest(dir));

    /// <summary>Load using a manifest already read and validated by the host, without reading it again.</summary>
    public static ModSource FromDirectory(string dir, ModManifest manifest)
    {
        using var trace = StartupTrace.Phase($"mod load {Path.GetFileName(dir)}");
        var alc = new ModLoadContext(Path.Combine(dir, manifest.Assembly));
        var asm = alc.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(dir, manifest.Assembly)));
        return FromAssembly(manifest, asm, dir);
    }

    /// <summary>Read and parse <c>mod.json</c> in a mod or base module folder.</summary>
    public static ModManifest ReadManifest(string root) =>
        JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(Path.Combine(root, "mod.json")), Json)
        ?? throw new InvalidDataException($"{root}/mod.json is empty");

    /// <summary>Use a host-loaded mod assembly with its actual sidecar manifest, including its version.</summary>
    public static ModSource FromAssembly(Assembly assembly, string root) => FromAssembly(ReadManifest(root), assembly, root);

    /// <summary>Use an already-loaded assembly (tests, or a mod compiled into the host).</summary>
    public static ModSource FromAssembly(ModManifest manifest, Assembly assembly, string root)
    {
        var types = assembly.GetExportedTypes().Where(t => !t.IsAbstract && typeof(IMod).IsAssignableFrom(t)).ToList();
        if (types.Count != 1)
            throw new InvalidOperationException($"Mod '{manifest.Id}' must expose exactly one public IMod, found {types.Count}");
        return new ModSource { Manifest = manifest, Instance = (IMod)Activator.CreateInstance(types[0])!, Root = root };
    }

    /// <summary>Order by dependencies, run every mod's Register, freeze. The base game goes through exactly this path too.</summary>
    public static ContentDb Build(IEnumerable<ModSource> mods)
    {
        using var trace = StartupTrace.Phase("content build");
        var ordered = Sort(mods.ToList());
        var data = PreparedModData.Prepare(ordered);
        var db = new ContentDb();
        db.CommandTypes.Register(new CommandTypeDef("core:end_turn", typeof(Game.EndTurnCommand)), "core");
        db.CommandTypes.Register(new CommandTypeDef("core:set_player_control", typeof(Game.SetPlayerControlCommand)), "core");
        for (int i = 0; i < ordered.Count; i++)
        {
            var mod = ordered[i];
            using var registration = StartupTrace.Phase($"mod register {mod.Manifest.Id}");
            data[i].ThrowInventoryError();
            var ctx = new ModContext(mod.Manifest.Id, data[i], db);
            mod.Instance.Register(ctx);
        }
        db.LoadedMods = ordered.Select(m => m.Manifest.Id).ToList();
        db.Mods = ordered.Select(m => new LoadedMod(m.Manifest.Id, m.Manifest.Version)).ToList();
        db.ValidateReferences(ordered.Select(m => m.Manifest));
        db.Freeze();
        return db;
    }

    private static List<ModSource> Sort(List<ModSource> mods)
    {
        var pending = new SortedDictionary<string, ModSource>(StringComparer.Ordinal);
        foreach (var mod in mods)
            if (!pending.TryAdd(mod.Manifest.Id, mod)) throw new InvalidOperationException($"Duplicate mod id '{mod.Manifest.Id}'");
        foreach (var mod in pending.Values)
foreach (var dependency in mod.Manifest.Dependencies.Concat(mod.Manifest.References))
                if (!pending.ContainsKey(dependency))
                    throw new InvalidOperationException($"Mod '{mod.Manifest.Id}' requires missing mod '{dependency}' (dependency or content reference)");

        var result = new List<ModSource>();
        var loaded = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            // Choose the smallest eligible id, independent of input and dependency declaration order.
            var next = pending.Values.FirstOrDefault(m => m.Manifest.Dependencies.All(loaded.Contains));
            if (next == null) throw new InvalidOperationException($"Mod dependency cycle among: {string.Join(", ", pending.Keys)}");
            pending.Remove(next.Manifest.Id);
            loaded.Add(next.Manifest.Id);
            result.Add(next);
        }
        return result;
    }

    /// <summary>Each mod gets its own load context, but the shared mod API always resolves to the host's copy.</summary>
    private sealed class ModLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        public ModLoadContext(string path) : base(Path.GetFileNameWithoutExtension(path), isCollectible: false)
            => _resolver = new AssemblyDependencyResolver(Path.GetFullPath(path));

        protected override Assembly? Load(AssemblyName name)
        {
            if (ResolveShared(name) is { } shared) return shared;
            var p = _resolver.ResolveAssemblyToPath(name);
            return p == null ? null : LoadFromAssemblyPath(p);
        }
    }
}

internal sealed class DirectoryModData(string root) : IModData
{
    private string Full(string path) => Path.Combine(root, path);
    public bool Exists(string path) => File.Exists(Full(path));
    public string ReadText(string path) => File.ReadAllText(Full(path));
    public T ReadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(ReadText(path), ModLoader.Json) ?? throw new InvalidDataException($"{path} is empty");
}

internal sealed class ModContext : IModContext
{
    private readonly ContentDb _db;
    public ModRegistry<T> Content<T>() where T : ContentDef => new(_db.Get<T>(), ModId);
    public void RegisterService<T>(string name, T service) where T : class => _db.Services.Register($"{ModId}:{name}", service, ModId);
    public void RegisterStateComponent<T>(string name) where T : class => _db.StateComponents.Register<T>($"{ModId}:{name}", ModId);
    public void Subscribe<TEvent>(string name, Action<Game.Simulation, TEvent> handler) where TEvent : Game.DomainEvent =>
        RegisterService<Game.IDomainEventSubscriber>(name, new EventHandler<TEvent>(handler));
    private sealed class EventHandler<TEvent>(Action<Game.Simulation, TEvent> handler) : Game.IDomainEventSubscriber where TEvent : Game.DomainEvent
    {
        public void Handle(Game.Simulation sim, Game.DomainEvent message) { if (message is TEvent typed) handler(sim, typed); }
    }
    public string ModId { get; }
    public IModData Data { get; }
    public ModRegistry<IPlayerAI> PlayerAIs { get; }
    public ModRegistry<IMapGenerator> MapGenerators { get; }
    public ModRegistry<ITurnSystem> TurnSystems { get; }
    public ModRegistry<CommandTypeDef> CommandTypes { get; }

    public ModContext(string modId, IModData data, ContentDb db)
    {
        _db = db; ModId = modId; Data = data;
        PlayerAIs = new(db.PlayerAIs, modId);
        MapGenerators = new(db.MapGenerators, modId);
        TurnSystems = new(db.TurnSystems, modId);
        CommandTypes = new(db.CommandTypes, modId);
    }

    public void RegisterCommand<TCommand>(string name) where TCommand : Game.GameCommand =>
        CommandTypes.Register(new CommandTypeDef($"{ModId}:{name}", typeof(TCommand)));
}
