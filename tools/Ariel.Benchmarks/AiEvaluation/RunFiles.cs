using System.Text;
using System.Text.Json;

namespace Ariel.Benchmarks.AiEvaluation;

/// <summary>Every JSONL append reaches disk before the next simulation step.</summary>
public sealed class RunFiles
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string DirectoryPath { get; }

    public RunFiles(string path)
    {
        DirectoryPath = Path.GetFullPath(path);
        if (Directory.Exists(DirectoryPath)) throw new IOException("The output directory already exists. Use a new directory.");
        Directory.CreateDirectory(DirectoryPath);
        using var claim = new FileStream(Path.Combine(DirectoryPath, ".run"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }

    public void Append(string name, object value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json) + "\n");
        using var stream = new FileStream(Path.Combine(DirectoryPath, name), FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public void Save(string name, object value) => Text(name, JsonSerializer.Serialize(value, Json) + "\n");

    public void Text(string name, string text)
    {
        string path = Path.Combine(DirectoryPath, name);
        using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(Encoding.UTF8.GetBytes(text));
            stream.Flush(flushToDisk: true);
        }
        File.Move(path + ".tmp", path, overwrite: true);
    }

    public void Progress(string status, ulong? seed = null, int? checkpoint = null, string? detail = null) =>
        Append("progress.jsonl", new { atUtc = DateTimeOffset.UtcNow, status, seed, scenario = EvaluationConfig.Scenario, checkpoint, detail });

    public static T Read<T>(string directory, string name) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, name)), Json)
        ?? throw new InvalidDataException($"Empty artifact: {name}");
}
