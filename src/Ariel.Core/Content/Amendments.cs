using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Alternatives = System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableArray<Ariel.Content.Requirement>>;

namespace Ariel.Content;

/// <summary>Merges attachment fields marked <see cref="AmendsTargetAttribute"/> into their targets when content
/// freezes.</summary>
internal static class ContentAmendments
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> FieldsByType = new();
    private static readonly MethodInfo AppendMethod = typeof(ContentAmendments).GetMethod(nameof(Append), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo MergeMapMethod = typeof(ContentAmendments).GetMethod(nameof(MergeMap), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>The amending fields of a definition type, by name; empty for most types.</summary>
    internal static PropertyInfo[] Fields(Type type) => FieldsByType.GetOrAdd(type, static t => typeof(ContentAttachment).IsAssignableFrom(t)
        ? t.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.IsDefined(typeof(AmendsTargetAttribute), true))
            .OrderBy(p => p.Name, StringComparer.Ordinal).ToArray()
        : []);

    /// <summary>The target type's field an amending field adds to, or null when the target type has none of that name.</summary>
    internal static PropertyInfo? TargetField(Type targetType, PropertyInfo field) =>
        targetType.GetProperty(field.GetCustomAttribute<AmendsTargetAttribute>(true)?.Field ?? field.Name, BindingFlags.Instance | BindingFlags.Public);

    /// <summary>The definition type an attachment type is about, from its <see cref="ContentAttachment{TTarget}"/> base.</summary>
    internal static Type? TargetTypeOf(Type attachmentType)
    {
        for (var type = attachmentType; type is not null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ContentAttachment<>)) return type.GetGenericArguments()[0];
        return null;
    }

    /// <summary>A copy of <paramref name="target"/> with <paramref name="row"/>'s amending fields added to it.</summary>
    internal static ContentDef Amend(ContentDef target, ContentAttachment row, CommerceVector zero, SortedSet<string> errors)
    {
        var copy = target.ShallowCopy();
        foreach (var field in Fields(row.GetType()))
        {
            var into = TargetField(copy.GetType(), field);
            bool alternatives = into?.PropertyType == typeof(Requirement) && field.PropertyType == typeof(Alternatives);
            if (into is null || into.PropertyType != field.PropertyType && !alternatives || into.SetMethod is null)
            {
                errors.Add($"{row.Id}.{field.Name}: {copy.GetType().Name} '{target.Id}' has no field {into?.Name ?? field.Name} of type {field.PropertyType.Name} to amend");
                continue;
            }
            var addition = field.GetValue(row);
            if (addition is null || IsEmpty(addition)) continue;
            var current = into.GetValue(copy);
            try
            {
                into.SetValue(copy, alternatives ? AddAlternatives((Requirement?)current, (Alternatives)addition, $"{row.Id}.{field.Name}")
                    : current is null ? addition : Combine(field.PropertyType, current, addition, zero, $"{row.Id}.{field.Name}"));
            }
            catch (InvalidDataException e) { errors.Add(e.Message); }
        }
        return copy;
    }

    /// <summary>Whether a field adds nothing: an empty dictionary, the common case for rows that fill one or two of
    /// their fields. Arrays go to <see cref="Append{T}"/>, which also handles a default array, and a vector always
    /// amends, so the target's vector carries every loaded type even when the row's amounts are all zero.</summary>
    private static bool IsEmpty(object addition) => addition is System.Collections.IDictionary { Count: 0 };

    private static object Combine(Type type, object current, object addition, CommerceVector zero, string path)
    {
        if (type == typeof(CommerceVector)) return zero + (CommerceVector)current + (CommerceVector)addition;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
            return AppendMethod.MakeGenericMethod(type.GetGenericArguments()).Invoke(null, [current, addition])!;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
        {
            try { return MergeMapMethod.MakeGenericMethod(type.GetGenericArguments()).Invoke(null, [current, addition, zero, path])!; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidDataException inner) { throw inner; }
        }
        throw new InvalidDataException($"{path}: a field of type {type.Name} cannot amend its target");
    }

    /// <summary>The target's requirements with each key's requirements added to the one <c>any</c> group that already
    /// holds a leaf of that id, keys in ordinal order. The tree is rebuilt, never changed in place.</summary>
    private static Requirement AddAlternatives(Requirement? current, Alternatives addition, string path)
    {
        var result = current ?? throw new InvalidDataException($"{path}: the target has no requirements to add alternatives to");
        foreach (var (beside, items) in addition.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            int groups = 0;
            result = Add(result);
            if (groups != 1)
                throw new InvalidDataException($"{path}[{beside}]: {(groups == 0 ? "no" : groups.ToString(System.Globalization.CultureInfo.InvariantCulture))} 'any' group of the target holds the alternative '{beside}'; exactly one must");

            Requirement Add(Requirement node) => node switch
            {
                Requirement.Any any when any.Items.Any(i => i is Requirement.Leaf leaf && leaf.Id == beside) =>
                    Counted(new Requirement.Any([.. any.Items.Select(Add), .. items])),
                Requirement.Any any => new Requirement.Any([.. any.Items.Select(Add)]),
                Requirement.All all => new Requirement.All([.. all.Items.Select(Add)]),
                _ => node,
            };
            Requirement Counted(Requirement group) { groups++; return group; }
        }
        return result;
    }

    private static bool Combines(Type type) => type == typeof(CommerceVector)
        || type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(ImmutableArray<>) || type.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>));

    private static ImmutableArray<T> Append<T>(ImmutableArray<T> current, ImmutableArray<T> addition) =>
        current.IsDefault ? addition : addition.IsDefault ? current : current.AddRange(addition);

    private static ImmutableDictionary<TKey, TValue> MergeMap<TKey, TValue>(ImmutableDictionary<TKey, TValue> current,
        ImmutableDictionary<TKey, TValue> addition, CommerceVector zero, string path) where TKey : notnull
    {
        var result = current;
        foreach (var (key, value) in addition)
        {
            if (!result.TryGetValue(key, out var existing)) { result = result.Add(key, value); continue; }
            if (existing is null || value is null || !Combines(typeof(TValue)))
                throw new InvalidDataException($"{path}: '{key}' is already in the target");
            result = result.SetItem(key, (TValue)Combine(typeof(TValue), existing, value, zero, $"{path}[{key}]"));
        }
        return result;
    }
}
