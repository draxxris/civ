using Civ.Content.Schema;
using Civ.Modding;
using IModifierSource = Civ.Content.IModifierSource;
using Civ.Game;

namespace Civ.Mods.Peoples;

/// <summary>Identity, AI personality queries and leader trait contributions.</summary>
public sealed class PeoplesMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<CivilizationDef>(context, "civilization.json");
        ContentRegistration.Load<LeaderDef>(context, "leader.json");
        ContentRegistration.Load<TraitDef>(context, "trait.json");
        var peoples = new PeoplesService();
        context.RegisterService<IPeoplesService>("identity", peoples);
        context.RegisterService<IModifierSource>("traits", new TraitEffects(peoples));
    }
}
