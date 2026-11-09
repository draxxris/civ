using System.Text.Json.Nodes;

namespace Ariel.SyncCheck;

/// <summary>
/// Compares two saves of the same game, such as the files peers write to their desync folder, and prints the JSON
/// paths whose values differ, so a desync can be traced to the state that diverged.
/// </summary>
internal static class SaveDiff
{
    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("usage: Ariel.SyncCheck diff-saves A.json B.json [--max N]"); return 2; }
        int max = Array.IndexOf(args, "--max") is int i and >= 0 && i + 1 < args.Length ? int.Parse(args[i + 1]) : 40;
        var a = JsonNode.Parse(File.ReadAllText(args[0]));
        var b = JsonNode.Parse(File.ReadAllText(args[1]));
        var differences = new List<string>();
        Compare("$", a, b, differences, max);
        foreach (var line in differences) Console.WriteLine(line);
        Console.Error.WriteLine(differences.Count == 0 ? "identical" : $"{differences.Count}{(differences.Count >= max ? "+" : "")} differences");
        return differences.Count == 0 ? 0 : 1;
    }

    private static void Compare(string path, JsonNode? a, JsonNode? b, List<string> output, int max)
    {
        if (output.Count >= max) return;
        switch (a, b)
        {
            case (JsonObject x, JsonObject y):
                foreach (var key in x.Select(p => p.Key).Union(y.Select(p => p.Key)).Order(StringComparer.Ordinal))
                    Compare($"{path}.{key}", x[key], y[key], output, max);
                break;
            case (JsonArray x, JsonArray y):
                if (x.Count != y.Count) output.Add($"{path}: length {x.Count} vs {y.Count}");
                for (int i = 0; i < Math.Min(x.Count, y.Count); i++) Compare($"{path}[{i}]", x[i], y[i], output, max);
                break;
            default:
                string left = a?.ToJsonString() ?? "null", right = b?.ToJsonString() ?? "null";
                if (left != right) output.Add($"{path}: {Clip(left)} vs {Clip(right)}");
                break;
        }
    }

    private static string Clip(string s) => s.Length <= 80 ? s : s[..77] + "...";
}
