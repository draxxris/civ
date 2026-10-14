using Civ.Content.Schema;

namespace Civ.Mods.Ai;

/// <summary>Flavor-weighted integer value of a building in a specific city.
/// Happiness and health count heavily only when the city is at its limit.</summary>
internal sealed class BuildingValuer(AiContext ctx)
{
    public int Value(BuildingDef b, CityView v)
    {
        int value = ctx.FlavorScore(b.Flavors);
        value += YieldValue(b, v) + CommerceValue(b, v) + WellbeingValue(b, v);
        value += b.FreeSpecialist * 8 + b.SpecialistSlots.Values.Sum() * 2 + b.PopulationGrowthPercent / 5 + b.FoodKept / 6;
        value += v.Threat > 0 ? b.DefensePercent / 4 : b.DefensePercent / 20;
        value += b.GreatPeopleRateChange * 2 + b.FreeTechs * 40 + b.Experience * 3;
        value += Math.Min(Math.Max(b.AiWeight, 0), 1000) / 25;
        return value;
    }

    public static bool IsWonder(BuildingDef b) => b.Limits.World >= 0 || b.Limits.Team >= 0 || b.Limits.Player >= 0 && !b.Limits.NoLimit;

    private int YieldValue(BuildingDef b, CityView v)
    {
        int flat = (int)(b.YieldChanges.Food * 8 + b.YieldChanges.Production * 8 + b.YieldChanges.Commerce * 5);
        int percent = (int)(b.YieldModifiers.Food * v.Food * 8 + b.YieldModifiers.Production * v.Production * 8
            + b.YieldModifiers.Commerce * v.Commerce * 5) / 100;
        return flat + percent + (int)(b.RiverPlotYieldChanges.Food + b.SeaPlotYieldChanges.Food + b.SeaPlotYieldChanges.Commerce) * 2;
    }

    private int CommerceValue(BuildingDef b, CityView v)
    {
        var c = b.CommerceChanges;
        int flat = (int)(c.Gold * 5 + c.Research * (6 + ctx.Weight("science") / 5) + c.Culture * (3 + ctx.Weight("culture") / 8) + c.Espionage);
        var m = b.CommerceModifiers;
        int percent = (int)(m.Gold * 5 + m.Research * (6 + ctx.Weight("science") / 5) + m.Culture * 3) * v.Commerce / 100;
        return flat + percent + (int)b.CapitalExtraCommerce;
    }

    private static int WellbeingValue(BuildingDef b, CityView v)
    {
        int happy = b.Happiness + b.AreaHappiness / 2;
        int health = b.Health + b.AreaHealth / 2;
        int happyWeight = v.HappyMargin <= 0 ? 16 : v.HappyMargin <= 1 ? 6 : 2;
        int healthWeight = v.HealthMargin <= 0 ? 12 : v.HealthMargin <= 1 ? 5 : 1;
        return happy * happyWeight + health * healthWeight;
    }
}
