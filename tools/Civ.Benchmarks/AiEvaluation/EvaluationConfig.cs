using System.Globalization;

namespace Civ.Benchmarks.AiEvaluation;

public sealed record EvaluationConfig(ulong[] Seeds, int Turns, int Players, string World)
{
    public const string Scenario = "continents-noble-v1";
    public const string RulesPolicy = "shipped-noble-ai-subsidies-not-equal-rules";

    public static EvaluationConfig Parse(string[] args)
    {
        var values = ReadArguments(args);
        var seeds = Get(values, "--seeds", "1,42").Split(',')
            .Select(s => ulong.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray();
        if (seeds.Distinct().Count() != seeds.Length)
            throw new ArgumentException("Seeds must be distinct unsigned integers.");
        int turns = BoundedInteger(Get(values, "--turns", "100"), 0, 10000, "Turns");
        int players = BoundedInteger(Get(values, "--players", "4"), 2, 4, "Players");
        string world = Get(values, "--world", "small");
        if (!new[] { "duel", "tiny", "small", "standard", "large", "huge", "giant", "gigantic" }.Contains(world))
            throw new ArgumentException("Unknown world size.");
        return new(seeds, turns, players, "foundation:worldsize_" + world);
    }

    private static int BoundedInteger(string text, int minimum, int maximum, string name)
    {
        int value = int.Parse(text, CultureInfo.InvariantCulture);
        if (value < minimum || value > maximum)
            throw new ArgumentException($"{name} must be between {minimum} and {maximum}.");
        return value;
    }

    private static Dictionary<string, string> ReadArguments(string[] args)
    {
        string[] allowed = ["--output", "--baseline", "--seeds", "--turns", "--players", "--world"];
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (!allowed.Contains(args[i])) throw new ArgumentException($"Unknown option: {args[i]}");
            if (i + 1 == args.Length) throw new ArgumentException($"Missing value: {args[i]}");
            if (!values.TryAdd(args[i], args[i + 1])) throw new ArgumentException($"Duplicate option: {args[i]}");
        }
        return values;
    }

    private static string Get(Dictionary<string, string> values, string key, string fallback) =>
        values.GetValueOrDefault(key, fallback);

    public static string? Argument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
