using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

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

    private int YieldValue(BuildingDef b, CityView v)
    {
        int flat = (int)(b.YieldChanges.Food * 8 + b.YieldChanges.Production * 8 + b.YieldChanges.Commerce * 5);
        int percent = (int)(b.YieldModifiers.Food * v.Food * 8 + b.YieldModifiers.Production * v.Production * 8
            + b.YieldModifiers.Commerce * v.Commerce * 5) / 100;
        var buildings = ctx.Service<ICityBuildingService>();
        bool powered = buildings.IsPowered(ctx.Sim, v.City.Id);
        if (powered) percent += YieldPercentValue(b.PowerYieldModifiers, v);
        return flat + percent + PowerValue(b, v, buildings, powered)
            + (int)(b.RiverPlotYieldChanges.Food + b.SeaPlotYieldChanges.Food + b.SeaPlotYieldChanges.Commerce) * 2;
    }

    private static int YieldPercentValue(YieldVector modifiers, CityView v) =>
        (int)(modifiers.Food * v.Food * 8 + modifiers.Production * v.Production * 8 + modifiers.Commerce * v.Commerce * 5) / 100;

    private int PowerValue(BuildingDef b, CityView v, ICityBuildingService buildings, bool powered)
    {
        if (!buildings.ProvidesPower(ctx.Sim, v.City.Id, b.Id)) return 0;
        int value = PowerHealthValue(b, v, buildings, powered);
        if (powered) return value;
        value += YieldPercentValue(b.PowerYieldModifiers, v);
        var poweredBuildings = CityBuildingRules.Active(ctx.Sim, v.City, buildings.EffectiveBuildings(ctx.Sim, v.City.Id));
        foreach (var existing in poweredBuildings)
            value += YieldPercentValue(existing.PowerYieldModifiers, v);
        return value;
    }

    private int PowerHealthValue(BuildingDef b, CityView v, ICityBuildingService buildings, bool powered)
    {
        var constants = ctx.Sim.Content.Get<RuleConstantsDef>().All.Single().Values;
        bool dirty = buildings.IsDirtyPower(ctx.Sim, v.City.Id);
        int current = powered ? constants.PowerHealthChange : 0;
        if (dirty) current += constants.DirtyPowerHealthChange;
        int next = constants.PowerHealthChange;
        if (b.DirtyPower && !b.AreaCleanPower && (!powered || dirty)) next += constants.DirtyPowerHealthChange;
        int healthWeight = v.HealthMargin <= 0 ? 12 : v.HealthMargin <= 1 ? 5 : 1;
        return (next - current) * healthWeight;
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
