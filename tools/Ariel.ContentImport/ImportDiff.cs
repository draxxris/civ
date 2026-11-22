using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ariel.ContentImport;

/// <summary>Compares a staged import with the committed data, file by file and record by record (by <c>id</c>), so changes
/// can be brought in piece-meal. Read-only: it never writes. Only files the staging run produced are compared; hand-authored
/// files have no staged counterpart and are ignored.</summary>
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
        var committed = layout.Committed;
        var matched = false;
        var total = new Totals();
        foreach (var (stagedRoot, committedRoot) in new[] { (layout.Rules, committed.Rules), (layout.Mods, committed.Mods) })
        {
            if (!Directory.Exists(stagedRoot)) continue;
            foreach (var module in Directory.GetDirectories(stagedRoot).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(module);
                if (filters.Length > 0 && !filters.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                matched = true;
                foreach (var staged in Directory.GetFiles(Path.Combine(module, "data"), "*.json").Order(StringComparer.Ordinal))
                    total.Add(CompareFile(name, staged, Path.Combine(committedRoot, name, "data", Path.GetFileName(staged)), values));
            }
        }
        if (!matched)
        {
            Console.Error.WriteLine("No staged module matches: " + string.Join(", ", filters));
            return 1;
        }
        Console.WriteLine($"\nTotal: {total.Changed} changed, {total.New} new in staging, {total.Gone} gone from staging, {total.Same} identical.");
        return 0;
    }

    private static Totals CompareFile(string module, string stagedPath, string committedPath, bool values)
    {
        var staged = Records(stagedPath);
        var committed = File.Exists(committedPath) ? Records(committedPath) : [];
        var changed = new List<string>();
        var added = staged.Keys.Where(id => !committed.ContainsKey(id)).ToArray();
        var gone = committed.Keys.Where(id => !staged.ContainsKey(id)).ToArray();
        var same = 0;
        foreach (var (id, record) in staged)
        {
            if (!committed.TryGetValue(id, out var old)) continue;
            // Compare field by field: the order of an object's properties is not a difference.
            var fields = old.Select(p => p.Key).Union(record.Select(p => p.Key)).Order(StringComparer.Ordinal)
                .Where(f => !JsonNode.DeepEquals(old[f], record[f])).ToArray();
            if (fields.Length == 0) { same++; continue; }
            changed.Add(values
                ? $"  ~ {id}\n" + string.Join("\n", fields.Select(f => $"      {f}: {Show(old[f])} -> {Show(record[f])}"))
                : $"  ~ {id}: {string.Join(", ", fields)}");
        }
        if (changed.Count + added.Length + gone.Length > 0)
        {
            Console.WriteLine($"{module}/{Path.GetFileName(stagedPath)}: {changed.Count} changed, {added.Length} new, {gone.Length} gone, {same} identical");
            foreach (var line in changed) Console.WriteLine(line);
            foreach (var id in added) Console.WriteLine("  + " + id);
            foreach (var id in gone) Console.WriteLine("  - " + id);
        }
        return new() { Changed = changed.Count, New = added.Length, Gone = gone.Length, Same = same };
    }

    private static SortedDictionary<string, JsonObject> Records(string path)
    {
        var records = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in JsonNode.Parse(File.ReadAllText(path))!.AsArray())
            records[node!["id"]!.GetValue<string>()] = node.AsObject();
        return records;
    }

    private static string Show(JsonNode? node)
    {
        var text = node is null ? "(absent)" : node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return text.Length <= 120 ? text : text[..117] + "...";
    }

    private sealed class Totals
    {
        public int Changed, New, Gone, Same;
        public void Add(Totals other) { Changed += other.Changed; New += other.New; Gone += other.Gone; Same += other.Same; }
    }
}
