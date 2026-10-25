using System.Text.Json;
using Civ.Benchmarks.AiEvaluation;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class AiEvaluationTests(AiEvaluationTests.ValidBaseline valid) : IClassFixture<AiEvaluationTests.ValidBaseline>, IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "civ-ai-evaluation-tests-" + Guid.NewGuid().ToString("N"));
    internal static string ModsRoot => Path.Combine(AppContext.BaseDirectory, "mods");

    /// <summary>The CLI reloads and rebuilds every mod on each call, so the valid baseline every rejection case compares
    /// against is produced once per class and copied per case; a case damages only its own copy.</summary>
    public sealed class ValidBaseline : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "civ-ai-evaluation-baseline-" + Guid.NewGuid().ToString("N"));
        private readonly Lazy<string> baseline;

        public ValidBaseline() => baseline = new(() =>
        {
            string directory = Path.Combine(root, "baseline");
            Assert.Equal(0, Run(directory));
            return directory;
        });

        public string CopyTo(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (string file in Directory.GetFiles(baseline.Value))
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), overwrite: true);
            return directory;
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Cli_records_repeatable_games_settlements_events_and_paired_metric_directions()
    {
        string baseline = Path.Combine(workspace, "baseline");
        string repeat = Path.Combine(workspace, "repeat");
        Assert.Equal(0, Run(baseline, turns: "2", seeds: "1,42"));
        Assert.Equal(0, Run(repeat, turns: "2", seeds: "1,42", baseline: baseline));
        Assert.Equal(File.ReadAllText(Path.Combine(baseline, "economy.jsonl")), File.ReadAllText(Path.Combine(repeat, "economy.jsonl")));
        var summary = RunFiles.Read<RunSummary>(repeat, "summary.json");
        // The sample grid is the CLI arguments, not a literal: two seeds, two players, and one checkpoint per
        // turn plus the initial state.
        const int players = 2, turns = 2;
        int seeds = 2, checkpoints = turns + 1;
        Assert.Equal("completed", summary.Status);
        Assert.Equal(seeds, summary.CompletedSamples);
        Assert.Equal(seeds * players * checkpoints, summary.SnapshotCount);
        Assert.Equal(seeds * players, summary.FinalSnapshots.Length);
        Assert.All(Rows<MetricDelta>(repeat, "deltas.jsonl"), d => Assert.Equal("unchanged", d.Outcome));
        AssertSettlementsAndEvents(baseline);
        var manifest = RunFiles.Read<RunManifest>(baseline, "manifest.json");
        Assert.NotNull(manifest.ContentHash);
        Assert.NotNull(manifest.GitRevision);
        Assert.NotNull(manifest.GitDirty);
        Assert.Equal(RunManifest.CurrentSchema, manifest.SchemaVersion);
        Assert.Equal(EvaluationConfig.RulesPolicy, manifest.RulesPolicy);
        Assert.All(manifest.SampleSettings, s => Assert.Equal(new[] { "ai:standard", "ai:standard" }, s.PlayerAis));
        foreach (var row in Rows<EconomicSnapshot>(baseline, "economy.jsonl"))
        {
            Assert.Equal(row.Metrics["workerCount"], row.Metrics["workersBuilding"] + row.Metrics["workersNotBuilding"]);
            Assert.InRange(row.Metrics["workedLandWithoutImprovement"], 0, row.Metrics["workedLandTiles"]);
            Assert.InRange(row.Metrics["workerDisplacementsSincePreviousCheckpoint"], 0, row.Metrics["workerCount"]);
            Assert.True(row.Metrics["buildingWorkerCheckpointsTotal"] >= row.Metrics["workersBuilding"]);
            Assert.True(row.Metrics["workerDisplacementsTotal"] >= row.Metrics["workerDisplacementsSincePreviousCheckpoint"]);
            Assert.True(row.Metrics["improvementTransitionsTotal"] >= row.Metrics["improvementTransitionsSincePreviousCheckpoint"]);
            Assert.True(row.Metrics["routeTransitionsTotal"] >= row.Metrics["routeTransitionsSincePreviousCheckpoint"]);
            Assert.InRange(row.Metrics["ownedBuildProgressBasisPoints"], 0, row.Metrics["ownedBuildProgressEntries"] * 10_000);
        }
        // Synthetic baseline values isolate each documented direction at the real file boundary.
        var before = Rows<EconomicSnapshot>(baseline, "economy.jsonl");
        foreach (var row in before)
        {
            row.Metrics["cities"] += 2;
            row.Metrics["population"] -= 1;
            row.Metrics["unpaidGold"] += 3;
            row.Metrics["units"] += 4;
        }
        WriteRows(baseline, "economy.jsonl", before);
        string changed = Path.Combine(workspace, "changed");
        Assert.Equal(0, Run(changed, turns: "2", seeds: "1,42", baseline: baseline));
        var deltas = Rows<MetricDelta>(changed, "deltas.jsonl");
        Assert.Equal(summary.SnapshotCount * manifest.Metrics.Length, deltas.Length);
        Assert.All(deltas.Where(d => d.Metric == "cities"), d => { Assert.Equal(-2, d.Change); Assert.Equal("regressed", d.Outcome); });
        Assert.All(deltas.Where(d => d.Metric == "population"), d => Assert.Equal("improved", d.Outcome));
        Assert.All(deltas.Where(d => d.Metric == "unpaidGold"), d => Assert.Equal("improved", d.Outcome));
        Assert.All(deltas.Where(d => d.Metric == "units"), d => Assert.Equal("neutral-change", d.Outcome));
        Assert.Equal(before.Select(r => r.Key).OrderBy(k => k.ToString()),
            deltas.Where(d => d.Metric == "cities").Select(d => d.Key).OrderBy(k => k.ToString()));
        Assert.Contains("Baseline comparison", File.ReadAllText(Path.Combine(changed, "report.md")));
        Assert.Equal("run_completed", Rows<JsonElement>(changed, "progress.jsonl").Last().GetProperty("status").GetString());
    }

    private static void AssertSettlementsAndEvents(string directory)
    {
        var sources = Directory.GetDirectories(ModsRoot).Select(ModLoader.FromDirectory).ToArray();
        var content = ModLoader.Build(sources);
        var rows = Rows<EconomicSnapshot>(directory, "economy.jsonl");
        foreach (ulong seed in new ulong[] { 1, 42 })
        {
            var sim = GameFactory.CreateAi(content, seed, 2, "foundation:worldsize_duel");
            var initial = rows.Where(r => r.Key.Seed == seed && r.Key.Checkpoint == 1).ToArray();
            Assert.All(initial, r => { Assert.Null(r.SettledTurn); Assert.Equal(0, r.Metrics["settledResearchHundredthsTotal"]); });
            sim.AdvanceTurn();
            long first = sim.State.PlayerById(0).Extensions.TryGet<EconomyPlayerState>(content)!.SettledCommerceHundredths.Research;
            Assert.Equal(first, rows.Single(r => r.Key == new SampleKey(seed, 0, EvaluationConfig.Scenario, 2)).Metrics["settledResearchHundredthsTotal"]);
            sim.AdvanceTurn();
            long second = sim.State.PlayerById(0).Extensions.TryGet<EconomyPlayerState>(content)!.SettledCommerceHundredths.Research;
            var final = rows.Single(r => r.Key == new SampleKey(seed, 0, EvaluationConfig.Scenario, 3));
            Assert.Equal(first + second, final.Metrics["settledResearchHundredthsTotal"]);
            Assert.Equal(2, final.SettledTurn);
        }
        var events = Rows<JsonElement>(directory, "events.jsonl");
        var eventKeys = events.Select(e => (e.GetProperty("seed").GetUInt64(), e.GetProperty("sourceTurn").GetInt32(), e.GetProperty("ordinal").GetInt32()));
        Assert.Equal(events.Length, eventKeys.Distinct().Count());
        // Two seeds of two turns; the fixture's two players found one city per played turn between them.
        const int seeds = 2, turns = 2;
        Assert.Equal(seeds * (turns + 1), events.Count(e => e.GetProperty("eventType").GetString() == "TurnStarted"));
        Assert.Equal(seeds * turns, events.Count(e => e.GetProperty("eventType").GetString() == "TurnEnded"));
        Assert.Equal(seeds * turns, events.Count(e => e.GetProperty("eventType").GetString() == "CityFounded"));
    }

    [Theory]
    [InlineData("schema", "schema")]
    [InlineData("settings", "Incompatible baseline")]
    [InlineData("failed", "did not complete")]
    [InlineData("missing-row", "coverage")]
    [InlineData("duplicate-row", "Duplicate")]
    public void Cli_rejects_incompatible_or_incomplete_baselines_without_overwriting_them(string fault, string expectedError)
    {
        string baseline = valid.CopyTo(Path.Combine(workspace, "baseline"));
        DamageBaseline(baseline, fault);
        string before = File.ReadAllText(Path.Combine(baseline, "economy.jsonl"));
        string output = Path.Combine(workspace, "comparison");
        Assert.Equal(1, Run(output, baseline: baseline));
        AssertFailure(output, expectedError);
        Assert.Equal(before, File.ReadAllText(Path.Combine(baseline, "economy.jsonl")));
        Assert.False(File.Exists(Path.Combine(output, "comparison.json")));
        Assert.DoesNotContain("run_completed", File.ReadAllText(Path.Combine(output, "progress.jsonl")));
    }

    private static void DamageBaseline(string directory, string fault)
    {
        if (fault == "schema")
        {
            var json = File.ReadAllText(Path.Combine(directory, "manifest.json"));
            File.WriteAllText(Path.Combine(directory, "manifest.json"), json.Replace("\"schemaVersion\":3", "\"schemaVersion\":999"));
        }
        if (fault == "settings")
        {
            var manifest = RunFiles.Read<RunManifest>(directory, "manifest.json");
            manifest.SampleSettings[0] = manifest.SampleSettings[0] with { Leaders = ["different-leader"] };
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest, RunFiles.Json));
        }
        if (fault == "failed")
            File.WriteAllText(Path.Combine(directory, "summary.json"), JsonSerializer.Serialize(new RunSummary("failed", 0, 0, []), RunFiles.Json));
        var rows = Rows<EconomicSnapshot>(directory, "economy.jsonl");
        if (fault == "missing-row") WriteRows(directory, "economy.jsonl", rows.Skip(1));
        if (fault == "duplicate-row") WriteRows(directory, "economy.jsonl", rows.Append(rows[0]));
    }

    [Theory]
    [InlineData("invalid-config")]
    [InlineData("load-failure")]
    public void Cli_durably_records_failure_and_refuses_to_reuse_output(string fault)
    {
        string output = Path.Combine(workspace, "failure");
        int exit = fault == "invalid-config" ? Run(output, turns: "-1") : Run(output, modsRoot: Path.Combine(workspace, "missing-mods"));
        Assert.Equal(1, exit);
        AssertFailure(output, fault == "invalid-config" ? "Turns must" : "Could not find");
        string summary = File.ReadAllText(Path.Combine(output, "summary.json"));
        Assert.Equal(1, Run(output));
        Assert.Equal(summary, File.ReadAllText(Path.Combine(output, "summary.json")));
    }

    private static void AssertFailure(string output, string expectedError)
    {
        var summary = RunFiles.Read<RunSummary>(output, "summary.json");
        Assert.Equal("failed", summary.Status);
        Assert.Contains(expectedError, summary.Error);
        Assert.True(File.Exists(Path.Combine(output, "failure.json")));
        Assert.Equal("run_failed", Rows<JsonElement>(output, "progress.jsonl").Last().GetProperty("status").GetString());
        Assert.Contains("Status: failed", File.ReadAllText(Path.Combine(output, "report.md")));
    }

    internal static int Run(string output, string turns = "0", string seeds = "1", string? baseline = null, string? modsRoot = null)
    {
        var args = new List<string> { "--output", output, "--turns", turns, "--players", "2", "--world", "duel", "--seeds", seeds };
        if (baseline is not null) args.AddRange(["--baseline", baseline]);
        return EvaluationCli.Run(args.ToArray(), modsRoot ?? ModsRoot);
    }

    private static T[] Rows<T>(string directory, string name) => File.ReadLines(Path.Combine(directory, name))
        .Select(line => JsonSerializer.Deserialize<T>(line, RunFiles.Json)!).ToArray();

    private static void WriteRows<T>(string directory, string name, IEnumerable<T> rows) =>
        File.WriteAllLines(Path.Combine(directory, name), rows.Select(row => JsonSerializer.Serialize(row, RunFiles.Json)));

    public void Dispose()
    {
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
    }
}
