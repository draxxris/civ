using System.Collections;
using System.Reflection;
using System.Text.Json;
using Civ.Game;

namespace Civ.Content;

/// <summary>Frozen mod content and services. Generic content registries are shared across feature mods.</summary>
public sealed class ContentDb
{
    private sealed record Entry(object Registry, Action Freeze, Func<IEnumerable<ContentDef>> Items);
    private readonly Dictionary<Type, Entry> _registries = new();
    private bool _frozen;
    /// <summary>Create a registration database with empty shared-schema registries.</summary>
    public ContentDb()
    {
        _ = Terrains; _ = Units; _ = Promotions; _ = ExperienceLevels; _ = Specialists;
        _ = Civics; _ = Techs; _ = Buildings; _ = CultureLevels; _ = Civilizations;
    }
    /// <summary>Get a registry by content subtype; an unknown type after freezing is an error.</summary>
    public Registry<T> Get<T>() where T : ContentDef
    {
        if (_registries.TryGetValue(typeof(T), out var entry)) return (Registry<T>)entry.Registry;
        if (_frozen) throw new KeyNotFoundException($"Content type '{typeof(T).FullName}' is not registered");
        var registry = new Registry<T>(typeof(T).Name);
        _registries.Add(typeof(T), new(registry, registry.Freeze, () => registry.All));
        return registry;
    }
    /// <summary>Get a definition directly by its subtype and id.</summary>
    public T Get<T>(string id) where T : ContentDef => Get<T>().Get(id);
    /// <summary>Try a subtype/id lookup without creating a new registry.</summary>
    public bool TryGet<T>(string id, out T item) where T : ContentDef
    {
        item = null!;
        return _registries.TryGetValue(typeof(T), out var entry) && ((Registry<T>)entry.Registry).TryGet(id, out item);
    }
    /// <summary>Terrain convenience registry.</summary>
    public Registry<TerrainDef> Terrains => Get<TerrainDef>();
    /// <summary>Unit convenience registry.</summary>
    public Registry<UnitDef> Units => Get<UnitDef>();
    /// <summary>Promotion convenience registry.</summary>
    public Registry<PromotionDef> Promotions => Get<PromotionDef>();
    /// <summary>Experience level convenience registry.</summary>
    public Registry<ExperienceLevelDef> ExperienceLevels => Get<ExperienceLevelDef>();
    /// <summary>Specialist convenience registry.</summary>
    public Registry<SpecialistDef> Specialists => Get<SpecialistDef>();
    /// <summary>Civic convenience registry.</summary>
    public Registry<CivicDef> Civics => Get<CivicDef>();
    /// <summary>Technology convenience registry.</summary>
    public Registry<TechDef> Techs => Get<TechDef>();
    /// <summary>Building convenience registry.</summary>
    public Registry<BuildingDef> Buildings => Get<BuildingDef>();
    /// <summary>Culture level convenience registry.</summary>
    public Registry<CultureLevelDef> CultureLevels => Get<CultureLevelDef>();
    /// <summary>Civilization convenience registry.</summary>
    public Registry<CivilizationDef> Civilizations => Get<CivilizationDef>();
    /// <summary>Player AI implementations.</summary>
    public Registry<IPlayerAI> PlayerAIs { get; } = new("player AI");
    /// <summary>Map generator implementations.</summary>
    public Registry<IMapGenerator> MapGenerators { get; } = new("map generator");
    /// <summary>Turn systems (execution order is Order, then ordinal Id).</summary>
    public Registry<ITurnSystem> TurnSystems { get; } = new("turn system");
    /// <summary>Command serialization contracts.</summary>
    public Registry<CommandTypeDef> CommandTypes { get; } = new("command type");
    /// <summary>Ordered non-content extension implementations.</summary>
    public ServiceRegistry Services { get; } = new();
    /// <summary>Typed extension state identities.</summary>
    public StateComponentRegistry StateComponents { get; } = new();
    /// <summary>Requirement dispatch after content loading.</summary>
    public RequirementService Requirements { get; private set; } = new([]);
    /// <summary>Loaded mod ids in dependency order.</summary>
    public IReadOnlyList<string> LoadedMods { get; internal set; } = [];
    /// <summary>Loaded mod versions in dependency order.</summary>
    public IReadOnlyList<LoadedMod> Mods { get; internal set; } = [];
    /// <summary>SHA-256 of frozen definitions, mod versions and serialized contract identities.</summary>
    public string ContentHash { get; private set; } = "";

    /// <summary>Validate typed identities after all providers register, before freezing or hashing.</summary>
    public void ValidateReferences(IEnumerable<Modding.ModManifest> manifests)
    {
        var declared = manifests.ToDictionary(m => m.Id, m => m.Dependencies.Concat(m.References).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var targets = _registries.ToDictionary(e => e.Key, e => e.Value.Items().Select(d => d.Id).ToHashSet(StringComparer.Ordinal));
        targets[typeof(ContentDef)] = _registries.Values.SelectMany(e => e.Items()).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var errors = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var definition in _registries.Values.SelectMany(e => e.Items()).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var owner = definition.Id.Split(':')[0];
            foreach (var reference in ContentReferences.Enumerate(definition, Services.Get<IRequirementEvaluator>()))
            {
                if (!targets.TryGetValue(reference.DefinitionType, out var ids) || !ids.Contains(reference.Id))
                    errors.Add($"{reference.Path}: '{reference.Id}' does not resolve to {reference.DefinitionType.FullName}");
                var provider = reference.Id.Split(':')[0];
                if (provider != owner && (!declared.TryGetValue(owner, out var providers) || !providers.Contains(provider)))
                    errors.Add($"{reference.Path}: mod '{owner}' must declare content provider '{provider}' in references or dependencies");
            }
        }
        if (errors.Count > 0) throw new InvalidDataException("Content reference validation failed:\n" + string.Join("\n", errors));
    }

    internal void Freeze()
    {
        Requirements = new(Services.Get<IRequirementEvaluator>());
        foreach (var entry in _registries.Values)
            foreach (var item in entry.Items()) ValidateRequirements(item, new HashSet<object>(ReferenceEqualityComparer.Instance));
        var definitions = _registries.OrderBy(kv => kv.Key.FullName, StringComparer.Ordinal)
            .SelectMany(kv => kv.Value.Items().Select(item => new
            {
                Type = kv.Key.FullName, Definition = JsonSerializer.SerializeToElement(item, item.GetType(), CanonicalJson.Options),
            })).ToArray();
        ContentHash = CanonicalJson.Digest(new
        {
            Mods, Definitions = definitions,
            Commands = CommandTypes.All.Select(c => new { c.Id, Type = c.Type.FullName }).ToArray(),
            Components = StateComponents.Ids.Select(id => new { Id = id, Type = StateComponents.TypeOf(id).FullName }).ToArray(),
            Services = Services.Identities.ToArray(),
            MapGenerators = MapGenerators.All.Select(g => new { g.Id, Type = g.GetType().FullName }).ToArray(),
            PlayerAIs = PlayerAIs.All.Select(ai => new { ai.Id, Type = ai.GetType().FullName }).ToArray(),
            TurnSystems = TurnSystems.All.Select(s => new { s.Id, s.Order, Type = s.GetType().FullName }).ToArray(),
        });
        foreach (var entry in _registries.Values) entry.Freeze();
        MapGenerators.Freeze(); PlayerAIs.Freeze(); TurnSystems.Freeze(); CommandTypes.Freeze(); Services.Freeze(); StateComponents.Freeze();
        _frozen = true;
    }
    private void ValidateRequirements(object? value, HashSet<object> seen)
    {
        if (value == null) return;
        if (IsScalar(value) || !seen.Add(value)) return;
        if (value is Requirement requirement) { Requirements.Validate(requirement); return; }
        if (value is IEnumerable sequence) { foreach (var item in sequence) ValidateRequirements(item, seen); return; }
        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            if (property.CanRead && property.GetIndexParameters().Length == 0) ValidateRequirements(property.GetValue(value), seen);
    }
    private static bool IsScalar(object value) => value is string || value is decimal
        || value.GetType().IsPrimitive || value.GetType().IsEnum;
    /// <summary>Highest culture level reached, or null below the first threshold.</summary>
    public CultureLevelDef? CultureLevelFor(long culture)
    {
        CultureLevelDef? best = null;
        foreach (var level in CultureLevels.All)
            if (culture >= level.Threshold && (best == null || level.Threshold >= best.Threshold)) best = level;
        return best;
    }
}

/// <summary>Mod-owned deterministic map generation.</summary>
public interface IMapGenerator : IHasId
{
    /// <summary>Fill every plot, in tile order, for the given map specification. Array length must equal Width * Height.</summary>
    void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content);
}
/// <summary>Deterministic player AI; called on every peer, returning ordinary validated commands.</summary>
public interface IPlayerAI : IHasId
{
    /// <summary>Decide using only simulation state and RNG.</summary>
    IEnumerable<GameCommand> Decide(Simulation sim, int playerId);
}
/// <summary>A rule step executed once per turn in (Order, Id) order.</summary>
public interface ITurnSystem : IHasId
{
    /// <summary>Ascending execution priority.</summary>
    int Order { get; }
    /// <summary>Run the rule step.</summary>
    void Run(Simulation sim);
}
