using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

/// <summary>CvPlotGroup connectivity. Recomputed per query, so tech, route and ownership edits never leave stale caches.</summary>
internal sealed class ResourceNetwork : IResourceNetwork, ICityConnectionService
{
    public int ConnectedCount(Simulation sim, int player, int city, string bonus) => ConnectedCounts(sim, player, city).GetValueOrDefault(bonus);
    public IReadOnlyDictionary<string, int> ConnectedCounts(Simulation sim, int player, int city)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var target = sim.State.FindCity(city);
        if (target is null || target.Owner != player) return counts;
        foreach (var tile in Reachable(sim, player, target))
            if (sim.State.Plots[tile.Value].Bonus is { } bonus && ConnectedBonus(sim, player, tile, bonus))
                counts[bonus] = counts.GetValueOrDefault(bonus) + 1;
        return counts;
    }
    public bool Connected(Simulation sim, int firstCity, int secondCity)
    {
        var first = sim.State.CityById(firstCity);
        var second = sim.State.CityById(secondCity);
        if (sim.TeamOf(first.Owner).Wars.Contains(sim.TeamOf(second.Owner).Id)) return false;
        return Reachable(sim, first.Owner, first, peacefulForeign: true).Contains(sim.State.Grid.TileAt(second.Position));
    }
    private static IEnumerable<TileId> Reachable(Simulation sim, int player, City target, bool peacefulForeign = false)
    {
        var modes = Modes(sim, player, peacefulForeign);
        var start = sim.State.Grid.TileAt(target.Position);
        var seen = new bool[modes.Length];
        var queue = new Queue<TileId>();
        queue.Enqueue(start);
        seen[start.Value] = true;

        while (queue.TryDequeue(out var tile))
        {
            yield return tile;
            foreach (var neighbor in sim.State.Grid.Neighbors(tile))
            {
                if (seen[neighbor.Value] || !Edge(sim, tile, neighbor, modes)) continue;
                seen[neighbor.Value] = true;
                queue.Enqueue(neighbor);
            }
        }
    }
    [Flags] private enum NetworkMode { None = 0, Route = 1, River = 2, Water = 4, City = 8 }
    private static NetworkMode[] Modes(Simulation sim, int player, bool peacefulForeign)
    {
        var techs = sim.TeamOf(player).Techs.Select(t => sim.Content.Get<S.TechDef>(t)).ToArray();
        bool river = techs.Any(t => t.RiverTrade);
        var terrainTrade = techs.SelectMany(t => t.TradeTerrains.Where(p => p.Value).Select(p => p.Key)).ToHashSet(StringComparer.Ordinal);
        return sim.State.Grid.Tiles.Select(t => Mode(sim, player, t, river, terrainTrade, peacefulForeign)).ToArray();
    }
    private static NetworkMode Mode(Simulation sim, int player, TileId tile, bool river, HashSet<string> terrains, bool peacefulForeign)
    {
        var plot = sim.State.Plots[tile.Value];
        if (!Access(sim, player, plot, peacefulForeign) || Geography.Impassable(sim.Content, plot)) return NetworkMode.None;
        if (Geography.Water(sim.Content, plot)) return terrains.Contains(plot.Terrain) || plot.Owner == player ? NetworkMode.Water : NetworkMode.None;
        var mode = NetworkMode.None;
        if (plot.Route is not null) mode |= NetworkMode.Route;
        if (river && plot.RiverEdges != 0) mode |= NetworkMode.River;
        if (CityNode(sim, player, tile, peacefulForeign)) mode |= NetworkMode.City;
        return mode;
    }
private static bool Access(Simulation sim, int player, Plot plot, bool peacefulForeign)
    {
        if (plot.Owner is { } owner) return sim.TeamOf(owner).Id == sim.TeamOf(player).Id
            || (peacefulForeign && !sim.TeamOf(player).Wars.Contains(sim.TeamOf(owner).Id)
            && plot.Revealed.Contains(sim.State.PlayerById(player).TeamId));
        return plot.Revealed.Contains(sim.State.PlayerById(player).TeamId);
    }
    private static bool Edge(Simulation sim, TileId from, TileId to, NetworkMode[] modes)
    {
        var a = modes[from.Value]; var b = modes[to.Value];
        if (a == NetworkMode.None || b == NetworkMode.None) return false;
        if (((a & b) & (NetworkMode.Route | NetworkMode.Water)) != 0) return true;
        if (a.HasFlag(NetworkMode.City) || b.HasFlag(NetworkMode.City)) return true;
        return RiverEdge(sim, from, to, a, b);
    }
    private static bool RiverEdge(Simulation sim, TileId from, TileId to, NetworkMode a, NetworkMode b)
    {
        if (((a | b) & NetworkMode.River) == 0) return false;
        if ((a & (NetworkMode.River | NetworkMode.Water)) == 0 || (b & (NetworkMode.River | NetworkMode.Water)) == 0) return false;
        int direction = Enumerable.Range(0, 6).First(d => sim.State.Grid.Neighbor(from, d) == to);
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

internal sealed class BonusEvaluator(IResourceNetwork network) : IContentRequirementEvaluator
{
    public string Kind => "bonus";
    public Type DefinitionType => typeof(S.BonusDef);
    public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
    {
        if (leaf.Param == 2)
        {
            var service = context.Sim.Content.Services.Get<ICityRequirementService>().SingleOrDefault();
            return service is null ? "Vicinity resources require Cities scope rules" : service.Evaluate(leaf, context);
        }
        var cities = context.City is { } city ? new[] { city } : context.Sim.State.Cities.Where(c => c.Owner == context.Player);
        int count = cities.Select(c => network.ConnectedCount(context.Sim, context.Player, c.Id, leaf.Id)).DefaultIfEmpty().Max();
        return count >= (leaf.Count ?? 1) ? null : $"Required connected bonus: {leaf.Id}";
    }
}
