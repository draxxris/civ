using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Civ.Content;

/// <summary>The part of a collection that contains content identities.</summary>
public enum ReferencePart
{
    /// <summary>Scalar, sequence entries, or map values.</summary>
    Values,
    /// <summary>Map keys.</summary>
    Keys
}

/// <summary>A typed reference on a string, sequence, or map. Null values mean no reference.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class ContentReferenceAttribute(Type definitionType, ReferencePart part = ReferencePart.Values) : Attribute
{
    /// <summary>Required definition subtype.</summary>
    public Type DefinitionType { get; } = definitionType;
    /// <summary>Collection part to resolve.</summary>
    public ReferencePart Part { get; } = part;
}

/// <summary>A requirement evaluator whose leaves refer to a particular definition type.</summary>
public interface IContentRequirementEvaluator : IRequirementEvaluator
{
    /// <summary>Definition subtype referenced by this leaf kind.</summary>
    Type DefinitionType { get; }
}

/// <summary>A reference discovered in a definition, with its diagnostic property path.</summary>
public sealed record ContentRef(string Id, Type DefinitionType, string Path);

/// <summary>Discovers annotated identities and typed requirement leaves, including nested maps and records.</summary>
public static class ContentReferences
{
    private sealed record ReferenceProperty(PropertyInfo Property, ContentReferenceAttribute[] Attributes);
    private static readonly ConditionalWeakTable<Type, ReferenceProperty[]> Properties = new();

    private static ReferenceProperty[] DescribeProperties(Type type) => type
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .OrderBy(p => p.Name, StringComparer.Ordinal)
        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
        .Select(p => new ReferenceProperty(p, p.GetCustomAttributes<ContentReferenceAttribute>().ToArray()))
        .Where(p => p.Attributes.Length > 0 || (!p.Property.PropertyType.IsPrimitive && !p.Property.PropertyType.IsEnum))
        .ToArray();
    /// <summary>Find all annotated identities and neutral text links in a definition.</summary>
    public static IEnumerable<ContentRef> Enumerate(ContentDef definition, IEnumerable<IRequirementEvaluator> evaluators)
    {
        var kinds = evaluators.OfType<IContentRequirementEvaluator>().ToDictionary(e => e.Kind, e => e.DefinitionType, StringComparer.Ordinal);
        return Enumerate(definition, kinds);
    }

    internal static IEnumerable<ContentRef> Enumerate(ContentDef definition, IReadOnlyDictionary<string, Type> kinds) =>
        Walk(definition, definition.Id, kinds, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static IEnumerable<ContentRef> Walk(object? value, string path, IReadOnlyDictionary<string, Type> kinds, HashSet<object> seen)
    {
        if (value is string text)
        {
            if (!text.Contains("{link:", StringComparison.Ordinal)) yield break;
            foreach (Match match in Regex.Matches(text, @"\{link:([^}]+)\}", RegexOptions.CultureInvariant))
                yield return new(match.Groups[1].Value, typeof(ContentDef), path);
            yield break;
        }
        if (value is null || value.GetType().IsPrimitive || value.GetType().IsEnum || !seen.Add(value)) yield break;
        if (value is Requirement requirement)
        {
            foreach (var leaf in Leaves(requirement))
                if (kinds.TryGetValue(leaf.Kind, out var type)) yield return new(leaf.Id, type, path);
            yield break;
        }
        var children = value is IEnumerable sequence
            ? WalkSequence(sequence, path, kinds, seen)
            : WalkProperties(value, path, kinds, seen);
        foreach (var reference in children) yield return reference;
    }

    private static IEnumerable<ContentRef> WalkSequence(IEnumerable sequence, string path, IReadOnlyDictionary<string, Type> kinds, HashSet<object> seen)
    {
        foreach (var item in sequence)
            foreach (var reference in Walk(MapEntryValue(item), path, kinds, seen)) yield return reference;
    }

    private static object? MapEntryValue(object? item)
    {
        var type = item?.GetType();
        return type?.IsGenericType == true && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
            ? type.GetProperty("Value")!.GetValue(item) : item;
    }

    private static IEnumerable<ContentRef> WalkProperties(object value, string path, IReadOnlyDictionary<string, Type> kinds, HashSet<object> seen)
    {
        foreach (var metadata in Properties.GetValue(value.GetType(), DescribeProperties))
        {
            var child = metadata.Property.GetValue(value);
            var childPath = path + "." + metadata.Property.Name;
            foreach (var attribute in metadata.Attributes)
                foreach (var id in Identities(child, attribute.Part)) yield return new(id, attribute.DefinitionType, childPath);
            foreach (var reference in Walk(child, childPath, kinds, seen)) yield return reference;
        }
    }

    private static IEnumerable<string> Identities(object? value, ReferencePart part)
    {
        if (value is null) yield break;
        if (value is string id) { yield return id; yield break; }
        if (value is not IEnumerable sequence) throw new InvalidDataException("Content references must be strings, sequences or maps");
        foreach (var item in sequence)
        {
            object? entry = item;
            if (item?.GetType().IsGenericType == true && item.GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                entry = item.GetType().GetProperty(part == ReferencePart.Keys ? "Key" : "Value")!.GetValue(item);
            foreach (var identity in Identities(entry, ReferencePart.Values)) yield return identity;
        }
    }

    /// <summary>Traverse leaves in expression order, retaining scope and count.</summary>
    public static IEnumerable<Requirement.Leaf> Leaves(Requirement requirement) => requirement switch
    {
        Requirement.Leaf leaf => [leaf],
        Requirement.All all => all.Items.SelectMany(Leaves),
        Requirement.Any any => any.Items.SelectMany(Leaves),
        Requirement.Not not => Leaves(not.Item),
        _ => throw new InvalidDataException("Unknown requirement expression")
    };
}
