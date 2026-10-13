using System.Reflection;
using System.Text.Json;
using Civ.Content.Schema;

namespace Civ.ContentImport;

public sealed record FieldMapping(string Source, string Property, string Kind, string? Target = null,
    string? ValueTarget = null, string? Key = null, string? Value = null, JsonElement Default = default);
public sealed record RequirementMapping(string Source, string Kind, string Target, string Mode, int? Scope = null);
public sealed record DefinitionMapping(string SourceClass, string Definition, string Mod,
    FieldMapping[] Fields, RequirementMapping[] Requirements)
{
    public Type DefinitionType => typeof(GameContentDef).Assembly.GetType("Civ.Content.Schema." + Definition)
        ?? throw new InvalidDataException("Unknown schema definition: " + Definition);
}

/// <summary>Explicit source-to-contract mapping. The catalog belongs to tooling, not the game.</summary>
public static class SchemaCatalog
{
    public static IReadOnlyList<DefinitionMapping> Definitions { get; } = Read<DefinitionMapping[]>("SchemaMapping.json");
    public static IReadOnlyDictionary<string, string> Constants { get; } = Read<Dictionary<string, string>>("RuleConstantMapping.json");
    private static T Read<T>(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Civ.ContentImport." + name)
            ?? throw new InvalidDataException("Missing importer mapping resource: " + name);
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
