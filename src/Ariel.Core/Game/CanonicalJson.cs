using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ariel.Game;

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

    [ThreadStatic] private static ArrayBufferWriter<byte>? _serialized, _canonical;
    [ThreadStatic] private static bool _serializing, _busy;
    /// <summary>Larger reusable buffers are dropped after use, so one huge state does not pin them for good.</summary>
    private const int MaxKeptBuffer = 8 * 1024 * 1024;

    /// <summary>Serialize data and recursively sort object properties; reject non-integer numbers.
    /// A parsed <see cref="JsonElement"/> keeps the element path, because its escapes were not produced by this
    /// serializer and must be rewritten through the shared encoder.</summary>
    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        if (value is JsonElement element) return Normalize(element);
        if (value is JsonDocument document) return Normalize(document.RootElement);
        return Canonicalize(value, options ?? Options, Encoding.UTF8.GetString);
    }

    /// <summary>Normalize a JSON data tree.</summary>
    public static string Normalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Reject every non-integer number in a serialized save without building another document or
    /// string. Replaces the reserialize that used to cost a second canonical write of the whole save.</summary>
    public static void ValidateIntegers(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.Number) continue;
            if (reader.TryGetInt64(out _) || reader.TryGetUInt64(out _)) continue;
            throw new InvalidDataException("Deterministic data must contain only integer numbers");
        }
    }

    /// <summary>SHA-256 of canonical UTF-8 data.</summary>
    public static string Digest<T>(T value)
    {
        if (value is JsonElement element) return DigestElement(element);
        if (value is JsonDocument document) return DigestElement(document.RootElement);
        return Canonicalize(value, Options, Sha256Hex);
    }

    /// <summary>64-bit FNV-1a of canonical UTF-8 data, taken over the canonical bytes where they are written
    /// rather than over a decoded string and a second encoded copy.</summary>
    public static ulong Fnv1a64<T>(T value) => Canonicalize(value, Options, Fnv1a64);

    private static string Sha256Hex(ReadOnlySpan<byte> utf8) => Convert.ToHexString(SHA256.HashData(utf8));

    private static ulong Fnv1a64(ReadOnlySpan<byte> utf8)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in utf8) { hash ^= value; hash = unchecked(hash * 1099511628211UL); }
        return hash;
    }

    private static string DigestElement(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
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

    // ---- canonicalizer over serializer output ------------------------------------------------------------
    //
    // Serializer output already carries canonical scalar bytes: property order is the only thing left to fix, and
    // the serializer and Utf8JsonWriter share the same encoder, so a scalar's raw bytes are what re-encoding it
    // would produce. That lets one pass copy scalars verbatim and only reorder each object's properties, instead
    // of materialising a JsonElement tree and writing it a second time.

    /// <summary>Serialize into a reused buffer, so a large state allocates no fresh copy of its JSON each time,
    /// then canonicalize it.</summary>
    private static TResult Canonicalize<T, TResult>(T value, JsonSerializerOptions options, Func<ReadOnlySpan<byte>, TResult> finish)
    {
        // A converter that canonicalizes while the outer value is being serialized must not reuse its buffer.
        if (_serializing) return Canonicalize(JsonSerializer.SerializeToUtf8Bytes(value, options), finish);
        _serializing = true;
        var input = _serialized ??= new ArrayBufferWriter<byte>(4096);
        try
        {
            input.Clear();
            using (var writer = new Utf8JsonWriter(input)) JsonSerializer.Serialize(writer, value, options);
            return Canonicalize(input.WrittenSpan, finish);
        }
        finally
        {
            _serializing = false;
            if (input.Capacity > MaxKeptBuffer) _serialized = null;
        }
    }

    /// <summary>Canonicalize serializer output and hand the canonical bytes to <paramref name="finish"/>, which
    /// turns them into a string or a hash without staging a second copy.</summary>
    private static TResult Canonicalize<TResult>(ReadOnlySpan<byte> utf8, Func<ReadOnlySpan<byte>, TResult> finish)
    {
        // A nested call (a converter that itself canonicalizes) must not reuse the buffer an outer call is filling.
        if (_busy)
        {
            var scratch = new ArrayBufferWriter<byte>(utf8.Length + 64);
            WriteCanonical(utf8, scratch);
            return finish(scratch.WrittenSpan);
        }
        _busy = true;
        try
        {
            var output = _canonical ??= new ArrayBufferWriter<byte>(4096);
            output.Clear();
            WriteCanonical(utf8, output);
            return finish(output.WrittenSpan);
        }
        finally
        {
            _busy = false;
            if (_canonical.Capacity > MaxKeptBuffer) _canonical = null;
        }
    }

    private struct Property
    {
        public int NameStart, NameLength, Offset, Length;
        /// <summary>Ordinal-comparable prefix of the name, so the sort compares a ulong instead of bytes.</summary>
        public ulong Key;
        /// <summary>Decoded name, set only when the raw bytes are escaped or non-ASCII.</summary>
        public string? Name;
    }

    /// <summary>The first seven name bytes, zero padded: a short name's padding sorts it before any longer name that
    /// shares its prefix, and names that agree on seven bytes fall back to the full comparison.</summary>
    private static void MeasureName(ReadOnlySpan<byte> json, ref Property property)
    {
        ulong packed = 0;
        bool escaped = false;
        for (int i = 0; i < property.NameLength; i++)
        {
            byte b = json[property.NameStart + i];
            // Escaped and non-ASCII names are sorted by their decoded text, because the escape bytes order
            // differently from the characters behind them.
            if (b >= 0x80 || b == (byte)'\\') escaped = true;
            if (i < 7) packed |= (ulong)b << (56 - i * 8);
        }
        property.Key = packed;
        if (escaped) property.Name = DecodeName(json, property.NameStart, property.NameLength);
    }

    private static string DecodeName(ReadOnlySpan<byte> json, int start, int length)
    {
        var quoted = new byte[length + 2];
        quoted[0] = (byte)'"';
        json.Slice(start, length).CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        using var document = JsonDocument.Parse(quoted);
        return document.RootElement.GetString() ?? throw new InvalidDataException("Property name is not a string");
    }

    private static void WriteCanonical(ReadOnlySpan<byte> json, ArrayBufferWriter<byte> output)
    {
        var reader = new Utf8JsonReader(json);
        reader.Read();
        WriteValue(json, ref reader, output, depth: 0);
    }

    private static void WriteValue(ReadOnlySpan<byte> json, ref Utf8JsonReader reader, ArrayBufferWriter<byte> output, int depth)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject: WriteObject(json, ref reader, output, depth); break;
            case JsonTokenType.StartArray: WriteArray(json, ref reader, output, depth); break;
            case JsonTokenType.String: AppendRawString(json, ref reader, output); break;
            case JsonTokenType.Number:
                if (!reader.TryGetInt64(out _) && !reader.TryGetUInt64(out _))
                    throw new InvalidDataException("Deterministic data must contain only integer numbers");
                output.Write(json.Slice((int)reader.TokenStartIndex, (int)(reader.BytesConsumed - reader.TokenStartIndex)));
                break;
            case JsonTokenType.True: output.Write("true"u8); break;
            case JsonTokenType.False: output.Write("false"u8); break;
            case JsonTokenType.Null: output.Write("null"u8); break;
            default: throw new InvalidDataException($"Unexpected JSON token {reader.TokenType}");
        }
    }

    private static void WriteObject(ReadOnlySpan<byte> json, ref Utf8JsonReader reader, ArrayBufferWriter<byte> output, int depth)
    {
        // Property values must be buffered until the names are sorted. The buffer belongs to this nesting
        // level only: arrays emit their elements one after another, so sibling objects share the level below.
        var values = RentScratch();
        var properties = ArrayPool<Property>.Shared.Rent(16);
        try
        {
            int count = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (count == properties.Length) Grow(ref properties, count);
                var property = new Property
                {
                    NameStart = (int)reader.TokenStartIndex + 1,
                    NameLength = reader.ValueSpan.Length,
                    Offset = values.WrittenCount,
                };
                MeasureName(json, ref property);
                reader.Read();
                WriteValue(json, ref reader, values, depth + 1);
                property.Length = values.WrittenCount - property.Offset;
                properties[count++] = property;
            }
            SortByName(json, properties, count);
            output.Write("{"u8);
            var buffered = values.WrittenSpan;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) output.Write(","u8);
                output.Write("\""u8);
                output.Write(json.Slice(properties[i].NameStart, properties[i].NameLength));
                output.Write("\":"u8);
                output.Write(buffered.Slice(properties[i].Offset, properties[i].Length));
            }
            output.Write("}"u8);
            Array.Clear(properties, 0, count);
        }
        finally
        {
            ArrayPool<Property>.Shared.Return(properties);
            ReturnScratch(values);
        }
    }

    private static void WriteArray(ReadOnlySpan<byte> json, ref Utf8JsonReader reader, ArrayBufferWriter<byte> output, int depth)
    {
        output.Write("["u8);
        bool first = true;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) break;
            if (!first) output.Write(","u8);
            first = false;
            WriteValue(json, ref reader, output, depth);
        }
        output.Write("]"u8);
    }

    private static void AppendRawString(ReadOnlySpan<byte> json, ref Utf8JsonReader reader, ArrayBufferWriter<byte> output)
    {
        output.Write("\""u8);
        output.Write(json.Slice((int)reader.TokenStartIndex + 1, reader.ValueSpan.Length));
        output.Write("\""u8);
    }

    [ThreadStatic] private static Stack<ArrayBufferWriter<byte>>? _scratches;

    private static ArrayBufferWriter<byte> RentScratch()
    {
        var pool = _scratches ??= new Stack<ArrayBufferWriter<byte>>();
        if (pool.Count == 0) return new ArrayBufferWriter<byte>(256);
        var buffer = pool.Pop();
        buffer.Clear();
        return buffer;
    }

    private static void ReturnScratch(ArrayBufferWriter<byte> buffer)
    {
        if (buffer.Capacity > MaxKeptBuffer) return; // One huge state must not pin its buffer forever.
        _scratches!.Push(buffer);
    }
    private static void Grow(ref Property[] properties, int count)
    {
        var larger = ArrayPool<Property>.Shared.Rent(count * 2);
        Array.Copy(properties, larger, count);
        ArrayPool<Property>.Shared.Return(properties);
        properties = larger;
    }

    /// <summary>Insertion sort: stable, so duplicate names keep their order, and object shapes are small.</summary>
    private static void SortByName(ReadOnlySpan<byte> json, Property[] properties, int count)
    {
        for (int i = 1; i < count; i++)
        {
            var property = properties[i];
            int j = i - 1;
            while (j >= 0 && CompareNames(json, properties[j], property) > 0)
            {
                properties[j + 1] = properties[j];
                j--;
            }
            properties[j + 1] = property;
        }
    }

    private static int CompareNames(ReadOnlySpan<byte> json, in Property left, in Property right)
    {
        // UTF-8 byte order is ordinal string order for plain ASCII names, which is every name the shipped content
        // uses; escaped or non-ASCII names compare their decoded text.
        if (left.Name is null && right.Name is null)
        {
            if (left.Key != right.Key) return left.Key < right.Key ? -1 : 1;
            return json.Slice(left.NameStart, left.NameLength).SequenceCompareTo(json.Slice(right.NameStart, right.NameLength));
        }
        return string.CompareOrdinal(NameOf(json, left), NameOf(json, right));
    }

    private static string NameOf(ReadOnlySpan<byte> json, in Property property) =>
        property.Name ?? Encoding.UTF8.GetString(json.Slice(property.NameStart, property.NameLength));
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
    /// <summary>Deep copy every attached component through its own JSON contract, as a save round trip would,
    /// without normalizing the whole save.</summary>
    public ExtensionState Clone()
    {
        var copy = new ExtensionState();
        foreach (var (id, component) in Values)
        {
            var type = component.GetType();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(component, type, CanonicalJson.Options);
            copy.Values.Add(id, JsonSerializer.Deserialize(bytes, type, CanonicalJson.Options)
                ?? throw new InvalidDataException($"Empty state component '{id}'"));
        }
        return copy;
    }
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
