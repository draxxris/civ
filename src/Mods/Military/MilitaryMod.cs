using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.Military;

/// <summary>Military definitions and requirement identities. Gameplay systems follow separately.</summary>
public sealed class MilitaryMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<DomainDef>(context, "domain.json");
        ContentRegistration.Load<InvisibilityDef>(context, "invisibility.json");
        ContentRegistration.Load<MissionDef>(context, "mission.json");
        ContentRegistration.Load<OutcomeDef>(context, "outcome.json");
        ContentRegistration.Load<PromotionDef>(context, "promotion.json");
        ContentRegistration.Load<UnitAiDef>(context, "unit-ai.json");
        ContentRegistration.Load<UnitCategoryDef>(context, "unit-category.json");
        ContentRegistration.Load<UnitCombatDef>(context, "unit-combat.json");
        ContentRegistration.Load<UnitDef>(context, "unit.json");
        ContentRegistration.Evaluator<UnitDef>(context, "unit", (leaf, subject) => subject.Unit?.Type == leaf.Id);
        ContentRegistration.Evaluator<UnitDef>(context, "unit-class");
        ContentRegistration.Evaluator<UnitCombatDef>(context, "unit-combat");
        ContentRegistration.Evaluator<PromotionDef>(context, "promotion", (leaf, subject) => subject.Unit?.Promotions.Contains(leaf.Id) == true);
    }
}
