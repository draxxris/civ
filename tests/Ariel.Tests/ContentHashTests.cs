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

        // SHA-256 of the protocol fixture: runtime converter data, mod version, core commands, empty extensions.
        // The probe type's namespace is part of the digest, so the probe's own name appears here; the value below was
        // the pre-rename one and was recomputed once when the namespaces became Ariel.
        Assert.Equal("A58A22535EC2BF3C5CDB78BA0418CC8A0DBAE2CAF8D47FFB297BAE95EC15C823", content.ContentHash);
        // The same fixture's digest as the pre-rename build computed it. Pinning the legacy digest here, rather
        // than only comparing it with the current one, is what catches a digest input that was not mapped back:
        // a forgotten mapping still yields a different, self-consistent legacy digest that no old save matches.
        // Both values changed again when core:set_player_control joined the built-in commands.
        Assert.Equal("2968C767D33A71934AB1047C65931A3AE880C8A00FEC02CE79BE369BF49C3E11", content.LegacyContentHash);
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
