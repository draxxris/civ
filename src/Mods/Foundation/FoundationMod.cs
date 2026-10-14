using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.Foundation;

/// <summary>Foundation definitions, shared hex tuning and new-game setup.</summary>
public sealed class FoundationMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<HexOverridesDef>(context, "hex-overrides.json");
        context.RegisterService<Civ.Game.INewGameSetup>("new_game", new NewGameSetup());
        ContentRegistration.Load<CommerceDef>(context, "commerce.json");
        ContentRegistration.Load<CorporationIdentityDef>(context, "corporation-identity.json");
        ContentRegistration.Load<CultureLevelDef>(context, "culture-level.json");
        ContentRegistration.Load<EraDef>(context, "era.json");
        ContentRegistration.Load<GameOptionDef>(context, "game-option.json");
        ContentRegistration.Load<GameSpeedDef>(context, "game-speed.json");
        ContentRegistration.Load<HandicapDef>(context, "handicap.json");

        ContentRegistration.Load<SpecialistDef>(context, "specialist.json");
        ContentRegistration.Load<VictoryIdentityDef>(context, "victory-identity.json");
        ContentRegistration.Load<WorldSizeDef>(context, "world-size.json");
        ContentRegistration.Load<YieldDef>(context, "yield.json");
        ContentRegistration.Load<ConditionDef>(context, "condition.json");
        ContentRegistration.Load<RuleConstantsDef>(context, "rule-constants.json");
        ContentRegistration.Evaluator<EraDef>(context, "era");
        ContentRegistration.Evaluator<CultureLevelDef>(context, "culture-level");
        ContentRegistration.Evaluator<ConditionDef>(context, "condition");
        ContentRegistration.Evaluator<GameOptionDef>(context, "game-option");

        ContentRegistration.Evaluator<CorporationIdentityDef>(context, "corporation");
        ContentRegistration.Evaluator<VictoryIdentityDef>(context, "victory");
    }
}
