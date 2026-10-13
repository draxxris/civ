using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.Economy;

/// <summary>Economy definitions and requirement identities. Gameplay systems follow separately.</summary>
public sealed class EconomyMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<ProcessDef>(context, "process.json");
    }
}
