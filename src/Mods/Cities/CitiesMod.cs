using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.Cities;

/// <summary>Cities definitions and requirement identities. Gameplay systems follow separately.</summary>
public sealed class CitiesMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<BuildingCategoryDef>(context, "building-category.json");
        ContentRegistration.Load<BuildingDef>(context, "building.json");
        ContentRegistration.Load<HurryDef>(context, "hurry.json");
        ContentRegistration.Evaluator<BuildingDef>(context, "building", (leaf, subject) => subject.City?.Buildings.Count(id => id == leaf.Id) >= (leaf.Count ?? 1));
        ContentRegistration.Evaluator<BuildingDef>(context, "building-class");
    }
}
