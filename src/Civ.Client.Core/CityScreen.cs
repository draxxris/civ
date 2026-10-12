using Civ.BaseGame;
using Civ.Content;
using Civ.Game;

namespace Civ.Client;

public sealed record SpecialistRow(string Id, string Name, int Count, int Slots, bool Unlimited, string Effects);

/// <summary>One thing a city can build or train. <see cref="Turns"/> counts from the city's current progress when it is the current item.</summary>
public sealed record ProductionItem(bool IsUnit, string Id, string Name, int Cost, int? Turns, string Effects, bool Current);

/// <summary>What the city screen shows, computed from the simulation so every renderer (and the tests) agree on the numbers.</summary>
public sealed record CityScreen(
    string Name, int Population, int FoodSurplus, int? TurnsToGrow, bool Starving,
    int Food, int Production, int Commerce, int Culture,
    int Workers, int SpecialistTotal, IReadOnlyList<SpecialistRow> Specialists,
    string? CurrentName, long CurrentProgress, int CurrentCost, int? CurrentTurns,
    IReadOnlyList<ProductionItem> Buildings, IReadOnlyList<ProductionItem> Units)
{
    public static CityScreen Of(Simulation sim, City city)
    {
        var y = Yields.OfCity(sim, city);
        int production = (int)(y.Production * (100L + Bonuses.Production(sim, city.Owner)) / 100);
        int culture = (int)((1 + y.Culture) * (100L + Bonuses.Culture(sim, city.Owner)) / 100);
        int surplus = y.Food - CityRules.FoodPerCitizen * city.Population;
        int? growIn = surplus > 0 ? Turns(CityRules.FoodToGrow(city.Population) - city.FoodStored, surplus) : null;
        bool starving = surplus < 0 && city.FoodStored + surplus < 0 && city.Population > 1;

        var specialists = new List<SpecialistRow>();
        foreach (var s in sim.Content.Specialists.All)
        {
            int count = city.Specialists.GetValueOrDefault(s.Id);
            int slots = CityRules.Slots(sim, city, s.Id);
            if (!s.Unlimited && slots == 0 && count == 0) continue;
            specialists.Add(new SpecialistRow(s.Id, s.Name, count, slots, s.Unlimited, SpecialistEffects(s)));
        }

        var me = sim.State.PlayerById(city.Owner);
        var buildings = sim.Content.Buildings.All
            .Where(b => !city.Buildings.Contains(b.Id) && (b.RequiresTech == null || me.Techs.Contains(b.RequiresTech)))
            .Select(b => new ProductionItem(false, b.Id, b.Name, b.Cost, Turns(b.Cost - (city.Building == b.Id ? city.BuildProgress : 0), production), BuildingEffects(sim, b), city.Building == b.Id))
            .ToList();
        var units = sim.Content.Units.All
            .Where(u => u.RequiresTech == null || me.Techs.Contains(u.RequiresTech))
            .Select(u => new ProductionItem(true, u.Id, u.Name, u.Cost, Turns(u.Cost - (city.Training == u.Id ? city.TrainProgress : 0), production), UnitEffects(u), city.Training == u.Id))
            .ToList();

        string? currentName = null; long progress = 0; int cost = 0;
        if (city.Building != null) { var b = sim.Content.Buildings.Get(city.Building); (currentName, progress, cost) = (b.Name, city.BuildProgress, b.Cost); }
        else if (city.Training != null) { var u = sim.Content.Units.Get(city.Training); (currentName, progress, cost) = (u.Name, city.TrainProgress, u.Cost); }

        return new CityScreen(city.Name, city.Population, surplus, growIn, starving,
            y.Food, production, y.Commerce, culture,
            city.Worked.Count, CityRules.SpecialistCount(city), specialists,
            currentName, progress, cost, currentName == null ? null : Turns(cost - progress, production),
            buildings, units);
    }

    /// <summary>Whole turns to cover <paramref name="remaining"/> at <paramref name="perTurn"/> (at least 1); null when nothing is produced.</summary>
    public static int? Turns(long remaining, int perTurn) => perTurn <= 0 ? null : (int)Math.Max(1, (remaining + perTurn - 1) / perTurn);

    public static string YieldText(int food, int production, int commerce, int culture)
    {
        var parts = new List<string>();
        if (food != 0) parts.Add($"{food:+#;-#} food");
        if (production != 0) parts.Add($"{production:+#;-#} production");
        if (commerce != 0) parts.Add($"{commerce:+#;-#} commerce");
        if (culture != 0) parts.Add($"{culture:+#;-#} culture");
        return string.Join(", ", parts);
    }

    public static string SpecialistEffects(SpecialistDef s) => YieldText(s.Food, s.Production, s.Commerce, s.Culture);

    public static string BuildingEffects(Simulation sim, BuildingDef b)
    {
        var parts = new List<string>();
        var y = YieldText(b.Food, b.Production, b.Commerce, b.Culture);
        if (y.Length > 0) parts.Add(y);
        foreach (var (id, n) in b.SpecialistSlots.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var name = sim.Content.Specialists.TryGet(id, out var s) ? s.Name : id;
            parts.Add(n < 0 ? $"unlimited {name} slots" : $"{n} {name} slot{(n == 1 ? "" : "s")}");
        }
        if (b.UnitExperience != 0) parts.Add($"{b.UnitExperience:+#;-#} XP for new units");
        return string.Join(", ", parts);
    }

    public static string UnitEffects(UnitDef u) => u.CanFoundCity ? "Founds a city" : $"Strength {u.Strength}, moves {u.Moves}";
}
