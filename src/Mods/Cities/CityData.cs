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
    internal static IEnumerable<S.BuildingDef> Active(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.Active, city, () =>
        {
            bool powered = IsPowered(sim, city);
            var buildings = city.Buildings.Concat(FreeBuildings(sim, city))
                .Where(id => powered || !sim.Content.Get<S.BuildingDef>(id).RequiresPower);
            return CityBuildingRules.Active(sim, city, buildings);
        }));
    // Power sources do not require power. Exclude consumers before resolving sources and their resources.
    internal static IReadOnlyList<S.BuildingDef> ActiveWithoutPower(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.ActiveWithoutPower, city, () => CityBuildingRules.Active(sim, city, city.Buildings.Concat(FreeBuildings(sim, city))
            .Where(id => !sim.Content.Get<S.BuildingDef>(id).RequiresPower))));
    internal static bool IsAreaCleanPower(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
    {
        int team = sim.TeamOf(city.Owner).Id, area = sim.State.PlotAt(city.Position).AreaId;
        return CityReadScope.Memo(scope.AreaCleanPower, (team, area), () => sim.State.Cities
            .Where(c => sim.TeamOf(c.Owner).Id == team && sim.State.PlotAt(c.Position).AreaId == area)
            .Any(c => ActiveWithoutPower(sim, c).Any(b => b.AreaCleanPower)));
    });
    internal static bool ProvidesLocalPower(Simulation sim, City city, S.BuildingDef building, IReadOnlyDictionary<string, int> bonuses) =>
        building.ProducesPower || building.PowerBonus is { } bonus && bonuses.GetValueOrDefault(bonus) > 0;
    private static S.BuildingDef[] LocalPowerSources(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.LocalPowerSources, city, () =>
        {
            var buildings = ActiveWithoutPower(sim, city);
            var bonuses = BuildingEffects.Bonuses(sim, city, buildings);
            return buildings.Where(b => ProvidesLocalPower(sim, city, b, bonuses)).ToArray();
        }));
    internal static bool IsPowered(Simulation sim, City city) =>
        IsAreaCleanPower(sim, city) || LocalPowerSources(sim, city).Length > 0;
    internal static bool IsDirtyPower(Simulation sim, City city)
    {
        if (IsAreaCleanPower(sim, city)) return false;
        var sources = LocalPowerSources(sim, city);
        return sources.Length > 0 && sources.All(b => b.DirtyPower);
    }
    /// <summary>Every building the city runs, free grants included. The whole-empire building, grant and power work is
    /// shared by the scope: an eligibility pass asks this once per candidate and would otherwise repeat it 600 times.</summary>
    internal static IReadOnlyList<S.BuildingDef> EffectiveBuildings(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.Effective, city,
            () => CityBuildingRules.Effective(sim, city, city.Buildings.Concat(FreeBuildings(sim, city))).ToArray()));
    internal static string? ResolveBuilding(Simulation sim, City city, string category)
    {
        string civilization = sim.State.PlayerById(city.Owner).Civilization;
        if (sim.IsOptionEnabled("foundation:gameoption_assimilation") && Read(sim, city).OriginalCivilization is { Length: > 0 } original)
            civilization = original;
        var overrides = sim.Content.Get<S.CivilizationDef>(civilization).BuildingOverrides;
        string? resolved = overrides.GetValueOrDefault(category, category);
        return resolved is { Length: > 0 } && sim.Content.TryGet<S.BuildingDef>(resolved, out _) ? resolved : null;
    }
    internal static IEnumerable<string> FreeBuildings(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.FreeBuildings, city, () => GrantCategories(sim, scope, city.Owner)
            .Select(category => ResolveBuilding(sim, city, category)).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    // Grants depend only on the receiving owner: its team's constructed sources and its own scripted wonders.
    private static SortedSet<string> GrantCategories(Simulation sim, CityReadScope scope, int owner) => CityReadScope.Memo(scope.GrantCategories, owner, () =>
    {
        var categories = new SortedSet<string>(StringComparer.Ordinal);
        int team = sim.TeamOf(owner).Id;
        foreach (var source in sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == team).OrderBy(c => c.Id))
        {
            // Only constructed sources initiate grants. This keeps grant cycles from creating their own sources.
            foreach (var building in CityBuildingRules.Active(sim, source, source.Buildings))
                if (building.FreeBuilding is { Length: > 0 } category && (source.Owner == owner || building.TeamShare))
                    categories.Add(category);
        }
        if (sim.Content.Services.Get<IScriptedWonderService>().SingleOrDefault() is { } scripted)
            categories.UnionWith(scripted.EmpireFreeBuildings(sim, owner));
        return categories;
    });
    internal static ModifierSink Effects(Simulation sim, City city) => ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city, sim.State.Grid.TileAt(city.Position), Team: sim.TeamOf(city.Owner)));
    internal static TileId Tile(Simulation sim, City city) => sim.State.Grid.TileAt(city.Position);
    internal static IEnumerable<Player> LivingPlayers(Simulation sim)
    {
        var owners = sim.State.Cities.Select(c => c.Owner).Concat(sim.State.Units.Select(u => u.Owner)).ToHashSet();
        return sim.State.Players.Where(p => owners.Contains(p.Id));
    }
    internal static int OwnerCulturePercent(Simulation sim, City city)
    {
        var plot = sim.State.PlotAt(city.Position);
        long total = plot.Culture.Values.Sum();
        return total > 0 ? (int)(plot.Culture.GetValueOrDefault(city.Owner) * 100 / total) : 100;
    }
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
    internal static IReadOnlyList<S.CultureLevelDef> Levels(Simulation sim, int player = 0, City? city = null) => sim.Content.Get<S.CultureLevelDef>().All
        .Where(l => sim.Content.Requirements.Evaluate(l.Requirements, new RequirementContext(sim, player, city)).Success)
        .OrderBy(l => l.SpeedThresholds[sim.Settings.GameSpeed]).ThenBy(l => l.Id, StringComparer.Ordinal).ToArray();
    internal static int LevelIndex(Simulation sim, City city) => LevelIndex(sim, city, Levels(sim, city.Owner, city));
    internal static int LevelIndex(Simulation sim, City city, IReadOnlyList<S.CultureLevelDef> levels)
    {
        int result = 0;
        for (int i = 0; i < levels.Count; i++)
            if (city.Culture >= levels[i].SpeedThresholds[sim.Settings.GameSpeed]) result = i;
        return result;
    }
    internal static S.CultureLevelDef Level(Simulation sim, City city) => CityReadScope.Run(sim, scope => CityReadScope.Memo(scope.Level, city,
        () => Levels(sim, city.Owner, city)[LevelIndex(sim, city)]));
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
