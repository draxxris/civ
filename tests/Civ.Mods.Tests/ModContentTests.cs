using System.Text.Json;
using System.Text.RegularExpressions;
using Civ.Content;
using Civ.Modding;
using Xunit;
using Schema = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class LoadedMods
{
    public string Root { get; } = Path.Combine(AppContext.BaseDirectory, "mods");
    public ModSource[] Sources { get; }
    public ContentDb Content { get; }
    // One catalog per xUnit class fixture: asset lookup caches are mutable, unlike frozen content.
    private readonly Lazy<Civ.Client.GameCatalog> _catalog;
    public Civ.Client.GameCatalog Catalog => _catalog.Value;
    private static readonly Lazy<(ModSource[] Sources, ContentDb Content)> Shared = new(() =>
    {
        var root = Path.Combine(AppContext.BaseDirectory, "mods");
        var sources = Directory.GetDirectories(root).Order(StringComparer.Ordinal).Select(ModLoader.FromDirectory).ToArray();
        return (sources, ModLoader.Build(sources.AsEnumerable().Reverse()));
    });
    public LoadedMods()
    {
        (Sources, Content) = Shared.Value;
        _catalog = new(() => new(Sources));
    }
}

/// <summary>Tests the shipped DLL/manifest/sidecar boundary, without a local extraction dependency.</summary>
public sealed class ModContentTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void All_mod_outputs_load_with_reference_closure_and_a_stable_content_hash()
    {
        // This public pass checks typed map keys, map values, nested records, text links and requirement leaves.
        mods.Content.ValidateReferences(mods.Sources.Select(s => s.Manifest));
        Assert.Equal(mods.Content.ContentHash, ModLoader.Build(mods.Sources).ContentHash);
    }

    [Fact]
    public void Game_option_requirements_have_typed_reference_closure_and_policy_options_are_listed()
    {
        var evaluator = mods.Content.Services.Get<IRequirementEvaluator>().Single(e => e.Kind == "game-option");
        var culture = new Schema.CultureLevelDef
        {
            Id = "option_probe:level",
            Requirements = new Requirement.Leaf("game-option", "foundation:gameoption_realistic_culture_spread"),
        };
        var reference = Assert.Single(ContentReferences.Enumerate(culture, [evaluator]));
        Assert.Equal(typeof(Schema.GameOptionDef), reference.DefinitionType);

        var policyOptions = Civ.Game.SetupPolicies.GameOptions(mods.Content);
        Assert.Contains(policyOptions, option => option.Id == "foundation:gameoption_multiple_religion_spread");
    }

    [Fact]
    public void Invalid_game_option_requirement_reference_is_rejected()
    {
        var probe = new ModSource { Manifest = new() { Id = "option_probe", Version = "1", Dependencies = ["foundation"] },
            Root = mods.Root, Instance = new InvalidOptionReferenceProbe() };
        var error = Assert.Throws<InvalidDataException>(() => ModLoader.Build(mods.Sources.Append(probe)));
        Assert.Contains("foundation:gameoption_missing", error.Message);
    }

    private sealed class InvalidOptionReferenceProbe : IMod
    {
        public void Register(IModContext context) => context.Content<Schema.CultureLevelDef>().Register(new()
        {
            Id = "option_probe:level",
            Requirements = new Requirement.Leaf("game-option", "foundation:gameoption_missing"),
        });
    }

    [Fact]
    public void Golden_records_retain_prerequisite_groups_effects_class_limits_and_module_overrides()
    {
        // Values independently inspected in the source trees. Paths and getter contracts are in golden-records.md.
        var db = mods.Content;
        var tech = db.Get<Schema.TechDef>("research:absolutism");
        Assert.Equal(2250, tech.Cost);
        Assert.Equal("foundation:era_renaissance", tech.Era);
        var clauses = Assert.IsType<Requirement.All>(tech.Requirements).Items;
        Assert.Contains(clauses, r => r is Requirement.All a && a.Items.Contains(new Requirement.Leaf("tech", "research:free_artistry")));
        Assert.Contains(clauses, r => r is Requirement.Any a && a.Items.Contains(new Requirement.Leaf("tech", "research:social_contract")));
        Assert.Equal(90, db.Get<Schema.TechDef>("research:bronze_working").Cost);
        var religion = db.Get<Schema.ReligionDef>("religion:buddhism");
        Assert.Equal("research:meditation", religion.FoundingTech);
        Assert.Equal("military:buddhist_missionary", religion.Missionary);
        Assert.Equal(35, religion.SpreadFactor);
        Assert.Equal(new CommerceVector(1, 0, 2, 0), religion.GlobalCityCommerce);
        Assert.Equal(new[] { "cities:buddhist_temple" }, religion.Temples);
        Assert.Equal(new[] { "cities:buddhist_monastery" }, religion.Monasteries);
        Assert.Equal(new[] { "cities:buddhist_cathedral" }, religion.Cathedrals);
        Assert.Equal(new[] { "cities:buddhist_shrine" }, religion.Shrines);

        var wonder = db.Get<Schema.BuildingDef>("cities:abu_simbel");
        Assert.Equal(150, wonder.Cost);
        Assert.Equal(1, wonder.Limits.World);
        Assert.Equal(-1, wonder.Limits.Team);
        Assert.Equal("cities:abu_simbel", wonder.DefaultBuilding);
        Assert.Equal(100, wonder.BonusProductionModifiers["world:stone"]);
        Assert.Equal(6, wonder.ObsoleteSafeCommerceChanges.Culture);
        Assert.Equal(30, db.Get<Schema.BuildingDef>("cities:granary").FoodKept);
        var forge = db.Get<Schema.BuildingDef>("cities:forge");
        Assert.Equal(new YieldVector(0, 15, 0), forge.YieldModifiers);
        Assert.Equal(1, forge.SpecialistSlots["foundation:specialist_engineer"]);
        var stable = Assert.IsType<Requirement.All>(db.Get<Schema.BuildingDef>("cities:stable").Requirements);
        Assert.Contains(stable.Items, r => r is Requirement.Any a && a.Items.Select(x => Assert.IsType<Requirement.Leaf>(x).Id)
            .SequenceEqual(new[] { "world:horse", "world:ivory" }));

        var unique = db.Get<Schema.UnitDef>("military:albanian_light_guerilla_cavalry");
        Assert.Equal("military:knight", unique.DefaultUnit);
        Assert.Equal(12, unique.Strength);
        Assert.Equal(2, unique.Moves);
        Assert.Equal(1, unique.FirstStrikes);
        Assert.Equal(30, unique.WithdrawalPercent);
        Assert.Equal(0, db.Get<Schema.UnitDef>("military:knight").FirstStrikes);
        var bear = db.Get<Schema.UnitDef>("military:bear");
        var subdue = bear.KillOutcomes.Single(o => o.Outcome == "military:outcome_subdue");
        Assert.Equal(10, subdue.Chance);
        Assert.Equal("military:captured_bear", subdue.Unit);
        Assert.False(subdue.DeliverUnitToCity);
        Assert.Equal(new YieldVector(2, 4, 0), bear.KillOutcomes.Single(o => o.Outcome == "military:outcome_hunting_kill").Yields);
        var civilization = db.Get<Schema.CivilizationDef>("peoples:albania");
        Assert.Equal(unique.Id, civilization.UnitOverrides["military:knight"]);
        Assert.Equal("cities:albania_illogor", civilization.BuildingOverrides["cities:garrison"]);
        Assert.True(civilization.StartingTechs["research:ritualism"]);
        Assert.Equal(1, civilization.StartingUnits["military:settler"]);
        var leader = db.Get<Schema.LeaderDef>("peoples:leader_gjergj_kastrioti");
        Assert.Equal(new[] { "peoples:trait_organized", "peoples:trait_philosophical" }, leader.Traits.Where(x => x.Value).Select(x => x.Key).Order(StringComparer.Ordinal));
        Assert.Equal(50, leader.BuildUnitProbability);
        Assert.Equal("government:nationalist", leader.FavoriteCivic);

        var civic = db.Get<Schema.CivicDef>("government:slavery");
        Assert.Equal("government:civicoption_society", civic.Option);
        Assert.Equal(25, civic.PopulationAngerModifier);
        Assert.Equal(new YieldVector(1, 0, 0), civic.ImprovementYieldChanges["world:improvement_improvement_farm"]);
        var promotion = db.Get<Schema.PromotionDef>("military:combat2");
        Assert.Equal(10, promotion.CombatPercent);
        Assert.Contains(ContentReferences.Leaves(promotion.Requirements!), l => l.Kind == "promotion" && l.Id == "military:combat1");
        Assert.True(promotion.CombatTypes["military:unitcombat_melee"]);
        var copper = db.Get<Schema.BonusDef>("world:copper");
        Assert.Equal(new YieldVector(0, 1, 0), copper.YieldChanges);
        Assert.Equal("research:bronze_working", copper.RevealTech);
        var mine = db.Get<Schema.ImprovementDef>("world:improvement_improvement_mine");
        Assert.Equal(-50, mine.HealthPercent);
        Assert.True(mine.BonusEffects["world:copper"].Connects);
        Assert.Equal(10000, mine.BonusEffects["world:copper"].DiscoveryRandomRange);
        Assert.Equal(new YieldVector(2, 0, 0), db.Get<Schema.TerrainDef>("world:grass").Yields);
        Assert.Equal(3, db.Get<Schema.SpecialistDef>("foundation:specialist_scientist").Commerces.Research);
        Assert.Equal(480, db.Get<Schema.CultureLevelDef>("foundation:culturelevel_developing").SpeedThresholds["foundation:gamespeed_normal"]);
        Assert.Equal(110, db.Get<Schema.GameSpeedDef>("foundation:gamespeed_normal").GrowthPercent);
        Assert.Equal(3, db.Get<Schema.RuleConstantsDef>("foundation:rule_constants").Values.FoodConsumptionPerPopulation);
        Assert.Equal(50, db.Get<Schema.RuleConstantsDef>("foundation:rule_constants").Values.FoodWastageGrowthPermille);
    }

    [Fact]
    public void Shipped_data_and_manifests_have_no_source_branding_or_floating_point_numbers()
    {
        foreach (var file in Directory.GetFiles(mods.Root, "*.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            Scan(document.RootElement);
        }
    }

    private static void Scan(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AssertNeutral(property.Name);
                    if (property.Name == "unsupported")
                    {
                        var paths = property.Value.EnumerateArray().Select(p => p.GetString()!).ToArray();
                        Assert.Equal(paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), paths);
                    }
                    Scan(property.Value);
                }
                break;
            case JsonValueKind.Array: foreach (var item in element.EnumerateArray()) Scan(item); break;
            case JsonValueKind.String: AssertNeutral(element.GetString()!); break;
            case JsonValueKind.Number: Assert.True(element.TryGetInt64(out _), "Definitions must use integer numbers"); break;
        }
    }
    private static void AssertNeutral(string value) => Assert.DoesNotMatch(
        @"(?<![A-Za-z0-9])\u0041ND(?= |[_.:/\\-]|$|Concept)|A[ _]+New[ _]+Dawn|Rise[ _]+of[ _]+Mankind|\u0052oM", value);
}
