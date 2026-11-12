using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Cities;

internal sealed class CityCulture(ICityYieldService yields, ICityWorkService work) : ICityCultureService
{
    internal void Accumulate(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        long rate = Rate(sim, city);
        state.LastCultureRateHundredths = rate;
        long total = state.CultureRemainder + rate;
        city.Culture += total / 100;
        state.CultureRemainder = total % 100;
        state.CultureHundredths[city.Owner] = city.Culture * 100 + state.CultureRemainder;
    }
    public int LevelIndex(Simulation sim, int cityId) => CityData.LevelIndex(sim, sim.State.CityById(cityId));
    public Ariel.Content.Schema.CultureLevelDef Level(Simulation sim, int cityId) => CityData.Level(sim, sim.State.CityById(cityId));
    public int OwnerCulturePercent(Simulation sim, int cityId) => CityData.OwnerCulturePercent(sim, sim.State.CityById(cityId));
    public int TeamCulturePercent(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var plot = sim.State.PlotAt(city.Position);
        long total = plot.Culture.Values.Sum();
        if (total == 0) return 100;
        int team = sim.TeamOf(city.Owner).Id;
        return CityData.LivingPlayers(sim).Where(p => sim.TeamOf(p.Id).Id == team)
            .Sum(p => (int)(plot.Culture.GetValueOrDefault(p.Id) * 100 / total));
    }
    public void AddCulture(Simulation sim, int cityId, long culture, int spreadTurns)
    {
        var city = sim.State.CityById(cityId);
        var state = CityData.Edit(sim, city);
        long savedRate = state.LastCultureRateHundredths;
        int turns = Math.Max(1, spreadTurns);
        long total = Math.Max(0, culture) * 100;
        for (int i = 0; i < turns; i++)
        {
            long amount = total / turns + (i == turns - 1 ? total % turns : 0);
            long stored = state.CultureRemainder + amount;
            city.Culture += stored / 100;
            state.CultureRemainder = stored % 100;
            state.CultureHundredths[city.Owner] = city.Culture * 100 + state.CultureRemainder;
            state.LastCultureRateHundredths = amount;
            Spread(sim, city);
        }
        state.LastCultureRateHundredths = savedRate;
    }
    private long Rate(Simulation sim, City city)
    {
        if (GovernmentStatus.IsDisorder(sim, city)) return 0;
        var source = sim.Content.Services.Get<ICityCultureSource>().SingleOrDefault();
        if (source is not null) return Math.Max(0, source.CultureHundredths(sim, city.Id));
        var result = yields.Calculate(sim, city.Id);
        return Math.Max(0, result.DirectCommerceHundredths.Culture * Math.Max(1, 100 + result.CommercePercent.Culture) / 100);
    }
    internal void Spread(Simulation sim, City city)
    {
        if (city.Culture <= 0 || GovernmentStatus.IsDisorder(sim, city)) return;
        int level = CityData.LevelIndex(sim, city);
        long rate = CityData.Read(sim, city).LastCultureRateHundredths / 100;
        var ranges = new CultureRanges(sim);
        foreach (var (tile, distance) in CultureDistances(sim, city, level).OrderBy(p => p.Key.Value))
        {
            long amount = (level - distance + 1) * CityData.Constants(sim).CityFreeCultureGrowthFactor + rate + 1;
            var plot = sim.State.Plots[tile.Value];
            plot.Culture[city.Owner] = plot.Culture.GetValueOrDefault(city.Owner) + amount;
            Resolve(sim, tile, ranges);
        }
        work.Assign(sim, city.Id);
    }
    internal static void Initialize(Simulation sim, City city)
    {
        var center = CityData.Tile(sim, city);
        var team = sim.TeamOf(city.Owner);
        // The founding grant ignores distance penalties, but never crosses an obstruction.
        foreach (var tile in sim.State.Grid.Range(center, 1))
        {
            var plot = sim.State.Plots[tile.Value];
            if (tile != center && IsObstructed(sim, team, plot)) continue;
            int free = tile == center ? CityData.Constants(sim).FreeCityCulture : CityData.Constants(sim).FreeCityAdjacentCulture;
            plot.Culture[city.Owner] = Math.Max(plot.Culture.GetValueOrDefault(city.Owner), free);
            Resolve(sim, tile);
        }

        long extraLevels = CityData.Effect(CityData.Effects(sim, city), "extra_culture_level");
        var levels = CityData.Levels(sim, city.Owner, city);
        for (long step = 0; step < extraLevels; step++)
        {
            var next = levels.FirstOrDefault(l => l.SpeedThresholds[sim.Settings.GameSpeed] > city.Culture);
            if (next is null) break;
            long threshold = next.SpeedThresholds[sim.Settings.GameSpeed];
            city.Culture = threshold;
            var state = CityData.Edit(sim, city);
            state.CultureRemainder = 0;
            state.CultureHundredths[city.Owner] = threshold * 100;
        }

        if (extraLevels > 0)
        {
            int radius = CultureRadius(sim, city);
            long growth = CityData.Constants(sim).CityFreeCultureGrowthFactor;
            var ranges = new CultureRanges(sim);
            foreach (var (tile, distance) in CultureDistances(sim, city, radius).OrderBy(p => p.Key.Value))
            {
                long amount = (radius - distance + 1) * growth + 1;
                var plot = sim.State.Plots[tile.Value];
                plot.Culture[city.Owner] = plot.Culture.GetValueOrDefault(city.Owner) + amount;
                Resolve(sim, tile, ranges);
            }
        }
    }
    internal static void DecayPlotCulture(Simulation sim)
    {
        var constants = CityData.Constants(sim);
        int halvingTurns = constants.CultureDecayTurns;
        if (halvingTurns <= 0) return;
        halvingTurns = Math.Max(10, halvingTurns * CityData.Speed(sim).GoldenAgePercent / 100);
        if (sim.State.Turn % halvingTurns != 0) return;
        var barbarians = sim.State.Players.Where(p => IsBarbarian(sim, p.Id))
            .Select(p => p.Id).ToHashSet();
        var ranges = new CultureRanges(sim);
        foreach (var tile in sim.State.Grid.Tiles)
            DecayPlot(sim, tile, constants.CityFreeCultureGrowthFactor, barbarians, ranges);
        foreach (var city in sim.State.Cities)
            DecayForeignCulture(sim, city, constants.CityFreeCultureGrowthFactor * 100L);
    }
    private static void DecayPlot(Simulation sim, TileId tile, int minimum, HashSet<int> barbarians, CultureRanges ranges)
    {
        var plot = sim.State.Plots[tile.Value];
        bool changed = false;
        foreach (int player in plot.Culture.Keys.ToArray())
        {
            long culture = plot.Culture[player];
            if (barbarians.Contains(player) || culture < minimum) continue;
            long decayed = culture / 2;
            if (decayed == culture) continue;
            plot.Culture[player] = decayed;
            changed = true;
        }
        if (changed) Resolve(sim, tile, ranges);
    }
    private static void DecayForeignCulture(Simulation sim, City city, long minimum)
    {
        var state = CityData.Edit(sim, city);
        foreach (int player in state.CultureHundredths.Keys.ToArray())
        {
            if (player == city.Owner || state.CultureHundredths[player] < minimum) continue;
            state.CultureHundredths[player] /= 2;
        }
    }
    internal static void Resolve(Simulation sim, TileId tile) => Resolve(sim, tile, new CultureRanges(sim));
    /// <summary>Settle one plot's owner. <paramref name="ranges"/> carries other cities' culture reach across a
    /// batch of plots; this keeps it correct when the new owner changes what blocks culture.</summary>
    private static void Resolve(Simulation sim, TileId tile, CultureRanges ranges)
    {
        var plot = sim.State.Plots[tile.Value];
        int? before = plot.Owner;
        plot.Owner = Owner(sim, tile, plot, ranges);
        if (plot.Owner != before) ranges.OwnerChanged(before, plot.Owner);
    }
    private static int? Owner(Simulation sim, TileId tile, Plot plot, CultureRanges ranges)
    {
        if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is { } city) return city.Owner;
        // Only players with a living city whose cultural radius covers the tile may own it.
        var eligible = plot.Culture.Where(p => p.Value > 0 && InRange(sim, p.Key, tile, ranges)).ToArray();
        if (eligible.Length == 0) return null;
        long maximum = eligible.Max(p => p.Value);
        // Preserve an incumbent on equal culture; otherwise lowest player id breaks ties.
        if (plot.Owner is { } owner && eligible.Any(p => p.Key == owner && p.Value == maximum)) return owner;
        return eligible.First(p => p.Value == maximum).Key;
    }
    private static bool InRange(Simulation sim, int player, TileId tile, CultureRanges ranges)
    {
        foreach (var c in sim.State.Cities)
        {
            if (c.Owner != player) continue;
            var center = CityData.Tile(sim, c);
            if (tile == center) return true;
            if (IsObstructed(sim, sim.TeamOf(player), sim.State.Plots[tile.Value])) continue;
            int geometricDistance = sim.State.Grid.Distance(center, tile);
            // Stored founding culture remains eligible throughout the unobstructed first ring.
            if (geometricDistance == 1) return true;
            var (levels, radius) = ranges.Levels(c);
            if (geometricDistance > radius) continue;
            if (ranges.Distances(c, radius).TryGetValue(tile, out int distance) && distance < levels) return true;
        }
        return false;
    }

    /// <summary>Each city's culture levels, radius and culture distances, reused across a batch of plot
    /// resolutions instead of recomputed for every plot. Levels and radius hold for the whole batch, since
    /// resolving plots changes no city's culture. Distances depend on which plots belong to a team at war with
    /// the city's owner, so <see cref="OwnerChanged"/> drops those of the cities such a change can affect.</summary>
    private sealed class CultureRanges(Simulation sim)
    {
        private readonly Dictionary<int, (int Levels, int Radius)> _levels = new();
        private readonly Dictionary<int, IReadOnlyDictionary<TileId, int>> _distances = new();
        private readonly List<int> _stale = new();

        public (int Levels, int Radius) Levels(City city)
        {
            if (_levels.TryGetValue(city.Id, out var known)) return known;
            var levels = CityData.Levels(sim, city.Owner, city);
            return _levels[city.Id] = (levels.Count, Math.Max(1, CityData.LevelIndex(sim, city, levels)));
        }

        public IReadOnlyDictionary<TileId, int> Distances(City city, int radius)
        {
            if (!_distances.TryGetValue(city.Id, out var distances)) _distances[city.Id] = distances = CultureDistances(sim, city, radius);
            return distances;
        }

        public void OwnerChanged(int? before, int? after)
        {
            if (_distances.Count == 0) return;
            int? beforeTeam = before is { } b ? sim.TeamOf(b).Id : null, afterTeam = after is { } a ? sim.TeamOf(a).Id : null;
            foreach (var (id, _) in _distances)
            {
                var wars = sim.TeamOf(sim.State.CityById(id).Owner).Wars;
                if ((beforeTeam is { } x && wars.Contains(x)) || (afterTeam is { } y && wars.Contains(y))) _stale.Add(id);
            }
            foreach (int id in _stale) _distances.Remove(id);
            _stale.Clear();
        }
    }
    internal static IReadOnlyDictionary<TileId, int> CultureDistances(Simulation sim, City city, int maxRadius)
    {
        var center = CityData.Tile(sim, city);
        var distances = new Dictionary<TileId, int> { [center] = 0 };
        var queue = new PriorityQueue<TileId, int>();
        queue.Enqueue(center, 0);
        var team = sim.TeamOf(city.Owner);
        while (queue.TryDequeue(out var current, out int distance))
        {
            if (distance != distances[current] || distance >= maxRadius) continue;
            foreach (var next in sim.State.Grid.Neighbors(current))
            {
                if (sim.State.Grid.Distance(center, next) > maxRadius || IsObstructed(sim, team, sim.State.Plots[next.Value])) continue;
                int candidate = distance + StepCultureCost(sim, current, next);
                if (candidate > maxRadius || candidate >= distances.GetValueOrDefault(next, int.MaxValue)) continue;
                distances[next] = candidate;
                queue.Enqueue(next, candidate);
            }
        }
        return distances;
    }
    private static bool IsObstructed(Simulation sim, Team team, Plot plot)
    {
        if (plot.Kind == PlotKind.Peak || sim.Content.Get<S.TerrainDef>(plot.Terrain).Impassable) return true;
        if (plot.Feature is { } id && sim.Content.Get<S.FeatureDef>(id).Impassable) return true;
        return plot.Owner is { } owner && team.Wars.Contains(sim.TeamOf(owner).Id);
    }
    private static int StepCultureCost(Simulation sim, TileId from, TileId to)
    {
        if (!sim.IsOptionEnabled("foundation:gameoption_realistic_culture_spread")) return 1;
        var plot = sim.State.Plots[to.Value];
        var feature = plot.Feature is { } id ? sim.Content.Get<S.FeatureDef>(id) : null;
        int penalty = feature?.CultureDistance ?? 0;
        if (feature is null || !feature.IgnoreTerrainCulture) penalty += sim.Content.Get<S.TerrainDef>(plot.Terrain).CultureDistance;
        if (CrossesRiver(sim, from, to)) penalty++;
        return Math.Max(1, 1 + penalty);
    }
    private static bool CrossesRiver(Simulation sim, TileId from, TileId to) => sim.State.HasRiver(from, to);
    /// <summary>Kept as this mod's entry point; <see cref="PlayerRules.IsBarbarian"/> is the single definition.</summary>
    internal static bool IsBarbarian(Simulation sim, int player) => PlayerRules.IsBarbarian(sim, player);
    internal static int CultureRadius(Simulation sim, City city) => Math.Max(1, CityData.LevelIndex(sim, city));
}

internal sealed class CityCultureDecayPhase : ITurnSystem
{
    public string Id => "cities:culture_decay";
    public int Order => TurnPhase.Map + 2;
    public void Run(Simulation sim) => CityCulture.DecayPlotCulture(sim);
}
