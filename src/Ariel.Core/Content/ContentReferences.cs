using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Ariel.Content;

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

/// <summary>The requirement leaf kind whose ids name definitions of the attributed type: <c>tech</c> on the technology
/// definition, <c>corporation</c> on a mod's corporation definition. The type that a mod publishes declares its own
/// kinds, so tooling that validates content without running any mod (the importer) learns every kind from the schema
/// assemblies it reads (<see cref="ContentLeafKinds.In"/>). At run time the evaluator a mod registers for the kind
/// (<see cref="IContentRequirementEvaluator.DefinitionType"/>) is what resolves its leaves.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ContentLeafKindAttribute(string kind) : Attribute
{
    /// <summary>The leaf kind, compared ordinally.</summary>
    public string Kind { get; } = kind;
}

/// <summary>Leaf kinds declared by definition types (<see cref="ContentLeafKindAttribute"/>).</summary>
public static class ContentLeafKinds
{
    /// <summary>Every leaf kind the definition types of these assemblies declare, with the type it names. A kind declared
    /// twice is a schema error.</summary>
    public static IReadOnlyDictionary<string, Type> In(IEnumerable<Assembly> assemblies)
    {
        var kinds = new SortedDictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in assemblies.Distinct().SelectMany(a => a.GetExportedTypes()).OrderBy(t => t.FullName, StringComparer.Ordinal))
            foreach (var attribute in type.GetCustomAttributes<ContentLeafKindAttribute>(false))
                if (!kinds.TryAdd(attribute.Kind, type))
                    throw new InvalidDataException($"Leaf kind '{attribute.Kind}' is declared by both {kinds[attribute.Kind].Name} and {type.Name}");
        return kinds;
    }
}

/// <summary>A reference by requirement leaf kind and id, such as what a building founds (<c>{"kind": "&lt;kind&gt;", "id":
/// "&lt;mod&gt;:&lt;name&gt;"}</c>): the kind says which definition type the id names, as it does for a leaf, so a field can
/// name content of a mod the declaring schema does not know. It resolves like a leaf, through the evaluator registered
/// for its kind; a kind no loaded mod registers is not resolved, and the rules reading the field treat it as naming
/// nothing that is loaded.</summary>
public sealed record LeafReference(string Kind, string Id);

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
        .Select(p => new ReferenceProperty(p, p.GetCustomAttributes<ContentReferenceAttribute>().Concat(AmendedReferences(type, p)).ToArray()))
        .Where(p => p.Attributes.Length > 0 || (!p.Property.PropertyType.IsPrimitive && !p.Property.PropertyType.IsEnum))
        .ToArray();
    /// <summary>An amending field (<see cref="AmendsTargetAttribute"/>) names what its target field names.</summary>
    private static IEnumerable<ContentReferenceAttribute> AmendedReferences(Type type, PropertyInfo property) =>
        property.IsDefined(typeof(AmendsTargetAttribute), true) && ContentAmendments.TargetTypeOf(type) is { } target
            && ContentAmendments.TargetField(target, property) is { } field
            ? field.GetCustomAttributes<ContentReferenceAttribute>() : [];
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
        if (value is ContentAttachment attachment) yield return new(attachment.Target, attachment.TargetType(), path + ".Target");
        if (value is LeafReference named)
        {
            if (kinds.TryGetValue(named.Kind, out var referenced)) yield return new(named.Id, referenced, path);
            yield break;
        }
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

    /// <summary>Every <typeparamref name="T"/> nested in a value (properties, sequences, map values), with its diagnostic
    /// path, in property-name and sequence order.</summary>
    public static IEnumerable<(T Value, string Path)> Find<T>(object? value, string path) where T : notnull =>
        Find<T>(value, path, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static IEnumerable<(T Value, string Path)> Find<T>(object? value, string path, HashSet<object> seen) where T : notnull
    {
        if (value is null or string or Requirement || value.GetType().IsPrimitive || value.GetType().IsEnum || !seen.Add(value)) yield break;
        if (value is T found) { yield return (found, path); yield break; }
        if (value is IEnumerable sequence)
        {
            int index = 0;
            foreach (var item in sequence)
                foreach (var nested in Find<T>(MapEntryValue(item), path + "[" + index++ + "]", seen)) yield return nested;
            yield break;
        }
        foreach (var metadata in Properties.GetValue(value.GetType(), DescribeProperties))
            foreach (var nested in Find<T>(metadata.Property.GetValue(value), path + "." + metadata.Property.Name, seen)) yield return nested;
    }

    /// <summary>Whether any <typeparamref name="T"/> nested in a value fails <paramref name="valid"/>. It walks what
    /// <see cref="Find{T}(object?, string)"/> walks, but builds no paths and descends only into members whose declared
    /// type can hold a <typeparamref name="T"/>, so it is cheap enough to run over all content. On a failure, call
    /// <see cref="Find{T}(object?, string)"/> to name the paths.</summary>
    public static bool AnyInvalid<T>(object? value, Func<T, bool> valid) where T : notnull =>
        AnyInvalid(value, valid, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static bool AnyInvalid<T>(object? value, Func<T, bool> valid, HashSet<object> seen) where T : notnull
    {
        if (value is null or string or Requirement) return false;
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || !seen.Add(value)) return false;
        if (value is T found) return !valid(found);
        if (value is IEnumerable sequence)
        {
            if (!Holders<T>.CanHold(type)) return false;
            foreach (var item in sequence)
                if (AnyInvalid(MapEntryValue(item), valid, seen)) return true;
            return false;
        }
        foreach (var property in Holders<T>.Members(type))
            if (AnyInvalid(property.GetValue(value), valid, seen)) return true;
        return false;
    }

    /// <summary>Which types can hold a <typeparamref name="T"/>, judged from declared types. A type whose instances may be
    /// a subtype (an unsealed class, an interface, <see cref="object"/>) can hold one; a sealed type or a struct can when a
    /// readable property or its sequence element can.</summary>
    private static class Holders<T>
    {
        private static readonly ConcurrentDictionary<Type, bool> Holds = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Plans = new();

        public static PropertyInfo[] Members(Type type) => Plans.GetOrAdd(type, static t => Readable(t).Where(p => CanHold(p.PropertyType)).ToArray());

        public static bool CanHold(Type type)
        {
            if (Holds.TryGetValue(type, out bool holds)) return holds;
            // A type that refers to itself counts as a holder while it is being judged; that only costs a descent.
            Holds[type] = true;
            return Holds[type] = Judge(type);
        }

        private static bool Judge(Type type)
        {
            if (typeof(T).IsAssignableFrom(type) || type.IsAssignableFrom(typeof(T))) return true;
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum || typeof(Requirement).IsAssignableFrom(type)) return false;
            if (!type.IsValueType && !type.IsSealed) return true;
            if (typeof(IEnumerable).IsAssignableFrom(type))
                return Element(type) is not { } element || CanHold(element);
            return Readable(type).Any(p => CanHold(p.PropertyType));
        }

        private static Type? Element(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            var sequence = type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            var element = sequence?.GetGenericArguments()[0];
            return element is { IsGenericType: true } pair && pair.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
                ? pair.GetGenericArguments()[1] : element;
        }

        private static IEnumerable<PropertyInfo> Readable(Type type) => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name, StringComparer.Ordinal);
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
