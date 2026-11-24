using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Cities;

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
    /// <summary>Constructed and granted buildings: every building the city could run. The running sets below only
    /// narrow it.</summary>
    internal static IEnumerable<string> Candidates(Simulation sim, City city) => city.Buildings.Concat(FreeBuildings(sim, city));
    internal static IEnumerable<S.BuildingDef> Active(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.Active, city, () =>
        {
            // Power is a whole-area question (clean power anywhere in the team's area, or the city's own sources and
            // their resources), so it is asked only of a city that holds a building needing it.
            bool? powered = null;
            var buildings = Candidates(sim, city)
                .Where(id => !sim.Content.Get<S.BuildingDef>(id).RequiresPower || (powered ??= IsPowered(sim, city)));
            return CityBuildingRules.Active(sim, city, buildings);
        }));
    // Power sources do not require power. Exclude consumers before resolving sources and their resources.
    internal static IReadOnlyList<S.BuildingDef> ActiveWithoutPower(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.ActiveWithoutPower, city, () => CityBuildingRules.Active(sim, city, Candidates(sim, city)
            .Where(id => !sim.Content.Get<S.BuildingDef>(id).RequiresPower))));
    /// <summary>What power answers read: cities' owners, places and running buildings, with those buildings'
    /// requirements, grants and resources, as building effects do (<see cref="BuildingEffects"/>), and nothing a turn
    /// tallies, units, sight or worker orders.</summary>
    private sealed class PowerReader : IStateReader
    {
        public IReadOnlyList<string>? UnreadDomains(ContentDb content) =>
            CityStateDomains.UnreadThroughBuildings(CityStateDomains.NeverReadByBuildings, content);
    }
    // Every wellbeing, building and yield query asks for power, each in a read span of its own, and the area question
    // resolves the buildings of every city the team has in the area: kept across writes, it is answered once per
    // building, grant or resource change instead.
    private static readonly KeptQuery<(int Team, int Area), bool> AreaCleanPowerKept = new("cities:area_clean_power", new PowerReader());
    private static readonly KeptQuery<int, (bool Powered, bool Dirty)> LocalPowerKept = new("cities:local_power", new PowerReader());
    internal static bool IsAreaCleanPower(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
    {
        int team = sim.TeamOf(city.Owner).Id, area = sim.State.PlotAt(city.Position).AreaId;
        return CityReadScope.Memo(scope.AreaCleanPower, (team, area), () => AreaCleanPowerKept.Get(sim, (team, area), () => sim.State.Cities
            .Where(c => sim.TeamOf(c.Owner).Id == team && sim.State.PlotAt(c.Position).AreaId == area)
            .Any(c => ActiveWithoutPower(sim, c).Any(b => b.AreaCleanPower))));
    });
    internal static bool ProvidesLocalPower(Simulation sim, City city, S.BuildingDef building, IReadOnlyDictionary<string, int> bonuses) =>
        building.ProducesPower || building.PowerBonus is { } bonus && bonuses.GetValueOrDefault(bonus) > 0;
    /// <summary>Whether the city's own running buildings power it, and whether every such source is dirty.</summary>
    private static (bool Powered, bool Dirty) LocalPower(Simulation sim, City city) => CityReadScope.Run(sim, scope =>
        CityReadScope.Memo(scope.LocalPower, city, () => LocalPowerKept.Get(sim, city.Id, () =>
        {
            var buildings = ActiveWithoutPower(sim, city);
            var bonuses = BuildingEffects.Bonuses(sim, city, buildings);
            var sources = buildings.Where(b => ProvidesLocalPower(sim, city, b, bonuses)).ToArray();
            return (sources.Length > 0, sources.Length > 0 && sources.All(b => b.DirtyPower));
        })));
    internal static bool IsPowered(Simulation sim, City city) =>
        IsAreaCleanPower(sim, city) || LocalPower(sim, city).Powered;
    internal static bool IsDirtyPower(Simulation sim, City city) => !IsAreaCleanPower(sim, city) && LocalPower(sim, city).Dirty;
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
    // Grants depend only on the receiving owner: its team's constructed sources and what features grant it.
    private static SortedSet<string> GrantCategories(Simulation sim, CityReadScope scope, int owner) => CityReadScope.Memo(scope.GrantCategories, owner, () =>
    {
        var categories = new SortedSet<string>(StringComparer.Ordinal);
        int team = sim.TeamOf(owner).Id;
        foreach (var source in sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == team).OrderBy(c => c.Id))
        {
            // Only constructed sources initiate grants. This keeps grant cycles from creating their own sources.
            // Activity only narrows the constructed set, so a city that built no granting building is not resolved.
            if (!source.Buildings.Any(id => sim.Content.Get<S.BuildingDef>(id).FreeBuilding is { Length: > 0 })) continue;
            foreach (var building in CityBuildingRules.Active(sim, source, source.Buildings))
                if (building.FreeBuilding is { Length: > 0 } category && (source.Owner == owner || building.TeamShare))
                    categories.Add(category);
        }
        foreach (var grants in sim.Rules().FreeBuildingGrants)
            categories.UnionWith(grants.Grants(sim, owner));
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
            // Areas are all water or all land, so the size of a water plot's area counts only water.
            if (sim.State.AreaSize(plot.AreaId) >= minimum) return true;
        }
        return false;
    }
    internal static long Effect(ModifierSink sink, string key) => sink.Total("cities:" + key);
    internal static IReadOnlyList<S.CultureLevelDef> Levels(Simulation sim, int player = 0, City? city = null) => sim.Content.Get<S.CultureLevelDef>().All
        .Where(l => sim.Content.Requirements.Evaluate(l.Requirements, new RequirementContext(sim, player, city)).Success)
        .OrderBy(l => l.SpeedThresholds[sim.Settings.GameSpeed]).ThenBy(l => l.Id, StringComparer.Ordinal).ToArray();
    internal static int LevelIndex(Simulation sim, City city) => LevelIndex(sim, city, Levels(sim, city.Owner, city));
    internal static int LevelIndex(Simulation sim, City city, IReadOnlyList<S.CultureLevelDef> levels) => LevelIndex(sim, levels, city.Culture);
    /// <summary>The index among <paramref name="levels"/> of the level a city with <paramref name="culture"/> points has.</summary>
    internal static int LevelIndex(Simulation sim, IReadOnlyList<S.CultureLevelDef> levels, long culture)
    {
        int result = 0;
        for (int i = 0; i < levels.Count; i++)
            if (culture >= levels[i].SpeedThresholds[sim.Settings.GameSpeed]) result = i;
        return result;
    }
    internal static S.CultureLevelDef Level(Simulation sim, City city) => CityReadScope.Run(sim, scope => CityReadScope.Memo(scope.Level, city,
        () => Levels(sim, city.Owner, city)[LevelIndex(sim, city)]));
}

/// <summary>Reusable ordered player/city phase adapter; calculations remain in small owning services. A phase that
/// names the state domains its step writes (<see cref="ITurnSystem.WriteDomains"/>) lets kept modifier contributions
/// survive it; null writes anything.</summary>
internal sealed class CityPhase(string name, int order, Action<Simulation, City> run, IReadOnlyList<string>? writes = null) : IPlayerTurnSystem
{
    public string Id => "cities:" + name;
    public int Order => order;
    public IReadOnlyList<string>? WriteDomains => writes;
    public void Run(Simulation sim, int player)
    {
        foreach (var city in CityData.Owned(sim, player).ToArray()) run(sim, city);
    }
    public void Run(Simulation sim)
    {
        foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id);
    }
}
