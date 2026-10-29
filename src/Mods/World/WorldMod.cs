using Ariel.Content.Schema;
using Ariel.Modding;

namespace Ariel.Mods.World;

/// <summary>World definitions and deterministic geography, worker and query rules.</summary>
public sealed class WorldMod : IPreparableMod
{
    private static readonly ModJsonFile<BonusClassDef[]> BonusClasses = new("data/bonus-class.json");
    private static readonly ModJsonFile<BonusDef[]> Bonuses = new("data/bonus.json");
    private static readonly ModJsonFile<BuildDef[]> Builds = new("data/build.json");
    private static readonly ModJsonFile<FeatureDef[]> Features = new("data/feature.json");
    private static readonly ModJsonFile<GoodyDef[]> Goodies = new("data/goody.json");
    private static readonly ModJsonFile<ImprovementDef[]> Improvements = new("data/improvement.json");
    private static readonly ModJsonFile<RouteDef[]> Routes = new("data/route.json");
    private static readonly ModJsonFile<TerrainDef[]> Terrains = new("data/terrain.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [BonusClasses, Bonuses, Builds, Features, Goodies, Improvements, Routes, Terrains];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, BonusClasses);
        ContentRegistration.Load(context, Bonuses);
        ContentRegistration.Load(context, Builds);
        ContentRegistration.Load(context, Features);
        ContentRegistration.Load(context, Goodies);
        ContentRegistration.Load(context, Improvements);
        ContentRegistration.Load(context, Routes);
        ContentRegistration.Load(context, Terrains);
        var water = new WaterService();
        var yields = new PlotYieldService(water);
        var network = new ResourceNetwork();
        var visibility = new VisibilityService();
        var workers = new WorkerBuildService(water);
        context.MapGenerators.Register(new ContinentsGenerator());
        context.MapGenerators.Register(new ContinentsGenerator("world:inland_sea", "Inland Sea", WorldLayout.InlandSea));
        context.MapGenerators.Register(new ContinentsGenerator("world:king_of_the_hill", "King of the Hill", WorldLayout.KingOfTheHill));
        context.MapGenerators.Register(new ContinentsGenerator("world:new_world", "New World", WorldLayout.NewWorld));
        context.MapGenerators.Register(new ContinentsGenerator("world:twin_continents", "Twin Continents", WorldLayout.TwinContinents));
        context.MapGenerators.Register(new ContinentsGenerator("world:great_plains", "Great Plains", WorldLayout.GreatPlains));
        context.RegisterService<Ariel.Game.ISetupPolicy>("setup_policy", new SetupPolicy());
        context.RegisterService<Ariel.Game.ITileAppearanceService>("tile_appearance", new TileAppearanceService());
        context.RegisterService<Ariel.Game.IWaterService>("water", water);
        context.RegisterService<Ariel.Game.IMapSanitizer>("cliffs", new CliffRules());
        context.RegisterService<Ariel.Game.IPlotYieldService>("plot_yields", yields);
        context.RegisterService<Ariel.Game.IResourceNetwork>("resource_network", network);
        context.RegisterService<Ariel.Game.ICityConnectionService>("city_connections", network);
        context.RegisterService<Ariel.Game.IStartingPositionService>("starts", new StartingPositions(water));
        context.RegisterService<Ariel.Game.IVisibilityService>("visibility", visibility);
        context.RegisterService<Ariel.Game.IWorkerBuildService>("workers", workers);
        context.RegisterService<Ariel.Content.IRequirementEvaluator>("bonus_requirements", new BonusEvaluator(network));
        context.RegisterStateComponent<WorkerOrder>("worker_order");
        context.RegisterCommand<Ariel.Game.StartBuildCommand>("start_build");
        context.TurnSystems.Register(workers);
        context.TurnSystems.Register(new ImprovementTurnSystem());
        context.TurnSystems.Register(new GlobalWarmingSystem(yields));
        context.TurnSystems.Register(visibility);
        context.Subscribe<Ariel.Game.CityFounded>("city_sight", (sim, e) =>
        {
            CliffRules.EnsureAccess(sim.State, sim.Content, sim.State.Grid.TileAt(e.Position));
            visibility.Recompute(sim);
        });
        context.Subscribe<Ariel.Game.UnitCreated>("unit_sight", (sim, _) => visibility.Recompute(sim));
        context.Subscribe<Ariel.Game.TechDiscovered>("resource_reveal", ResourceTurnRules.RevealResources);
        // Refresh at the mutation, not only at turn end: later map events can be out of sight.
        context.Subscribe<Ariel.Game.FeatureChanged>("feature_sight", (sim, _) => visibility.Recompute(sim));
        context.Subscribe<Ariel.Game.GlobalWarmingOccurred>("terrain_sight", (sim, _) => visibility.Recompute(sim));
        ContentRegistration.Evaluator<FeatureDef>(context, "feature");
        ContentRegistration.Evaluator<TerrainDef>(context, "terrain");
        ContentRegistration.Evaluator<ImprovementDef>(context, "improvement");
        ContentRegistration.Evaluator<RouteDef>(context, "route");
        ContentRegistration.Evaluator<BuildDef>(context, "build");
    }
}
