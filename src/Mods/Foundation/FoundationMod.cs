using Civ.Content.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using Civ.Modding;

namespace Civ.Mods.Foundation;

/// <summary>Foundation definitions, shared hex tuning and new-game setup.</summary>
public sealed class FoundationMod : IPreparableMod
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
    private static readonly ModJsonFile<WorldSizeDef[]> WorldSizes = new("data/world-size.json");
    private static readonly ModJsonFile<YieldDef[]> Yields = new("data/yield.json");
    private static readonly ModJsonFile<ConditionDef[]> Conditions = new("data/condition.json");
    private static readonly ModJsonFile<RuleConstantsDef[]> Constants = new("data/rule-constants.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [HexOverrides, Commerce, Corporations, CultureLevels, Eras, Options, Speeds, Handicaps,
            Specialists, Victories, WorldSizes, Yields, Conditions, Constants];

    private sealed class GameOptionRequirements : Civ.Content.IContentRequirementEvaluator
    {
        public string Kind => "game-option";
        public Type DefinitionType => typeof(GameOptionDef);
        public string? Evaluate(Civ.Content.Requirement.Leaf leaf, Civ.Content.RequirementContext context) =>
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
        context.RegisterService<Civ.Game.ISetupPolicy>("setup_policy", new SetupPolicy());
        ContentRegistration.Load(context, HexOverrides);
        context.RegisterService<Civ.Game.INewGameSetup>("new_game", new NewGameSetup());
        ContentRegistration.Load(context, Commerce);
        ContentRegistration.Load(context, Corporations);
        ContentRegistration.Load(context, CultureLevels);
        ContentRegistration.Load(context, Eras);
        ContentRegistration.Load(context, Options);
        ContentRegistration.Load(context, Speeds);
        LoadHexHandicaps(context);

        ContentRegistration.Load(context, Specialists);
        ContentRegistration.Load(context, Victories);
        ContentRegistration.Load(context, WorldSizes);
        ContentRegistration.Load(context, Yields);
        ContentRegistration.Load(context, Conditions);
        ContentRegistration.Load(context, Constants);
        var calendar = new GameCalendar();
        foreach (var speed in Speeds.Read(context.Data)) calendar.ValidateShipped(speed);
        context.RegisterService<Civ.Game.IGameCalendar>("calendar", calendar);
        ContentRegistration.Evaluator<EraDef>(context, "era");
        ContentRegistration.Evaluator<CultureLevelDef>(context, "culture-level");
        ContentRegistration.Evaluator<ConditionDef>(context, "condition");
        context.RegisterService<Civ.Content.IRequirementEvaluator>("game-option_requirements", new GameOptionRequirements());

        ContentRegistration.Evaluator<CorporationIdentityDef>(context, "corporation");
        ContentRegistration.Evaluator<VictoryIdentityDef>(context, "victory");
    }
}
