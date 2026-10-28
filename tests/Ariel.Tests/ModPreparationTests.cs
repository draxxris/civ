using System.Text.Json;
using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

public sealed class ModPreparationTests
{
    [Fact]
    public void SidecarsParseInParallelButRegisterRunsSeriallyInDependencyOrder()
    {
        using var files = new Sidecars();
        files.Write("provider.json", "{\"name\":\"Original\"}");
        files.Write("consumer.json", "{\"name\":\"Override\"}");
        using var reads = new CountdownEvent(Math.Min(Environment.ProcessorCount, 2));
        var previous = ObservedSidecar.Reads.Value;
        ObservedSidecar.Reads.Value = reads;
        try
        {
            var calls = new List<string>();
            var providerFile = new ModJsonFile<ObservedSidecar>("provider.json");
            var consumerFile = new ModJsonFile<ObservedSidecar>("consumer.json");
            var provider = files.Source("z", new PreparedMod([providerFile], ctx =>
            {
                Assert.True(reads.IsSet); // All workers reached deserialization before any registration.
                calls.Add("z");
                ctx.Terrains.Register(new TerrainDef { Id = "z:tile", Name = providerFile.Read(ctx.Data).Name });
            }));
            var consumer = files.Source("a", new PreparedMod([consumerFile], ctx =>
            {
                Assert.Equal(["z"], calls);
                Assert.Equal("Original", ctx.Terrains.Get("z:tile").Name);
                calls.Add("a");
                ctx.Terrains.Override(new TerrainDef { Id = "z:tile", Name = consumerFile.Read(ctx.Data).Name });
            }), "z");

            var db = ModLoader.Build([consumer, provider]);

            Assert.Equal(["z", "a"], calls);
            Assert.Equal(["z", "a"], db.LoadedMods);
            Assert.Equal("Override", db.Terrains.Get("z:tile").Name);
            Assert.Equal("a", db.Terrains.OwnerOf("z:tile"));
        }
        finally { ObservedSidecar.Reads.Value = previous; }
    }

    [Fact]
    public async Task ReusingSourcesDoesNotShareMutablePreparedValuesAcrossBuildsOrCacheFileEdits()
    {
        using var files = new Sidecars();
        files.Write("numbers.json", "[4]");
        var numbers = new ModJsonFile<List<int>>("numbers.json");
        var mod = files.Source("probe", new PreparedMod([numbers, numbers], ctx =>
        {
            var values = numbers.Read(ctx.Data);
            values.Add(99);
            // The descriptor and ordinary ReadJson share one value within this build only.
            ctx.Terrains.Register(new TerrainDef
            {
                Id = "probe:tile", Food = ctx.Data.ReadJson<List<int>>(numbers.Path).Count,
                Production = values[0],
            });
        }));

        var first = ModLoader.Build([mod]);
        var repeated = ModLoader.Build([mod]);
        Assert.Equal(2, first.Terrains.Get("probe:tile").Food);
        Assert.Equal(2, repeated.Terrains.Get("probe:tile").Food);
        Assert.Equal(first.ContentHash, repeated.ContentHash);

        files.Write("numbers.json", "[7,8]");
        var rebuilt = await Task.WhenAll(
            Task.Run(() => ModLoader.Build([mod])), Task.Run(() => ModLoader.Build([mod])));
        foreach (var db in rebuilt)
        {
            Assert.Equal(3, db.Terrains.Get("probe:tile").Food);
            Assert.Equal(7, db.Terrains.Get("probe:tile").Production);
            Assert.NotEqual(first.ContentHash, db.ContentHash);
        }
        Assert.NotSame(rebuilt[0].Terrains.Get("probe:tile"), rebuilt[1].Terrains.Get("probe:tile"));
        Assert.Equal(rebuilt[0].ContentHash, rebuilt[1].ContentHash);
        Assert.Equal(2, first.Terrains.Get("probe:tile").Food);
        Assert.Equal(4, first.Terrains.Get("probe:tile").Production);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyAndPreparedModsKeepTypedFallbackAndOtherDataAccess(bool prepare)
    {
        using var files = new Sidecars();
        files.Write("numbers.json", "[7]");
        files.Write("terrain.json", "// sidecar comment\n{\"iD\":\"probe:tile\",\"nAmE\":\"Unlisted\"}");
        files.Write("note.txt", "plain text");
        var numbers = new ModJsonFile<List<int>>("numbers.json");
        Action<IModContext> register = ctx =>
        {
            Assert.Equal(7, Assert.Single(numbers.Read(ctx.Data)));
            // A prepared path with a different result type must use the ordinary reader.
            Assert.Equal(7, ctx.Data.ReadJson<JsonElement>(numbers.Path)[0].GetInt32());
            Assert.True(ctx.Data.Exists(numbers.Path));
            Assert.False(ctx.Data.Exists("absent.json"));
            Assert.Equal("plain text", ctx.Data.ReadText("note.txt"));
            ctx.Terrains.Register(ctx.Data.ReadJson<TerrainDef>("terrain.json"));
        };
        IMod instance = prepare ? new PreparedMod([numbers], register) : new LegacyMod(register);

        var db = ModLoader.Build([files.Source("probe", instance)]);

        Assert.Equal("Unlisted", db.Terrains.Get("probe:tile").Name);
    }

    [Theory]
    [InlineData("{]", null, typeof(JsonException))]
    [InlineData(null, "{]", typeof(FileNotFoundException))]
    [InlineData("null", "{]", typeof(InvalidDataException))]
    public void PreparedFailuresMatchLegacyAtTheFirstReadNotTheFirstInventoryEntry(
        string? firstContents, string? secondContents, Type expectedType)
    {
        using var files = new Sidecars();
        if (firstContents != null) files.Write("read-first.json", firstContents);
        if (secondContents != null) files.Write("inventory-first.json", secondContents);
        var first = new ModJsonFile<List<int>>("read-first.json");
        var second = new ModJsonFile<List<int>>("inventory-first.json");
        Action<IModContext> register = ctx =>
        {
            ctx.Data.ReadJson<List<int>>(first.Path);
            second.Read(ctx.Data);
        };

        var legacy = Assert.Throws(expectedType, () => ModLoader.Build([
            files.Source("probe", new LegacyMod(register))]));
        var prepared = Assert.Throws(expectedType, () => ModLoader.Build([
            files.Source("probe", new PreparedMod([second, first], register))]));

        Assert.Equal(legacy.Message, prepared.Message);
        if (legacy is JsonException json)
        {
            var actual = Assert.IsType<JsonException>(prepared);
            Assert.Equal(json.Path, actual.Path);
            Assert.Equal(json.LineNumber, actual.LineNumber);
            Assert.Equal(json.BytePositionInLine, actual.BytePositionInLine);
        }
        if (legacy is FileNotFoundException missing)
            Assert.Equal(missing.FileName, Assert.IsType<FileNotFoundException>(prepared).FileName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparationFailuresDoNotMaskEarlierRegistrationErrors(bool sameMod)
    {
        using var files = new Sidecars();
        files.Write("bad.json", "{]");
        ModJsonFile[] inventory = [new ModJsonFile<List<int>>("missing.json"), new ModJsonFile<List<int>>("bad.json")];
        var calls = new List<string>();
        Action<IModContext> fail = ctx =>
        {
            calls.Add(ctx.ModId);
            ctx.Terrains.Register(new TerrainDef { Id = "wrong:namespace" });
        };
        Action<IModContext> read = ctx =>
        {
            if (sameMod) fail(ctx);
            else calls.Add(ctx.ModId);
            ctx.Data.ReadJson<List<int>>("bad.json");
        };
        ModSource[] Sources(IMod reader) => sameMod
            ? [files.Source("a", reader)]
            : [files.Source("b", reader, "a"), files.Source("a", new LegacyMod(fail))];

        var legacy = Assert.Throws<ArgumentException>(() => ModLoader.Build(Sources(new LegacyMod(read))));
        calls.Clear();
        var prepared = Assert.Throws<ArgumentException>(() => ModLoader.Build(Sources(new PreparedMod(inventory, read))));

        Assert.Equal(legacy.Message, prepared.Message);
        Assert.Equal(["a"], calls);
    }

    // ExecutionContext carries this test-local rendezvous into loader workers; no shared JSON options change.
    // On single-core hosts one worker is allowed. The timeout is a deadlock guard, not a speed assertion.
    public sealed class ObservedSidecar
    {
        internal static readonly AsyncLocal<CountdownEvent?> Reads = new();
        public string Name { get; }

        [JsonConstructor]
        public ObservedSidecar(string name)
        {
            Name = name;
            var reads = Reads.Value ?? throw new InvalidOperationException("Missing read probe");
            if (!reads.IsSet) reads.Signal();
            if (!reads.Wait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("Sidecar reads did not overlap");
        }
    }

    private sealed class LegacyMod(Action<IModContext> register) : IMod
    {
        public void Register(IModContext context) => register(context);
    }

    private sealed class PreparedMod(IReadOnlyList<ModJsonFile> files, Action<IModContext> register) : IPreparableMod
    {
        public IReadOnlyList<ModJsonFile> PreparationFiles => files;
        public void Register(IModContext context) => register(context);
    }

    private sealed class Sidecars : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("civ-preparation-").FullName;
        public void Write(string path, string contents) => File.WriteAllText(Path.Combine(_root, path), contents);
        public ModSource Source(string id, IMod instance, params string[] dependencies) => new()
        {
            Manifest = new ModManifest { Id = id, Dependencies = dependencies },
            Instance = instance, Root = _root,
        };
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
