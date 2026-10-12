using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Civ.Content;

namespace Civ.Modding;

public sealed class ModSource
{
    public required ModManifest Manifest { get; init; }
    public required IMod Instance { get; init; }
    public required string Root { get; init; }
}

public static class ModLoader
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

    /// <summary>Load a mod folder: <c>mod.json</c> + the DLL it names + optional sidecar data.</summary>
    public static ModSource FromDirectory(string dir)
    {
        var manifest = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(Path.Combine(dir, "mod.json")), Json)
            ?? throw new InvalidDataException($"{dir}/mod.json is empty");
        var alc = new ModLoadContext(Path.Combine(dir, manifest.Assembly));
        var asm = alc.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(dir, manifest.Assembly)));
        return FromAssembly(manifest, asm, dir);
    }

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
        var ordered = Sort(mods.ToList());
        var db = new ContentDb();
        db.CommandTypes.Register(new CommandTypeDef("core:end_turn", typeof(Game.EndTurnCommand)), "core");
        foreach (var mod in ordered)
        {
            var ctx = new ModContext(mod.Manifest.Id, new DirectoryModData(mod.Root), db);
            mod.Instance.Register(ctx);
        }
        db.LoadedMods = ordered.Select(m => m.Manifest.Id).ToList();
        db.Freeze();
        return db;
    }

    private static List<ModSource> Sort(List<ModSource> mods)
    {
        var byId = new Dictionary<string, ModSource>(StringComparer.Ordinal);
        foreach (var m in mods)
            if (!byId.TryAdd(m.Manifest.Id, m)) throw new InvalidOperationException($"Duplicate mod id '{m.Manifest.Id}'");

        var result = new List<ModSource>();
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 = visiting, 2 = done
        void Visit(ModSource m)
        {
            state.TryGetValue(m.Manifest.Id, out var s);
            if (s == 2) return;
            if (s == 1) throw new InvalidOperationException($"Mod dependency cycle at '{m.Manifest.Id}'");
            state[m.Manifest.Id] = 1;
            foreach (var dep in m.Manifest.Dependencies)
            {
                if (!byId.TryGetValue(dep, out var d))
                    throw new InvalidOperationException($"Mod '{m.Manifest.Id}' requires missing mod '{dep}'");
                Visit(d);
            }
            state[m.Manifest.Id] = 2;
            result.Add(m);
        }
        foreach (var m in mods.OrderBy(m => m.Manifest.Id, StringComparer.Ordinal)) Visit(m);
        return result;
    }

    /// <summary>Each mod gets its own load context, but the shared mod API (Civ.Core) always resolves to the host's copy.</summary>
    private sealed class ModLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        public ModLoadContext(string path) : base(Path.GetFileNameWithoutExtension(path), isCollectible: false)
            => _resolver = new AssemblyDependencyResolver(Path.GetFullPath(path));

        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == typeof(ModLoader).Assembly.GetName().Name) return null;
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
    public string ModId { get; }
    public IModData Data { get; }
    public ModRegistry<TerrainDef> Terrains { get; }
    public ModRegistry<CivilizationDef> Civilizations { get; }
    public ModRegistry<IPlayerAI> PlayerAIs { get; }
    public ModRegistry<UnitDef> Units { get; }
    public ModRegistry<PromotionDef> Promotions { get; }
    public ModRegistry<ExperienceLevelDef> ExperienceLevels { get; }
    public ModRegistry<SpecialistDef> Specialists { get; }
    public ModRegistry<CivicDef> Civics { get; }
    public ModRegistry<TechDef> Techs { get; }
    public ModRegistry<BuildingDef> Buildings { get; }
    public ModRegistry<CultureLevelDef> CultureLevels { get; }
    public ModRegistry<IMapGenerator> MapGenerators { get; }
    public ModRegistry<ITurnSystem> TurnSystems { get; }
    public ModRegistry<CommandTypeDef> CommandTypes { get; }

    public ModContext(string modId, IModData data, ContentDb db)
    {
        ModId = modId; Data = data;
        Terrains = new(db.Terrains, modId);
        Civilizations = new(db.Civilizations, modId);
        PlayerAIs = new(db.PlayerAIs, modId);
        Units = new(db.Units, modId);
        Promotions = new(db.Promotions, modId);
        ExperienceLevels = new(db.ExperienceLevels, modId);
        Specialists = new(db.Specialists, modId);
        Civics = new(db.Civics, modId);
        Techs = new(db.Techs, modId);
        Buildings = new(db.Buildings, modId);
        CultureLevels = new(db.CultureLevels, modId);
        MapGenerators = new(db.MapGenerators, modId);
        TurnSystems = new(db.TurnSystems, modId);
        CommandTypes = new(db.CommandTypes, modId);
    }

    public void RegisterCommand<TCommand>(string name) where TCommand : Game.GameCommand =>
        CommandTypes.Register(new CommandTypeDef($"{ModId}:{name}", typeof(TCommand)));
}
