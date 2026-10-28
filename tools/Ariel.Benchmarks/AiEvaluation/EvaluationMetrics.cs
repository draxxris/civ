using Ariel.Content;
using UnitSchema = Ariel.Content.Schema.UnitDef;
using Ariel.Game;

namespace Ariel.Benchmarks.AiEvaluation;

public sealed record MetricDefinition(string Name, string Direction, string Meaning);
public sealed record SampleKey(ulong Seed, int Player, string Scenario, int Checkpoint);
public sealed record EconomicSnapshot(SampleKey Key, string StateHash, int? SettledTurn, Dictionary<string, long> Metrics);

public static class EvaluationMetrics
{
    // Direction is a descriptive preference, not a score or proof of better decisions.
    public static readonly MetricDefinition[] Definitions =
    [
        new("cities", "positive", "Owned cities at the snapshot."),
        new("population", "positive", "Total population of owned cities."),
        new("treasury", "neutral", "Stored gold. Hoarding is not necessarily better."),
        new("income", "positive", "Current projected whole gold per turn, including deal income."),
        new("expenses", "neutral", "Current projected costs. Expansion can increase costs."),
        new("netGold", "positive", "Current projected income minus expenses, not settled cash flow."),
        new("unpaidGold", "negative", "Unpaid gold from the latest settlement."),
        new("strikeTurns", "negative", "Current consecutive turns in strike."),
        new("foodYield", "positive", "Sum of current city food yields before consumption."),
        new("foodSurplus", "positive", "Sum of current city growth differences after consumption."),
        new("productionYield", "positive", "Sum of current city production yields before queue modifiers."),
        new("queueProduction", "positive", "Sum of production previews for current queue heads. Not completed output."),
        new("rawCommerce", "positive", "Sum of current city commerce yields before sliders."),
        new("researchHundredths", "positive", "Current projected commerce research, before research cost modifiers."),
        new("cultureHundredths", "positive", "Current projected commerce culture."),
        new("espionageHundredths", "neutral", "Current projected commerce espionage."),
        new("researchRatePercent", "neutral", "Current research slider percentage."),
        new("technologies", "positive", "Known team technologies, including initial grants."),
        new("buildings", "positive", "Constructed buildings in currently owned cities."),
        new("improvedOwnedTiles", "positive", "Owned plots with an improvement, whether worked or not."),
        new("workerCount", "neutral", "Owned units that have a worker build rate and build options."),
        new("workersBuilding", "neutral", "Workers with a valid active build order at this checkpoint."),
        new("workersNotBuilding", "neutral", "Workers without a valid active build order at this checkpoint."),
        new("workerDisplacementsSincePreviousCheckpoint", "neutral", "Workers whose map position changed since the prior checkpoint. This does not prove useful travel."),
        new("buildingWorkerCheckpointsTotal", "neutral", "Sum of active building workers observed at checkpoints. This is a worker-turn proxy, not measured work time."),
        new("workerDisplacementsTotal", "neutral", "Sum of worker position changes observed between checkpoints. This does not prove useful travel."),
        new("improvementTransitionsSincePreviousCheckpoint", "neutral", "Owned-plot improvement id changes since the prior checkpoint. Changes are not attributed to workers and intermediate changes can be missed."),
        new("improvementTransitionsTotal", "neutral", "Sum of observed owned-plot improvement id changes. Changes are not attributed to workers."),
        new("routeTransitionsSincePreviousCheckpoint", "neutral", "Owned-plot route id changes since the prior checkpoint. Changes are not attributed to workers and intermediate changes can be missed."),
        new("routeTransitionsTotal", "neutral", "Sum of observed owned-plot route id changes. Changes are not attributed to workers."),
        new("ownedBuildProgressEntries", "neutral", "Build progress entries on currently owned plots at this checkpoint."),
        new("ownedBuildProgressBasisPoints", "neutral", "Sum of capped build progress fractions on currently owned plots. This is not attributed to active workers."),
        new("workedLandTiles", "neutral", "Unique owned city-center and assigned worked plots at this checkpoint."),
        new("workedLandWithoutImprovement", "neutral", "Worked land plots without an improvement at this checkpoint."),
        new("workedLandFoodYield", "neutral", "Current plot food yield on worked land. This is a snapshot preview."),
        new("workedLandProductionYield", "neutral", "Current plot production yield on worked land. This is a snapshot preview."),
        new("workedLandCommerceYield", "neutral", "Current plot commerce yield on worked land. This is a snapshot preview."),
        new("units", "neutral", "Currently owned units of all types."),
        new("wars", "neutral", "Current wars of the player team."),
        new("settledGoldHundredthsTotal", "neutral", "Sum of actual settled commerce gold. Excludes expenses and deal transfers."),
        new("settledResearchHundredthsTotal", "positive", "Sum of actual settled commerce research, before research modifiers."),
        new("settledCultureHundredthsTotal", "positive", "Sum of actual settled commerce culture."),
        new("settledEspionageHundredthsTotal", "neutral", "Sum of actual settled commerce espionage."),
    ];

    internal static EconomicSnapshot Capture(Simulation sim, ulong seed, Player player, CommerceVector accumulated,
        WorkerActivityHistory workerActivity)
    {
        var economy = sim.Content.Services.Get<IPlayerEconomyService>().Single();
        var result = economy.Calculate(sim, player.Id);
        var state = player.Extensions.TryGet<EconomyPlayerState>(sim.Content);
        var cities = sim.State.Cities.Where(c => c.Owner == player.Id).ToArray();
        var yields = sim.Content.Services.Get<ICityYieldService>().Single();
        var growth = sim.Content.Services.Get<ICityGrowthService>().Single();
        var production = sim.Content.Services.Get<ICityProductionService>().Single();
        var plotYields = sim.Content.Services.Get<IPlotYieldService>().Single();
        var workers = sim.State.Units.Where(u => u.Owner == player.Id && IsWorker(sim, u)).ToArray();
        var workerBuilds = sim.Content.Services.Get<IWorkerBuildService>().Single();
        int workersBuilding = workers.Count(u => workerBuilds.IsWorking(sim, u.Id));
        var ownedPlots = sim.State.Grid.Tiles.Select(tile => (Tile: tile, Plot: sim.State.Plots[tile.Value]))
            .Where(entry => entry.Plot.Owner == player.Id).ToArray();
        var activity = workerActivity.Observe(workers, workersBuilding, ownedPlots);
        long buildProgressEntries = 0;
        long buildProgressBasisPoints = 0;
        foreach (var (tile, plot) in ownedPlots)
        foreach (var (build, progress) in plot.BuildProgress)
        {
            int requiredWork = workerBuilds.RequiredWork(sim, tile, build);
            long boundedProgress = Math.Clamp(progress, 0, requiredWork);
            buildProgressEntries++;
            buildProgressBasisPoints += boundedProgress * 10_000 / requiredWork;
        }
        var workedTiles = cities.SelectMany(c => c.Worked.Append(c.Position))
            .Select(sim.State.Grid.TileAt).Distinct().ToArray();
        long workedLandWithoutImprovement = 0;
        YieldVector workedYield = default;
        foreach (var tile in workedTiles)
        {
            if (sim.State.Plots[tile.Value].Improvement is null) workedLandWithoutImprovement++;
            workedYield += plotYields.Calculate(sim, player.Id, tile).Yields;
        }
        YieldVector total = default;
        foreach (var city in cities) total += yields.Calculate(sim, city.Id).Yields;
        var metrics = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["cities"] = cities.Length,
            ["population"] = cities.Sum(c => (long)c.Population),
            ["treasury"] = player.Treasury,
            ["income"] = result.Income,
            ["expenses"] = result.Expenses,
            ["netGold"] = result.NetGold,
            ["unpaidGold"] = state?.UnpaidGold ?? 0,
            ["strikeTurns"] = state?.StrikeTurns ?? 0,
            ["foodYield"] = total.Food,
            ["foodSurplus"] = cities.Sum(c => growth.Calculate(sim, c.Id).Difference),
            ["productionYield"] = total.Production,
            ["queueProduction"] = cities.Sum(c => QueueProduction(sim, production, c)),
            ["rawCommerce"] = total.Commerce,
            ["researchHundredths"] = result.CommerceHundredths.Research,
            ["cultureHundredths"] = result.CommerceHundredths.Culture,
            ["espionageHundredths"] = result.CommerceHundredths.Espionage,
            ["researchRatePercent"] = economy.Rates(sim, player.Id).Research,
            ["technologies"] = sim.TeamOf(player.Id).Techs.Count,
            ["buildings"] = cities.Sum(c => (long)c.Buildings.Count),
            ["improvedOwnedTiles"] = sim.State.Plots.LongCount(p => p.Owner == player.Id && p.Improvement is not null),
            ["workerCount"] = workers.Length,
            ["workersBuilding"] = workersBuilding,
            ["workersNotBuilding"] = workers.Length - workersBuilding,
            ["workerDisplacementsSincePreviousCheckpoint"] = activity.Displacements,
            ["buildingWorkerCheckpointsTotal"] = workerActivity.BuildingWorkerCheckpointsTotal,
            ["workerDisplacementsTotal"] = workerActivity.WorkerDisplacementsTotal,
            ["improvementTransitionsSincePreviousCheckpoint"] = activity.ImprovementTransitions,
            ["improvementTransitionsTotal"] = workerActivity.ImprovementTransitionsTotal,
            ["routeTransitionsSincePreviousCheckpoint"] = activity.RouteTransitions,
            ["routeTransitionsTotal"] = workerActivity.RouteTransitionsTotal,
            ["ownedBuildProgressEntries"] = buildProgressEntries,
            ["ownedBuildProgressBasisPoints"] = buildProgressBasisPoints,
            ["workedLandTiles"] = workedTiles.Length,
            ["workedLandWithoutImprovement"] = workedLandWithoutImprovement,
            ["workedLandFoodYield"] = workedYield.Food,
            ["workedLandProductionYield"] = workedYield.Production,
            ["workedLandCommerceYield"] = workedYield.Commerce,
            ["units"] = sim.State.Units.LongCount(u => u.Owner == player.Id),
            ["wars"] = sim.TeamOf(player.Id).Wars.Count,
            ["settledGoldHundredthsTotal"] = accumulated.Gold,
            ["settledResearchHundredthsTotal"] = accumulated.Research,
            ["settledCultureHundredthsTotal"] = accumulated.Culture,
            ["settledEspionageHundredthsTotal"] = accumulated.Espionage,
        };
        return new(new(seed, player.Id, EvaluationConfig.Scenario, sim.State.Turn), sim.ComputeHash().ToString("X16"),
            state?.SettledTurn > 0 ? state.SettledTurn : null, metrics);
    }

    private static bool IsWorker(Simulation sim, Unit unit)
    {
        var definition = sim.Content.Get<UnitSchema>(unit.Type);
        return definition.Domain == "military:domain_land" && definition.WorkRate > 0 && definition.Builds.Count > 0;
    }

    private static long QueueProduction(Simulation sim, ICityProductionService service, City city)
    {
        var head = city.ProductionQueue.FirstOrDefault();
        return head is null ? 0 : service.Calculate(sim, city.Id, head.Kind, head.ContentId).Production;
    }
}

internal sealed record WorkerActivitySample(int Displacements, int ImprovementTransitions, int RouteTransitions);

internal sealed class WorkerActivityHistory
{
    private readonly Dictionary<int, Hex> previousPositions = new();
    private readonly Dictionary<int, PlotState> previousPlots = new();
    private readonly record struct PlotState(string? Improvement, string? Route);
    public long BuildingWorkerCheckpointsTotal { get; private set; }
    public long WorkerDisplacementsTotal { get; private set; }
    public long ImprovementTransitionsTotal { get; private set; }
    public long RouteTransitionsTotal { get; private set; }

    public WorkerActivitySample Observe(Unit[] workers, int workersBuilding, (TileId Tile, Plot Plot)[] ownedPlots)
    {
        int displacements = workers.Count(worker => previousPositions.TryGetValue(worker.Id, out var position)
            && position != worker.Position);
        previousPositions.Clear();
        foreach (var worker in workers) previousPositions.Add(worker.Id, worker.Position);
        int improvementTransitions = 0;
        int routeTransitions = 0;
        var currentPlots = new Dictionary<int, PlotState>(ownedPlots.Length);
        foreach (var (tile, plot) in ownedPlots)
        {
            var current = new PlotState(plot.Improvement, plot.Route);
            currentPlots.Add(tile.Value, current);
            if (!previousPlots.TryGetValue(tile.Value, out var previous)) continue;
            if (previous.Improvement != current.Improvement) improvementTransitions++;
            if (previous.Route != current.Route) routeTransitions++;
        }
        previousPlots.Clear();
        foreach (var (tile, state) in currentPlots) previousPlots.Add(tile, state);
        BuildingWorkerCheckpointsTotal += workersBuilding;
        WorkerDisplacementsTotal += displacements;
        ImprovementTransitionsTotal += improvementTransitions;
        RouteTransitionsTotal += routeTransitions;
        return new(displacements, improvementTransitions, routeTransitions);
    }
}
