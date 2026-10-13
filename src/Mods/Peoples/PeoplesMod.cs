using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.Peoples;

/// <summary>Peoples definitions and requirement identities. Gameplay systems follow separately.</summary>
public sealed class PeoplesMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<CivilizationDef>(context, "civilization.json");
        ContentRegistration.Load<LeaderDef>(context, "leader.json");
        ContentRegistration.Load<TraitDef>(context, "trait.json");
    }
}
