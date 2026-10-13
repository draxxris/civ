using Civ.Content.Schema;
using Civ.Modding;
using IModifierSource = Civ.Content.IModifierSource;
using Civ.Game;

namespace Civ.Mods.Government;

/// <summary>Civic adoption, anarchy and derived civic effects.</summary>
public sealed class GovernmentMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<CivicDef>(context, "civic.json");
        ContentRegistration.Load<CivicOptionDef>(context, "civic-option.json");
        ContentRegistration.Load<UpkeepDef>(context, "upkeep.json");
        ContentRegistration.Evaluator<CivicDef>(context, "civic", (leaf, subject) =>
            (subject.ProposedCivics ?? subject.Sim.State.PlayerById(subject.Player).Civics).Values.Contains(leaf.Id, StringComparer.Ordinal));
        context.RegisterService<IGovernmentService>("civics", new GovernmentService());
        context.RegisterService<IModifierSource>("civics", new CivicEffects());
        context.RegisterStateComponent<GovernmentPlayerState>("player_state");
        context.RegisterCommand<SetCivicsCommand>("set_civics");
        context.TurnSystems.Register(new GovernmentTimers());
    }
}
