using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Civ.Content;
using Civ.Content.Schema;

namespace Civ.ContentImport;

public sealed record DroppedContent(string Id, string Reason);
public sealed record EmissionResult(IReadOnlyList<MappedDefinition> Definitions, IReadOnlyList<DroppedContent> Dropped,
    IReadOnlyList<DataIssue> Issues, IReadOnlyDictionary<string, HashSet<string>> MappedPaths);

/// <summary>Deterministic per-mod output, reference closure, era filtering and diagnostics.</summary>
public static class ContentEmission
{
    public static EmissionResult Compile(ImportRegistry registry, string? maximumEra = null)
    {
        var mapper = new ContentMapper(registry.Records, registry.Text.Lookup, registry.Defines);
        var definitions = mapper.Map().ToList();
        definitions.Add(new(new("", "RuleConstantsDef", "foundation", [], []), Constants(registry)));
        definitions.AddRange(mapper.Conditions.Select(name => new MappedDefinition(new("", "ConditionDef", "foundation", [], []),
            new ConditionDef { Id = "foundation:" + name, Name = name.Replace('_', ' ') })));
        Validate(definitions);
        var eraOrder = registry.EraOrder.Select(era => registry.Ids.Single(i => i.Class == "EraInfo" && i.SourceType == era).Id).ToArray();
        var (kept, dropped) = Filter(definitions, maximumEra, eraOrder);
        Validate(kept);
        return new(kept, dropped, mapper.Issues.Distinct().OrderBy(i => i.Record, StringComparer.Ordinal).ThenBy(i => i.Field, StringComparer.Ordinal).ToArray(), mapper.MappedPaths);
    }

    public static void Write(EmissionResult result, string destination, string reports)
    {
        // Prepare every payload and scan decoded strings before touching any mod sidecar.
        var families = SchemaCatalog.Definitions.Select(m => (m.Mod, m.Definition)).Concat(new[] { (Mod: "foundation", Definition: "ConditionDef"), (Mod: "foundation", Definition: "RuleConstantsDef") });
        var files = families.OrderBy(f => f.Mod, StringComparer.Ordinal).ThenBy(f => f.Definition, StringComparer.Ordinal)
            .Select(f => (f.Mod, File: FileName(f.Definition), Json: Serialize(result.Definitions
                .Where(d => d.Mapping.Mod == f.Mod && d.Mapping.Definition == f.Definition)
                .OrderBy(d => d.Definition.Id, StringComparer.Ordinal).Select(d => d.Definition).ToArray()))).ToArray();
        foreach (var file in files) AssertNeutral(file.Json);
        foreach (var file in files)
        {
            var directory = Path.Combine(destination, DisplayMod(file.Mod), "data");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, file.File), file.Json, new UTF8Encoding(false));
        }
        WriteReports(result, reports);
    }

    private static RuleConstantsDef Constants(ImportRegistry registry)
    {
        var values = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var raw = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, define) in registry.Defines)
        {
            if (!SchemaCatalog.Constants.TryGetValue(key, out var property)) { raw[Branding.Identifier(key)] = Branding.Output(define.Value); continue; }
            var number = decimal.Parse(define.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (property.EndsWith("Permille", StringComparison.Ordinal)) number *= 1000;
            if (number != decimal.Truncate(number)) throw new InvalidDataException("Constant cannot be represented exactly as an integer: " + key);
            values[JsonNamingPolicy.CamelCase.ConvertName(property)] = checked((int)number);
        }
        var typed = JsonSerializer.Deserialize<RuleConstants>(JsonSerializer.Serialize(values, ContentMapper.Json), ContentMapper.Json)!;
        return new() { Id = "foundation:rule_constants", Name = "Rule constants", Values = typed, Raw = raw.ToImmutable() };
    }

    private static (List<MappedDefinition> Kept, List<DroppedContent> Dropped) Filter(List<MappedDefinition> definitions, string? maximumEra, string[] eraOrder)
    {
        if (maximumEra is null) return (definitions, []);
        // Use the effective source's enum order, not alphabetically sorted definition ids.
        var ranks = eraOrder.Select((id, rank) => (id, rank)).ToDictionary(e => e.id, e => e.rank, StringComparer.Ordinal);
        if (!ranks.TryGetValue(maximumEra, out var maximum)) throw new ArgumentException("Unknown maximum era id: " + maximumEra);
        var removed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
            if (definition.Definition.Era is { } era && ranks.TryGetValue(era, out var rank) && rank > maximum)
                removed[definition.Definition.Id] = "Era " + era + " exceeds " + maximumEra;
        var references = definitions.ToDictionary(d => d.Definition.Id, d => References(d.Definition).Select(r => r.Id).ToArray(), StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var definition in definitions.OrderBy(d => d.Definition.Id, StringComparer.Ordinal))
            {
                if (removed.ContainsKey(definition.Definition.Id)) continue;
                var missing = references[definition.Definition.Id].Where(removed.ContainsKey).Order(StringComparer.Ordinal).FirstOrDefault();
                if (missing is null) continue;
                removed[definition.Definition.Id] = "References dropped content " + missing;
                changed = true;
            }
        } while (changed);
        return (definitions.Where(d => !removed.ContainsKey(d.Definition.Id)).ToList(), removed.Select(d => new DroppedContent(d.Key, d.Value)).ToList());
    }

    public static IReadOnlyList<ContentRef> References(GameContentDef definition) => ContentReferences.Enumerate(definition,
        ContentLeafKinds.Types.Select(k => new IdentityEvaluator(k.Key, k.Value))).ToArray();

    private static void Validate(IReadOnlyList<MappedDefinition> definitions)
    {
        var targets = definitions.ToDictionary(d => d.Definition.Id, d => d.Definition.GetType(), StringComparer.Ordinal);
        var errors = definitions.SelectMany(d => References(d.Definition)).Where(r => !targets.TryGetValue(r.Id, out var type) || !r.DefinitionType.IsAssignableFrom(type))
            .Select(r => r.Path + ": " + r.Id + " is not a registered " + r.DefinitionType.Name).Order(StringComparer.Ordinal).ToArray();
        if (errors.Length > 0) throw new InvalidDataException("Imported reference closure failed:\n" + string.Join("\n", errors));
    }

    private sealed class IdentityEvaluator(string kind, Type type) : IContentRequirementEvaluator
    {
        public string Kind => kind;
        public Type DefinitionType => type;
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context) => throw new NotSupportedException("Importer only resolves identities");
    }

    private static void WriteReports(EmissionResult result, string directory)
    {
        Directory.CreateDirectory(directory);
        var classes = result.Definitions.GroupBy(d => (d.Mapping.Mod, d.Mapping.Definition)).OrderBy(g => g.Key.Mod, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Definition, StringComparer.Ordinal).Select(g => new { mod = g.Key.Mod, definition = g.Key.Definition, count = g.Count() }).ToArray();
        var mods = result.Definitions.GroupBy(d => d.Mapping.Mod).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new { id = g.Key, count = g.Count(), references = g.SelectMany(d => References(d.Definition)).Select(r => r.Id.Split(':')[0])
                .Where(id => id != g.Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() }).ToArray();
        Reports.WriteJson(directory, "emitted-counts", new { total = result.Definitions.Count, mods, classes });
        Reports.WriteJson(directory, "dropped-content", result.Dropped);
        Reports.WriteJson(directory, "data-issues", result.Issues);
        var text = new StringBuilder("# Emitted content\n\nThe counts describe typed v1 content, not the generic archive inventory.\n\n");
        foreach (var mod in mods) text.AppendLine($"- {mod.id}: {mod.count}. Content providers: {string.Join(", ", mod.references)}.");
        text.AppendLine("\n## Definition counts\n");
        foreach (var item in classes) text.AppendLine($"- {item.mod}/{item.definition}: {item.count}.");
        File.WriteAllText(Path.Combine(directory, "emitted-counts.md"), text.ToString());
        WriteUnsupportedHighlights(result, directory);
    }

    private static void WriteUnsupportedHighlights(EmissionResult result, string directory)
    {
        var text = new StringBuilder("# Unsupported field highlights\n\nCounts are non-default uses per emitted record. Each class lists its three most-used direct unsupported fields.\n\n");
        foreach (var family in result.Definitions.Where(d => d.Mapping.SourceClass.Length > 0).GroupBy(d => d.Mapping.SourceClass).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var fields = family.SelectMany(d => d.Definition.Unsupported.Where(p => !p.Contains('/', StringComparison.Ordinal)))
                .GroupBy(p => p, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(3)
                .Select(g => $"`{g.Key}` ({g.Count()})").ToArray();
            text.AppendLine($"- {family.Key}: {(fields.Length == 0 ? "none" : string.Join(", ", fields))}.");
        }
        File.WriteAllText(Path.Combine(directory, "unsupported-highlights.md"), text.ToString());
    }

    private static string Serialize(GameContentDef[] definitions)
    {
        var array = new JsonArray(definitions.Select(d => JsonSerializer.SerializeToNode(d, d.GetType(), ContentMapper.Json)).ToArray());
        // Immutable hash maps do not promise enumeration order. Sort every object, preserve all sequence order.
        return SortObject(array).ToJsonString(ContentMapper.Json) + "\n";
    }
    private static JsonNode SortObject(JsonNode node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : SortObject(p.Value)))),
        JsonArray array => new JsonArray(array.Select(n => n is null ? null : SortObject(n)).ToArray()),
        _ => node.DeepClone()
    };
    private static void AssertNeutral(string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var value in Strings(document.RootElement))
            if (Branding.Scan("generated", value).Any()) throw new InvalidDataException("Forbidden branding remains in typed output");
    }
    private static IEnumerable<string> Strings(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => [value.GetString()!],
        JsonValueKind.Array => value.EnumerateArray().SelectMany(Strings),
        JsonValueKind.Object => value.EnumerateObject().SelectMany(p => new[] { p.Name }.Concat(Strings(p.Value))),
        _ => []
    };
    public static string FileName(string definition) => string.Concat(definition[..^3].Select((c, i) => char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())) + ".json";
    public static string DisplayMod(string mod) => char.ToUpperInvariant(mod[0]) + mod[1..];
}
