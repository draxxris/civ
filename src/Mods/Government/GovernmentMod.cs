using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.Government;

/// <summary>Government definitions and requirement identities. Gameplay systems follow separately.</summary>
public sealed class GovernmentMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<CivicDef>(context, "civic.json");
        ContentRegistration.Load<CivicOptionDef>(context, "civic-option.json");
        ContentRegistration.Load<UpkeepDef>(context, "upkeep.json");
        ContentRegistration.Evaluator<CivicDef>(context, "civic", (leaf, subject) => subject.Sim.State.PlayerById(subject.Player).Civics.Values.Contains(leaf.Id, StringComparer.Ordinal));
    }
}
