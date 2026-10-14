using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Civ.Game;

/// <summary>Canonical JSON: ordinal object keys, unchanged array order, integer numeric values only.
/// Mod state must be JSON data (no floats, unordered sets, cycles, runtime handles, or computed properties).</summary>
public static class CanonicalJson
{
    /// <summary>Shared save/state serializer options. Runtime indexes and derived properties are ignored.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new ExtensionStateConverter(), new OrdinalCollectionConverter() },
    };
    /// <summary>Serialize data and recursively sort object properties; reject non-integer numbers.</summary>
    public static string Serialize<T>(T value, JsonSerializerOptions? options = null) => Normalize(JsonSerializer.SerializeToElement(value, options ?? Options));
    /// <summary>Normalize a JSON data tree.</summary>
    public static string Normalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(writer, p.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var e in element.EnumerateArray()) Write(writer, e); writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var signed)) writer.WriteNumberValue(signed);
                else if (element.TryGetUInt64(out var unsigned)) writer.WriteNumberValue(unsigned);
                else throw new InvalidDataException("Deterministic data must contain only integer numbers");
                break;
            default: element.WriteTo(writer); break;
        }
    }
    /// <summary>SHA-256 of canonical UTF-8 data.</summary>
    public static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value))));
}

/// <summary>Typed mod state keyed by registered component id. Mutable instances stay attached by reference.</summary>
[JsonConverter(typeof(ExtensionStateConverter))]
public sealed class ExtensionState
{
    internal SortedDictionary<string, object> Values { get; } = new(StringComparer.Ordinal);
    /// <summary>Attach or replace a registered state instance.</summary>
    public void Set<T>(Content.ContentDb content, T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.GetType() != typeof(T)) throw new ArgumentException("State components must use their exact registered type", nameof(instance));
        Values[content.StateComponents.IdOf(typeof(T))] = instance;
    }
    /// <summary>Get a component by its registered type, or null when absent.</summary>
    public T? Get<T>(Content.ContentDb content) where T : class => Values.GetValueOrDefault(content.StateComponents.IdOf(typeof(T))) as T;
    /// <summary>Get a component by its registered type, or null when the type is unregistered or the component is absent. Never throws for unregistered types.</summary>
    public T? TryGet<T>(Content.ContentDb content) where T : class =>
        content.StateComponents.TryIdOf(typeof(T)) is { } id ? Values.GetValueOrDefault(id) as T : null;
    /// <summary>Remove a registered component.</summary>
    public bool Remove<T>(Content.ContentDb content) where T : class => Values.Remove(content.StateComponents.IdOf(typeof(T)));
}

/// <summary>Serializes extension state as {component-id: data}. Reads require a content-aware converter.</summary>
public sealed class ExtensionStateConverter : JsonConverter<ExtensionState>
{
    private readonly Content.StateComponentRegistry? _registry;
    /// <summary>Write-only converter for hashes.</summary>
    public ExtensionStateConverter() { }
    /// <summary>Content-aware converter for loading registered state types.</summary>
    public ExtensionStateConverter(Content.StateComponentRegistry registry) => _registry = registry;
    /// <inheritdoc/>
    public override ExtensionState Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var state = new ExtensionState();
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            var componentType = _registry?.TypeOf(p.Name) ?? throw new InvalidDataException($"State component '{p.Name}' has no registered type");
            state.Values.Add(p.Name, p.Value.Deserialize(componentType, options) ?? throw new InvalidDataException($"Empty state component '{p.Name}'"));
        }
        return state;
    }
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ExtensionState value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (id, component) in value.Values) { writer.WritePropertyName(id); JsonSerializer.Serialize(writer, component, component.GetType(), options); }
        writer.WriteEndObject();
    }
}
