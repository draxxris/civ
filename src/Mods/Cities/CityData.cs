using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal static class CityData
{
    internal static CityRuleState Read(Simulation sim, City city) => city.Extensions.Get<CityRuleState>(sim.Content) ?? new();
    internal static CityRuleState Edit(Simulation sim, City city)
    {
        var state = city.Extensions.Get<CityRuleState>(sim.Content);
        if (state is not null) return state;
        state = new(); city.Extensions.Set(sim.Content, state); return state;
    }
    internal static CityPlayerState PlayerState(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<CityPlayerState>(sim.Content) ?? new();
    internal static S.EraDef Era(Simulation sim, int player) => sim.Content.Get<S.EraDef>(PlayerState(sim, player).CurrentEra ?? sim.Settings.StartingEra);
    internal static S.GameSpeedDef Speed(Simulation sim) => sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed);
    internal static S.HandicapDef Handicap(Simulation sim) => sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap);
    internal static S.WorldSizeDef World(Simulation sim) => sim.Content.Get<S.WorldSizeDef>(sim.Settings.WorldSize!);
    internal static S.RuleConstants Constants(Simulation sim) => sim.Content.Get<S.RuleConstantsDef>().All.Single().Values;
    internal static int Raw(Simulation sim, string key, int fallback) => sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value) && int.TryParse(value, out var n) ? n : fallback;
    internal static IEnumerable<City> Owned(Simulation sim, int player) => sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id);
    internal static IEnumerable<S.BuildingDef> Active(Simulation sim, City city) => city.Buildings.Order(StringComparer.Ordinal)
        .Select(sim.Content.Get<S.BuildingDef>).Where(b => IsActive(sim, city, b));
    private static bool IsActive(Simulation sim, City city, S.BuildingDef b)
    {
        if (b.ObsoleteTech is { } obsolete && sim.KnowsTech(city.Owner, obsolete)) return false;
        return !b.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(b.Requirements, new(sim, city.Owner, city, Tile(sim, city)), "civic").Success;
    }
    internal static ModifierSink Effects(Simulation sim, City city) => ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city, sim.State.Grid.TileAt(city.Position), Team: sim.TeamOf(city.Owner)));
    internal static TileId Tile(Simulation sim, City city) => sim.State.Grid.TileAt(city.Position);
    internal static bool Coastal(Simulation sim, City city, int minimum = 0) => Coastal(sim, Tile(sim, city), minimum);
    internal static bool Coastal(Simulation sim, TileId tile, int minimum = 0)
    {
        foreach (var n in sim.State.Grid.Neighbors(tile))
        {
            var plot = sim.State.Plots[n.Value];
            if (!sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater || plot.IsLake) continue;
            if (minimum <= 0) return true;
            int size = sim.State.Plots.Count(p => p.AreaId == plot.AreaId && sim.Content.Get<S.TerrainDef>(p.Terrain).IsWater);
            if (size >= minimum) return true;
        }
        return false;
    }
    internal static long Effect(ModifierSink sink, string key) => sink.Ledger.Total("cities:" + key);
    internal static IReadOnlyList<S.CultureLevelDef> Levels(Simulation sim) => sim.Content.Get<S.CultureLevelDef>().All
        .Where(l => sim.Content.Requirements.Evaluate(l.Requirements, new(sim, 0)).Success)
        .OrderBy(l => l.SpeedThresholds[sim.Settings.GameSpeed]).ThenBy(l => l.Id, StringComparer.Ordinal).ToArray();
    internal static int LevelIndex(Simulation sim, City city)
    {
        var levels = Levels(sim);
        int result = 0;
        for (int i = 0; i < levels.Count; i++)
            if (city.Culture >= levels[i].SpeedThresholds[sim.Settings.GameSpeed]) result = i;
        return result;
    }
    internal static S.CultureLevelDef Level(Simulation sim, City city) => Levels(sim)[LevelIndex(sim, city)];
}

/// <summary>Reusable ordered player/city phase adapter; calculations remain in small owning services.</summary>
internal sealed class CityPhase(string name, int order, Action<Simulation, City> run) : IPlayerTurnSystem
{
    public string Id => "cities:" + name;
    public int Order => order;
    public void Run(Simulation sim, int player)
    {
        foreach (var city in CityData.Owned(sim, player).ToArray()) run(sim, city);
    }
    public void Run(Simulation sim)
    {
        foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id);
    }
}
