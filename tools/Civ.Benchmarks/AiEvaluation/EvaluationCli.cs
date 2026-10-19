using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Benchmarks.AiEvaluation;

public static class EvaluationCli
{
    public static int Run(string[] args, string modsRoot)
    {
        if (args.SequenceEqual(new[] { "--help" }))
        {
            Console.WriteLine("AI economic evaluation: --output DIR --seeds 1,42 --turns 100 --players 4 --world small [--baseline DIR]");
            Console.WriteLine("All slots use fixed AI leaders and shipped Noble subsidies, not equal human/AI rules. No AI edits occur.");
            return 0;
        }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { return RunWithLog(args, modsRoot, cancellation.Token); }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static int RunWithLog(string[] args, string modsRoot, CancellationToken cancellation)
    {
        RunFiles? files = null;
        EvaluationRun? run = null;
        try
        {
            var manifest = RunManifest.Create(args);
            string output = EvaluationConfig.Argument(args, "--output")
                ?? Path.Combine(".ai-eval", DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
            files = new RunFiles(output);
            files.Progress("run_started");
            files.Save("manifest.json", manifest);
            run = new EvaluationRun(files, manifest);
            run.Execute(args, modsRoot, cancellation);
            Console.WriteLine($"Evaluation completed: {files.DirectoryPath}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Evaluation failed: {error.Message}");
            if (files is not null) WriteFailure(files, run, error);
            return 1;
        }
    }

    private static void WriteFailure(RunFiles files, EvaluationRun? run, Exception error)
    {
        try
        {
            files.Save("failure.json", new { atUtc = DateTimeOffset.UtcNow, error = error.ToString() });
            files.Progress("run_failed", detail: error.Message);
            files.Save("summary.json", run?.Summary("failed", error.Message) ?? new("failed", 0, 0, [], error.Message));
            files.Text("report.md", "# AI economic evaluation\n\nStatus: failed. Read `failure.json` and `progress.jsonl`.\n");
        }
        catch (Exception logError)
        {
            Console.Error.WriteLine($"The failure log could not reach disk: {logError.Message}");
        }
    }
}

internal sealed class EvaluationRun(RunFiles files, RunManifest manifest)
{
    private int completed;
    private int snapshotCount;
    private readonly List<EconomicSnapshot> finals = [];

    public RunSummary Summary(string status, string? error = null) => new(status, completed, snapshotCount, finals.ToArray(), error);

    public void Execute(string[] args, string modsRoot, CancellationToken cancellation)
    {
        files.Save("summary.json", Summary("running"));
        var config = EvaluationConfig.Parse(args);
        manifest.Config = config;
        files.Save("manifest.json", manifest);
        files.Progress("content_load_started");
        var sources = Directory.GetDirectories(modsRoot).Order(StringComparer.Ordinal).Select(ModLoader.FromDirectory).ToArray();
        var content = ModLoader.Build(sources);
        RecordContent(sources, content);
        foreach (ulong seed in config.Seeds) RunSample(content, config, seed, cancellation);
        cancellation.ThrowIfCancellationRequested();
        string comparison = Compare(args);
        cancellation.ThrowIfCancellationRequested();
        files.Text("report.md", Report() + LatestSnapshots() + comparison);
        files.Save("summary.json", Summary("completed"));
        files.Progress("run_completed");
    }

    private void RecordContent(ModSource[] sources, Civ.Content.ContentDb content)
    {
        manifest.ContentHash = content.ContentHash;
        manifest.Mods = sources.Select(s => s.Manifest.Id).Order(StringComparer.Ordinal).ToArray();
        var handicap = content.Get<HandicapDef>("foundation:handicap_noble");
        manifest.AiHandicap = typeof(HandicapDef).GetProperties()
            .Where(p => p.Name.StartsWith("Ai", StringComparison.Ordinal) && p.PropertyType == typeof(int))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(p => p.Name, p => (int)p.GetValue(handicap)!);
        files.Save("manifest.json", manifest);
        files.Progress("content_load_completed");
    }

    private void RunSample(Civ.Content.ContentDb content, EvaluationConfig config, ulong seed, CancellationToken cancellation)
    {
        var game = new EvaluationGame(files, config, seed, cancellation);
        try
        {
            game.Run(content, settings =>
            {
                manifest.SampleSettings.Add(settings);
                files.Save("manifest.json", manifest);
            });
            completed++;
        }
        finally
        {
            snapshotCount += game.SnapshotCount;
            finals.AddRange(game.FinalSnapshots);
            files.Save("summary.json", Summary("running"));
        }
    }

    private string Compare(string[] args)
    {
        string? baseline = EvaluationConfig.Argument(args, "--baseline");
        if (baseline is null) return "\nNo baseline was supplied. No improvement or regression claim is made.\n";
        files.Progress("comparison_started");
        string report = BaselineComparison.Compare(files, manifest, baseline);
        files.Progress("comparison_completed");
        return report;
    }

    private string LatestSnapshots()
    {
        var text = new System.Text.StringBuilder("\n## Latest economic snapshots\n\n");
        foreach (var snapshot in finals)
        {
            var key = snapshot.Key;
            var metrics = snapshot.Metrics;
            text.AppendLine($"- Seed `{key.Seed}`, player `{key.Player}`, scenario `{key.Scenario}`, checkpoint `{key.Checkpoint}`:");
            text.AppendLine($"  - Cities: {metrics["cities"]}. Population: {metrics["population"]}. Treasury: {metrics["treasury"]}.");
            text.AppendLine($"  - Settled research: {metrics["settledResearchHundredthsTotal"]} hundredths. Projected net gold: {metrics["netGold"]}.");
            text.AppendLine($"  - Unpaid gold: {metrics["unpaidGold"]}. Strike turns: {metrics["strikeTurns"]}.");
        }
        return text.ToString();
    }

    private string Report() => $"""
        # AI economic evaluation

        Status: completed. Samples: {completed}. Economic snapshots: {snapshotCount}.

        Rules: shipped Noble AI subsidies. These are not equal human/AI rules.
        The manifest records the AI handicap percentages and all resolved sample settings.
        All measured slots use the same fixed AI controller and leaders across versions.

        Each checkpoint is a snapshot after AI actions. Checkpoint 1 includes the initial AI turn, but no settlement.
        Each later checkpoint follows one settlement and the next AI turn.
        Only `settled*Total` metrics accumulate actual settled commerce. Other yield and income metrics are current previews.
        Queue production includes item modifiers, overflow, and food conversion where the rules permit them.
        Metric definitions and directions are in `manifest.json`. Raw events are in `events.jsonl`.

        This run does not infer win rate or general intelligence. No composite score is calculated.
        This is a generated game, not a controlled peaceful scenario. Conflict and barbarians can affect economic output.
        The loaded mod set is the benchmark payload. Optional game mods are not loaded automatically.
        Events describe observed rule notifications, not decision scores or reasons.
        The tool does not edit the AI or replace a baseline.
        Git provenance describes the working checkout. The module ID identifies the built harness.

        """;
}
