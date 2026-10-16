using Civ.Content.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using Civ.Modding;

namespace Civ.Mods.Foundation;

/// <summary>Foundation definitions, shared hex tuning and new-game setup.</summary>
public sealed class FoundationMod : IMod
{
    private sealed class GameOptionRequirements : Civ.Content.IContentRequirementEvaluator
    {
        public string Kind => "game-option";
        public Type DefinitionType => typeof(GameOptionDef);
        public string? Evaluate(Civ.Content.Requirement.Leaf leaf, Civ.Content.RequirementContext context) =>
            context.Sim.IsOptionEnabled(leaf.Id) ? null : $"Required game-option: {leaf.Id}";
    }
    private static void LoadHexHandicaps(IModContext context)
    {
        var tuning = context.Data.ReadJson<HexOverridesDef[]>("data/hex-overrides.json").Single();
        if (tuning.BarbarianUnownedTilesMultiplier < 1 || tuning.AiMinimumGarrison < 1
            || tuning.AiThreatStrengthPerDefender < 1 || tuning.AiMaximumGarrison < tuning.AiMinimumGarrison)
            throw new InvalidDataException("Invalid hex barbarian or garrison tuning");
        // Adapt the loaded definitions, not the imported sidecar. Military consumes the effective handicap.
        var rows = context.Data.ReadJson<JsonArray>("data/handicap.json");
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
        ContentRegistration.Load<HexOverridesDef>(context, "hex-overrides.json");
        context.RegisterService<Civ.Game.INewGameSetup>("new_game", new NewGameSetup());
        ContentRegistration.Load<CommerceDef>(context, "commerce.json");
        ContentRegistration.Load<CorporationIdentityDef>(context, "corporation-identity.json");
        ContentRegistration.Load<CultureLevelDef>(context, "culture-level.json");
        ContentRegistration.Load<EraDef>(context, "era.json");
        ContentRegistration.Load<GameOptionDef>(context, "game-option.json");
        ContentRegistration.Load<GameSpeedDef>(context, "game-speed.json");
        LoadHexHandicaps(context);

        ContentRegistration.Load<SpecialistDef>(context, "specialist.json");
        ContentRegistration.Load<VictoryIdentityDef>(context, "victory-identity.json");
        ContentRegistration.Load<WorldSizeDef>(context, "world-size.json");
        ContentRegistration.Load<YieldDef>(context, "yield.json");
        ContentRegistration.Load<ConditionDef>(context, "condition.json");
        ContentRegistration.Load<RuleConstantsDef>(context, "rule-constants.json");
        ContentRegistration.Evaluator<EraDef>(context, "era");
        ContentRegistration.Evaluator<CultureLevelDef>(context, "culture-level");
        ContentRegistration.Evaluator<ConditionDef>(context, "condition");
        context.RegisterService<Civ.Content.IRequirementEvaluator>("game-option_requirements", new GameOptionRequirements());

        ContentRegistration.Evaluator<CorporationIdentityDef>(context, "corporation");
        ContentRegistration.Evaluator<VictoryIdentityDef>(context, "victory");
    }
}
