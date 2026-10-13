using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Civ.ContentImport;

public sealed record CensusField(string Field, int Present, int NonDefault, IReadOnlyList<string> Examples, string Status = "preserved");
public sealed record CensusClass(string Class, int Records, IReadOnlyList<CensusField> Fields,
    IReadOnlyDictionary<string, int> EraCounts, IReadOnlyList<object> RecordEras);

public static class FieldCensus
{
    public static IReadOnlyList<CensusClass> Build(IReadOnlyList<EffectiveRecord> records) => records
        .GroupBy(x => IdMapping.CanonicalClass(x.Class)).OrderBy(x => x.Key, StringComparer.Ordinal)
        .Select(BuildClass).ToArray();

    private static CensusClass BuildClass(IGrouping<string, EffectiveRecord> group)
    {
        var fields = new SortedDictionary<string, List<DataNode[]>>(StringComparer.Ordinal);
        foreach (var record in group)
            foreach (var field in Flatten(record.Node).GroupBy(x => x.Path, StringComparer.Ordinal))
            {
                if (!fields.TryGetValue(field.Key, out var values)) fields[field.Key] = values = [];
                values.Add(field.Select(x => x.Node).ToArray());
            }
        var rows = fields.Select(x => new CensusField(x.Key, x.Value.Count,
            x.Value.Count(nodes => nodes.Any(RecordMerger.IsNonDefault)), x.Value.SelectMany(nodes => nodes)
                .Where(RecordMerger.IsNonDefault).Select(Example).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).Take(3).ToArray())).ToArray();
        var eras = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var era in group.GroupBy(x => x.Era ?? "unknown")) eras[era.Key] = era.Count();
        return new(group.Key, group.Count(), rows, eras, group.Select(x => (object)new { type = x.Type, era = x.Era }).ToArray());
    }

    public static IEnumerable<(string Path, DataNode Node)> Flatten(DataNode node, string prefix = "")
    {
        foreach (var child in node.Children)
        {
            var path = prefix.Length == 0 ? child.Name : prefix + "/" + child.Name;
            yield return (path, child);
            foreach (var descendant in Flatten(child, path)) yield return descendant;
        }
    }

    private static string Example(DataNode node)
    {
        var value = node.Text ?? JsonSerializer.Serialize(node.ToTree(), Reports.JsonOptions);
        return value.Length <= 160 ? value : value[..157] + "...";
    }
}

public static class Reports
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new NeutralStringConverter() }
    };

    public static void Write(ImportRegistry registry, string source, string reports, string? dump)
    {
        Directory.CreateDirectory(reports);
        var census = FieldCensus.Build(registry.Records);
        WriteJson(reports, "collisions-and-merges", new { version = RecordMerger.Version,
            collisions = registry.Collisions, merges = registry.Merges, ignored = registry.IgnoredContributions,
            defines = registry.DefineOverrides, warnings = registry.Warnings, recovery = registry.Recovery });
        WriteMarkdown(reports, "collisions-and-merges", MergeMarkdown(registry));
        WriteJson(reports, "census", census);
        WriteMarkdown(reports, "census", CensusMarkdown(census));
        WriteJson(reports, "unresolved-text-keys", registry.UnresolvedText);
        WriteMarkdown(reports, "unresolved-text-keys", UnresolvedMarkdown(registry));
        WriteJson(reports, "branding", registry.BrandingHits);
        WriteMarkdown(reports, "branding", BrandingMarkdown(registry));
        WriteJson(reports, "id-map", new { classes = IdMapping.ClassMods, records = registry.Ids });
        WriteMarkdown(reports, "id-map", IdMarkdown(registry));
        WriteJson(reports, "global-defines", registry.Defines);
        WriteJson(reports, "profile-files", new { moduleDirectories = registry.Profile.ModuleDirectories,
            files = registry.Profile.Files.Select(x => new { x.Order, x.IsModule,
                path = Path.GetRelativePath(Path.Combine(source, "data"), x.ExportPath).Replace('\\', '/') }),
            defineFiles = registry.DefineFiles });
        WriteCounts(registry, source, reports);
        if (dump is not null) Dump(registry, dump);
    }

    public static void WriteJson(string directory, string name, object value) => File.WriteAllText(
        Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(value, JsonOptions) + "\n", new UTF8Encoding(false));
    private static void WriteMarkdown(string directory, string name, string value) => File.WriteAllText(
        Path.Combine(directory, name + ".md"), Branding.Output(value), new UTF8Encoding(false));

    private static string MergeMarkdown(ImportRegistry registry)
    {
        var text = new StringBuilder("# Collisions and merges\n\n");
        text.AppendLine($"- Collision keys: {registry.Collisions.Count}");
        text.AppendLine($"- Extra definitions: {registry.Collisions.Sum(x => x.Definitions - 1)}");
        text.AppendLine($"- Merge operations: {registry.Merges.Count}");
        text.AppendLine($"- Merge policy: {RecordMerger.Version}\n");
        foreach (var warning in registry.Warnings) text.AppendLine("- " + warning);
        text.AppendLine("\n## Recovery\n");
        text.AppendLine($"{registry.Recovery.Records} civic records. {registry.Recovery.RepairedComments} comments repaired. Full-tree equality and original hash passed.\n");
        foreach (var collision in registry.Collisions)
        {
            text.AppendLine($"## {IdMapping.CanonicalClass(collision.Class)} / {collision.Type}\n");
            foreach (var path in collision.Sources) text.AppendLine("- " + path);
            foreach (var merge in registry.Merges.Where(x => x.Class == collision.Class && x.Type == collision.Type))
                text.AppendLine($"- `{merge.Rule}`: {string.Join(", ", merge.ChangedFields)}");
            text.AppendLine();
        }
        return text.ToString();
    }

    private static string CensusMarkdown(IReadOnlyList<CensusClass> census)
    {
        var text = new StringBuilder("# Field census\n\nCounts are per record. Repeated paths count once per record. Examples are limited to three, at 160 characters.\n\n"
            + "Non-default means non-empty and not 0, NONE, -1, or false. A branch is non-default when any descendant is non-default.\n\n"
            + "These are census sentinels, not DLL getter defaults. Status is preserved, not parsed or implemented.\n\n");
        foreach (var family in census)
        {
            text.AppendLine($"## {family.Class}: {family.Records} records\n");
            text.AppendLine("Era counts: " + string.Join(", ", family.EraCounts.Select(x => x.Key + "=" + x.Value)) + "\n");
            foreach (var field in family.Fields)
            {
                text.AppendLine($"- `{field.Field}`");
                text.AppendLine($"  - Present: {field.Present}. Non-default: {field.NonDefault}. Status: {field.Status}.");
                foreach (var example in field.Examples)
                    text.AppendLine("  - Example: " + example.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal));
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    private static string UnresolvedMarkdown(ImportRegistry registry)
    {
        var text = new StringBuilder($"# Unresolved text keys\n\nDistinct keys: {registry.UnresolvedText.Count}. Uses: {registry.UnresolvedText.Sum(x => x.Uses.Count)}.\n\n");
        foreach (var key in registry.UnresolvedText)
        {
            text.AppendLine($"## {key.Key}\n");
            foreach (var use in key.Uses) text.AppendLine("- " + use);
            text.AppendLine();
        }
        return text.ToString();
    }

    private static string BrandingMarkdown(ImportRegistry registry)
    {
        var text = new StringBuilder($"# Branding substitutions\n\nHit rows: {registry.BrandingHits.Count}. Occurrences: {registry.BrandingHits.Sum(x => x.Occurrences)}.\n\n"
            + "Excerpts show neutral replacements. Original text remains only in the local source archive.\n\n");
        foreach (var hit in registry.BrandingHits)
        {
            text.AppendLine($"- `{hit.Context}`: {hit.Rule}, {hit.Occurrences} occurrences.");
            text.AppendLine("  - " + hit.NeutralExcerpt.Replace("\n", "\\n", StringComparison.Ordinal));
        }
        return text.ToString();
    }

    private static string IdMarkdown(ImportRegistry registry)
    {
        var text = new StringBuilder($"# ID map\n\n{registry.Ids.Count} unique game IDs. Full record mapping is in id-map.json.\n\n"
            + "Primary classes remove their documented type prefix. Secondary classes retain their complete symbol.\n\n"
            + "Cross-class name conflicts add the class stem and complete source symbol. Neutral substitutions precede lowercase conversion.\n\n"
            + "## Class ownership\n\n| Class | Mod | Records |\n|---|---|---:|\n");
        foreach (var item in IdMapping.ClassMods)
            text.AppendLine($"| {item.Key} | {item.Value} | {registry.Ids.Count(x => x.Class == item.Key)} |");
        text.AppendLine("\nLater mods with no dedicated Info class: great-people and trade. Their eventual rules consume units, specialists, routes, and constants.\n");
        return text.ToString();
    }

    private static void WriteCounts(ImportRegistry registry, string source, string directory)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "data/validation-report.json")));
        var baseline = report.RootElement.GetProperty("sparse").EnumerateArray()
            .Where(x => x.GetProperty("path").GetString()!.StartsWith("XML/", StringComparison.Ordinal)
                || x.GetProperty("path").GetString()!.StartsWith("RECOVERED_NONAUTHORITATIVE/XML/", StringComparison.Ordinal))
            .Where(x => !Path.GetFileName(x.GetProperty("path").GetString()!).StartsWith("README", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => IdMapping.CanonicalClass(x.GetProperty("kind").GetString()!))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.GetProperty("records").GetInt32()), StringComparer.Ordinal);
        var counts = registry.Records.GroupBy(x => IdMapping.CanonicalClass(x.Class)).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new { @class = x.Key, effective = x.Count(), mainTable = baseline.TryGetValue(x.Key, out var n) ? (int?)n : null,
                delta = baseline.ContainsKey(x.Key) ? (int?)(x.Count() - baseline[x.Key]) : null }).ToArray();
        WriteJson(directory, "counts", new { total = registry.Records.Count, classes = counts.Length,
            localizationKeys = registry.Text.Entries.Count, duplicateLocalizationKeys = registry.Text.DuplicateKeys,
            globalDefines = registry.Defines.Count, counts });
        var text = new StringBuilder($"# Counts\n\nTotal: {registry.Records.Count}. Classes: {counts.Length}. English keys: {registry.Text.Entries.Count}. Defines: {registry.Defines.Count}.\n\n"
            + "Main counts come from the validation report's core sparse inventory, not an effective profile. Civic recovery remains non-authoritative.\n\n"
            + "| Class | Effective | Main | Delta |\n|---|---:|---:|---:|\n");
        foreach (var count in counts) text.AppendLine($"| {count.@class} | {count.effective} | {count.mainTable?.ToString() ?? "n/a"} | {count.delta?.ToString() ?? "n/a"} |");
        WriteMarkdown(directory, "counts", text.ToString());
    }

    private static void Dump(ImportRegistry registry, string directory)
    {
        Directory.CreateDirectory(directory);
        var ids = registry.Ids.ToDictionary(x => (x.Class, x.SourceType), x => x.Id);
        foreach (var group in registry.Records.GroupBy(x => IdMapping.CanonicalClass(x.Class)).OrderBy(x => x.Key, StringComparer.Ordinal))
            WriteJson(directory, group.Key, group.Select(x => new
            {
                id = ids[(group.Key, x.Type)], @class = group.Key, sourceType = x.Type, x.Era, x.Sources,
                tree = x.Node.ToTree(), text = x.Node.DescendantsAndSelf().Where(n => n.Text is not null)
                    .SelectMany(n => EnglishText.KeysIn(n.Text!)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                    .ToDictionary(k => k, k => registry.Text.Lookup(k), StringComparer.Ordinal)
            }).ToArray());
        WriteJson(directory, "global-defines", registry.Defines);
        WriteJson(directory, "english", registry.Text.Entries.Values.Select(x => new { x.Key, x.Text, english = x.English.ToTree(), x.Source }).ToArray());
    }

    private sealed class NeutralStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetString();
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(Branding.Output(value));
        public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WritePropertyName(Branding.Identifier(value));
    }
}
