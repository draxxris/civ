using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

/// <summary>Only military grants are handled here until Government and Peoples provide full modifier sources.</summary>
internal sealed class TrainingEffects : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.Unit || context.Unit is not { } unit) return;
        var player = context.Sim.State.PlayerById(unit.Owner);
        foreach (var civic in player.Civics.Values.Order(StringComparer.Ordinal).Select(context.Sim.Content.Get<S.CivicDef>))
        {
            sink.Add(civic.Id, "military:initial_experience", civic.FreeExperience);
            sink.Add(civic.Id, "military:experience_in_borders_percent", civic.ExpInBorderModifier);
            sink.Add(civic.Id, "military:city_defense", civic.ExtraCityDefense);
        }
        if (!context.Sim.Content.TryGet<S.LeaderDef>(player.Leader, out var leader)) return;
        string combat = MilitaryData.Definition(context.Sim, unit).CombatType ?? "";
        foreach (var trait in leader.Traits.Where(t => t.Value).OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => context.Sim.Content.Get<S.TraitDef>(t.Key)))
        {
            sink.Add(trait.Id, "military:level_experience_percent", trait.ExperienceThresholdPercent);
            foreach (var grant in trait.CombatTypePromotions.OrderBy(g => g.Key, StringComparer.Ordinal))
                if (grant.Value.Contains(combat)) sink.Add(trait.Id, "military:free_promotion:" + grant.Key, 1);
        }
    }
}
