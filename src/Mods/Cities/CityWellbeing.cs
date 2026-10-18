using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityWellbeingService : ICityWellbeingService
{
    public CityWellbeing Calculate(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var effects = CityData.Effects(sim, city);
        var lines = new List<LedgerLine>();
        Add(lines, "cities:handicap", "happiness", CityData.Handicap(sim).HappyBonus);
        Add(lines, "cities:handicap", "health", CityData.Handicap(sim).HealthBonus);
        Add(lines, "cities:population", "happiness", -city.Population * Math.Max(0, 100 + CityData.Effect(effects, "population_anger_percent")) / 100);
        Add(lines, "cities:city_count", "happiness", -CityData.Owned(sim, city.Owner).Count() / Math.Max(1, CityData.World(sim).NumCitiesUnhappinessDivisor));
        bool noPopulation = CityData.Effect(effects, "no_unhealthy_population") > 0;
        if (!noPopulation) Add(lines, "cities:population", "health", -city.Population);
        if (sim.Content.Services.Get<IWaterService>().Single().IsFreshWater(sim, CityData.Tile(sim, city)))
            Add(lines, "cities:fresh_water", "health", CityData.Constants(sim).FreshWaterHealthChange);
        AddModifiers(sim, effects, lines);
        AddCultureAnger(sim, city, lines);
        AddCivicAnger(sim, city, effects, lines);
        AddEnvironment(sim, city, lines);
        AddSpecialists(sim, city, effects, lines);
        var ledger = new Ledger(lines);
        int happy = (int)ledger.Total("cities:happiness_good"), unhappy = (int)ledger.Total("cities:happiness_bad");
        int healthy = (int)ledger.Total("cities:health_good"), unhealthy = (int)ledger.Total("cities:health_bad");
        if (CityData.Effect(effects, "no_unhappiness") > 0) unhappy = 0;
        return new(happy, unhappy, Math.Clamp(unhappy - happy, 0, city.Population), healthy, unhealthy, Math.Max(0, unhealthy - healthy), ledger);
    }
    private static void Add(List<LedgerLine> lines, string source, string kind, long value)
    {
        if (value != 0) lines.Add(new(source, "cities:" + kind + (value > 0 ? "_good" : "_bad"), Math.Abs(value)));
    }
    private static void AddModifiers(Simulation sim, ModifierSink effects, List<LedgerLine> lines)
    {
        bool onlyHealthy = CityData.Effect(effects, "building_only_healthy") > 0;
        foreach (var line in effects.Ledger.Lines)
        {
            if (onlyHealthy && line.Key == "cities:health" && line.Amount < 0 && IsBuildingSource(sim, line.Source)) continue;
            if (line.Key == "cities:happiness") Add(lines, line.Source, "happiness", line.Amount);
            if (line.Key == "cities:health") Add(lines, line.Source, "health", line.Amount);
            if (line.Key == "cities:unhappiness") Add(lines, line.Source, "happiness", -Math.Max(0, line.Amount));
            if (line.Key == "cities:war_weariness") Add(lines, line.Source, "happiness",
                -Math.Max(0, line.Amount) * Math.Max(0, 100 + CityData.Effect(effects, "war_weariness_percent")) / 100);
        }
    }
    private static long CulturePercentAnger(Simulation sim, City city)
    {
        var plot = sim.State.PlotAt(city.Position);
        long totalCulture = plot.Culture.Values.Sum();
        if (totalCulture <= 0) return 0;
        var ownerTeam = sim.TeamOf(city.Owner);
        long angryCulture = 0;
        foreach (var player in CityData.LivingPlayers(sim))
        {
            var team = sim.TeamOf(player.Id);
            long culture = plot.Culture.GetValueOrDefault(player.Id);
            if (team.Id == ownerTeam.Id || culture <= 0) continue;
            if (ownerTeam.Wars.Contains(team.Id))
                culture = culture * Math.Max(0, 100 + CityData.Constants(sim).AtWarCultureAngerModifier) / 100;
            angryCulture += culture;
        }
        return CityData.Constants(sim).CulturePercentAnger * angryCulture / totalCulture;
    }
    private static void AddCultureAnger(Simulation sim, City city, List<LedgerLine> lines)
    {
        long unhappy = CulturePercentAnger(sim, city) * city.Population / Math.Max(1, CityData.Constants(sim).PercentAngerDivisor);
        if (unhappy > 0) Add(lines, "cities:culture_anger", "happiness", -unhappy);
    }
    private static void AddCivicAnger(Simulation sim, City city, ModifierSink effects, List<LedgerLine> lines)
    {
        long civicUnhappy = CityData.Effect(effects, "civic_percent_anger") * city.Population
            / Math.Max(1, CityData.Constants(sim).PercentAngerDivisor);
        if (civicUnhappy > 0) Add(lines, "government:civic_anger", "happiness", -civicUnhappy);
        long foreignAnger = CityData.Effect(effects, "foreigner_unhappy_percent");
        if (foreignAnger == 0) return;
        long factor = 100 / foreignAnger;
        long foreignUnhappy = Math.Max(0, (100 - CityData.OwnerCulturePercent(sim, city)) * factor / 100);
        if (foreignUnhappy > 0) Add(lines, "government:foreigner_anger", "happiness", -foreignUnhappy);
    }
    private static bool IsBuildingSource(Simulation sim, string source) =>
        sim.Content.TryGet<S.BuildingDef>(source.Split(':', 3) is { Length: >= 2 } p ? p[0] + ":" + p[1] : source, out _);
    private static void AddEnvironment(Simulation sim, City city, List<LedgerLine> lines)
    {
        long featureGood = 0, featureBad = 0, terrainGood = 0, terrainBad = 0;
        foreach (var tile in sim.State.Grid.Range(CityData.Tile(sim, city), 3))
        {
            var plot = sim.State.Plots[tile.Value];
            int terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain).HealthPercent;
            terrainGood += Math.Max(0, terrain); terrainBad += Math.Min(0, terrain);
            if (plot.Feature is not { } id) continue;
            int feature = sim.Content.Get<S.FeatureDef>(id).HealthPercent;
            featureGood += Math.Max(0, feature); featureBad += Math.Min(0, feature);
        }
        Add(lines, "cities:features", "health", featureGood / 100);
        Add(lines, "cities:features", "health", featureBad * Math.Clamp(city.Population - 2, 0, 5) / 500);
        Add(lines, "cities:terrain", "health", terrainGood / 100);
        Add(lines, "cities:terrain", "health", terrainBad / 100);
    }
    private static void AddSpecialists(Simulation sim, City city, ModifierSink effects, List<LedgerLine> lines)
    {
        long health = 0, happy = 0;
        foreach (var pair in Specialists.All(sim, city, effects))
        {
            var def = sim.Content.Get<S.SpecialistDef>(pair.Key);
            health += def.HealthPercent * pair.Value; happy += def.HappinessPercent * pair.Value;
        }
        foreach (var building in CityData.Active(sim, city))
        {
            health += building.HealthPercentPerPopulation * city.Population;
            happy += building.HappinessPercentPerPopulation * city.Population;
        }
        Add(lines, "cities:population_effects", "health", health / 100);
        Add(lines, "cities:population_effects", "happiness", happy / 100);
    }
}
