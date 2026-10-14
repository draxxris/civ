using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.World;

/// <summary>World definitions and deterministic geography, worker and query rules.</summary>
public sealed class WorldMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<BonusClassDef>(context, "bonus-class.json");
        ContentRegistration.Load<BonusDef>(context, "bonus.json");
        ContentRegistration.Load<BuildDef>(context, "build.json");
        ContentRegistration.Load<FeatureDef>(context, "feature.json");
        ContentRegistration.Load<GoodyDef>(context, "goody.json");
        ContentRegistration.Load<ImprovementDef>(context, "improvement.json");
        ContentRegistration.Load<RouteDef>(context, "route.json");
        ContentRegistration.Load<TerrainDef>(context, "terrain.json");
        var water = new WaterService();
        var yields = new PlotYieldService(water);
        var network = new ResourceNetwork();
        var visibility = new VisibilityService();
        var workers = new WorkerBuildService(water);
        context.MapGenerators.Register(new ContinentsGenerator());
        context.RegisterService<Civ.Game.ISetupPolicy>("setup_policy", new SetupPolicy());
        context.RegisterService<Civ.Game.ITileAppearanceService>("tile_appearance", new TileAppearanceService());
        context.RegisterService<Civ.Game.IWaterService>("water", water);
        context.RegisterService<Civ.Game.IPlotYieldService>("plot_yields", yields);
        context.RegisterService<Civ.Game.IResourceNetwork>("resource_network", network);
        context.RegisterService<Civ.Game.ICityConnectionService>("city_connections", network);
        context.RegisterService<Civ.Game.IStartingPositionService>("starts", new StartingPositions(water));
        context.RegisterService<Civ.Game.IVisibilityService>("visibility", visibility);
        context.RegisterService<Civ.Game.IWorkerBuildService>("workers", workers);
        context.RegisterService<Civ.Content.IRequirementEvaluator>("bonus_requirements", new BonusEvaluator(network));
        context.RegisterStateComponent<WorkerOrder>("worker_order");
        context.RegisterCommand<Civ.Game.StartBuildCommand>("start_build");
        context.TurnSystems.Register(workers);
        context.TurnSystems.Register(new ImprovementTurnSystem());
        context.TurnSystems.Register(visibility);
        context.Subscribe<Civ.Game.CityFounded>("city_sight", (sim, _) => visibility.Recompute(sim));
        context.Subscribe<Civ.Game.UnitCreated>("unit_sight", (sim, _) => visibility.Recompute(sim));
        ContentRegistration.Evaluator<FeatureDef>(context, "feature");
        ContentRegistration.Evaluator<TerrainDef>(context, "terrain");
        ContentRegistration.Evaluator<ImprovementDef>(context, "improvement");
        ContentRegistration.Evaluator<RouteDef>(context, "route");
        ContentRegistration.Evaluator<BuildDef>(context, "build");
    }
}
