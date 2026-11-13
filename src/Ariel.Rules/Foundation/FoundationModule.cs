using Ariel.Content.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Rules.Foundation;

/// <summary>Foundation definitions, shared hex tuning and new-game setup.</summary>
public sealed class FoundationModule : IPreparableMod
{
    private static readonly ModJsonFile<HexOverridesDef[]> HexOverrides = new("data/hex-overrides.json");
    private static readonly ModJsonFile<CommerceDef[]> Commerce = new("data/commerce.json");
    private static readonly ModJsonFile<CorporationIdentityDef[]> Corporations = new("data/corporation-identity.json");
    private static readonly ModJsonFile<CultureLevelDef[]> CultureLevels = new("data/culture-level.json");
    private static readonly ModJsonFile<EraDef[]> Eras = new("data/era.json");
    private static readonly ModJsonFile<GameOptionDef[]> Options = new("data/game-option.json");
    private static readonly ModJsonFile<GameSpeedDef[]> Speeds = new("data/game-speed.json");
    private static readonly ModJsonFile<JsonArray> Handicaps = new("data/handicap.json");
    private static readonly ModJsonFile<SpecialistDef[]> Specialists = new("data/specialist.json");
    private static readonly ModJsonFile<VictoryIdentityDef[]> Victories = new("data/victory-identity.json");
    private static readonly ModJsonFile<ReligionIdentityDef[]> Religions = new("data/religion-identity.json");
    private static readonly ModJsonFile<WorldSizeDef[]> WorldSizes = new("data/world-size.json");
    private static readonly ModJsonFile<YieldDef[]> Yields = new("data/yield.json");
    private static readonly ModJsonFile<ConditionDef[]> Conditions = new("data/condition.json");
    private static readonly ModJsonFile<RuleConstantsDef[]> Constants = new("data/rule-constants.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [HexOverrides, Commerce, Corporations, CultureLevels, Eras, Options, Speeds, Handicaps,
            Specialists, Victories, Religions, WorldSizes, Yields, Conditions, Constants];

    private sealed class GameOptionRequirements : Ariel.Content.IContentRequirementEvaluator
    {
        public string Kind => "game-option";
        public Type DefinitionType => typeof(GameOptionDef);
        public string? Evaluate(Ariel.Content.Requirement.Leaf leaf, Ariel.Content.RequirementContext context) =>
            context.Sim.IsOptionEnabled(leaf.Id) ? null : $"Required game-option: {leaf.Id}";
    }
    private static void LoadHexHandicaps(IModContext context)
    {
        var tuning = HexOverrides.Read(context.Data).Single();
        if (tuning.BarbarianUnownedTilesMultiplier < 1 || tuning.AiMinimumGarrison < 1
            || tuning.AiThreatStrengthPerDefender < 1 || tuning.AiMaximumGarrison < tuning.AiMinimumGarrison)
            throw new InvalidDataException("Invalid hex barbarian or garrison tuning");
        // Adapt the loaded definitions, not the imported sidecar. Military consumes the effective handicap.
        var rows = Handicaps.Read(context.Data);
        foreach (var row in rows)
        {
            int tiles = row!["unownedTilesPerBarbarianUnit"]!.GetValue<int>();
            row["unownedTilesPerBarbarianUnit"] = checked(tiles * tuning.BarbarianUnownedTilesMultiplier);
            context.Content<HandicapDef>().Register(row.Deserialize<HandicapDef>(ModLoader.Json)!);
        }
    }

    public void Register(IModContext context)
    {
        context.RegisterService<Ariel.Game.ISetupPolicy>("setup_policy", new SetupPolicy());
        ContentRegistration.Load(context, HexOverrides);
        context.RegisterService<Ariel.Game.IRuleset>("ruleset", new Ariel.Game.GameRules());
        ContentRegistration.Load(context, Commerce);
        ContentRegistration.Load(context, Corporations);
        ContentRegistration.Load(context, CultureLevels);
        ContentRegistration.Load(context, Eras);
        ContentRegistration.Load(context, Options);
        ContentRegistration.Load(context, Speeds);
        LoadHexHandicaps(context);

        ContentRegistration.Load(context, Specialists);
        ContentRegistration.Load(context, Victories);
        ContentRegistration.Load(context, Religions);
        ContentRegistration.Load(context, WorldSizes);
        ContentRegistration.Load(context, Yields);
        ContentRegistration.Load(context, Conditions);
        ContentRegistration.Load(context, Constants);
        var calendar = new GameCalendar();
        foreach (var speed in Speeds.Read(context.Data)) calendar.ValidateShipped(speed);
        context.RegisterService<Ariel.Game.IGameCalendar>("calendar", calendar);
        ContentRegistration.Evaluator<EraDef>(context, "era");
        ContentRegistration.Evaluator<CultureLevelDef>(context, "culture-level");
        ContentRegistration.Evaluator<ConditionDef>(context, "condition");
        context.RegisterService<Ariel.Content.IRequirementEvaluator>("game-option_requirements", new GameOptionRequirements());

        // A corporation requirement is a branch in the city (CvCity.cpp:4039-4046). The mod that runs corporations
        // answers it; without one the leaf fails, which keeps corporation-gated buildings unbuildable rather than
        // granting them for free.
        ContentRegistration.Evaluator<CorporationIdentityDef>(context, "corporation", (leaf, subject) =>
            subject.City is { } city && subject.Sim.Rules().CityCorporations
                is { } corporations && corporations.HasIdentity(subject.Sim, city.Id, leaf.Id));
        ContentRegistration.Evaluator<VictoryIdentityDef>(context, "victory");
        // A religion requirement is the religion's presence in the city (no parameter), the player's state religion
        // (1) or the city's holy city status (2). The mod that runs religion answers; without one the leaf fails, so
        // religious buildings, missionaries and promotions stay unavailable rather than free.
        ContentRegistration.Evaluator<ReligionIdentityDef>(context, "religion", (leaf, subject) =>
            subject.Sim.Rules().CityReligions is { } religions && leaf.Param switch
            {
                1 => religions.IsStateReligion(subject.Sim, subject.Player, leaf.Id),
                2 => subject.City is { } city && religions.IsHolyCityOf(subject.Sim, city.Id, leaf.Id),
                null or 0 => subject.City is { } city && religions.Has(subject.Sim, city.Id, leaf.Id),
                _ => false,
            });
    }
}
