using Civ.Content;

namespace Civ.BaseGame;

/// <summary>The test ruleset owns the tech requirement kind.</summary>
internal sealed class TechRequirementEvaluator : IRequirementEvaluator
{
    public string Kind => "tech";
    public string? Evaluate(Requirement.Leaf leaf, RequirementContext context) =>
        context.Sim.KnowsTech(context.Player, leaf.Id) ? null : $"Technology not known: {leaf.Id}";
}

/// <summary>Example cross-mod civilization effects, expressed as typed contributions.</summary>
internal sealed class CivilizationModifiers : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.Player) return;
        if (context.Sim.CivilizationOf(context.Player) is not { } civ) return;
        sink.AddYield(civ.Name, new(0, civ.ProductionBonus, 0), percent: true);
        sink.AddCommerce(civ.Name, new(0, civ.ResearchBonus, civ.CultureBonus, 0), percent: true);
    }
}

/// <summary>Example government effects, independent of the calculating turn systems.</summary>
internal sealed class CivicModifiers : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.Player) return;
        foreach (var id in context.Sim.State.PlayerById(context.Player).Civics.Values)
        {
            var civic = context.Sim.Content.Civics.Get(id);
            sink.AddYield(civic.Name, new(0, civic.ProductionBonus, 0), percent: true);
            sink.AddCommerce(civic.Name, new(0, civic.ResearchBonus, civic.CultureBonus, 0), percent: true);
        }
    }
}
