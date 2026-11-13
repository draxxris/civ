using System.Reflection;
using System.Text.Json;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.ContentImport;

/// <summary>One source field. An <c>effect*</c> kind adds data-driven modifiers (<see cref="Ariel.Content.ModifierEffect"/>)
/// under <see cref="Modifier"/> in <see cref="Scope"/> to the definition's <c>Effects</c> instead of setting
/// <see cref="Property"/>.</summary>
public sealed record FieldMapping(string Source, string Property, string Kind, string? Target = null,
    string? ValueTarget = null, string? Key = null, string? Value = null, JsonElement Default = default,
    string? Scope = null, string? Modifier = null);
public sealed record RequirementMapping(string Source, string Kind, string Target, string Mode, int? Scope = null);
/// <summary>One emitted family. A family whose definition is a <see cref="ContentAttachment"/> is what a feature mod says
/// about the same source records in its own vocabulary: each record with a non-default field becomes one row, with
/// id <c>mod:name</c>, attached to the record's primary definition.</summary>
public sealed record DefinitionMapping(string SourceClass, string Definition, string Mod,
    FieldMapping[] Fields, RequirementMapping[] Requirements)
{
    // The base schema, and the published APIs whose definitions the importer emits for their mods.
    private static readonly System.Reflection.Assembly[] Schemas =
    [
        typeof(GameContentDef).Assembly, typeof(Ariel.Mods.Corporations.CivicCorporationsDef).Assembly,
        typeof(Ariel.Mods.Trade.CivicTradeDef).Assembly, typeof(Ariel.Mods.Diplomacy.CivicDiplomacyDef).Assembly,
        typeof(Ariel.Mods.Revolution.CivicRevolutionDef).Assembly, typeof(Ariel.Mods.ProjectsVictory.CivicProjectsDef).Assembly,
    ];
    public Type DefinitionType => Schemas.SelectMany(a => a.GetExportedTypes())
        .SingleOrDefault(t => t.Name == Definition && typeof(ContentDef).IsAssignableFrom(t) && !t.IsAbstract)
        ?? throw new InvalidDataException("Unknown schema definition: " + Definition);
    public bool IsAttachment => typeof(ContentAttachment).IsAssignableFrom(DefinitionType);
}

/// <summary>Explicit source-to-contract mapping. The catalog belongs to tooling, not the game.</summary>
public static class SchemaCatalog
{
    public static IReadOnlyList<DefinitionMapping> Definitions { get; } = Read<DefinitionMapping[]>("SchemaMapping.json");
    public static IReadOnlyDictionary<string, string> Constants { get; } = Read<Dictionary<string, string>>("RuleConstantMapping.json");
    private static T Read<T>(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ariel.ContentImport." + name)
            ?? throw new InvalidDataException("Missing importer mapping resource: " + name);
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
