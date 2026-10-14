using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Civ.Game;

namespace Civ.Content;

/// <summary>Composable sidecar requirement. A bare content id means a tech leaf.
/// Objects combine all/any/not; explicit leaves use {kind,id,count?,param?}.</summary>
[JsonConverter(typeof(RequirementConverter))]
public abstract record Requirement
{
    /// <summary>Every child must pass; empty means true.</summary>
    public sealed record All(IReadOnlyList<Requirement> Items) : Requirement;
    /// <summary>At least one child must pass; empty means false.</summary>
    public sealed record Any(IReadOnlyList<Requirement> Items) : Requirement;
    /// <summary>Invert a requirement.</summary>
    public sealed record Not(Requirement Item) : Requirement;
    /// <summary>Mod-owned leaf kind; count and integer parameter have kind-specific semantics.</summary>
    public sealed record Leaf(string Kind, string Id, int? Count = null, int? Param = null) : Requirement;
}

/// <summary>Simulation and subject against which a requirement is evaluated.</summary>
public sealed record RequirementContext(Simulation Sim, int Player, City? City = null, TileId? Plot = null, Unit? Unit = null,
    IReadOnlyDictionary<string, string>? ProposedCivics = null);
/// <summary>A mod owns one leaf kind; false results supply a human-readable reason.</summary>
public interface IRequirementEvaluator
{
    /// <summary>Leaf kind, compared ordinally. Exactly one evaluator per kind is allowed.</summary>
    string Kind { get; }
    /// <summary>Evaluate without changing state; null means success, otherwise a failure explanation.</summary>
    string? Evaluate(Requirement.Leaf leaf, RequirementContext context);
}
/// <summary>Requirement result plus unsatisfied leaves (or satisfied leaves forbidden by a Not).</summary>
public sealed record RequirementResult(bool Success, IReadOnlyList<RequirementFailure> Failures);
/// <summary>A failed leaf and its explanation.</summary>
public sealed record RequirementFailure(Requirement.Leaf? Leaf, string Explanation);

/// <summary>Evaluator dispatch shared by feature mods.</summary>
public sealed class RequirementService
{
    private readonly Dictionary<string, IRequirementEvaluator> _evaluators = new(StringComparer.Ordinal);
    internal RequirementService(IEnumerable<IRequirementEvaluator> evaluators)
    {
        foreach (var e in evaluators) if (!_evaluators.TryAdd(e.Kind, e)) throw new InvalidOperationException($"Duplicate requirement evaluator for kind '{e.Kind}'");
    }
    /// <summary>Fail fast on unknown leaf kinds during content loading.</summary>
    public void Validate(Requirement requirement)
    {
        switch (requirement)
        {
            case Requirement.Leaf leaf: _ = Evaluator(leaf.Kind); break;
            case Requirement.All all: foreach (var r in all.Items) Validate(r); break;
            case Requirement.Any any: foreach (var r in any.Items) Validate(r); break;
            case Requirement.Not not: Validate(not.Item); break;
        }
    }
    private IRequirementEvaluator Evaluator(string kind) => _evaluators.GetValueOrDefault(kind) ?? throw new InvalidDataException($"Unknown requirement leaf kind '{kind}'; load a mod that registers its evaluator");
    /// <summary>Evaluate every relevant branch in data order. Successful alternatives discard their failed branches.</summary>
    public RequirementResult Evaluate(Requirement? requirement, RequirementContext context)
    {
        if (requirement == null) return new(true, []);
        switch (requirement)
        {
            case Requirement.Leaf leaf:
                var error = Evaluator(leaf.Kind).Evaluate(leaf, context);
                return error == null ? new(true, []) : new(false, [new(leaf, error)]);
            case Requirement.All all: return Combine(all.Items, context, false);
            case Requirement.Any any: return Combine(any.Items, context, true);
            case Requirement.Not not:
                var result = Evaluate(not.Item, context);
                if (!result.Success) return new(true, []);
                var forbidden = Leaves(not.Item).Where(l => Evaluate(l, context).Success)
                    .Select(l => new RequirementFailure(l, $"Forbidden requirement: {l.Kind} {l.Id}")).ToArray();
                return new(false, forbidden.Length > 0 ? forbidden : [new(null, "Forbidden requirement is satisfied")]);
            default: throw new InvalidDataException("Unknown requirement expression");
        }
    }
    /// <summary>Evaluate only one requirement kind, for persistent effects with scoped activation gates.</summary>
    public RequirementResult EvaluateKind(Requirement? requirement, RequirementContext context, string kind)
    {
        if (requirement == null || !Leaves(requirement).Any(l => l.Kind == kind)) return new(true, []);
        switch (requirement)
        {
            case Requirement.Leaf: return Evaluate(requirement, context);
            case Requirement.All all: return CombineKind(all.Items, context, kind, false);
            case Requirement.Any any: return CombineKind(any.Items, context, kind, true);
            case Requirement.Not not:
                var inner = EvaluateKind(not.Item, context, kind);
                return inner.Success ? new(false, [new(null, "Forbidden requirement is satisfied")]) : new(true, []);
            default: throw new InvalidDataException("Unknown requirement expression");
        }
    }
    private RequirementResult CombineKind(IReadOnlyList<Requirement> items, RequirementContext context, string kind, bool alternative)
    {
        // In All, branches without target leaves are neutral; in Any they only count when they actually pass.
        var results = items.Select(r => Leaves(r).Any(l => l.Kind == kind) ? EvaluateKind(r, context, kind)
            : alternative ? Evaluate(r, context) : new RequirementResult(true, [])).ToArray();
        bool success = alternative ? results.Any(r => r.Success) : results.All(r => r.Success);
        var failures = results.SelectMany(r => r.Failures).ToArray();
        if (!success && failures.Length == 0) failures = [new(null, "No requirement alternative is available")];
        return new(success, success ? [] : failures);
    }
    private RequirementResult Combine(IReadOnlyList<Requirement> items, RequirementContext context, bool alternative)
    {
        var results = items.Select(r => Evaluate(r, context)).ToArray();
        bool success = alternative ? results.Any(r => r.Success) : results.All(r => r.Success);
        var failures = results.SelectMany(r => r.Failures).ToArray();
        if (!success && failures.Length == 0) failures = [new(null, "No requirement alternative is available")];
        return new(success, success ? [] : failures);
    }
    private static IEnumerable<Requirement.Leaf> Leaves(Requirement r) => r switch
    {
        Requirement.Leaf l => [l], Requirement.All a => a.Items.SelectMany(Leaves),
        Requirement.Any a => a.Items.SelectMany(Leaves), Requirement.Not n => Leaves(n.Item), _ => [],
    };
}

/// <summary>Compact JSON converter: strings denote tech ids, composite objects may contain multiple clauses,
/// interpreted as All; explicit leaves preserve kind/count/param.</summary>
public sealed class RequirementConverter : JsonConverter<Requirement>
{
    /// <inheritdoc/>
    public override Requirement Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return Parse(doc.RootElement);
    }
    private static Requirement Parse(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String) return new Requirement.Leaf("tech", e.GetString()!);
        if (e.ValueKind != JsonValueKind.Object) throw new JsonException("Requirement must be a content id or an object");
        if (e.TryGetProperty("kind", out var kind))
        {
            CheckKeys(e, ["kind", "id", "count", "param"]);
            return new Requirement.Leaf(kind.GetString()!, e.GetProperty("id").GetString()!, Number(e, "count"), Number(e, "param"));
        }
        CheckKeys(e, ["all", "any", "not"]);
        var clauses = new List<Requirement>();
        // Fixed clause order makes combined forms independent of JSON property order.
        if (e.TryGetProperty("all", out var all)) clauses.Add(new Requirement.All(all.EnumerateArray().Select(Parse).ToImmutableArray()));
        if (e.TryGetProperty("any", out var any)) clauses.Add(new Requirement.Any(any.EnumerateArray().Select(Parse).ToImmutableArray()));
        if (e.TryGetProperty("not", out var not)) clauses.Add(new Requirement.Not(Parse(not)));
        if (clauses.Count == 0) throw new JsonException("Empty requirement object");
        return clauses.Count == 1 ? clauses[0] : new Requirement.All(clauses.ToImmutableArray());
    }
    private static int? Number(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetInt32() : null;
    private static void CheckKeys(JsonElement e, string[] keys)
    {
        foreach (var p in e.EnumerateObject()) if (!keys.Contains(p.Name, StringComparer.Ordinal)) throw new JsonException($"Unknown requirement property '{p.Name}'");
    }
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Requirement value, JsonSerializerOptions options)
    {
        if (value is Requirement.Leaf { Kind: "tech", Count: null, Param: null } shorthand) { writer.WriteStringValue(shorthand.Id); return; }
        writer.WriteStartObject();
        switch (value)
        {
            case Requirement.Leaf l:
                writer.WriteString("kind", l.Kind); writer.WriteString("id", l.Id);
                if (l.Count is { } count) writer.WriteNumber("count", count);
                if (l.Param is { } param) writer.WriteNumber("param", param);
                break;
            case Requirement.All a: WriteItems(writer, "all", a.Items, options); break;
            case Requirement.Any a: WriteItems(writer, "any", a.Items, options); break;
            case Requirement.Not n: writer.WritePropertyName("not"); JsonSerializer.Serialize(writer, n.Item, options); break;
        }
        writer.WriteEndObject();
    }
    private static void WriteItems(Utf8JsonWriter writer, string name, IReadOnlyList<Requirement> items, JsonSerializerOptions options)
    {
        writer.WritePropertyName(name); writer.WriteStartArray();
        foreach (var r in items) JsonSerializer.Serialize(writer, r, options);
        writer.WriteEndArray();
    }
}
