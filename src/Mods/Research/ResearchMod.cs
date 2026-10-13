using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.Research;

/// <summary>Research definitions and requirement identities. Gameplay systems follow separately.</summary>
public sealed class ResearchMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<TechDef>(context, "tech.json");
        ContentRegistration.Evaluator<TechDef>(context, "tech", (leaf, subject) => subject.Sim.KnowsTech(subject.Player, leaf.Id));
    }
}
