using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Rules.Foundation;

/// <summary>Foundation definitions, shared hex tuning and new-game setup.</summary>
public sealed class FoundationModule : IPreparableMod
{
    private static readonly ModJsonFile<HexOverridesDef[]> HexOverrides = new("data/hex-overrides.json");
    private static readonly ModJsonFile<CommerceDef[]> Commerce = new("data/commerce.json");
    private static readonly ModJsonFile<CultureLevelDef[]> CultureLevels = new("data/culture-level.json");
    private static readonly ModJsonFile<EraDef[]> Eras = new("data/era.json");
    private static readonly ModJsonFile<GameOptionDef[]> Options = new("data/game-option.json");
    private static readonly ModJsonFile<GameSpeedDef[]> Speeds = new("data/game-speed.json");
    private static readonly ModJsonFile<HandicapDef[]> Handicaps = new("data/handicap.json");
    private static readonly ModJsonFile<SpecialistDef[]> Specialists = new("data/specialist.json");
    private static readonly ModJsonFile<VictoryIdentityDef[]> Victories = new("data/victory-identity.json");
    private static readonly ModJsonFile<WorldSizeDef[]> WorldSizes = new("data/world-size.json");
    private static readonly ModJsonFile<YieldDef[]> Yields = new("data/yield.json");
    private static readonly ModJsonFile<ConditionDef[]> Conditions = new("data/condition.json");
    private static readonly ModJsonFile<RuleConstantsDef[]> Constants = new("data/rule-constants.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [HexOverrides, Commerce, CultureLevels, Eras, Options, Speeds, Handicaps,
            Specialists, Victories, WorldSizes, Yields, Conditions, Constants];

    private sealed class GameOptionRequirements : Ariel.Content.IContentRequirementEvaluator
    {
        public string Kind => "game-option";
        public Type DefinitionType => typeof(GameOptionDef);
        public string? Evaluate(Ariel.Content.Requirement.Leaf leaf, Ariel.Content.RequirementContext context) =>
            context.Sim.IsOptionEnabled(leaf.Id) ? null : $"Required game-option: {leaf.Id}";
    }
    private static void ValidateHexTuning(IModContext context)
    {
        var tuning = HexOverrides.Read(context.Data).Single();
        if (tuning.AiMinimumGarrison < 1 || tuning.AiThreatStrengthPerDefender < 1
            || tuning.AiMaximumGarrison < tuning.AiMinimumGarrison)
            throw new InvalidDataException("Invalid hex garrison tuning");
    }

    public void Register(IModContext context)
    {
        context.RegisterService<Ariel.Game.ISetupPolicy>("setup_policy", new SetupPolicy());
        ValidateHexTuning(context);
        ContentRegistration.Load(context, HexOverrides);
        context.RegisterService<Ariel.Game.IRuleset>("ruleset", new Ariel.Game.GameRules());
        ContentRegistration.Load(context, Commerce);
        ContentRegistration.Load(context, CultureLevels);
        ContentRegistration.Load(context, Eras);
        ContentRegistration.Load(context, Options);
        ContentRegistration.Load(context, Speeds);
        ContentRegistration.Load(context, Handicaps);

        ContentRegistration.Load(context, Specialists);
        ContentRegistration.Load(context, Victories);
        ContentRegistration.Load(context, WorldSizes);
        ContentRegistration.Load(context, Yields);
        ContentRegistration.Load(context, Conditions);
        ContentRegistration.Load(context, Constants);
        var timers = new Timers();
        context.RegisterService<Ariel.Game.ITimers>("timers", timers);
        context.RegisterStateComponent<Ariel.Game.TimerState>("timers");
        context.TurnSystems.Register(new TimersTurnStart(timers));
        context.TurnSystems.Register(new TimersTurnEnd(timers));
        // A timer held by a city follows its kind's rule when the city changes hands, and ends when it is destroyed.
        context.Subscribe<Ariel.Game.CityCaptured>("timers_capture", (sim, captured) => timers.CityLeft(sim, captured.City, captured.Player));
        context.Subscribe<Ariel.Game.CitySeceded>("timers_secession", (sim, seceded) => timers.CityLeft(sim, seceded.City, seceded.Player));
        context.Subscribe<Ariel.Game.CityRazed>("timers_raze", (sim, razed) => timers.CityLeft(sim, razed.City, null));
        var calendar = new GameCalendar();
        foreach (var speed in Speeds.Read(context.Data)) calendar.ValidateShipped(speed);
        context.RegisterService<Ariel.Game.IGameCalendar>("calendar", calendar);
        ContentRegistration.Evaluator<EraDef>(context, "era");
        ContentRegistration.Evaluator<CultureLevelDef>(context, "culture-level");
        ContentRegistration.Evaluator<ConditionDef>(context, "condition");
        context.RegisterService<Ariel.Content.IRequirementEvaluator>("game-option_requirements", new GameOptionRequirements());

        // A victory requirement is that victory being on in this game (CvGame::isVictoryValid). A victory whose
        // condition no loaded module registers is not on, so its buildings stay unavailable.
        ContentRegistration.Evaluator<VictoryIdentityDef>(context, "victory", (leaf, subject) =>
            subject.Sim.Rules().Victory.IsEnabled(subject.Sim, leaf.Id));
    }
}
