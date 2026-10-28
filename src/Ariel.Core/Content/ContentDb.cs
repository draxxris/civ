using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ariel.Game;

namespace Ariel.Content;

/// <summary>Frozen mod content and services. Generic content registries are shared across feature mods.</summary>
public sealed class ContentDb
{
    private sealed record Entry(object Registry, Action Freeze, Func<IEnumerable<ContentDef>> Items);

    // Serialize each runtime type with its own root writer, without retaining a per-item JSON tree.
    [JsonConverter(typeof(HashDefinitionConverter))]
    private readonly record struct HashDefinition(ContentDef Value);
    private sealed class HashDefinitionConverter : JsonConverter<HashDefinition>
    {
        public override HashDefinition Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, HashDefinition value, JsonSerializerOptions options) =>
            // Serializer output is validated when Digest parses the aggregate JSON tree.
            writer.WriteRawValue(JsonSerializer.SerializeToUtf8Bytes(value.Value, value.Value.GetType(), options), skipInputValidation: true);
    }
    private static readonly ConditionalWeakTable<Type, PropertyInfo[]> RequirementProperties = new();
    private static PropertyInfo[] DescribeRequirementProperties(Type type) => type
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !IsScalarType(p.PropertyType)).ToArray();
    private readonly Dictionary<Type, Entry> _registries = new();
    private bool _frozen;
    private Lazy<string>? _hash;
    private Lazy<string>? _legacyHash;
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
    /// <summary>SHA-256 of frozen definitions, mod versions and serialized contract identities. Computed on first
    /// read, and prefetched by the client on the thread pool after loading, so a later reader normally does not pay
    /// for it. Empty before <see cref="Freeze"/>.</summary>
    public string ContentHash => _hash is null ? "" : _hash.Value;

    /// <summary>Start the content digest on the thread pool once the database is frozen. Fire and forget: the lazy
    /// itself rethrows a failure on the next read.</summary>
    public void PrefetchContentHash()
    {
        if (_hash is { } hash) _ = Task.Run(() => _ = hash.Value);
    }

    /// <summary>Validate typed identities after all providers register, before freezing or hashing.</summary>
    public void ValidateReferences(IEnumerable<Modding.ModManifest> manifests)
    {
        using var trace = Modding.StartupTrace.Phase("content reference validation");
        var declared = manifests.ToDictionary(m => m.Id, m => m.Dependencies.Concat(m.References).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var targets = _registries.ToDictionary(e => e.Key, e => e.Value.Items().Select(d => d.Id).ToHashSet(StringComparer.Ordinal));
        targets[typeof(ContentDef)] = _registries.Values.SelectMany(e => e.Items()).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var kinds = Services.Get<IRequirementEvaluator>().OfType<IContentRequirementEvaluator>()
            .ToDictionary(e => e.Kind, e => e.DefinitionType, StringComparer.Ordinal);
        var errors = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var definition in _registries.Values.SelectMany(e => e.Items()).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var owner = definition.Id.Split(':')[0];
            foreach (var reference in ContentReferences.Enumerate(definition, kinds))
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
        using var trace = Modding.StartupTrace.Phase("content freeze");
        Requirements = new(Services.Get<IRequirementEvaluator>());
        using (Modding.StartupTrace.Phase("content requirement validation"))
            foreach (var entry in _registries.Values)
                foreach (var item in entry.Items()) ValidateRequirements(item, new HashSet<object>(ReferenceEqualityComparer.Instance));
        _hash = HashLazy();
        _legacyHash = LegacyHashLazy();
        using var freezeTrace = Modding.StartupTrace.Phase("registry freeze");
        foreach (var entry in _registries.Values) entry.Freeze();
        MapGenerators.Freeze(); PlayerAIs.Freeze(); TurnSystems.Freeze(); CommandTypes.Freeze(); Services.Freeze(); StateComponents.Freeze();
        _frozen = true;
    }
    /// <summary>Capture the digest input now and return the digest to run on first read. Registry enumeration order
    /// is stable before and after the freeze, so taking the digest later yields the identical value.</summary>
    private Lazy<string> HashLazy()
    {
        using var trace = Modding.StartupTrace.Phase("content hash input");
        var definitions = Definitions(Identity);
        return new Lazy<string>(() =>
        {
            using var hashTrace = Modding.StartupTrace.Phase("content hash");
            return CanonicalJson.Digest(Input(definitions, Identity));
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }
    /// <summary>Digest of the same content under the pre-rename type names, or empty before <see cref="Freeze"/>.
    /// A save written before the project rename namespaces were renamed validates against this digest, and only
    /// this digest: content, mod versions and every other digest input must still match exactly. Computed on first
    /// read because a save from that era is rare.</summary>
    public string LegacyContentHash => _legacyHash?.Value ?? "";

    private Lazy<string> LegacyHashLazy() => new(() =>
    {
        using var trace = Modding.StartupTrace.Phase("legacy content hash");
        return CanonicalJson.Digest(Input(Definitions(PreRenameName), PreRenameName));
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Undo the namespace part of the project rename for digest input, so a digest of current content
    /// reproduces the digest the pre-rename build computed.</summary>
    private static string? PreRenameName(string? name) => name switch
    {
        null => null,
        "Ariel" => "Civ",
        _ when name.StartsWith("Ariel.", StringComparison.Ordinal) => "Civ." + name["Ariel.".Length..],
        _ => name,
    };

    private static string? Identity(string? name) => name;

    private object[] Definitions(Func<string?, string?> name) =>
        _registries.OrderBy(kv => name(kv.Key.FullName), StringComparer.Ordinal)
            .SelectMany(kv => kv.Value.Items().Select(item => new
            {
                Type = name(kv.Key.FullName), Definition = new HashDefinition(item),
            })).ToArray();

    /// <summary>Every identity that reaches the digest, with type names passed through <paramref name="name"/>.</summary>
    private object Input(object[] definitions, Func<string?, string?> name) => new
    {
        Mods, Definitions = definitions,
        Commands = CommandTypes.All.Select(c => new { c.Id, Type = name(c.Type.FullName) }).ToArray(),
        Components = StateComponents.Ids.Select(id => new { Id = id, Type = name(StateComponents.TypeOf(id).FullName) }).ToArray(),
        Services = Services.Identities.Select(name).ToArray(),
        MapGenerators = MapGenerators.All.Select(g => new { g.Id, Type = name(g.GetType().FullName) }).ToArray(),
        PlayerAIs = PlayerAIs.All.Select(ai => new { ai.Id, Type = name(ai.GetType().FullName) }).ToArray(),
        TurnSystems = TurnSystems.All.Select(s => new { s.Id, s.Order, Type = name(s.GetType().FullName) }).ToArray(),
    };
    private void ValidateRequirements(object? value, HashSet<object> seen)
    {
        if (value == null) return;
        if (IsScalar(value) || !seen.Add(value)) return;
        if (value is Requirement requirement) { Requirements.Validate(requirement); return; }
        if (value is IEnumerable sequence) { foreach (var item in sequence) ValidateRequirements(item, seen); return; }
        foreach (var property in RequirementProperties.GetValue(value.GetType(), DescribeRequirementProperties))
            ValidateRequirements(property.GetValue(value), seen);
    }
    private static bool IsScalar(object value) => IsScalarType(value.GetType());
    private static bool IsScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string) || type == typeof(decimal) || type.IsPrimitive || type.IsEnum;
    }
}

/// <summary>Mod-owned deterministic map generation.</summary>
public interface IMapGenerator : IHasId
{
    /// <summary>Display name for map setup choices.</summary>
    string Name => Id;
    /// <summary>Fill every plot, in tile order, for the given map specification. Array length must equal Width * Height.</summary>
    void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content);
    /// <summary>Setup-aware placement. Legacy generators ignore player-dependent resource density.</summary>
    void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content, int players) => Generate(spec, plots, rng, content);
}
/// <summary>Deterministic player AI; called on every peer, returning ordinary validated commands.</summary>
public interface IPlayerAI : IHasId
{
    /// <summary>Decide using only simulation state and RNG.</summary>
    IEnumerable<GameCommand> Decide(Simulation sim, int playerId);
}
/// <summary>A rule step in (Order, Id) order. Player-interval steps implement IPlayerTurnSystem and run once per player.</summary>
public interface ITurnSystem : IHasId
{
    /// <summary>Ascending execution priority.</summary>
    int Order { get; }
    /// <summary>Run the rule step.</summary>
    void Run(Simulation sim);
}
