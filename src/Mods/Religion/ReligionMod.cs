using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Mods.Religion;

/// <summary>Religions, their founding, spread, decay, state religion and inquisitions, with the computer player's use
/// of them and their screens, with every building, unit and promotion of a faith. Without this mod the base game has no
/// religion: no base row names a faith, and the seams this mod answers (production rules, raze protection, attitude
/// sources, the planner's building exclusion) have nothing to say.</summary>
public sealed class ReligionMod : IPreparableMod
{
    private static readonly ModJsonFile<ReligionDef[]> Religions = new("data/religion.json");
    /// <summary>The requirements about religion that name no faith: a state religion, any religion.</summary>
    private static readonly ModJsonFile<ReligionConditionDef[]> Conditions = new("data/religion-condition.json");
    /// <summary>The temples, monasteries, cathedrals, shrines and religious wonders, the missionaries and the inquisitor,
    /// and each faith's promotions: religion's own content, which no base row names.</summary>
    private static readonly ModJsonFile<Ariel.Content.Schema.BuildingDef[]> OwnBuildings = new("data/building.json");
    private static readonly ModJsonFile<UnitDef[]> OwnUnits = new("data/unit.json");
    private static readonly ModJsonFile<PromotionDef[]> OwnPromotions = new("data/promotion.json");
    /// <summary>The great person the points of each religious wonder and shrine count towards, in the Great People mod's
    /// row type.</summary>
    private static readonly ModJsonFile<Ariel.Mods.GreatPeople.BuildingGreatPeopleDef[]> GreatPeople = new("data/building-great-people.json");
    /// <summary>What base buildings and units get that is religion's: the Shaolin Temple's promotion, the great prophet's
    /// shrines, the crusader's and Shaolin monk's promotions.</summary>
    private static readonly ModJsonFile<BuildingGrantsReligionDef[]> BuildingGrants = new("data/building-grants-religion.json");
    private static readonly ModJsonFile<UnitGrantsReligionDef[]> UnitGrants = new("data/unit-grants-religion.json");
    /// <summary>The religious buildings and units each civilization builds instead (the barbarians build none).</summary>
    private static readonly ModJsonFile<CivilizationReligionDef[]> Civilizations = new("data/civilization-religion.json");
    /// <summary>One world wonder per religion; completing it founds the religion in that city.</summary>
    private static readonly ModJsonFile<Ariel.Content.Schema.BuildingDef[]> FoundingWonders = new("data/founding-wonders.json");
    /// <summary>What an inquisition, or a razed holy city, leaves in the memory of the faithful.</summary>
    private static readonly ModJsonFile<MemoryKindDef[]> Memories = new("data/memory-kind.json");
    /// <summary>What each civic means to religion, attached to the base civics.</summary>
    private static readonly ModJsonFile<CivicReligionDef[]> Civics = new("data/civic-religion.json");
    /// <summary>What buildings, units and the rule constants mean to religion, attached to the base rows.</summary>
    private static readonly ModJsonFile<BuildingReligionDef[]> Buildings = new("data/building-religion.json");
    private static readonly ModJsonFile<UnitReligionDef[]> Units = new("data/unit-religion.json");
    private static readonly ModJsonFile<RuleConstantsReligionDef[]> Constants = new("data/rule-constants-religion.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [Religions, Conditions, OwnBuildings, OwnUnits, OwnPromotions, FoundingWonders, Memories, Civics, Buildings, Units, Constants, GreatPeople, BuildingGrants, UnitGrants, Civilizations];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Religions);
        ContentRegistration.Load(context, Conditions);
        ContentRegistration.Load(context, OwnBuildings);
        ContentRegistration.Load(context, OwnUnits);
        ContentRegistration.Load(context, OwnPromotions);
        ContentRegistration.Load(context, FoundingWonders);
        MemoryKinds.Load(context, Memories);
        ContentRegistration.Load(context, Civics);
        ContentRegistration.Load(context, Buildings);
        ContentRegistration.Load(context, Units);
        ContentRegistration.Load(context, Constants);
        ContentRegistration.Load(context, GreatPeople);
        ContentRegistration.Load(context, BuildingGrants);
        ContentRegistration.Load(context, UnitGrants);
        ContentRegistration.Load(context, Civilizations);
        var service = new ReligionService();
        context.RegisterService<IReligionService>("religion", service);
        ReligionRequirements.Register(context, service);
        var production = new ReligionProductionRules(service);
        context.RegisterService<IBuildingRule>("building_rule", production);
        context.RegisterService<IUnitTrainingRule>("training_rule", production);
        context.RegisterService<IRazeProtection>("holy_city_protection", new HolyCityProtection(service));
        context.RegisterService<IAttitudeSource>("attitude", new FaithAttitude());
        context.RegisterService<ILedgerSourceLabel>("attitude_labels", new FaithAttitudeLabels());
        context.RegisterStateComponent<ReligionRegardState>("attitudes");
        context.TurnSystems.Register(new FaithAttitudeTurns());
        context.RegisterService<IModifierSource>("effects", new ReligionEffects());
        context.RegisterStateComponent<ReligionCityState>("city_state");
        context.RegisterStateComponent<ReligionPlayerState>("player_state");
        // Only Religion's own commands and planners read the conversion timer.
        context.RegisterPrivateStateDomain("conversion", "Players/*/Extensions/religion:player_state/ConversionTimer");
        context.RegisterStateComponent<ReligionGameState>("game_state");
        context.RegisterCommand<SetStateReligionCommand>("set_state_religion");
        context.RegisterService<IMission>("inquisition", new InquisitionMission(service));
        context.RegisterService<IMission>("spread", new SpreadReligionMission());
        context.RegisterService<IMission>("shrine", new ShrineMission(service));
        context.RegisterService<IBuildingFounder>("founding_wonder", new ReligionFounder());
        context.Subscribe<CityRazed>("razed_holy_city", HolyCityRazing.Razed);
        context.TurnSystems.Register(new ReligionTurns(service));
        context.RegisterService<ICapitalWeight>("capital_weight", new FaithCapitalWeight(service));
        context.RegisterService<ISetupPolicy>("setup", new ReligionSetupPolicy());
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new ReligionAiPlanner());
        context.RegisterService<Ariel.Ai.IProductionWishes>("inquisitors", new InquisitorWishes());
        context.RegisterService<Ariel.Ai.IAiBuildingExclusion>("ai_buildings", new ReligiousBuildingExclusion());
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.BuildingSubject>>("founding_valuer", new FoundingWonderValuer());
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>("civic_valuer", new StateReligionCivicValuer());
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.CitySubject>>("holy_city_valuer", new HolyCityValuer());
        context.RegisterService<Ariel.Presentation.IGameScreen>("screen", new ReligionScreen());
        context.RegisterService<Ariel.Presentation.ICityPanel>("city", new ReligionCityPanel());
        context.RegisterService<Ariel.Presentation.ICityTag>("city_tag", new ReligionCityTag());
        context.RegisterService<Ariel.Presentation.INotificationSource>("notices", new ReligionNotices());
        context.RegisterService<Ariel.Presentation.IPediaSource>("pedia", new ReligionPedia());
    }
}

/// <summary>The setup switches religion honours. The options themselves are base content (<c>foundation:*</c>), so
/// the setup screen offers them only while this mod is loaded.</summary>
internal sealed class ReligionSetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => null;

    public IEnumerable<SetupGameOption> GameOptions(ContentDb content)
    {
        foreach (string id in (string[])["foundation:gameoption_religion_decay", "foundation:gameoption_no_inquisitions"])
            if (content.TryGet<GameOptionDef>(id, out var option)) yield return new(option.Id, option.Name, option.Default, option.Help);
        yield return new("foundation:gameoption_multiple_religion_spread", "Multiple Religion Spread", false,
            "Cities with an existing religion can still receive additional religions via natural spread.");
    }
}
/// <summary>A new capital leans toward a city with more faiths: one point for each religion present.</summary>
internal sealed class FaithCapitalWeight(IReligionService religion) : ICapitalWeight
{
    public int Weight(Simulation sim, int city) => religion.Present(sim, city).Count;
}
internal sealed class ReligionTurns(ReligionService service) : IPlayerTurnSystem
{
    public string Id => "religion:city_turns";
    public int Order => TurnPhase.CityPostProduction - 1;
    /// <summary>A turn counts down the conversion timer; a faith that spreads or decays widens the step.</summary>
    public IReadOnlyList<string>? WriteDomains => Conversion;
    private static readonly IReadOnlyList<string> Conversion = ["religion:conversion"];
    public void Run(Simulation sim, int player)
    {
        var spread = service.BeginSpreadTurn(sim);
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id)) service.NaturalSpread(sim, city, spread);
        var state = sim.State.PlayerById(player).Extensions.Get<ReligionPlayerState>(sim.Content);
        if (state is not null) state.ConversionTimer = Math.Max(0, state.ConversionTimer - 1);
    }
}
