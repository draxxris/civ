using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

internal sealed class ReligionEffects : IModifierSource
{
    /// <summary>Religion effects follow faiths, civics, traits and running buildings, never a turn's tallies, units,
    /// sight or worker orders in a kept (city, player or team) calculation. A unit's own calculation finds the city it
    /// stands in, but unit calculations are never kept.</summary>
    public IReadOnlyList<string>? UnreadDomains(ContentDb content) => CityStateDomains.UnreadThroughBuildings(CityStateDomains.NeverReadByBuildings, content);
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        var sim = context.Sim;
        var state = ReligionData.StateReligion(sim, context.Player);
        var city = context.City ?? (context.Unit is { } u ? sim.State.CityAt(u.Position) : null);
        if (city is null) return;
        if (context.Scope == ModifierScope.City) City(sim, city, state, sink);
        if (state is null || !ReligionData.Has(sim, city, state)) return;
        foreach (var civic in ReligionData.Civics(sim, context.Player))
        {
            if (context.Scope == ModifierScope.City) StateCivic(civic, sink);
            if (context.Scope == ModifierScope.Unit) sink.Add(civic.Id, "military:initial_experience", civic.Rules.StateReligionFreeExperience);
        }
    }
    private static void City(Simulation sim, City city, string? state, ModifierSink sink)
    {
        var presence = ReligionData.Present(sim, city);
        if (presence.Count == 0 && city.Buildings.Count == 0) return;
        var civics = ReligionData.Civics(sim, city.Owner).ToArray();
        bool allCommerce = sim.Rules().Peoples?.Traits(sim, city.Owner).Any(t => t.NonStateReligionCommerce) == true;
        foreach (var religion in presence)
        {
            var def = sim.Content.Get<ReligionDef>(religion);
            Happiness(sim, religion, state, civics, sink);
            if (state is null || religion == state || allCommerce)
            {
                sink.AddCommerce(def.Id, def.StateReligionCommerce);
                if (ReligionData.Game(sim).Founded.GetValueOrDefault(religion)?.HolyCity == city.Id) sink.AddCommerce(def.Id + ":holy_city", def.HolyCityCommerce);
            }
        }
        Buildings(sim, city, state, sink);
    }
    private static void Happiness(Simulation sim, string religion, string? state, HeldCivic[] civics, ModifierSink sink)
    {
        bool active = religion == state;
        int initial = active ? ReligionData.Raw(sim, "INITIAL_STATE_RELIGION_HAPPINESS", 1) : ReligionData.Raw(sim, "INITIAL_NON_STATE_RELIGION_HAPPINESS", 0);
        sink.Add(religion, "cities:happiness", initial);
        if (active) sink.Add(religion, "cities:health", ReligionData.Raw(sim, "INITIAL_STATE_RELIGION_HEALTH", 0));
        foreach (var civic in civics)
        {
            sink.Add(civic.Id, "cities:happiness", active ? civic.Rules.StateReligionHappiness : civic.Rules.NonStateReligionHappiness);
            if (active) sink.Add(civic.Id, "cities:health", civic.Rules.StateReligionHealth);
        }
    }
    private static void Buildings(Simulation sim, City city, string? state, ModifierSink sink)
    {
        var buildings = ReligionData.Active(sim, city).ToArray();
        var global = default(CommerceVector);
        if (state is not null && buildings.Any(b => ReligionData.Building(sim.Content, b.Id).Religion == state))
            global = sim.State.Cities.Where(c => c.Owner == city.Owner).SelectMany(c => ReligionData.Active(sim, c))
                .Aggregate(default(CommerceVector), (total, b) => total + ReligionData.Building(sim.Content, b.Id).StateReligionCommerces);
        foreach (var building in buildings)
        {
            var rules = ReligionData.Building(sim.Content, building.Id);
            if (state is not null && rules.Religion == state)
            {
                sink.Add(building.Id, "cities:happiness", rules.StateReligionHappiness);
                sink.AddCommerce(building.Id + ":state_religion", global);
            }
            if (rules.GlobalReligionCommerce is not { } faith) continue;
            var shrine = sim.Content.Get<ReligionDef>(faith);
            int cities = sim.State.Cities.Count(c => ReligionData.Has(sim, c, shrine.Id));
            sink.AddCommerce(building.Id + ":shrine", shrine.GlobalCityCommerce * cities);
        }
    }
    private static void StateCivic(HeldCivic civic, ModifierSink sink)
    {
        sink.Add(civic.Id, "cities:unit_production_percent", civic.Rules.StateReligionUnitProductionModifier);
        sink.Add(civic.Id, "cities:building_production_percent", civic.Rules.StateReligionBuildingProductionModifier);
        sink.Add(civic.Id, "cities:great_people_percent", civic.Rules.StateReligionGreatPeopleRateModifier);
    }
}
