using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.ContentImport;
using Ariel.Modding;
using Xunit;

namespace Ariel.ContentImport.Tests;

public sealed class EraProgressionTests
{
    [SourceFact]
    [Trait("Category", "Extractor")]
    public void Every_era_slice_round_trips_through_ModLoader_with_nondecreasing_counts_closed_references_and_neutral_text()
    {
        var registry = new ImportRegistry(ExtractionLocation.Find()!);
        // The active profile starts at ancient, not prehistoric. Do not invent an era absent from the source.
        string[] eras = ["ancient", "classical", "medieval", "renaissance", "industrial", "modern", "transhuman", "future"];
        Assert.Equal(eras.Select(e => "ERA_" + e.ToUpperInvariant()), registry.EraOrder);
        var previousIds = new HashSet<string>(StringComparer.Ordinal);
        var previousCounts = new Dictionary<string, int>();
        foreach (var era in eras.Append(null))
        {
            var result = ContentEmission.Compile(registry, era);
            var ids = result.Definitions.Select(d => d.Definition.Id).ToHashSet(StringComparer.Ordinal);
            Assert.True(previousIds.IsSubsetOf(ids), $"Lifting the cap to {era ?? "all"} lost content");
            var counts = result.Definitions.GroupBy(d => d.Mapping.Definition).ToDictionary(g => g.Key, g => g.Count());
            foreach (var (family, count) in previousCounts)
                Assert.True(counts.GetValueOrDefault(family) >= count, $"{family} shrank at {era ?? "all"}");
            AssertEraCoverage(registry, result, eras, era, previousIds, ids, counts);
            AssertLoadsNeutralSidecars(result);
            previousIds = ids;
            previousCounts = counts;
        }
        Assert.Equal(previousIds.Order(StringComparer.Ordinal), ContentEmission.Compile(registry, "all").Definitions.Select(d => d.Definition.Id).Order(StringComparer.Ordinal));
        var canonical = ContentEmission.Compile(registry, "foundation:era_ancient");
        Assert.Equal(ContentEmission.Compile(registry, "ancient").Definitions.Select(d => d.Definition.Id), canonical.Definitions.Select(d => d.Definition.Id));
        Assert.Throws<ArgumentException>(() => ContentEmission.Compile(registry, "prehistoric"));
    }

    private static void AssertEraCoverage(ImportRegistry registry, EmissionResult result, string[] eras, string? era,
        HashSet<string> previousIds, HashSet<string> ids, Dictionary<string, int> counts)
    {
        if (era is null)
        {
            Assert.True(previousIds.SetEquals(ids), "Future must include the entire unfiltered extraction");
            Assert.Empty(result.Dropped);
            foreach (var mapping in SchemaCatalog.Definitions)
                Assert.Equal(registry.Records.Count(r => r.Class == mapping.SourceClass)
                    + (mapping.SourceClass == "UnitAIInfo" ? ContentMapper.NativeUnitAiRoles.Length : 0),
                    counts.GetValueOrDefault(mapping.Definition));
            return;
        }
        Assert.True(ids.Count > previousIds.Count, $"The {era} slice must add content");
        var allowed = eras.Take(Array.IndexOf(eras, era) + 1).Select(e => "foundation:era_" + e).ToHashSet();
        Assert.All(result.Definitions, d => Assert.True(d.Definition.Era is null || allowed.Contains(d.Definition.Era)));
    }

    private static void AssertLoadsNeutralSidecars(EmissionResult result)
    {
        using var output = new TemporaryDirectory();
        ContentEmission.Write(result, output.Path, Path.Combine(output.Path, "reports"));
        foreach (var file in Directory.GetFiles(output.Path, "*.json", SearchOption.AllDirectories).Where(p => p.Contains("/data/", StringComparison.Ordinal)))
        {
            using var payload = JsonDocument.Parse(File.ReadAllText(file));
            AssertNeutral(payload.RootElement);
        }
        var families = SchemaCatalog.Definitions.Concat(new[]
        {
                new DefinitionMapping("", "ConditionDef", "foundation", [], []),
                new DefinitionMapping("", "RuleConstantsDef", "foundation", [], [])
            }).ToArray();
        var mods = families.GroupBy(m => m.Mod).Select(group => new ModSource
        {
            Manifest = new ModManifest { Id = group.Key, References = families.Select(m => m.Mod).Distinct().Where(m => m != group.Key).ToArray() },
            Root = Path.Combine(output.Path, ContentEmission.DisplayMod(group.Key)),
            Instance = new EmittedDataMod(group.ToArray())
        }).ToArray();
        // Build validates annotated keys, values, nested effects, text links and all prerequisite leaf kinds.
        var db = ModLoader.Build(mods.Reverse());
        foreach (var group in result.Definitions.GroupBy(d => d.Definition.GetType()))
        {
            var get = typeof(ContentDb).GetMethods().Single(m => m.Name == "Get" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            var loaded = get.MakeGenericMethod(group.Key).Invoke(db, null)!;
            Assert.Equal(group.Count(), (int)loaded.GetType().GetProperty("Count")!.GetValue(loaded)!);
        }
    }

    private static void AssertNeutral(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject()) { AssertNeutralText(property.Name); AssertNeutral(property.Value); }
                break;
            case JsonValueKind.Array: foreach (var item in value.EnumerateArray()) AssertNeutral(item); break;
            case JsonValueKind.String: AssertNeutralText(value.GetString()!); break;
        }
    }

    private static void AssertNeutralText(string value)
    {
        Assert.DoesNotMatch(@"(?<![A-Za-z0-9])AND(?![A-Za-z0-9])|RoM", value);
        Assert.False(Regex.IsMatch(value, @"A[ _]+New[ _]+Dawn|Rise[ _]+of[ _]+Mankind|(?<![A-Za-z0-9])and(?=[_:/\\.-])", RegexOptions.IgnoreCase), value);
    }

    private sealed class EmittedDataMod(DefinitionMapping[] mappings) : IMod
    {
        public void Register(IModContext context)
        {
            foreach (var mapping in mappings)
                typeof(EmittedDataMod).GetMethod(nameof(Load), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(mapping.DefinitionType).Invoke(null, [context, mapping.Definition]);
            if (context.ModId == "foundation")
                foreach (var (kind, type) in ContentLeafKinds.Types)
                    context.RegisterService<IRequirementEvaluator>("leaf_" + kind, new IdentityEvaluator(kind, type));
        }

        private static void Load<T>(IModContext context, string definition) where T : GameContentDef
        {
            foreach (var record in context.Data.ReadJson<T[]>("data/" + ContentEmission.FileName(definition)))
                context.Content<T>().Register(record);
        }
    }

    private sealed class IdentityEvaluator(string kind, Type definitionType) : IContentRequirementEvaluator
    {
        public string Kind => kind;
        public Type DefinitionType => definitionType;
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context) => throw new NotSupportedException("Only loading is under test");
    }
}
