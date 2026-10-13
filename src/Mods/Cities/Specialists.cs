using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal static class Specialists
{
    internal const string Citizen = "foundation:specialist_citizen";
    internal static int Slots(Simulation sim, City city, string id, ModifierSink effects) => id == Citizen ? city.Population : (int)Math.Max(0, CityData.Effect(effects, "slots:" + id));
    internal static SortedDictionary<string, int> Free(Simulation sim, City city, ModifierSink effects)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var def in sim.Content.Get<S.SpecialistDef>().All)
        {
            int count = (int)Math.Max(0, CityData.Effect(effects, "free:" + def.Id));
            if (count > 0) result[def.Id] = count;
        }
        int generic = (int)Math.Max(0, CityData.Effect(effects, "free_specialists"));
        if (generic > 0) result[Citizen] = result.GetValueOrDefault(Citizen) + generic;
        return result;
    }
    internal static SortedDictionary<string, int> All(Simulation sim, City city, ModifierSink effects)
    {
        var result = Free(sim, city, effects);
        foreach (var pair in city.Specialists) result[pair.Key] = result.GetValueOrDefault(pair.Key) + pair.Value;
        return result;
    }
    internal static void Contribute(Simulation sim, City city, ModifierSink effects)
    {
        var buildings = CityData.Active(sim, city).ToArray();
        foreach (var pair in All(sim, city, effects))
        {
            var def = sim.Content.Get<S.SpecialistDef>(pair.Key);
            effects.AddYield(def.Id, def.Yields * pair.Value);
            effects.AddCommerce(def.Id, def.Commerces * pair.Value);
            foreach (var building in buildings)
            {
                effects.AddYield(building.Id + ":" + def.Id, building.SpecialistYieldChanges.GetValueOrDefault(def.Id) * pair.Value);
                effects.AddCommerce(building.Id + ":" + def.Id, building.SpecialistExtraCommerces * pair.Value);
                // These source fields are hundredths of a commerce point per specialist, not city rate percentages.
                var fraction = building.SpecialistCommercePercentChanges.GetValueOrDefault(def.Id) * pair.Value;
                effects.Add(building.Id, "cities:direct_gold_hundredths", fraction.Gold);
                effects.Add(building.Id, "cities:direct_research_hundredths", fraction.Research);
                effects.Add(building.Id, "cities:direct_culture_hundredths", fraction.Culture);
                effects.Add(building.Id, "cities:direct_espionage_hundredths", fraction.Espionage);
            }
        }
    }
}
