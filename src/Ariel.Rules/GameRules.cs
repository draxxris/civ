using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Rules.Foundation;

namespace Ariel.Game;

/// <summary>The base game as the kernel and everyone else sees it. It answers the kernel's <see cref="IRuleset"/>
/// questions and holds every base service as a field, resolved once when content freezes. Rules, mods and the client
/// reach a service through <see cref="GameRulesAccess.Rules(Simulation)"/> instead of searching the service registry
/// on each call. The registry stays the source of truth: a base module registers its services there, and the seams
/// that many mods implement (modifier sources, requirement evaluators, production kinds) are still read from it.</summary>
public sealed class GameRules : IRuleset
{
    private const string ExploreAiSuffix = ":unitai_explore";
    private readonly NewGameSetup _setup = new();

    internal GameRules() { }
    /// <summary>Turn-to-date conversion.</summary>
    public IGameCalendar Calendar { get; private set; } = null!;
    /// <summary>Cliff and height rules of the map.</summary>
    public IMapGeometry MapGeometry { get; private set; } = null!;
    /// <summary>Start plot scoring and assignment.</summary>
    public IStartingPositionService StartingPositions { get; private set; } = null!;
    /// <summary>Sight, fog memory and unit visibility.</summary>
    public IVisibilityService Visibility { get; private set; } = null!;
    /// <summary>Plot yield calculation.</summary>
    public IPlotYieldService PlotYields { get; private set; } = null!;
    /// <summary>Fresh water and irrigation queries.</summary>
    public IWaterService Water { get; private set; } = null!;
    /// <summary>Worker builds and their validation.</summary>
    public IWorkerBuildService WorkerBuilds { get; private set; } = null!;
    /// <summary>Resources connected to a city.</summary>
    public IResourceNetwork Resources { get; private set; } = null!;
    /// <summary>City-to-city trade network connections.</summary>
    public ICityConnectionService CityConnections { get; private set; } = null!;
    /// <summary>Research targets, progress and discovery.</summary>
    public IResearchService Research { get; private set; } = null!;
    /// <summary>Commerce rates, income and upkeep.</summary>
    public IPlayerEconomyService Economy { get; private set; } = null!;
    /// <summary>Research output a player's economy supplies.</summary>
    public IPlayerResearchSource ResearchSource { get; private set; } = null!;
    /// <summary>Espionage output a player's economy supplies.</summary>
    public IPlayerEspionageSource EspionageSource { get; private set; } = null!;
    /// <summary>Culture a city's commerce supplies.</summary>
    public ICityCultureSource CityCultureSource { get; private set; } = null!;
    /// <summary>Production-to-commerce processes.</summary>
    public ICityProcessService Processes { get; private set; } = null!;
    /// <summary>Hurrying production.</summary>
    public ICityHurryService Hurry { get; private set; } = null!;
    /// <summary>Buildings a city runs.</summary>
    public ICityBuildingService CityBuildings { get; private set; } = null!;
    /// <summary>City capture, razing and transfer.</summary>
    public ICityCaptureService CityCapture { get; private set; } = null!;
    /// <summary>Validated city orders.</summary>
    public ICityCommandService CityCommands { get; private set; } = null!;
    /// <summary>Drafting units from cities.</summary>
    public ICityConscriptService Conscription { get; private set; } = null!;
    /// <summary>City culture and borders.</summary>
    public ICityCultureService CityCulture { get; private set; } = null!;
    /// <summary>City food and growth.</summary>
    public ICityGrowthService CityGrowth { get; private set; } = null!;
    /// <summary>City maintenance costs.</summary>
    public ICityMaintenanceService CityMaintenance { get; private set; } = null!;
    /// <summary>City production queues and eligibility.</summary>
    public ICityProductionService CityProduction { get; private set; } = null!;
    /// <summary>City-scoped requirement leaves.</summary>
    public ICityRequirementService CityRequirements { get; private set; } = null!;
    /// <summary>City health and happiness.</summary>
    public ICityWellbeingService CityWellbeing { get; private set; } = null!;
    /// <summary>Citizen and specialist assignment.</summary>
    public ICityWorkService CityWork { get; private set; } = null!;
    /// <summary>City yields and commerce.</summary>
    public ICityYieldService CityYields { get; private set; } = null!;
    /// <summary>Unit lifecycle, cargo, promotion and upkeep.</summary>
    public IUnitService Units { get; private set; } = null!;
    /// <summary>Unit movement rules.</summary>
    public IMovementService Movement { get; private set; } = null!;
    /// <summary>Unit pathfinding.</summary>
    public IPathfindingService Paths { get; private set; } = null!;
    /// <summary>Combat between units, city defences and the combat record.</summary>
    public ICombatService Combat { get; private set; } = null!;
    /// <summary>Air missions.</summary>
    public IAirMissionService AirMissions { get; private set; } = null!;
    /// <summary>The barbarian player.</summary>
    public IBarbarianFaction Barbarians { get; private set; } = null!;
    /// <summary>Civics and anarchy.</summary>
    public IGovernmentService Government { get; private set; } = null!;
    /// <summary>Civilizations, leaders and traits.</summary>
    public IPeoplesService Peoples { get; private set; } = null!;
    /// <summary>Contact, war and peace, border passage and what players remember of each other.</summary>
    public IRelationsService Relations { get; private set; } = null!;

    // Seams one feature answers. Null when no loaded mod answers them; rules then apply the base game's default.
    /// <summary>Agreements between teams (open borders, defensive pacts), answered by the mod that negotiates them, or
    /// null without one. Rules ask <see cref="Relations"/>, which applies the default.</summary>
    public ITeamAgreements? TeamAgreements { get; private set; }
    /// <summary>How players regard each other, answered by the mod that judges it, or null without one. Rules ask
    /// <see cref="Relations"/>, which applies the default.</summary>
    public IDispositions? Dispositions { get; private set; }
    /// <summary>Resources and gold players exchange, answered by the mod that runs deals, or null without one.</summary>
    public IPlayerExchanges? Exchanges { get; private set; }
    /// <summary>Corporations in a city, answered by the mod that runs corporations, or null without one.</summary>
    public ICityCorporations? CityCorporations { get; private set; }
    /// <summary>Religion in cities and what it allows, answered by the mod that runs religion, or null without one.</summary>
    public ICityReligions? CityReligions { get; private set; }
    /// <summary>Where espionage commerce goes, answered by the feature that spends it, or null without one; the economy
    /// banks it then.</summary>
    public IEspionageSpending? EspionageSpending { get; private set; }
    /// <summary>Whether two cities trade, answered by the feature that runs trade routes, or null without one.</summary>
    public ICityTradeLinks? TradeLinks { get; private set; }
    /// <summary>How restless a city is, answered by the feature that tracks unrest, or null without one.</summary>
    public ICityUnrest? CityUnrest { get; private set; }
    /// <summary>Players' scores, answered by the feature that keeps score, or null without one.</summary>
    public IPlayerScores? Scores { get; private set; }
    /// <summary>Leaders that emerge from general points, answered by the feature that has them, or null without one.</summary>
    public ICombatLeaders? CombatLeaders { get; private set; }

    /// <summary>Missions: what a unit does to the city it stands in, for every feature that registers a kind.</summary>
    public IMissionService Missions { get; private set; } = null!;
    /// <summary>Every registered kind of mission, base and mod, in registration order.</summary>
    public IReadOnlyList<IMission> MissionKinds { get; private set; } = [];
    /// <summary>Every feature's unit orders, base and mod, in registration order.</summary>
    public IReadOnlyList<IUnitActionSource> UnitActions { get; private set; } = [];
    /// <summary>Every kind of fire that can support an assault, base and mod, in registration order.</summary>
    public IReadOnlyList<ISupportFire> SupportFire { get; private set; } = [];
    /// <summary>Everything that weighs a city as a new capital, base and mod, in registration order.</summary>
    public IReadOnlyList<ICapitalWeight> CapitalWeights { get; private set; } = [];
    /// <summary>Every source of empire-wide free buildings besides constructed ones, in registration order.</summary>
    public IReadOnlyList<IFreeBuildingGrants> FreeBuildingGrants { get; private set; } = [];
    /// <summary>Everything that bars barbarians from plots, in registration order.</summary>
    public IReadOnlyList<IBarbarianBarrier> BarbarianBarriers { get; private set; } = [];

    /// <inheritdoc/>
    public void Bind(ContentDb content)
    {
        var services = content.Services;
        Missions = services.Get<IMissionService>().Single();
        MissionKinds = services.Get<IMission>();
        UnitActions = services.Get<IUnitActionSource>();
        SupportFire = services.Get<ISupportFire>();
        CapitalWeights = services.Get<ICapitalWeight>();
        FreeBuildingGrants = services.Get<IFreeBuildingGrants>();
        BarbarianBarriers = services.Get<IBarbarianBarrier>();
        Calendar = services.Get<IGameCalendar>().Single();
        MapGeometry = services.Get<IMapGeometry>().Single();
        StartingPositions = services.Get<IStartingPositionService>().Single();
        Visibility = services.Get<IVisibilityService>().Single();
        PlotYields = services.Get<IPlotYieldService>().Single();
        Water = services.Get<IWaterService>().Single();
        WorkerBuilds = services.Get<IWorkerBuildService>().Single();
        Resources = services.Get<IResourceNetwork>().Single();
        CityConnections = services.Get<ICityConnectionService>().Single();
        Research = services.Get<IResearchService>().Single();
        Economy = services.Get<IPlayerEconomyService>().Single();
        ResearchSource = services.Get<IPlayerResearchSource>().Single();
        EspionageSource = services.Get<IPlayerEspionageSource>().Single();
        CityCultureSource = services.Get<ICityCultureSource>().Single();
        Processes = services.Get<ICityProcessService>().Single();
        Hurry = services.Get<ICityHurryService>().Single();
        CityBuildings = services.Get<ICityBuildingService>().Single();
        CityCapture = services.Get<ICityCaptureService>().Single();
        CityCommands = services.Get<ICityCommandService>().Single();
        Conscription = services.Get<ICityConscriptService>().Single();
        CityCulture = services.Get<ICityCultureService>().Single();
        CityGrowth = services.Get<ICityGrowthService>().Single();
        CityMaintenance = services.Get<ICityMaintenanceService>().Single();
        CityProduction = services.Get<ICityProductionService>().Single();
        CityRequirements = services.Get<ICityRequirementService>().Single();
        CityWellbeing = services.Get<ICityWellbeingService>().Single();
        CityWork = services.Get<ICityWorkService>().Single();
        CityYields = services.Get<ICityYieldService>().Single();
        Units = services.Get<IUnitService>().Single();
        Movement = services.Get<IMovementService>().Single();
        Paths = services.Get<IPathfindingService>().Single();
        Combat = services.Get<ICombatService>().Single();
        AirMissions = services.Get<IAirMissionService>().Single();
        Barbarians = services.Get<IBarbarianFaction>().Single();
        Government = services.Get<IGovernmentService>().Single();
        Peoples = services.Get<IPeoplesService>().Single();
        Relations = services.Get<IRelationsService>().Single();
        TeamAgreements = services.Get<ITeamAgreements>().SingleOrDefault();
        Dispositions = services.Get<IDispositions>().SingleOrDefault();
        Exchanges = services.Get<IPlayerExchanges>().SingleOrDefault();
        CityCorporations = services.Get<ICityCorporations>().SingleOrDefault();
        CityReligions = services.Get<ICityReligions>().SingleOrDefault();
        EspionageSpending = services.Get<IEspionageSpending>().SingleOrDefault();
        TradeLinks = services.Get<ICityTradeLinks>().SingleOrDefault();
        CityUnrest = services.Get<ICityUnrest>().SingleOrDefault();
        Scores = services.Get<IPlayerScores>().SingleOrDefault();
        CombatLeaders = services.Get<ICombatLeaders>().SingleOrDefault();
    }

    /// <inheritdoc/>
    public MapSpec ResolveMap(ContentDb content, SimulationSettings settings)
    {
        if (settings.WorldSize is null)
            throw new ArgumentException("The base game sizes its map from a world size: set SimulationSettings.WorldSize.", nameof(settings));
        return _setup.ResolveMap(content, settings);
    }
    /// <inheritdoc/>
    public void CreatePlayers(Simulation sim) => _setup.CreatePlayers(sim);
    /// <inheritdoc/>
    public void Complete(Simulation sim) => _setup.Complete(sim);
    /// <inheritdoc/>
    public void CompleteScenario(Simulation sim) => Visibility.Recompute(sim);

    /// <inheritdoc/>
    public void Validate(Simulation sim)
    {
        var content = sim.Content;
        foreach (var plot in sim.State.Plots)
        {
            _ = content.Get<TerrainDef>(plot.Terrain);
            if (plot.Feature is { } feature) _ = content.Get<FeatureDef>(feature);
            if (plot.Bonus is { } bonus) _ = content.Get<BonusDef>(bonus);
            if (plot.Improvement is { } improvement) _ = content.Get<ImprovementDef>(improvement);
            if (plot.Route is { } route) _ = content.Get<RouteDef>(route);
        }
        foreach (var unit in sim.State.Units) _ = content.Get<UnitDef>(unit.Type);
    }

    /// <inheritdoc/>
    public bool OptionDefault(ContentDb content, string option) =>
        content.TryGet<GameOptionDef>(option, out var definition) && definition.Default;

    /// <inheritdoc/>
    public bool IsScout(Simulation sim, Unit unit) => sim.Content.TryGet<UnitDef>(unit.Type, out var def)
        && (def.CanFoundCity || def.UnitAiTypes.Any(p => p.Value && p.Key.EndsWith(ExploreAiSuffix, StringComparison.Ordinal)));
}

/// <summary>Typed access to the base game from a simulation or its content.</summary>
public static class GameRulesAccess
{
    /// <summary>The base game's services for this simulation's content.</summary>
    public static GameRules Rules(this Simulation sim) => (GameRules)sim.Content.Ruleset;
    /// <summary>The base game's services for frozen content.</summary>
    public static GameRules Rules(this ContentDb content) => (GameRules)content.Ruleset;
}
