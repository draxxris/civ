using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.World;

/// <summary>Plot group connectivity. Recomputed per query outside kernel work, so host edits of techs, routes and
/// ownership never leave stale caches; inside a read epoch, where nothing can change, a city's counts are kept per
/// thread, and inside kernel work a city's counts and reached plots are kept until something the flood reads is written
/// (<see cref="ConnectedCounts"/>, <see cref="ConnectedTiles"/>).</summary>
internal sealed class ResourceNetwork : IResourceNetwork, ICityConnectionService
{
    public int ConnectedCount(Simulation sim, int player, int city, string bonus) => ConnectedCounts(sim, player, city).GetValueOrDefault(bonus);
    /// <summary>Inside a read epoch (<see cref="Simulation.OpenReadEpoch"/>) the state cannot change, so a thread keeps
    /// the epoch's answers: an AI segment asks the same city again for every view, eligibility check and exporter's
    /// city group. Across epochs, threads and writes the answer is also kept (<see cref="KeptQuery{TKey,TValue}"/>) until
    /// something the flood reads is written: a city's production choice, a citizen reassignment or a plain unit move
    /// leaves it standing. Outside kernel work every call floods afresh.</summary>
    public IReadOnlyDictionary<string, int> ConnectedCounts(Simulation sim, int player, int city) =>
        _counts.GetOrAdd(sim, (player, city), this,
            static (sim, key, network) => network._kept.Get(sim, key, () => ConnectedCountsUncached(sim, key.Player, key.City)));
    private readonly ReadEpochMemo<(int Player, int City), IReadOnlyDictionary<string, int>> _counts = new();
    private readonly KeptQuery<(int Player, int City), IReadOnlyDictionary<string, int>> _kept =
        new("world:connected_counts", new FloodReader(), KeptAnswerEquality.Dictionary<string, int>());
    /// <summary>What the flood reads: plots' owners, terrain, routes, rivers, improvements, resources and which teams
    /// revealed them, cities' places and owners, techs, wars, and units only through naval blockades. It reads none of
    /// <see cref="CityStateDomains.NeverReadByBuildings"/> (owners only through plot culture's threshold, blockading
    /// units only through the units domain's), the same promise building effects make for the counts they ask.</summary>
    private sealed class FloodReader : IStateReader
    {
        public IReadOnlyList<string>? UnreadDomains(ContentDb content) => CityStateDomains.NeverReadByBuildings;
    }
    private static IReadOnlyDictionary<string, int> ConnectedCountsUncached(Simulation sim, int player, int city)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var target = sim.State.FindCity(city);
        if (target is null || target.Owner != player) return counts;
        // Foreign plots can carry the network, but ConnectedBonus only counts owned sources.
        if (!sim.State.Plots.Any(p => p.Owner == player && p.Bonus is not null)) return counts;
        using var search = Reach(sim, player, target, peacefulForeign: true);
        foreach (var tile in search.Tiles)
            if (sim.State.Plots[tile.Value].Bonus is { } bonus && ConnectedBonus(sim, player, tile, bonus))
                counts[bonus] = counts.GetValueOrDefault(bonus) + 1;
        return counts;
    }
    public bool Connected(Simulation sim, int firstCity, int secondCity)
    {
        var first = sim.State.CityById(firstCity);
        var second = sim.State.CityById(secondCity);
        if (sim.TeamOf(first.Owner).Wars.Contains(sim.TeamOf(second.Owner).Id)) return false;
        using var search = Reach(sim, first.Owner, first, peacefulForeign: true);
        // Seen marks only queued tiles, and the flood has drained its queue, so it is exactly the reached set.
        return search.Seen[sim.State.Grid.TileAt(second.Position).Value];
    }
    /// <summary>One flood from <paramref name="city"/> answers every other city, exactly as <see cref="Connected"/>
    /// from it would: the flood depends only on the first city.</summary>
    public bool[] ConnectedFrom(Simulation sim, int city, IReadOnlyList<int> others)
    {
        var first = sim.State.CityById(city);
        var wars = sim.TeamOf(first.Owner).Wars;
        var result = new bool[others.Count];
        using var search = Reach(sim, first.Owner, first, peacefulForeign: true);
        for (int i = 0; i < result.Length; i++)
        {
            var second = sim.State.CityById(others[i]);
            result[i] = !wars.Contains(sim.TeamOf(second.Owner).Id) && search.Seen[sim.State.Grid.TileAt(second.Position).Value];
        }
        return result;
    }
    /// <summary>Each group's first member is the first city no earlier group reached, so one flood from it decides
    /// every later city still without a group, exactly as <see cref="Connected"/> from it would.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Groups(Simulation sim, IReadOnlyList<int> cities)
    {
        var groups = new List<IReadOnlyList<int>>();
        var members = cities.Select(sim.State.CityById).ToArray();
        var grouped = new bool[members.Length];
        for (int i = 0; i < members.Length; i++)
        {
            if (grouped[i]) continue;
            var first = members[i];
            var group = new List<int> { first.Id };
            grouped[i] = true;
            groups.Add(group);
            var wars = sim.TeamOf(first.Owner).Wars;
            using var search = Reach(sim, first.Owner, first, peacefulForeign: true);
            for (int j = i + 1; j < members.Length; j++)
            {
                if (grouped[j] || wars.Contains(sim.TeamOf(members[j].Owner).Id)
                    || !search.Seen[sim.State.Grid.TileAt(members[j].Position).Value]) continue;
                grouped[j] = true;
                group.Add(members[j].Id);
            }
        }
        return groups;
    }
    /// <summary>The plots the city's network reaches, kept as <see cref="ConnectedCounts"/> is: vicinity resources ask it
    /// for every candidate building that needs one.</summary>
    public IReadOnlySet<TileId> ConnectedTiles(Simulation sim, int player, int city) =>
        _tiles.Get(sim, (player, city), () => ConnectedTilesUncached(sim, player, city));
    private readonly KeptQuery<(int Player, int City), IReadOnlySet<TileId>> _tiles =
        new("world:connected_tiles", new FloodReader(), KeptAnswerEquality.Set<TileId>());
    private static IReadOnlySet<TileId> ConnectedTilesUncached(Simulation sim, int player, int city)
    {
        var target = sim.State.FindCity(city);
        if (target is null || target.Owner != player) return new HashSet<TileId>();
        using var search = Reach(sim, player, target, peacefulForeign: true);
        return search.Tiles.ToHashSet();
    }
    public bool IsTileBlockaded(Simulation sim, TileId tile, int team) =>
        sim.Content.Services.Get<INavalBlockadeService>().Any(s => s.IsBlockaded(sim, tile, team));
    public bool IsCityBlockaded(Simulation sim, int city)
    {
        var target = sim.State.CityById(city);
        return sim.State.Grid.Neighbors(sim.State.Grid.TileAt(target.Position))
            .Any(t => IsTileBlockaded(sim, t, sim.TeamOf(target.Owner).Id));
    }
    /// <summary>Flood the network from a city. The result borrows this thread's buffers: dispose it before the next
    /// search on the thread, which every caller does by reading it at once.</summary>
    private static Search Reach(Simulation sim, int player, City target, bool peacefulForeign = false)
    {
        var search = Search.Begin(sim, player, peacefulForeign);
        var start = sim.State.Grid.TileAt(target.Position);
        if (search.ModeOf(start.Value) == NetworkMode.None) return search;
        var queue = search.Queue;
        queue.Enqueue(start);
        search.Seen[start.Value] = true;
        while (queue.TryDequeue(out var tile))
        {
            search.Tiles.Add(tile);
            foreach (var neighbor in sim.State.Grid.Neighbors(tile))
            {
                if (search.Seen[neighbor.Value] || !Edge(sim, tile, neighbor, search)) continue;
                search.Seen[neighbor.Value] = true;
                queue.Enqueue(neighbor);
            }
        }
        return search;
    }
    [Flags] private enum NetworkMode : byte { None = 0, Route = 1, River = 2, Water = 4, City = 8, Resolved = 0x80 }
    /// <summary>One network search: plot modes, resolved on first use so a search only pays for the plots it reaches,
    /// and the tiles reached in breadth-first order. Searches run thousands of times a turn, so each thread keeps one
    /// set of map-sized buffers and reuses it; a search started while another is open on the thread gets its own.</summary>
    private sealed class Search : IDisposable
    {
        [ThreadStatic] private static Search? _spare;
        private Simulation _sim = null!;
        private int _player;
        private bool _river, _peacefulForeign, _open;
        private readonly HashSet<string> _terrainTrade = new(StringComparer.Ordinal);
        private IReadOnlySet<TileId>[] _blockaded = [];
        private NetworkMode[] _modes = [];
        /// <summary>Tiles the flood has queued or reached.</summary>
        public bool[] Seen { get; private set; } = [];
        public Queue<TileId> Queue { get; } = new();
        /// <summary>Reached tiles, nearest first.</summary>
        public List<TileId> Tiles { get; } = new();

        public static Search Begin(Simulation sim, int player, bool peacefulForeign)
        {
            var search = _spare is { _open: false } spare ? spare : new Search();
            _spare ??= search;
            search.Reset(sim, player, peacefulForeign);
            return search;
        }

        private void Reset(Simulation sim, int player, bool peacefulForeign)
        {
            _sim = sim; _player = player; _peacefulForeign = peacefulForeign; _open = true;
            _river = false;
            _terrainTrade.Clear();
            foreach (var id in sim.TeamOf(player).Techs)
            {
                var tech = sim.Content.Get<S.TechDef>(id);
                _river |= tech.RiverTrade;
                foreach (var (terrain, trades) in tech.TradeTerrains) if (trades) _terrainTrade.Add(terrain);
            }
            // Resolve blockades once for the whole query instead of scanning every unit for every plot.
            int team = sim.TeamOf(player).Id;
            var blockades = sim.Content.Services.Get<INavalBlockadeService>();
            if (_blockaded.Length != blockades.Count) _blockaded = new IReadOnlySet<TileId>[blockades.Count];
            for (int i = 0; i < blockades.Count; i++) _blockaded[i] = blockades[i].BlockadedTiles(sim, team);
            int count = sim.State.Grid.Count;
            if (_modes.Length != count) { _modes = new NetworkMode[count]; Seen = new bool[count]; }
            else { Array.Clear(_modes); Array.Clear(Seen); }
            Queue.Clear();
            Tiles.Clear();
        }

        public NetworkMode ModeOf(int tile)
        {
            var mode = _modes[tile];
            if (mode != NetworkMode.None) return mode & ~NetworkMode.Resolved;
            mode = Mode(_sim, _player, new TileId(tile), _river, _terrainTrade, _peacefulForeign, _blockaded);
            _modes[tile] = mode | NetworkMode.Resolved;
            return mode;
        }

        public void Dispose()
        {
            _open = false;
            _sim = null!; // A parked search must not keep a finished simulation fork alive.
            Array.Clear(_blockaded);
        }
    }
    private static NetworkMode Mode(Simulation sim, int player, TileId tile, bool river, HashSet<string> terrains, bool peacefulForeign,
        IReadOnlySet<TileId>[] blockaded)
    {
        var plot = sim.State.Plots[tile.Value];
        foreach (var blockade in blockaded) if (blockade.Contains(tile)) return NetworkMode.None;
        if (!Access(sim, player, tile, plot, peacefulForeign) || Geography.Impassable(sim.Content, plot))
            return NetworkMode.None;

        if (Geography.Water(sim.Content, plot)) return terrains.Contains(plot.Terrain) || plot.Owner == player ? NetworkMode.Water : NetworkMode.None;
        var mode = NetworkMode.None;
        if (plot.Route is not null) mode |= NetworkMode.Route;
        if (river && plot.RiverEdges != 0) mode |= NetworkMode.River;
        if (CityNode(sim, player, tile, peacefulForeign)) mode |= NetworkMode.City;
        return mode;
    }
    private static bool Access(Simulation sim, int player, TileId tile, Plot plot, bool peacefulForeign)
    {
        if (plot.Owner is { } owner) return sim.TeamOf(owner).Id == sim.TeamOf(player).Id
            || (peacefulForeign && !sim.TeamOf(player).Wars.Contains(sim.TeamOf(owner).Id)
            && plot.Revealed.Contains(sim.State.PlayerById(player).TeamId));
        return plot.Revealed.Contains(sim.State.PlayerById(player).TeamId);
    }

    private static bool Edge(Simulation sim, TileId from, TileId to, Search search)
    {
        var a = search.ModeOf(from.Value); var b = search.ModeOf(to.Value);
        if (a == NetworkMode.None || b == NetworkMode.None) return false;
        if (((a & b) & (NetworkMode.Route | NetworkMode.Water)) != 0) return true;
        if (a.HasFlag(NetworkMode.City) || b.HasFlag(NetworkMode.City)) return true;
        return RiverEdge(sim, from, to, a, b);
    }
    private static bool RiverEdge(Simulation sim, TileId from, TileId to, NetworkMode a, NetworkMode b)
    {
        if (((a | b) & NetworkMode.River) == 0) return false;
        if ((a & (NetworkMode.River | NetworkMode.Water)) == 0 || (b & (NetworkMode.River | NetworkMode.Water)) == 0) return false;
        int direction = 0;
        while (sim.State.Grid.Neighbor(from, direction) != to) direction++;
        int fromEdges = sim.State.Plots[from.Value].RiverEdges, toEdges = sim.State.Plots[to.Value].RiverEdges;
        // Same river corridor: the shared edge, or an edge meeting it at either shared vertex.
        return (fromEdges & (1 << direction)) != 0
            || (fromEdges & ((1 << ((direction + 1) % 6)) | (1 << ((direction + 5) % 6)))) != 0
            || (toEdges & ((1 << ((direction + 2) % 6)) | (1 << ((direction + 4) % 6)))) != 0;
    }
    private static bool CityNode(Simulation sim, int player, TileId tile, bool peacefulForeign = false)
    {
        if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is { } city) return sim.TeamOf(city.Owner).Id == sim.TeamOf(player).Id
            || (peacefulForeign && !sim.TeamOf(player).Wars.Contains(sim.TeamOf(city.Owner).Id));
        var plot = sim.State.Plots[tile.Value];
        return plot.Owner == player && plot.Improvement is { } id && sim.Content.Get<S.ImprovementDef>(id).ActsAsCity;
    }
    private static bool ConnectedBonus(Simulation sim, int player, TileId tile, string id)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Owner != player || plot.Bonus != id) return false;
        var bonus = PlotYieldService.RevealedBonus(sim, player, plot);
        if (bonus is null || (bonus.TradeTech is { } tech && !sim.KnowsTech(player, tech))) return false;
        if (CityNode(sim, player, tile)) return true;
        return plot.Improvement is { } improvement && sim.Content.Get<S.ImprovementDef>(improvement).BonusEffects.GetValueOrDefault(id)?.Connects == true;
    }
}

internal sealed class BonusEvaluator : IContentRequirementEvaluator
{
    public string Kind => "bonus";
    public Type DefinitionType => typeof(S.BonusDef);
    public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
    {
        if (leaf.Param == 2)
        {
            var service = context.Sim.Rules().CityRequirements;
            return service is null ? "Vicinity resources require Cities scope rules" : service.Evaluate(leaf, context);
        }
        var cities = context.City is { } city ? new[] { city } : context.Sim.State.Cities.Where(c => c.Owner == context.Player);
        long count = cities.Select(c => ResourceAccess.Count(context.Sim, context.Player, c.Id, leaf.Id)).DefaultIfEmpty().Max();
        return count >= (leaf.Count ?? 1) ? null : $"Required connected bonus: {leaf.Id}";
    }
}
