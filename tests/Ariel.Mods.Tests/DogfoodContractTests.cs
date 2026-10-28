using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

public sealed class DogfoodContractTests
{
    private static GameCatalog Catalog() => new([new ModSource
    {
        Manifest = new() { Id = "alternate", Version = "1" }, Root = AppContext.BaseDirectory, Instance = new AlternateMod(),
    }]);

    [Fact]
    public void SetupResolvesModDefaultsAndPreservesExplicitChoicesThroughSaveLoad()
    {
        var catalog = Catalog();
        var options = catalog.DefaultOptions();
        Assert.Equal("alternate:wide", options.WorldSize);
        Assert.Equal("alternate:quick", options.GameSpeed);
        Assert.Equal("alternate:fair", options.Handicap);
        Assert.Equal("alternate:fair", catalog.Handicaps[0].Id);
        var settings = catalog.SettingsFor(options);
        Assert.Equal("alternate:second_map", settings.MapGenerator);
        Assert.Equal("alternate:beginning", settings.StartingEra);

        var omitted = new SimulationSettings { Map = new(3, 2), PlayerCount = 1 };
        var sim = new Simulation(catalog.Content, omitted);
        Assert.Equal("", omitted.MapGenerator);
        Assert.Equal("alternate:second_map", sim.Settings.MapGenerator);
        Assert.All(sim.State.Plots, p => Assert.Equal("alternate:green", p.Terrain));
        var explicitMap = new Simulation(catalog.Content, omitted with { MapGenerator = "alternate:first_map" });
        Assert.All(explicitMap.State.Plots, p => Assert.Equal("alternate:grey", p.Terrain));
        var loaded = SaveGame.Load(catalog.Content, SaveGame.Save(sim));
        Assert.Equal(sim.Settings.MapGenerator, loaded.Settings.MapGenerator);
        Assert.Equal(sim.Settings.StartingEra, loaded.Settings.StartingEra);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, null)]
    public void ClientUsesCustomProductionQueriesAndAppearanceWithoutInterpretingIds(bool continuous, int? turns)
    {
        var catalog = Catalog();
        var sim = new Simulation(catalog.Content, new SimulationSettings { Map = new(3, 2), PlayerCount = 1 });
        var session = new GameSession(sim);
        var city = new City { Id = 1, Owner = 0, Position = new(0, 0) };
        sim.State.AddCity(city);
        ulong before = sim.ComputeHash();
        var kind = continuous ? "alternate:chant" : "alternate:craft";
        var choice = Assert.Single(session.ProductionChoices(city, kind));
        Assert.Equal("alternate:token", choice.Id);
        Assert.Equal("Token", choice.Name);
        Assert.Equal(12, choice.Cost);
        Assert.Equal(turns, choice.Turns);
        Assert.Equal(continuous, choice.IsContinuous);
        Assert.True(choice.Available);
        Assert.Equal(2, session.ProductionChoices(city).Count);
        Assert.Contains(session.ProductionKinds, k => k.Label == "Chants");
        Assert.Equal(Rgb.Hex(0x123456), MapStyle.TerrainColor(sim.Content, "alternate:green"));
        Assert.Equal(Rgb.Hex(0x777777), MapStyle.TerrainColor(sim.Content, "unknown:green"));
        var feature = MapStyle.FeatureOf(sim.Content, "alternate:sparkles");
        Assert.NotNull(feature);
        Assert.Equal(0.25f, feature.TintStrength);
        Assert.Equal(2, feature.Marks);
        Assert.Equal("Custom plot label", Labels.Source(sim, "alternate:plot:0"));
        Assert.Equal(before, sim.ComputeHash());
    }

    private sealed class AlternateMod : IMod
    {
        public void Register(IModContext ctx)
        {
            ctx.Terrains.Register(new TerrainDef { Id = "alternate:green" });
            ctx.Terrains.Register(new TerrainDef { Id = "alternate:grey" });
            ctx.MapGenerators.Register(new Generator("alternate:first_map", "alternate:grey"));
            ctx.MapGenerators.Register(new Generator("alternate:second_map", "alternate:green"));
            ctx.Content<S.WorldSizeDef>().Register(new() { Id = "alternate:wide", Name = "Wide", GridWidth = 3, GridHeight = 2 });
            ctx.Content<S.GameSpeedDef>().Register(new() { Id = "alternate:quick", Name = "Quick" });
            ctx.Content<S.HandicapDef>().Register(new() { Id = "alternate:hard", Name = "Hard" });
            ctx.Content<S.HandicapDef>().Register(new() { Id = "alternate:fair", Name = "Fair" });
            ctx.Content<S.EraDef>().Register(new() { Id = "alternate:beginning", Name = "Beginning" });
            ctx.Content<S.LeaderDef>().Register(new() { Id = "alternate:guide", Name = "Guide" });
            ctx.Content<S.CivilizationDef>().Register(new()
            {
                Id = "alternate:people", Name = "People", Playable = true,
                Leaders = System.Collections.Immutable.ImmutableDictionary<string, bool>.Empty.Add("alternate:guide", true),
            });
            ctx.Content<TokenDef>().Register(new() { Id = "alternate:token", Name = "Token" });
            ctx.RegisterStateComponent<CityRuleState>("city_state");
            ctx.RegisterService<ISetupPolicy>("setup", new Policy());
            ctx.RegisterService<IProductionKind>("craft", new Kind("alternate:craft", "Crafts", false));
            ctx.RegisterService<IProductionKind>("chant", new Kind("alternate:chant", "Chants", true));
            ctx.RegisterService<ITileAppearanceService>("appearance", new Appearance());
            ctx.RegisterService<ILedgerSourceLabel>("labels", new SourceLabel());
        }
    }

    private sealed class Generator(string id, string terrain) : IMapGenerator
    {
        public string Id => id;
        public void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content)
        {
            foreach (var plot in plots) plot.Terrain = terrain;
        }
        public bool[] PreviewLand(MapSpec spec, ulong seed, ContentDb content) =>
            Enumerable.Repeat(!content.Get<S.TerrainDef>(terrain).IsWater, spec.Width * spec.Height).ToArray();
    }

    private sealed class Policy : ISetupPolicy
    {
        public string? Default(SetupField field) => field switch
        {
            SetupField.WorldSize => "alternate:wide", SetupField.GameSpeed => "alternate:quick",
            SetupField.Handicap => "alternate:fair", SetupField.StartingEra => "alternate:beginning",
            SetupField.MapGenerator => "alternate:second_map", _ => null,
        };
        public int? SortOrder(SetupField field, string id) => field == SetupField.Handicap ? (id == "alternate:fair" ? 0 : 1) : null;
    }

    private sealed class TokenDef : ContentDef;
    private sealed class Kind(string id, string label, bool continuous) : IProductionKind
    {
        public string Id => id;
        public string Label => label;
        public bool IsContinuous => continuous;
        public IEnumerable<ContentDef> Candidates(ContentDb content) => content.Get<TokenDef>().All;
        public ContentDef Definition(ContentDb content, string id) => content.Get<TokenDef>(id);
        public long BaseCost(ContentDef definition) => 12;
        public bool IsVisible(RequirementResult eligibility) => true;
        public RequirementResult Validate(Simulation sim, int city, string id) => new(true, []);
        public CityProductionResult Calculate(Simulation sim, int city, string id) => new(12, 3, 100, new([]));
    }
    private sealed class Appearance : ITileAppearanceService
    {
        public TileAppearance? Terrain(string id) => id == "alternate:green" ? new(0x123456) : null;
        public TileAppearance? Feature(string id) => id == "alternate:sparkles" ? new(0xabcdef, 25, 2) : null;
    }
    private sealed class SourceLabel : ILedgerSourceLabel
    {
        public string? Label(Simulation sim, string source) => source == "alternate:plot:0" ? "Custom plot label" : null;
    }
}
