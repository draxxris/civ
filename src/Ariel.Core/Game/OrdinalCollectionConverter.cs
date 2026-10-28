using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ariel.Game;

// String-sorted collections must retain their comparer across JSON loading, including mod components.
internal sealed class OrdinalCollectionConverter : JsonConverterFactory
{
    public override bool CanConvert(Type type)
    {
        if (!type.IsGenericType || type.GetGenericArguments()[0] != typeof(string)) return false;
        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(SortedSet<>) || definition == typeof(SortedDictionary<,>);
    }
    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
    {
        if (type == typeof(SortedSet<string>)) return new StringSetConverter();
        var converter = typeof(StringDictionaryConverter<>).MakeGenericType(type.GetGenericArguments()[1]);
        return (JsonConverter)Activator.CreateInstance(converter)!;
    }
    private sealed class StringSetConverter : JsonConverter<SortedSet<string>>
    {
        public override SortedSet<string> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<string[]>(ref reader, options) ?? throw new JsonException("Empty string set"), StringComparer.Ordinal);
        public override void Write(Utf8JsonWriter writer, SortedSet<string> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Order(StringComparer.Ordinal).ToArray(), options);
    }
    private sealed class StringDictionaryConverter<T> : JsonConverter<SortedDictionary<string, T>>
    {
        public override SortedDictionary<string, T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<Dictionary<string, T>>(ref reader, options) ?? throw new JsonException("Empty string dictionary"), StringComparer.Ordinal);
        public override void Write(Utf8JsonWriter writer, SortedDictionary<string, T> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var (key, item) in value.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, item, options);
            }
            writer.WriteEndObject();
        }
    }
}
