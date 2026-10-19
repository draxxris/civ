using System.Text;
using System.Text.Json;

namespace Civ.Benchmarks.AiEvaluation;

public sealed record MetricDelta(SampleKey Key, string Metric, string Direction, long Baseline, long Current,
    decimal Change, string Outcome);
public sealed record DeltaCounts(string Metric, int Improved, int Regressed, int Unchanged, int NeutralChanged);

internal static class BaselineComparison
{
    public static string Compare(RunFiles files, RunManifest current, string baselineDirectory)
    {
        var baseline = RunFiles.Read<RunManifest>(baselineDirectory, "manifest.json");
        var summary = RunFiles.Read<RunSummary>(baselineDirectory, "summary.json");
        if (summary.Status != "completed") throw new InvalidDataException("The baseline run did not complete.");
        RequireCompatible(current, baseline);
        var before = ReadSnapshots(baselineDirectory, baseline);
        var after = ReadSnapshots(files.DirectoryPath, current);
        var counts = WriteDeltas(files, before, after);
        bool contentChanged = current.ContentHash != baseline.ContentHash;
        files.Save("comparison.json", new
        {
            status = "completed", baselineDirectory = Path.GetFullPath(baselineDirectory),
            baselineRevision = baseline.GitRevision, currentRevision = current.GitRevision,
            contentChanged, baselineContentHash = baseline.ContentHash, currentContentHash = current.ContentHash,
            pairedSnapshots = after.Count, metrics = counts,
        });
        return Report(counts, contentChanged);
    }

    private static void RequireCompatible(RunManifest current, RunManifest baseline)
    {
        if (baseline.SchemaVersion != RunManifest.CurrentSchema)
            throw new InvalidDataException("Incompatible baseline schema.");
        var a = Compatibility(current);
        var b = Compatibility(baseline);
        if (JsonSerializer.Serialize(a, RunFiles.Json) != JsonSerializer.Serialize(b, RunFiles.Json))
            throw new InvalidDataException("Incompatible baseline scenario, configuration, metrics, handicap, mods, or resolved sample settings.");
    }

    private static object Compatibility(RunManifest manifest) => new
    {
        manifest.Scenario, manifest.RulesPolicy, manifest.Config, manifest.Metrics, manifest.Mods,
        aiHandicap = manifest.AiHandicap.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray(),
        manifest.SampleSettings,
    };

    private static Dictionary<SampleKey, EconomicSnapshot> ReadSnapshots(string directory, RunManifest manifest)
    {
        var config = manifest.Config ?? throw new InvalidDataException("Missing baseline configuration.");
        var snapshots = new Dictionary<SampleKey, EconomicSnapshot>();
        foreach (string line in File.ReadLines(Path.Combine(directory, "economy.jsonl")))
        {
            var row = JsonSerializer.Deserialize<EconomicSnapshot>(line, RunFiles.Json)
                ?? throw new InvalidDataException("Empty economic snapshot.");
            RequireValidRow(row, config);
            if (!snapshots.TryAdd(row.Key, row)) throw new InvalidDataException("Duplicate economic sample key.");
        }
        long expected = (long)config.Seeds.Length * config.Players * (config.Turns + 1);
        if (snapshots.Count != expected) throw new InvalidDataException("Incomplete economic sample coverage.");
        return snapshots;
    }

    private static void RequireValidRow(EconomicSnapshot row, EvaluationConfig config)
    {
        var key = row.Key;
        if (key.Scenario != EvaluationConfig.Scenario || !config.Seeds.Contains(key.Seed))
            throw new InvalidDataException("Unexpected sample scenario or seed.");
        RequireSlotAndCheckpoint(key, config);
        string[] names = EvaluationMetrics.Definitions.Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();
        if (!row.Metrics.Keys.Order(StringComparer.Ordinal).SequenceEqual(names))
            throw new InvalidDataException("Incomplete or unknown economic metrics.");
    }

    private static void RequireSlotAndCheckpoint(SampleKey key, EvaluationConfig config)
    {
        if (key.Player < 0 || key.Player >= config.Players || key.Checkpoint < 1 || key.Checkpoint > config.Turns + 1)
            throw new InvalidDataException("Unexpected sample player or checkpoint.");
    }

    private static DeltaCounts[] WriteDeltas(RunFiles files, Dictionary<SampleKey, EconomicSnapshot> before,
        Dictionary<SampleKey, EconomicSnapshot> after)
    {
        var counts = new List<DeltaCounts>();
        foreach (var metric in EvaluationMetrics.Definitions)
        {
            int improved = 0, regressed = 0, unchanged = 0, neutralChanged = 0;
            foreach (var (key, current) in after)
            {
                long oldValue = before[key].Metrics[metric.Name];
                long newValue = current.Metrics[metric.Name];
                string outcome = Outcome(metric.Direction, oldValue, newValue);
                if (outcome == "improved") improved++;
                if (outcome == "regressed") regressed++;
                if (outcome == "unchanged") unchanged++;
                if (outcome == "neutral-change") neutralChanged++;
                files.Append("deltas.jsonl", new MetricDelta(key, metric.Name, metric.Direction,
                    oldValue, newValue, (decimal)newValue - oldValue, outcome));
            }
            counts.Add(new(metric.Name, improved, regressed, unchanged, neutralChanged));
        }
        return counts.ToArray();
    }

    private static string Outcome(string direction, long before, long after)
    {
        if (before == after) return "unchanged";
        if (direction == "neutral") return "neutral-change";
        bool increased = after > before;
        return increased == (direction == "positive") ? "improved" : "regressed";
    }

    private static string Report(DeltaCounts[] counts, bool contentChanged)
    {
        var text = new StringBuilder("\n## Baseline comparison\n\n");
        text.AppendLine("Every delta pairs the same seed, player, scenario, and checkpoint. Timing is not an intelligence metric.");
        if (contentChanged) text.AppendLine("Content hashes differ. Rule or content changes can confound the comparison.");
        text.AppendLine("\nCounts refer to paired snapshots, not independent games. No composite score is calculated.\n");
        text.AppendLine("| Metric | Improved | Regressed | Unchanged | Neutral change |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var c in counts)
            text.AppendLine($"| {c.Metric} | {c.Improved} | {c.Regressed} | {c.Unchanged} | {c.NeutralChanged} |");
        return text.ToString();
    }
}
