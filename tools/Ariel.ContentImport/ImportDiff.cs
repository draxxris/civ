using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ariel.ContentImport;

/// <summary>Compares a staged import with the committed data, record by record, so changes can be brought in piece-meal.
/// Read-only: it never writes. A record pairs with its committed counterpart by the family's file name and its <c>id</c>,
/// wherever the committed file lives: a mod keeps the rows it registers only with an optional provider in a subfolder of
/// its <c>data</c> (<c>data/corporations/building-espionage.json</c>), and a row the importer now writes to another mod's
/// folder pairs with the committed row there and is reported as moved. Only families the staging run produced are
/// compared; hand-authored files have no staged counterpart and are ignored.</summary>
public static class ImportDiff
{
    public static int Run(string[] arguments)
    {
        var values = arguments.Contains("--values");
        var filters = arguments.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var layout = StagingLayout.Locate();
        if (!Directory.Exists(layout.Root))
        {
            Console.Error.WriteLine($"No staged import at {StagingLayout.DirectoryName}/. Run `mise run import:stage -- --source DIR` first.");
            return 1;
        }
        var total = Compare(Console.Out, new(layout.Rules, layout.Mods), layout.Committed, filters, values);
        if (total is null)
        {
            Console.Error.WriteLine("No staged module matches: " + string.Join(", ", filters));
            return 1;
        }
        Console.WriteLine($"\nTotal: {total.Changed} changed, {total.Moved} moved, {total.New} new in staging, {total.Gone} gone from staging, {total.Same} identical.");
        return 0;
    }

    /// <summary>Compares the modules of the staged roots (those named in <paramref name="filters"/>, or all) with the committed
    /// roots, writing what differs. Null when no staged module matches the filters.</summary>
    public static Totals? Compare(TextWriter output, ContentRoots staged, ContentRoots committed, string[] filters, bool values)
    {
        var modules = new[] { (staged.Rules, committed.Rules), (staged.Mods, committed.Mods) }
            .Where(r => Directory.Exists(r.Item1))
            .SelectMany(r => Directory.GetDirectories(r.Item1).Order(StringComparer.Ordinal).Select(d => (Staged: d, Name: Path.GetFileName(d), Committed: r.Item2)))
            .Where(m => filters.Length == 0 || filters.Contains(m.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (modules.Length == 0) return null;
        // Every committed row of a family a staged module has, by family file and id, wherever it lives.
        var families = modules.SelectMany(m => StagedFiles(m.Staged)).Select(f => Path.GetFileName(f)).ToHashSet(StringComparer.Ordinal);
        var index = new Dictionary<(string File, string Id), List<Row>>();
        foreach (var committedRoot in modules.Select(m => m.Committed).Distinct())
            foreach (var module in Directory.Exists(committedRoot) ? Directory.GetDirectories(committedRoot).Order(StringComparer.Ordinal).ToArray() : Array.Empty<string>())
                foreach (var file in CommittedFiles(module).Where(f => families.Contains(Path.GetFileName(f))))
                    foreach (var (id, record) in Records(file))
                    {
                        var key = (Path.GetFileName(file), id);
                        if (!index.TryGetValue(key, out var list)) index[key] = list = [];
                        list.Add(new(Path.GetRelativePath(committedRoot, file).Replace('\\', '/'), record));
                    }
        var consumed = new HashSet<Row>(ReferenceEqualityComparer.Instance);
        var total = new Totals();
        foreach (var module in modules)
            foreach (var file in StagedFiles(module.Staged))
            {
                var relative = Path.GetRelativePath(Path.GetDirectoryName(module.Staged)!, file).Replace('\\', '/');
                total.Add(CompareFile(output, relative, Path.GetFileName(file), Records(file), index, consumed, values));
            }
        // Committed rows of the staged families that no staged row paired with: the importer no longer writes them.
        var selected = modules.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var gone = index.Values.SelectMany(l => l).Where(r => !consumed.Contains(r) && selected.Contains(r.Path[..r.Path.IndexOf('/')])).GroupBy(r => r.Path).OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();
        foreach (var group in gone)
        {
            output.WriteLine($"{group.Key}: {group.Count()} gone from staging");
            foreach (var row in group.OrderBy(r => r.Id, StringComparer.Ordinal)) output.WriteLine("  - " + row.Id);
        }
        total.Gone = gone.Sum(g => g.Count());
        return total;
    }

    private static IEnumerable<string> StagedFiles(string module) =>
        Directory.Exists(Path.Combine(module, "data")) ? Directory.GetFiles(Path.Combine(module, "data"), "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray() : Array.Empty<string>();
    private static IEnumerable<string> CommittedFiles(string module) => StagedFiles(module);

    private static Totals CompareFile(TextWriter output, string path, string family, SortedDictionary<string, JsonObject> staged,
        Dictionary<(string File, string Id), List<Row>> index, HashSet<Row> consumed, bool values)
    {
        var changed = new List<string>();
        var moved = new List<string>();
        var added = new List<string>();
        var same = 0;
        foreach (var (id, record) in staged)
        {
            var candidates = index.GetValueOrDefault((family, id)) ?? [];
            // The committed row at the same path, else any other; a row is paired with one staged row only.
            var old = candidates.FirstOrDefault(c => !consumed.Contains(c) && SamePath(c.Path, path)) ?? candidates.FirstOrDefault(c => !consumed.Contains(c));
            if (old is null) { added.Add(id); continue; }
            consumed.Add(old);
            var relocated = !SamePath(old.Path, path);
            // Compare field by field: the order of an object's properties is not a difference.
            var fields = old.Record.Select(p => p.Key).Union(record.Select(p => p.Key)).Order(StringComparer.Ordinal)
                .Where(f => !JsonNode.DeepEquals(old.Record[f], record[f])).ToArray();
            if (relocated) moved.Add($"  > {id}: committed in {old.Path}" + (fields.Length == 0 ? "" : $", differs in {string.Join(", ", fields)}"));
            if (fields.Length == 0) { if (!relocated) same++; continue; }
            if (!relocated)
                changed.Add(values
                    ? $"  ~ {id}\n" + string.Join("\n", fields.Select(f => $"      {f}: {Show(old.Record[f])} -> {Show(record[f])}"))
                    : $"  ~ {id}: {string.Join(", ", fields)}");
        }
        if (changed.Count + moved.Count + added.Count > 0)
        {
            output.WriteLine($"{path}: {changed.Count} changed, {moved.Count} moved, {added.Count} new, {same} identical");
            foreach (var line in changed.Concat(moved)) output.WriteLine(line);
            foreach (var id in added) output.WriteLine("  + " + id);
        }
        return new() { Changed = changed.Count, Moved = moved.Count, New = added.Count, Same = same };
    }

    /// <summary>Whether two repository-relative paths name the same module folder and the same file below its <c>data</c>.</summary>
    private static bool SamePath(string committed, string staged) => string.Equals(committed, staged, StringComparison.Ordinal);

    /// <summary>The records of a family file by id; none for a file that is not an array of records (hand-authored
    /// singletons, such as a mod's rules row).</summary>
    private static SortedDictionary<string, JsonObject> Records(string path)
    {
        var records = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        if (JsonNode.Parse(File.ReadAllText(path)) is not JsonArray array) return records;
        foreach (var node in array)
            if (node is JsonObject { } obj && obj["id"]?.GetValue<string>() is { } id) records[id] = obj;
        return records;
    }

    private static string Show(JsonNode? node)
    {
        var text = node is null ? "(absent)" : node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return text.Length <= 120 ? text : text[..117] + "...";
    }

    private sealed record Row(string Path, JsonObject Record)
    {
        public string Id => Record["id"]!.GetValue<string>();
    }

    public sealed class Totals
    {
        public int Changed, Moved, New, Gone, Same;
        public void Add(Totals other) { Changed += other.Changed; Moved += other.Moved; New += other.New; Same += other.Same; }
    }
}
