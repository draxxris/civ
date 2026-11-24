using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;
using BuildingDef = Ariel.Content.Schema.BuildingDef;

namespace Ariel.Mods.Religion;

/// <summary>The <c>religion</c> requirement leaf and the <c>religion-condition</c> leaves, which only this mod's content
/// names. A religion leaf is the faith's presence in the city (no parameter), the player's state religion (1) or the
/// city's holy city status (2).</summary>
internal static class ReligionRequirements
{
    internal static void Register(IModContext context, ReligionService service)
    {
        ContentRegistration.Evaluator<ReligionDef>(context, ReligionFounder.Kind, (leaf, subject) => leaf.Param switch
        {
            1 => service.StateReligion(subject.Sim, subject.Player) == leaf.Id,
            2 => subject.City is { } city && service.HolyCity(subject.Sim, leaf.Id) == city.Id,
            null or 0 => subject.City is { } city && ReligionData.Has(subject.Sim, city, leaf.Id),
            _ => false,
        });
        ContentRegistration.Evaluator<ReligionConditionDef>(context, "religion-condition", (leaf, subject) => subject.City is { } city && leaf.Id switch
        {
            ReligionConditionDef.AnyReligion => service.Present(subject.Sim, city.Id).Count > 0,
            ReligionConditionDef.StateReligion => service.StateReligion(subject.Sim, subject.Player) is { } state && ReligionData.Has(subject.Sim, city, state),
            _ => false,
        });
    }
}

/// <summary>What the state religion allows a city to produce: a religious building only of the state religion (unless
/// it is free from it, or a civic frees every faith's buildings), a building that needs a state religion only with one,
/// and an inquisitor only where civics and the state religion allow inquisitions. A founding wonder is
/// <see cref="ReligionFounder"/>'s to refuse.</summary>
internal sealed class ReligionProductionRules(ReligionService service) : IBuildingRule, IUnitTrainingRule
{
    public string? RefuseBuilding(Simulation sim, City city, BuildingDef building)
    {
        var rules = ReligionData.Building(sim.Content, building.Id);
        if (rules.FreeFromStateReligion) return null;
        var state = service.StateReligion(sim, city.Owner);
        // A building that requires a state religion cannot be built without one, even when it names no religion.
        if (state is null) return rules.RequiresStateReligion ? "Requires an active state religion" : null;
        if (ReligionData.Civics(sim, city.Owner).Any(c => c.Rules.FreeReligion)) return null;
        var required = ContentReferences.Leaves(building.Requirements ?? new Requirement.All([]))
            .FirstOrDefault(l => l.Kind == "religion" && l.Param is null);
        return required is null || required.Id == state ? null
            : "Requires matching state religion: " + required.Id;
    }

    public string? RefuseUnit(Simulation sim, City city, UnitDef unit) =>
        ReligionData.IsInquisitor(sim.Content, unit.Id) && !service.IsInquisitionEnabled(sim, city.Owner) ? "Civics and state religion do not allow inquisitions" : null;
}

/// <summary>The computer player keeps a holy city it conquers rather than razing it.</summary>
internal sealed class HolyCityProtection(ReligionService service) : IRazeProtection
{
    public bool Protects(Simulation sim, City city) =>
        sim.Content.Get<ReligionDef>().All.Any(religion => service.HolyCity(sim, religion.Id) == city.Id);
}

/// <summary>The standard city planner leaves religious buildings to the religion rules: a building of a faith, or one
/// that needs a state religion, is never one of its general choices.</summary>
internal sealed class ReligiousBuildingExclusion : Ariel.Ai.IAiBuildingExclusion
{
    public bool Excludes(ContentDb content, BuildingDef building) => ReligionData.Building(content, building.Id) is var rules
        && (rules.RequiresReligion || rules.RequiresStateReligion || rules.Religion is not null);
}

/// <summary>Followers of a religion remember who razed its holy city. The founding record keeps the holy city's id after
/// the city is gone, so the religions are read from it.</summary>
internal static class HolyCityRazing
{
    internal static void Razed(Simulation sim, CityRazed razed)
    {
        var founded = ReligionData.Game(sim).Founded;
        var relations = sim.Rules().Relations;
        foreach (var religion in sim.Content.Get<ReligionDef>().All.Where(r => founded.GetValueOrDefault(r.Id)?.HolyCity == razed.City))
            foreach (var player in sim.State.Players.OrderBy(p => p.Id).Where(p => ReligionData.StateReligion(sim, p.Id) == religion.Id))
                relations.Remember(sim, player.Id, razed.Player, ReligionMemories.RazedHolyCity);
    }
}
