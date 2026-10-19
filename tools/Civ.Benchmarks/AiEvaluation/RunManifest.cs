using System.Diagnostics;
using Civ.Game;

namespace Civ.Benchmarks.AiEvaluation;

public sealed class RunManifest
{
    public const int CurrentSchema = 3;
    public int SchemaVersion { get; init; } = CurrentSchema;
    public string Scenario { get; init; } = EvaluationConfig.Scenario;
    public string RulesPolicy { get; init; } = EvaluationConfig.RulesPolicy;
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string[] Arguments { get; init; } = [];
    public string? GitRevision { get; init; }
    public bool? GitDirty { get; init; }
    public string WorkingDirectory { get; init; } = Environment.CurrentDirectory;
    public string HarnessModuleId { get; init; } = typeof(RunManifest).Module.ModuleVersionId.ToString();
    public EvaluationConfig? Config { get; set; }
    public string? ContentHash { get; set; }
    public string[] Mods { get; set; } = [];
    public Dictionary<string, int> AiHandicap { get; set; } = new();
    public List<SimulationSettings> SampleSettings { get; set; } = [];
    public MetricDefinition[] Metrics { get; init; } = EvaluationMetrics.Definitions;

    public static RunManifest Create(string[] args)
    {
        string? status = Git("status", "--porcelain", "--untracked-files=normal");
        return new()
        {
            Arguments = args, GitRevision = Git("rev-parse", "HEAD"),
            GitDirty = status is null ? null : status.Length > 0,
        };
    }

    private static string? Git(params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
}

public sealed record RunSummary(string Status, int CompletedSamples, int SnapshotCount,
    EconomicSnapshot[] FinalSnapshots, string? Error = null);
