using System.Text.Json;
using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

public sealed class ContentHashTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContentHashUsesIsolatedRuntimeConvertersAndOrdinalDefinitionOrder(bool reverse)
    {
        ContentDef[] definitions = [
            new WireDef { Id = "probe:a", Amount = long.MinValue },
            new WireDef { Id = "probe:z", Amount = long.MaxValue },
        ];
        if (reverse) Array.Reverse(definitions);
        var content = ModLoader.Build([new ModSource
        {
            Manifest = new ModManifest { Id = "probe", Version = "7" },
            Root = AppContext.BaseDirectory,
            Instance = new ProbeMod(definitions),
        }]);

        // SHA-256 of the protocol fixture: runtime converter data, mod version, core command, empty extensions.
        // The probe type's namespace is part of the digest, so the probe's own name appears here; the value below was
        // the pre-rename one and was recomputed once when the namespaces became Ariel.
        Assert.Equal("5B7DBC7A0C35382B87131E9367E9D253F65BEA742E6E07618CFC3ABCC9F0CAA5", content.ContentHash);
    }

    private sealed class ProbeMod(ContentDef[] definitions) : IMod
    {
        public void Register(IModContext context)
        {
            foreach (var definition in definitions) context.Content<ContentDef>().Register(definition);
        }
    }

    [JsonConverter(typeof(WireDefConverter))]
    private sealed class WireDef : ContentDef
    {
        public long Amount { get; init; }
    }

    private sealed class WireDefConverter : JsonConverter<WireDef>
    {
        public override WireDef Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, WireDef value, JsonSerializerOptions options)
        {
            int depth = writer.CurrentDepth;
            writer.WriteStartObject();
            writer.WriteNumber("depth", depth);
            writer.WriteNumber("z", value.Amount);
            writer.WriteString("a", value.Id);
            writer.WriteEndObject();
        }
    }
}
