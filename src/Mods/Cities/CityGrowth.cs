using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityGrowth(ICityYieldService yields, ICityWellbeingService wellbeing, ICityWorkService work) : ICityGrowthService
{
    public CityGrowthResult Calculate(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var effects = CityData.Effects(sim, city);
        var sink = new ModifierSink();
        long threshold = Threshold(sim, city, effects, sink);
        var health = wellbeing.Calculate(sim, cityId);
        long normal = city.Population * CityData.Constants(sim).FoodConsumptionPerPopulation;
        long food = yields.Calculate(sim, cityId).Yields.Food;
        long waste = Waste(sim, food - normal - health.FoodPenalty, normal);
        long consumption = normal + health.FoodPenalty + waste;
        long difference = food - consumption;
        if (FoodProduction(sim, city)) difference = Math.Min(0, difference);
        if (CityData.Read(sim, city).Disorder) difference = 0;
        int kept = (int)Math.Clamp(CityData.Effect(effects, "food_kept_percent"), 0, 100);
        sink.Add("cities:population", "cities:food_consumption", normal);
        sink.Add("cities:health", "cities:food_consumption", health.FoodPenalty);
        sink.Add("cities:wastage", "cities:food_consumption", waste);
        sink.Add("cities:total", "cities:food_difference", difference);
        sink.Add("cities:buildings", "cities:food_kept_percent", kept);
        return new(threshold, consumption, difference, kept, sink.Ledger);
    }
    private long Threshold(Simulation sim, City city, ModifierSink effects, ModifierSink ledger)
    {
        long threshold = CityData.Constants(sim).BaseCityGrowthThreshold + city.Population * CityData.Constants(sim).CityGrowthMultiplier;
        ledger.Add("cities:population", "cities:growth_base", threshold);
        threshold = Step(threshold, CityData.Speed(sim).GrowthPercent, "cities:speed", ledger);
        threshold = Step(threshold, CityData.Era(sim, city.Owner).GrowthPercent, "cities:era", ledger);
        if (sim.State.PlayerById(city.Owner).Ai is not null) threshold = Step(threshold, CityData.Handicap(sim).AiGrowthPercent, "cities:handicap", ledger);
        if (CityData.PlayerState(sim, city.Owner).GoldenAge) threshold = Step(threshold, 100 + CityData.Raw(sim, "GOlDEN_AGE_PERCENT_LESS_FOOD_FOR_GROWTH", -25), "cities:golden_age", ledger);
        threshold = Step(threshold, 100 + ModifierSink.Collect(new(sim, ModifierScope.Player, city.Owner)).Ledger.Total("cities:growth_percent"), "cities:player", ledger);
        threshold = Step(threshold, 100 + CityData.Effect(effects, "growth_percent"), "cities:city", ledger);
        if (work.Radius(sim, city.Id) == 3) threshold = Step(threshold, 100 + CityData.Constants(sim).CityThirdRingExtraGrowthThresholdPercent, "cities:third_ring", ledger);
        threshold = Math.Max(1, threshold);
        ledger.Add("cities:total", "cities:growth_threshold", threshold);
        return threshold;
    }
    private static long Step(long value, long percent, string source, ModifierSink ledger)
    {
        ledger.Add(source, "cities:growth_factor_percent", percent);
        return value * percent / 100;
    }
    internal static bool FoodProduction(Simulation sim, City city) => city.ProductionQueue.FirstOrDefault() is { Kind: "cities:unit" } order && sim.Content.Get<S.UnitDef>(order.ContentId).FoodProduction;
    internal static long Waste(Simulation sim, long surplus, long normal)
    {
        int start = CityData.Raw(sim, "WASTAGE_START_CONSUMPTION_PERCENT", -1);
        if (start < 0) return 0;
        long s = surplus - normal * start / 100;
        if (s <= 0) return 0;
        const long scale = 1_000_000_000;
        long g = CityData.Constants(sim).FoodWastageGrowthPermille;
        if (g == 0) g = 50;
        long power = scale, total = 0;
        // Fixed-point adaptation of W(s); the native cache uses entries 0..499, then a linear tail.
        for (long i = 1; i <= Math.Min(s, 499); i++)
        {
            power = power * (1000 - g) / 1000;
            total += scale - (g * scale + 1000 * power) / (1000 + g);
        }
        return total / scale + Math.Max(0, s - 499);
    }
    internal void Run(Simulation sim, City city)
    {
        var result = Calculate(sim, city.Id);
        var state = CityData.Edit(sim, city);
        city.FoodStored += result.Difference;
        state.FoodKept = Math.Clamp(state.FoodKept + result.Difference, 0, result.Threshold * result.FoodKeptPercent / 100);
        if (city.FoodStored >= result.Threshold)
        {
            if (state.AvoidGrowth) city.FoodStored = result.Threshold;
            else
            {
                city.FoodStored -= Math.Max(0, result.Threshold - state.FoodKept);
                city.Population++;
                sim.Events.Publish(new CityGrew(city.Id, city.Population));
            }
        }
        else if (city.FoodStored < 0)
        {
            city.FoodStored = 0;
            city.Population = Math.Max(1, city.Population - 1);
        }
        work.Assign(sim, city.Id);
    }
}
