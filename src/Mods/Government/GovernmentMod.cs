using Civ.Content.Schema;
using Civ.Modding;
using IModifierSource = Civ.Content.IModifierSource;
using Civ.Game;

namespace Civ.Mods.Government;

/// <summary>Civic adoption, anarchy and derived civic effects.</summary>
public sealed class GovernmentMod : IPreparableMod
{
    private static readonly ModJsonFile<CivicDef[]> Civics = new("data/civic.json");
    private static readonly ModJsonFile<CivicOptionDef[]> Options = new("data/civic-option.json");
    private static readonly ModJsonFile<UpkeepDef[]> Upkeep = new("data/upkeep.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Civics, Options, Upkeep];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Civics);
        ContentRegistration.Load(context, Options);
        ContentRegistration.Load(context, Upkeep);
        ContentRegistration.Evaluator<CivicDef>(context, "civic", (leaf, subject) =>
            (subject.ProposedCivics ?? subject.Sim.State.PlayerById(subject.Player).Civics).Values.Contains(leaf.Id, StringComparer.Ordinal));
        context.RegisterService<IGovernmentService>("civics", new GovernmentService());
        context.RegisterService<IModifierSource>("civics", new CivicEffects());
        context.RegisterStateComponent<GovernmentPlayerState>("player_state");
        context.RegisterCommand<SetCivicsCommand>("set_civics");
        context.TurnSystems.Register(new GovernmentTimers());
    }
}
