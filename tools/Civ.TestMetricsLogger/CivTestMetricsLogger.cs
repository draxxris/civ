using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Civ.TestMetricsLogger;

[ExtensionUri(ExtensionUri)]
[FriendlyName(FriendlyName)]
public sealed class CivTestMetricsLogger : ITestLogger
{
    private const string ExtensionUri = "logger://civ-test-metrics/v1";
    private const string FriendlyName = "CivTestMetrics";
    private const long MaxFileBytes = 10 * 1024 * 1024;
    // A trim rewrites the whole log, so it must not run per result. Once the log passes MaxFileBytes it is cut
    // back to this low-water mark, which pays for one rewrite per ~10k results instead of one per result.
    // Trimming to the cap instead cost ~110 ms per result and tripled the default suite's wall time.
    private const long TrimTargetBytes = MaxFileBytes * 3 / 4;
    private static readonly string MutexName = "CivTestMetricsLogger";

    public void Initialize(TestLoggerEvents events, string testRunDirectory)
    {
        events.TestResult += (_, args) => Record(args.Result);
    }

    private static void Record(TestResult result)
    {
        var path = Path.Combine(FindRepositoryRoot(), ".test-metrics", "test-runtimes.jsonl");
        var record = JsonSerializer.Serialize(new RuntimeRecord(
            DateTimeOffset.UtcNow,
            result.TestCase.FullyQualifiedName,
            result.TestCase.DisplayName,
            result.Duration.TotalMilliseconds,
            result.Outcome.ToString()));
        var line = Encoding.UTF8.GetBytes(record + "\n");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var mutex = new Mutex(false, MutexName);
        mutex.WaitOne();
        try
        {
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                stream.Write(line);
            }
            TrimFile(path);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static void TrimFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length <= MaxFileBytes)
            return;

        var retained = new Queue<byte[]>();
        long retainedBytes = 0;
        foreach (var line in File.ReadLines(path).Reverse())
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            if (retainedBytes + bytes.Length > TrimTargetBytes)
                break;
            retained.Enqueue(bytes);
            retainedBytes += bytes.Length;
        }

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        foreach (var line in retained.Reverse())
            output.Write(line);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName ?? Environment.CurrentDirectory;
    }

    private sealed record RuntimeRecord(
        DateTimeOffset Timestamp, string Test, string Name, double DurationMs, string Outcome);
}
