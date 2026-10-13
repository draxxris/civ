using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal static class Specialists
{
    internal const string Citizen = "foundation:specialist_citizen";
    internal static int Slots(Simulation sim, City city, string id, ModifierSink effects) =>
        id == Citizen || CityData.Effect(effects, "unlimited:" + id) > 0 ? city.Population : (int)Math.Max(0, CityData.Effect(effects, "slots:" + id));
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
    private static void AddModifiers(string id, int count, ModifierSink effects)
    {
        // Specialist changes use hundredths of a point, despite their historical percent field names.
        var lines = effects.Ledger.Lines.ToArray();
        foreach (var line in lines)
        {
            if (!line.Key.StartsWith("cities:specialist_", StringComparison.Ordinal)) continue;
            if (!line.Key.EndsWith(":" + id, StringComparison.Ordinal) && !line.Key.EndsWith(":all", StringComparison.Ordinal)) continue;
            string kind = line.Key["cities:specialist_".Length..].Split(':')[0];
            long value = line.Amount * count;
            AddSpecialistValue(line.Source, kind, value, effects);
        }
    }
    private static void AddSpecialistValue(string source, string kind, long value, ModifierSink sink)
    {
        switch (kind)
        {
            case "food_hundredths": sink.AddYield(source, new(value / 100, 0, 0)); break;
            case "production_hundredths": sink.AddYield(source, new(0, value / 100, 0)); break;
            case "commerce_hundredths": sink.AddYield(source, new(0, 0, value / 100)); break;
            default: sink.Add(source, "cities:direct_" + kind, value); break;
        }
    }
    internal static void Contribute(Simulation sim, City city, ModifierSink effects)
    {
        var buildings = CityData.Active(sim, city).ToArray();
        foreach (var pair in All(sim, city, effects))
        {
            var def = sim.Content.Get<S.SpecialistDef>(pair.Key);
            effects.AddYield(def.Id, def.Yields * pair.Value);
            effects.AddCommerce(def.Id, def.Commerces * pair.Value);
            AddModifiers(def.Id, pair.Value, effects);
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
