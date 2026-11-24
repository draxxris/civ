using System.Reflection;
using System.Text.Json;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.ContentImport;

/// <summary>One source field. An <c>effect*</c> kind adds data-driven modifiers (<see cref="Ariel.Content.ModifierEffect"/>)
/// under <see cref="Modifier"/> in <see cref="Scope"/> to the definition's <c>Effects</c> instead of setting
/// <see cref="Property"/>. With <see cref="Owned"/>, a map keeps only the entries naming records the emitting mod owns
/// outright (<see cref="IdMapping.RecordOwners"/>): what a mod says about a base row in terms of its own records. A
/// <c>leafRef</c> field names its <see cref="Target"/> record as a <see cref="LeafReference"/> of <see cref="LeafKind"/>;
/// a missing reference leaves the property as it is, so several source fields can fill one reference.
/// <para>A commerce field (<c>commerce</c>, <c>mapCommerce</c>, <c>buildingCommerce</c>, <c>effectCommerce</c>,
/// <c>effectMapCommerce</c>) carries only the commerce types its row may name: the base game's, and those the emitting mod
/// owns. With <see cref="Part"/>, it carries only that type, which is how an amending row (<c>AmendsTarget</c>) keeps a mod's
/// share of a vector a base row has given up. <see cref="OnlyKeys"/> and <see cref="ExceptKeys"/> split a map whose keys
/// are native names (a leader's memory attitudes) the same way.</para></summary>
public sealed record FieldMapping(string Source, string Property, string Kind, string? Target = null,
    string? ValueTarget = null, string? Key = null, string? Value = null, JsonElement Default = default,
    string? Scope = null, string? Modifier = null, bool Owned = false, string? LeafKind = null,
    string? Part = null, string[]? OnlyKeys = null, string[]? ExceptKeys = null);
public sealed record RequirementMapping(string Source, string Kind, string Target, string Mode, int? Scope = null);
/// <summary>One emitted family. A family whose definition is a <see cref="ContentAttachment"/> is what a feature mod says
/// about the same source records in its own vocabulary: each record with a non-default field becomes one row, with
/// id <c>mod:name</c>, attached to the record's primary definition. With <see cref="TargetMod"/>, the row is about that
/// mod's own row about the record (Espionage's amendment of the Religion mod's building row), whose id is
/// <c>TargetMod:name</c>, not the record's.</summary>
public sealed record DefinitionMapping(string SourceClass, string Definition, string Mod,
    FieldMapping[] Fields, RequirementMapping[] Requirements, string? TargetMod = null)
{
    /// <summary>The folder under the mod's <c>data</c> its rows go to, when the mod registers them only with another mod
    /// (<see cref="IdMapping.OptionalProviders"/>): empty for the data folder itself.</summary>
    public string Folder => TargetMod is null ? "" : IdMapping.Subfolder(Mod, TargetMod) ?? "";
    public Type DefinitionType => SchemaCatalog.DefinitionType(Definition);
    public bool IsAttachment => typeof(ContentAttachment).IsAssignableFrom(DefinitionType);
}

/// <summary>Which defines become typed rule constants: <see cref="Values"/> maps a define to a property of the base
/// game's <see cref="RuleConstants"/>, and each of <see cref="Attachments"/> maps defines to a property of a mod's
/// attachment to <see cref="RuleConstantsDef"/>. Every other define is kept as a raw string.</summary>
public sealed record ConstantMapping(Dictionary<string, string> Values, ConstantAttachmentMapping[] Attachments);
/// <summary>The rule constants a mod keeps in its own row about the base game's rule constants.</summary>
public sealed record ConstantAttachmentMapping(string Mod, string Definition, Dictionary<string, string> Constants)
{
    public Type DefinitionType => SchemaCatalog.DefinitionType(Definition);
}

/// <summary>Explicit source-to-contract mapping. The catalog belongs to tooling, not the game.</summary>
public static class SchemaCatalog
{
    // The base schema, and the published APIs whose definitions the importer emits for their mods.
    public static readonly System.Reflection.Assembly[] Schemas =
    [
        typeof(GameContentDef).Assembly, typeof(Ariel.Mods.Corporations.CivicCorporationsDef).Assembly,
        typeof(Ariel.Mods.Trade.CivicTradeDef).Assembly,
        typeof(Ariel.Mods.Revolution.CivicRevolutionDef).Assembly, typeof(Ariel.Mods.ProjectsVictory.CivicProjectsDef).Assembly,
        typeof(Ariel.Mods.Religion.ReligionDef).Assembly, typeof(Ariel.Mods.Votes.VoteDef).Assembly,
        typeof(Ariel.Mods.GreatPeople.UnitGreatPeopleDef).Assembly, typeof(Ariel.Mods.Espionage.UnitEspionageDef).Assembly,
        typeof(Ariel.Mods.AirCombat.UnitAirCombatDef).Assembly, typeof(Ariel.Mods.RangedCombat.UnitRangedCombatDef).Assembly,
        typeof(Ariel.Mods.Barbarians.HandicapBarbariansDef).Assembly, typeof(Ariel.Mods.GlobalWarming.FeatureGlobalWarmingDef).Assembly,
    ];
    public static IReadOnlyList<DefinitionMapping> Definitions { get; } = Read<DefinitionMapping[]>("SchemaMapping.json");
    /// <summary>Every requirement leaf kind the schemas declare (<see cref="ContentLeafKindAttribute"/>), base and mod,
    /// with the definition type its ids name.</summary>
    public static IReadOnlyDictionary<string, Type> LeafKinds { get; } = ContentLeafKinds.In(Schemas);
    public static ConstantMapping Constants { get; } = Read<ConstantMapping>("RuleConstantMapping.json");
    /// <summary>The schema or published API definition type with this name.</summary>
    public static Type DefinitionType(string name) => Schemas.SelectMany(a => a.GetExportedTypes())
        .SingleOrDefault(t => t.Name == name && typeof(ContentDef).IsAssignableFrom(t) && !t.IsAbstract)
        ?? throw new InvalidDataException("Unknown schema definition: " + name);
    private static T Read<T>(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ariel.ContentImport." + name)
            ?? throw new InvalidDataException("Missing importer mapping resource: " + name);
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
