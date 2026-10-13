using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

/// <summary>CvPlotGroup connectivity. Recomputed per query, so tech, route and ownership edits never leave stale caches.</summary>
internal sealed class ResourceNetwork : IResourceNetwork
{
    public int ConnectedCount(Simulation sim, int player, int city, string bonus) => ConnectedCounts(sim, player, city).GetValueOrDefault(bonus);
    public IReadOnlyDictionary<string, int> ConnectedCounts(Simulation sim, int player, int city)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var target = sim.State.FindCity(city);
        if (target is null || target.Owner != player) return counts;
        var modes = Modes(sim, player);
        var start = sim.State.Grid.TileAt(target.Position);
        var seen = new bool[modes.Length];
        var queue = new Queue<TileId>();
        queue.Enqueue(start);
        seen[start.Value] = true;

        while (queue.TryDequeue(out var tile))
        {
            if (sim.State.Plots[tile.Value].Bonus is { } bonus && ConnectedBonus(sim, player, tile, bonus))
                counts[bonus] = counts.GetValueOrDefault(bonus) + 1;
            foreach (var neighbor in sim.State.Grid.Neighbors(tile))
            {
                if (seen[neighbor.Value] || !Edge(sim, tile, neighbor, modes)) continue;
                seen[neighbor.Value] = true;
                queue.Enqueue(neighbor);
            }
        }
        return counts;
    }
    [Flags] private enum NetworkMode { None = 0, Route = 1, River = 2, Water = 4, City = 8 }
    private static NetworkMode[] Modes(Simulation sim, int player)
    {
        var techs = sim.TeamOf(player).Techs.Select(t => sim.Content.Get<S.TechDef>(t)).ToArray();
        bool river = techs.Any(t => t.RiverTrade);
        var terrainTrade = techs.SelectMany(t => t.TradeTerrains.Where(p => p.Value).Select(p => p.Key)).ToHashSet(StringComparer.Ordinal);
        return sim.State.Grid.Tiles.Select(t => Mode(sim, player, t, river, terrainTrade)).ToArray();
    }
    private static NetworkMode Mode(Simulation sim, int player, TileId tile, bool river, HashSet<string> terrains)
    {
        var plot = sim.State.Plots[tile.Value];
        if (!Access(sim, player, plot) || Geography.Impassable(sim.Content, plot)) return NetworkMode.None;
        if (Geography.Water(sim.Content, plot)) return terrains.Contains(plot.Terrain) ? NetworkMode.Water : NetworkMode.None;
        var mode = NetworkMode.None;
        if (plot.Route is not null) mode |= NetworkMode.Route;
        if (river && plot.RiverEdges != 0) mode |= NetworkMode.River;
        if (CityNode(sim, player, tile)) mode |= NetworkMode.City;
        return mode;
    }
    private static bool Access(Simulation sim, int player, Plot plot)
    {
        if (plot.Owner is { } owner) return sim.State.PlayerById(owner).TeamId == sim.State.PlayerById(player).TeamId;
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
        return (sim.State.Plots[from.Value].RiverEdges & (1 << direction)) != 0;
    }
    private static bool CityNode(Simulation sim, int player, TileId tile)
    {
        if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is { } city) return city.Owner == player;
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
