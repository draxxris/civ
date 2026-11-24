using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.ContentImport;

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
        definitions.AddRange(Constants(registry.Defines));
        definitions.AddRange(mapper.Conditions.Select(name => new MappedDefinition(new("", "ConditionDef", "foundation", [], []),
            new ConditionDef { Id = "foundation:" + name, Name = name.Replace('_', ' ') })));
        Validate(definitions, mapper.Claimant);
        var eraOrder = registry.EraOrder.Select(era => registry.Ids.Single(i => i.Class == "EraInfo" && i.SourceType == era).Id).ToArray();
        var (kept, dropped) = Filter(definitions, maximumEra, eraOrder);
        Validate(kept, mapper.Claimant);
        return new(kept, dropped, mapper.Issues.Distinct().OrderBy(i => i.Record, StringComparer.Ordinal).ThenBy(i => i.Field, StringComparer.Ordinal).ToArray(), mapper.MappedPaths);
    }

    /// <summary>Every (mod, definition) family a run writes, one file each, empty after an era filter included: the
    /// catalog's, Foundation's conditions and rule constants, each mod's row about those, and the families a mod that owns
    /// records of a base class outright writes for them (that class's own, and any row another mod keeps about them).</summary>
    public static IReadOnlyList<(string Mod, string Definition, string Folder)> Families(EmissionResult result) =>
        SchemaCatalog.Definitions.Select(m => (m.Mod, m.Definition, m.Folder))
            .Concat([("foundation", "ConditionDef", ""), ("foundation", "RuleConstantsDef", "")])
            .Concat(SchemaCatalog.Constants.Attachments.Select(a => (a.Mod, a.Definition, "")))
            .Concat(IdMapping.RecordOwners.SelectMany(o => SchemaCatalog.Definitions.Where(m => m.SourceClass == o.Class && !m.IsAttachment)
                .Select(m => (o.Mod, m.Definition, ""))))
            // A mod that registers rows about an optional provider's records only with that provider keeps them in its folder.
            .Concat(IdMapping.RecordOwners.SelectMany(o => SchemaCatalog.Definitions.Where(m => m.SourceClass == o.Class && m.IsAttachment
                    && IdMapping.Emitter(o.Mod, m.Mod) != o.Mod && IdMapping.Subfolder(m.Mod, o.Mod) is not null)
                .Select(m => (m.Mod, m.Definition, IdMapping.Subfolder(m.Mod, o.Mod)!))))
            .Concat(result.Definitions.Select(d => (d.Mod, d.Mapping.Definition, d.Folder)))
            .Distinct().OrderBy(f => f.Item1, StringComparer.Ordinal).ThenBy(f => f.Item3, StringComparer.Ordinal)
            .ThenBy(f => f.Item2, StringComparer.Ordinal).ToArray();

    public static void Write(EmissionResult result, ContentRoots destination, string reports)
    {
        // Prepare every payload and scan decoded strings before touching any mod sidecar.
        var files = Families(result)
            .Select(f => (f.Mod, File: FileName(f.Definition), f.Folder, Json: Serialize(result.Definitions
                .Where(d => d.Mod == f.Mod && d.Mapping.Definition == f.Definition && d.Folder == f.Folder)
                .OrderBy(d => d.Definition.Id, StringComparer.Ordinal).Select(d => d.Definition).ToArray()))).ToArray();
        // Hand-authored geometry tuning is never part of the generated family list.
        if (files.Any(f => f.File == "hex-overrides.json")) throw new InvalidOperationException("Reserved hand-authored sidecar");
        foreach (var file in files) AssertNeutral(file.Json);
        foreach (var file in files)
        {
            var directory = Path.Combine(destination.Module(file.Mod), "data", file.Folder);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, file.File), file.Json, new UTF8Encoding(false));
        }
        WriteReports(result, reports);
    }

    /// <summary>The base game's rule constants, and the row each mod in <see cref="ConstantMapping.Attachments"/> keeps
    /// about them, emitted only when one of its constants is not at its default, like any attachment.</summary>
    public static IReadOnlyList<MappedDefinition> Constants(IReadOnlyDictionary<string, DefineValue> defines)
    {
        const string id = "foundation:rule_constants";
        var mapping = SchemaCatalog.Constants;
        var values = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var attached = mapping.Attachments.ToDictionary(a => a, _ => new SortedDictionary<string, object>(StringComparer.Ordinal));
        var raw = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, define) in defines.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            // A mod's own, unread constant is not the base game's to carry (the Espionage mod keeps none of its 25).
            if (IdMapping.ConstantOwner(key) is not null) continue;
            if (mapping.Values.TryGetValue(key, out var property)) values[JsonNamingPolicy.CamelCase.ConvertName(property)] = Integer(key, property, define);
            else if (mapping.Attachments.FirstOrDefault(a => a.Constants.ContainsKey(key)) is { } owner)
                attached[owner][JsonNamingPolicy.CamelCase.ConvertName(owner.Constants[key])] = Integer(key, owner.Constants[key], define);
            else raw[Branding.Identifier(key)] = Branding.Output(define.Value);
        }
        var typed = JsonSerializer.Deserialize<RuleConstants>(JsonSerializer.Serialize(values, ContentMapper.Json), ContentMapper.Json)!;
        var result = new List<MappedDefinition>
        {
            new(new("", "RuleConstantsDef", "foundation", [], []),
                new RuleConstantsDef { Id = id, Name = "Rule constants", Values = typed, Raw = raw.ToImmutable() }),
        };
        foreach (var (attachment, constants) in attached)
        {
            var identity = new SortedDictionary<string, object>(StringComparer.Ordinal) { ["id"] = attachment.Mod + id[id.IndexOf(':')..], ["target"] = id };
            var row = JsonSerializer.Deserialize(JsonSerializer.Serialize(identity.Concat(constants).ToDictionary(), ContentMapper.Json), attachment.DefinitionType, ContentMapper.Json)!;
            var empty = JsonSerializer.Deserialize(JsonSerializer.Serialize(identity, ContentMapper.Json), attachment.DefinitionType, ContentMapper.Json)!;
            if (JsonSerializer.Serialize(row, attachment.DefinitionType, ContentMapper.Json) != JsonSerializer.Serialize(empty, attachment.DefinitionType, ContentMapper.Json))
                result.Add(new(new("", attachment.Definition, attachment.Mod, [], []), (ContentDef)row));
        }
        return result;
    }

    private static int Integer(string key, string property, DefineValue define)
    {
        var number = decimal.Parse(define.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (property.EndsWith("Permille", StringComparison.Ordinal)) number *= 1000;
        if (number != decimal.Truncate(number)) throw new InvalidDataException("Constant cannot be represented exactly as an integer: " + key);
        return checked((int)number);
    }

    private static (List<MappedDefinition> Kept, List<DroppedContent> Dropped) Filter(List<MappedDefinition> definitions, string? maximumEra, string[] eraOrder)
    {
        if (maximumEra is null or "all") return (definitions, []);
        if (!maximumEra.Contains(':', StringComparison.Ordinal)) maximumEra = "foundation:era_" + maximumEra;
        // Use the effective source's enum order, not alphabetically sorted definition ids.
        var ranks = eraOrder.Select((id, rank) => (id, rank)).ToDictionary(e => e.id, e => e.rank, StringComparer.Ordinal);
        if (!ranks.TryGetValue(maximumEra, out var maximum)) throw new ArgumentException("Unknown maximum era id: " + maximumEra);
        var removed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
            if ((definition.Definition as GameContentDef)?.Era is { } era && ranks.TryGetValue(era, out var rank) && rank > maximum)
                removed[definition.Definition.Id] = "Era " + era + " exceeds " + maximumEra;
        // By definition, not id: a mod's attachment about a record it owns outright shares the record's id.
        var references = definitions.ToDictionary<MappedDefinition, MappedDefinition, string[]>(d => d,
            d => References(d.Definition).Select(r => r.Id).ToArray(), ReferenceEqualityComparer.Instance);
        bool changed;
        do
        {
            changed = false;
            foreach (var definition in definitions.OrderBy(d => d.Definition.Id, StringComparer.Ordinal))
            {
                if (removed.ContainsKey(definition.Definition.Id)) continue;
                var missing = references[definition].Where(removed.ContainsKey).Order(StringComparer.Ordinal).FirstOrDefault();
                if (missing is null) continue;
                removed[definition.Definition.Id] = "References dropped content " + missing;
                changed = true;
            }
        } while (changed);
        return (definitions.Where(d => !removed.ContainsKey(d.Definition.Id)).ToList(), removed.Select(d => new DroppedContent(d.Key, d.Value)).ToList());
    }

    public static IReadOnlyList<ContentRef> References(ContentDef definition) => ContentReferences.Enumerate(definition,
        SchemaCatalog.LeafKinds.Select(k => new IdentityEvaluator(k.Key, k.Value))).ToArray();

    /// <summary>Every reference resolves to an emitted definition of its type, and a record a mod owns outright is named
    /// only by that mod's own rows: no other mod could load without it.</summary>
    public static void Validate(IReadOnlyList<MappedDefinition> definitions, Func<string, string?> claimant)
    {
        // Attachments are never reference targets, and one a mod keeps about its own record shares that record's id. Only
        // another attachment may name one: Espionage's amendment of the Religion mod's row about a building.
        var targets = definitions.Where(d => d.Definition is not ContentAttachment)
            .ToDictionary(d => d.Definition.Id, d => d.Definition.GetType(), StringComparer.Ordinal);
        var attached = definitions.Where(d => d.Definition is ContentAttachment).Select(d => (d.Definition.Id, Type: d.Definition.GetType())).ToHashSet();
        bool Registered(ContentRef r) => targets.TryGetValue(r.Id, out var type) && r.DefinitionType.IsAssignableFrom(type)
            || typeof(ContentAttachment).IsAssignableFrom(r.DefinitionType) && attached.Any(a => a.Id == r.Id && r.DefinitionType.IsAssignableFrom(a.Type));
        var errors = definitions.SelectMany(d => References(d.Definition)).Where(r => !Registered(r))
            .Select(r => r.Path + ": " + r.Id + " is not a registered " + r.DefinitionType.Name)
            .Concat(definitions.SelectMany(d => References(d.Definition).Where(r => claimant(r.Id) is { } owner && !IdMapping.MayName(d.Mod, owner))
                .Select(r => $"{r.Path}: {d.Mod} names {r.Id}, which {claimant(r.Id)} owns")))
            .Order(StringComparer.Ordinal).ToArray();
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
        var classes = result.Definitions.GroupBy(d => (d.Mod, d.Mapping.Definition)).OrderBy(g => g.Key.Mod, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Definition, StringComparer.Ordinal).Select(g => new { mod = g.Key.Mod, definition = g.Key.Definition, count = g.Count() }).ToArray();
        var mods = result.Definitions.GroupBy(d => d.Mod).OrderBy(g => g.Key, StringComparer.Ordinal)
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
            var fields = family.Select(d => d.Definition).OfType<GameContentDef>().SelectMany(d => d.Unsupported.Where(p => !p.Contains('/', StringComparison.Ordinal)))
                .GroupBy(p => p, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(3)
                .Select(g => $"`{g.Key}` ({g.Count()})").ToArray();
            text.AppendLine($"- {family.Key}: {(fields.Length == 0 ? "none" : string.Join(", ", fields))}.");
        }
        File.WriteAllText(Path.Combine(directory, "unsupported-highlights.md"), text.ToString());
    }

    private static string Serialize(ContentDef[] definitions)
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
    /// <summary>The folder of a module or mod: its id in Pascal case (<c>projects_victory</c> is <c>ProjectsVictory</c>).</summary>
    public static string DisplayMod(string mod) =>
        string.Concat(mod.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
