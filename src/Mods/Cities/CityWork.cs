using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityWork(ICityWellbeingService wellbeing) : ICityWorkService
{
    public int Radius(Simulation sim, int cityId) => Math.Clamp(CityData.Level(sim, sim.State.CityById(cityId)).CityRadius, 2, 3);
    public IReadOnlyList<TileId> WorkArea(Simulation sim, int cityId) => sim.State.Grid.Range(CityData.Tile(sim, sim.State.CityById(cityId)), Radius(sim, cityId)).ToArray();
    public string? Validate(Simulation sim, int cityId, IReadOnlyList<Hex> tiles, IReadOnlyDictionary<string, int> specialists)
    {
        var city = sim.State.CityById(cityId);
        var effects = CityData.Effects(sim, city);
        int available = city.Population - wellbeing.Calculate(sim, city.Id).Angry;
        if (tiles.Count + specialists.Values.Sum() > available) return "Too many working citizens";
        var seen = new HashSet<TileId>();
        foreach (var hex in tiles)
        {
            if (!sim.State.Grid.TryTile(hex, out var tile)) return "Work plot is outside the map";
            if (!seen.Add(tile)) return "Duplicate work plot";
            if (!CanWork(sim, city, tile)) return "Work plot is unavailable";
        }
        foreach (var pair in specialists)
        {
            if (!sim.Content.TryGet<S.SpecialistDef>(pair.Key, out _)) return "Unknown specialist";
            if (pair.Value < 0 || pair.Value > Specialists.Slots(sim, city, pair.Key, effects)) return "Insufficient specialist slots: " + pair.Key;
        }
        return null;
    }
    private bool CanWork(Simulation sim, City city, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (tile == CityData.Tile(sim, city) || sim.State.CityAt(sim.State.Grid.HexOf(tile)) is not null) return false;
        if (plot.Owner != city.Owner || sim.State.Grid.Distance(CityData.Tile(sim, city), tile) > Radius(sim, city.Id)) return false;
        if (sim.Content.Get<S.TerrainDef>(plot.Terrain).Impassable || plot.Kind == PlotKind.Peak) return false;
        if (plot.Feature is { } f && sim.Content.Get<S.FeatureDef>(f).Impassable) return false;
        return !sim.State.Cities.Any(c => c.Id != city.Id && c.Worked.Any(h => sim.State.Grid.TileAt(h) == tile));
    }
    public void Assign(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var state = CityData.Edit(sim, city);
        if (state.ManualWork) { Prune(sim, city); return; }
        city.Worked.Clear(); city.Specialists.Clear();
        int available = city.Population - wellbeing.Calculate(sim, cityId).Angry;
        var yields = sim.Content.Services.Get<IPlotYieldService>().Single();
        var effects = CityData.Effects(sim, city);
        var candidates = WorkArea(sim, cityId).Where(t => CanWork(sim, city, t))
            .Select(t => new WorkChoice(t.Value, null, Score(sim, yields.Calculate(sim, city.Owner, t).Yields))).ToList();
        foreach (var specialist in sim.Content.Get<S.SpecialistDef>().All)
        {
            int slots = Math.Min(available, Specialists.Slots(sim, city, specialist.Id, effects));
            long score = Score(sim, specialist.Yields) + 4 * (specialist.Commerces.Gold + specialist.Commerces.Research + specialist.Commerces.Culture + specialist.Commerces.Espionage);
            for (int i = 0; i < slots; i++) candidates.Add(new(int.MaxValue, specialist.Id, score));
        }
        foreach (var choice in candidates.OrderByDescending(p => p.Score).ThenBy(p => p.Tile).ThenBy(p => p.Specialist, StringComparer.Ordinal).Take(available))
        {
            if (choice.Specialist is { } specialist) city.Specialists[specialist] = city.Specialists.GetValueOrDefault(specialist) + 1;
            else city.Worked.Add(sim.State.Grid.HexOf(new(choice.Tile)));
        }
        city.Worked = city.Worked.OrderBy(h => h.Q).ThenBy(h => h.R).ToList();
    }
    private sealed record WorkChoice(int Tile, string? Specialist, long Score);
    private static long Score(Simulation sim, YieldVector y)
    {
        var tuning = sim.Content.Get<S.HexOverridesDef>().All.Single();
        return y.Food * tuning.GovernorFoodWeight + y.Production * tuning.GovernorProductionWeight + y.Commerce * tuning.GovernorCommerceWeight;
    }
    private void Prune(Simulation sim, City city)
    {
        int available = city.Population - wellbeing.Calculate(sim, city.Id).Angry;
        var effects = CityData.Effects(sim, city);
        city.Worked = city.Worked.Where(h => sim.State.Grid.TryTile(h, out var t) && CanWork(sim, city, t)).Distinct().Take(available).ToList();
        available -= city.Worked.Count;
        var specialists = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in city.Specialists)
        {
            int count = Math.Clamp(pair.Value, 0, Math.Min(available, Specialists.Slots(sim, city, pair.Key, effects)));
            if (count > 0) specialists[pair.Key] = count;
            available -= count;
        }
        city.Specialists = specialists;
    }
    internal void AssignPlayer(Simulation sim, int player)
    {
        // Clear automatic assignments first so saved previous-turn city order cannot bias shared plot choice.
        foreach (var city in CityData.Owned(sim, player))
            if (!CityData.Read(sim, city).ManualWork) city.Worked.Clear();
        foreach (var city in CityData.Owned(sim, player)) Assign(sim, city.Id);
    }
}

internal sealed class AssignWorkPhase(CityWork work) : IPlayerTurnSystem
{
    public string Id => "cities:assign_work";
    public int Order => TurnPhase.AssignWork;
    public void Run(Simulation sim, int player) => work.AssignPlayer(sim, player);
    public void Run(Simulation sim) { foreach (var p in sim.State.Players.OrderBy(p => p.Id)) Run(sim, p.Id); }
}
