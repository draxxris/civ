using System.Reflection;
using System.Text.Json.Serialization;

namespace Civ.Content;

// Reject floating-point component schemas at registration, before they can enter authoritative state.
internal static class StateSchema
{
    internal static void Validate(Type type) => Visit(type, new HashSet<Type>());
    private static void Visit(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type)) return;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            throw new InvalidOperationException($"State component contains non-integer numeric type '{type.Name}'");
        if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return;
        if (Nullable.GetUnderlyingType(type) is { } inner) { Visit(inner, seen); return; }
        if (ElementType(type) is { } element) { Visit(element, seen); return; }
        VisitMembers(type, seen);
    }
    private static void VisitMembers(Type type, HashSet<Type> seen)
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            if (IsSerialized(property)) Visit(property.PropertyType, seen);
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (field.GetCustomAttribute<JsonIncludeAttribute>() != null) Visit(field.FieldType, seen);
    }
    private static Type? ElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        var enumerable = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }
    private static bool IsSerialized(PropertyInfo property) => property.CanRead && property.GetIndexParameters().Length == 0
        && property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always;
}
