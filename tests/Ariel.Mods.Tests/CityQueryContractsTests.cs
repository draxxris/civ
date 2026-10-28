
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

public sealed class CityQueryContractsTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Resource = "city_query_probe:resource";
    private const string Technology = "city_query_probe:technology";
    private const string DraftUnit = "city_query_probe:draft";

    private Simulation Scenario()
    {
        var sources = mods.Sources.Select(s => s.Manifest.Id is "world" or "research"
            ? new ModSource { Manifest = s.Manifest, Root = s.Root, Instance = new ProviderMod(s.Instance) } : s);
        var probe = new ModSource
        {
            Root = mods.Root, Manifest = new() { Id = "city_query_probe", Version = "1",
                References = ["foundation", "cities"] }, Instance = new ProbeMod(),
        };
        var sim = GameFactory.Scenario(ModLoader.Build(sources.Append(probe)));
        GameFactory.NeutralPolitics(sim);
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(39)) });
        return sim;
    }

    [Fact]
    public void City_wellbeing_includes_resources_supplied_without_bonus_plots()
    {
        var sim = Scenario();
        var before = sim.ComputeHash();
        var wellbeing = sim.Content.Services.Get<ICityWellbeingService>().Single().Calculate(sim, 1);
        Assert.Equal(9, wellbeing.Ledger.Lines.Single(l => l.Source == Resource && l.Key == "cities:happiness_good").Amount);
        Assert.Equal(7, wellbeing.Ledger.Lines.Single(l => l.Source == Resource && l.Key == "cities:health_good").Amount);
        Assert.Equal(before, sim.ComputeHash());
    }

    [Fact]
    public void Draft_selection_honours_a_plot_dependent_requirement_on_the_city_tile()
    {
        var sim = Scenario();
        sim.TeamOf(0).Techs.Add(Technology);
        var production = sim.Content.Services.Get<ICityProductionService>().Single();
        var conscript = sim.Content.Services.Get<ICityConscriptService>().Single();
        // The probe unit needs a river on the city's own plot, evaluated by the shipped condition leaf, so
        // this proves the city plot reaches requirement evaluation instead of pinning an evaluator's call shape.
        var before = sim.ComputeHash();
        Assert.Contains(production.Validate(sim, 1, "cities:unit", DraftUnit).Failures,
            f => f.Explanation.Contains("foundation:river"));
        Assert.NotEqual(DraftUnit, conscript.Calculate(sim, 1)?.UnitId);
        Assert.Equal(before, sim.ComputeHash());
        sim.State.SetRiver(sim.State.Grid.TileAt(sim.State.CityById(1).Position), 0, true);
        Assert.True(production.Validate(sim, 1, "cities:unit", DraftUnit).Success);
        Assert.Equal(DraftUnit, Assert.IsType<ConscriptResult>(conscript.Calculate(sim, 1)).UnitId);
    }

    private sealed class ProbeMod : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.BonusDef>().Register(new() { Id = Resource, Happiness = 9, Health = 7 });
            context.Content<S.TechDef>().Register(new() { Id = Technology });
            context.Content<S.UnitDef>().Register(new()
            {
                Id = DraftUnit, DefaultUnit = DraftUnit, Cost = 60, Moves = 1, Conscription = int.MaxValue,
                Requirements = new Requirement.All([new Requirement.Leaf("tech", Technology),
                    new Requirement.Leaf("condition", "foundation:river")]),
            });
        }
    }

    private sealed class ProviderMod(IMod original) : IMod
    {
        public void Register(IModContext context)
        {
            original.Register(new ProviderContext(context));
        }
    }

    // Replace only the two providers under test; keep real content, commands and all other services.
    private sealed class ProviderContext(IModContext inner) : IModContext
    {
        public void RegisterService<T>(string name, T service) where T : class
        {
            object replacement = service;
            if (typeof(T) == typeof(IResourceNetwork)) replacement = new ResourceProvider((IResourceNetwork)service);
            inner.RegisterService(name, (T)replacement);
        }
        public ModRegistry<T> Content<T>() where T : ContentDef => inner.Content<T>();
        public void RegisterStateComponent<T>(string name) where T : class => inner.RegisterStateComponent<T>(name);
        public void Subscribe<T>(string name, Action<Simulation, T> handler) where T : DomainEvent => inner.Subscribe(name, handler);
        public void RegisterCommand<T>(string name) where T : GameCommand => inner.RegisterCommand<T>(name);
        public string ModId => inner.ModId;
        public IModData Data => inner.Data;
        public ModRegistry<TerrainDef> Terrains => inner.Terrains;
        public ModRegistry<CivilizationDef> Civilizations => inner.Civilizations;
        public ModRegistry<IPlayerAI> PlayerAIs => inner.PlayerAIs;
        public ModRegistry<UnitDef> Units => inner.Units;
        public ModRegistry<PromotionDef> Promotions => inner.Promotions;
        public ModRegistry<ExperienceLevelDef> ExperienceLevels => inner.ExperienceLevels;
        public ModRegistry<SpecialistDef> Specialists => inner.Specialists;
        public ModRegistry<CivicDef> Civics => inner.Civics;
        public ModRegistry<TechDef> Techs => inner.Techs;
        public ModRegistry<BuildingDef> Buildings => inner.Buildings;
        public ModRegistry<CultureLevelDef> CultureLevels => inner.CultureLevels;
        public ModRegistry<IMapGenerator> MapGenerators => inner.MapGenerators;
        public ModRegistry<ITurnSystem> TurnSystems => inner.TurnSystems;
        public ModRegistry<CommandTypeDef> CommandTypes => inner.CommandTypes;
    }

    private sealed class ResourceProvider(IResourceNetwork original) : IResourceNetwork
    {
        public IReadOnlyDictionary<string, int> ConnectedCounts(Simulation sim, int player, int city) =>
            new Dictionary<string, int>(original.ConnectedCounts(sim, player, city)) { [Resource] = 1 };
        public int ConnectedCount(Simulation sim, int player, int city, string bonus) => ConnectedCounts(sim, player, city).GetValueOrDefault(bonus);
        public IReadOnlySet<TileId> ConnectedTiles(Simulation sim, int player, int city) => original.ConnectedTiles(sim, player, city);
    }
}
