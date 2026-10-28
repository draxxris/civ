using System.Numerics;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Cities;

internal sealed class CityMaintenance : ICityMaintenanceService
{
    private const long Scale = 1_000_000_000_000;
    public CityMaintenanceResult Calculate(Simulation sim, int cityId) => CityReadScope.Run(sim, _ => CalculateScoped(sim, cityId));
    private CityMaintenanceResult CalculateScoped(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var effects = CityData.Effects(sim, city);
        var sink = new ModifierSink();
        long distance = Distance(sim, city, effects);
        long count = CityCount(sim, city, effects);
        long flat = CorporateMaintenance(effects.Ledger);
        long total = (distance + count + flat) * Math.Max(0, 100 + CityData.Effect(effects, "maintenance_percent")) / 100;
        sink.Add("cities:distance", "cities:maintenance_hundredths", distance);
        sink.Add("cities:city_count", "cities:maintenance_hundredths", count);
        sink.Add("cities:flat", "cities:maintenance_hundredths", flat);
        sink.Add("cities:modifiers", "cities:maintenance_hundredths", total - distance - count - flat);
        if (GovernmentStatus.IsDisorder(sim, city)) { sink.Add("cities:disorder", "cities:maintenance_hundredths", -total); total = 0; }
        return new(distance, count, total, sink.Ledger, flat);
    }
    private static long CorporateMaintenance(Ledger effects)
    {
        var flat = effects.Lines.Where(l => l.Key == "cities:maintenance_flat_hundredths").ToArray();
        var sources = flat.Select(l => l.Source).ToHashSet(StringComparer.Ordinal);
        // A source can expose both keys. Its city-cost contribution already includes its corporate cost.
        return flat.Sum(l => l.Amount) + effects.Lines
            .Where(l => l.Key == "corporations:maintenance_hundredths" && !sources.Contains(l.Source)).Sum(l => l.Amount);
    }
    private static long Distance(Simulation sim, City city, ModifierSink effects)
    {
        long worst = 0, best = long.MaxValue;
        var owned = CityData.Owned(sim, city.Owner).ToArray();
        var player = ModifierSink.Collect(new(sim, ModifierScope.Player, city.Owner));
        long modifier = player.Ledger.Total("cities:distance_maintenance_percent") + CityData.Effect(effects, "distance_maintenance_percent");
        long coastal = player.Ledger.Total("cities:coastal_maintenance_percent") + CityData.Effect(effects, "coastal_maintenance_percent");
        bool coast = CityData.Coastal(sim, city, CityData.Raw(sim, "MIN_WATER_SIZE_FOR_OCEAN", 10));
        foreach (var other in owned)
        {
            long value = 100L * CityData.Constants(sim).MaxDistanceCityMaintenance * sim.State.Grid.Distance(CityData.Tile(sim, city), CityData.Tile(sim, other));
            value = value * (city.Population + 7) / 10;
            value = value * Math.Max(0, 100 + modifier) / 100;
            if (coast) value = value * Math.Max(0, 100 + coastal) / 100;
            value = value * CityData.World(sim).DistanceMaintenancePercent / 100;
            value = value * CityData.Handicap(sim).DistanceMaintenancePercent / 100;
            value /= Math.Max(1, sim.State.Grid.MaximumDistance);
            if (sim.State.PlayerById(city.Owner).Ai is not null && owned.Length < 3) value /= 3;
            worst = Math.Max(worst, value);
            if (CityData.Active(sim, other).Any(b => b.GovernmentCenter || b.Capital)) best = Math.Min(best, value);
        }
        return Math.Min(worst, best);
    }
    private static long CityCount(Simulation sim, City city, ModifierSink effects)
    {
        int n = CityData.Owned(sim, city.Owner).Count();
        if (n == 0) return 0;
        var world = CityData.World(sim); var handicap = CityData.Handicap(sim);
        long factor = 100L * (city.Population + 17) / 18;
        factor = factor * world.NumCitiesMaintenancePercent / 100;
        factor = factor * handicap.NumCitiesMaintenancePercent / 100;
        // Deterministic fixed-point nth root replaces native pow(base, i / (target + 1)).
        var root = Root(Math.Max(1, CityData.Raw(sim, "BASE_NUM_CITY_MAINTENANCE_VALUE", 5)), Math.Max(1, world.TargetNumCities + 1));
        BigInteger term = Scale, sum = 0;
        for (int i = 0; i < n; i++) { sum += term; term = term * root / Scale; }
        BigInteger value = sum * factor / n;
        long modifier = CityData.Effect(effects, "count_maintenance_percent") + ModifierSink.Collect(new(sim, ModifierScope.Player, city.Owner)).Ledger.Total("cities:count_maintenance_percent");
        value = value * Math.Max(0, 100 + modifier) / 100;
        value = BigInteger.Min(value, (BigInteger)handicap.MaxNumCitiesMaintenance * 100 * Scale);
        int elapsed = Math.Max(0, sim.State.Turn - 1);
        if (elapsed < 100) value = value * (50 + elapsed / 2) / 100;
        long result = (long)BigInteger.Min(value / Scale, long.MaxValue);
        return CityData.Read(sim, city).IsPuppet ? result / 2 : result;
    }
    private static long Root(int value, int degree)
    {
        long low = Scale, high = value * Scale;
        BigInteger target = (BigInteger)value * BigInteger.Pow(Scale, degree);
        while (high - low > 1)
        {
            long middle = low + (high - low) / 2;
            if (BigInteger.Pow(middle, degree) <= target) low = middle;
            else high = middle;
        }
        return low;
    }
}
