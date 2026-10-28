using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>Generated coastal framing, with a connected debug mountain ridge, territory and demonstration road.</summary>
internal static class TerrainShowcase
{
    public static Hex Focus(GameSession session)
    {
        var state = session.Sim.State;
        var peaks = state.Grid.Tiles.Where(t => state.Plots[t.Value].Kind == PlotKind.Peak);
        return peaks.OrderByDescending(t => Score(session, state.Grid.HexOf(t))).ThenBy(t => t.Value)
            .Select(state.Grid.HexOf).FirstOrDefault();
    }

    /// <summary>A generated river worth inspecting: many river edges, bends and a mouth on the coast or a lake nearby.
    /// Read-only, so the river stage shows the generator's own rivers.</summary>
    public static Hex RiverFocus(GameSession session)
    {
        var state = session.Sim.State;
        bool Water(Plot plot) => session.Sim.Content.Get<Schema.TerrainDef>(plot.Terrain).IsWater;
        int Score(Hex centre)
        {
            var near = state.Grid.Range(centre, 2).Select(state.PlotAt).ToArray();
            int edges = near.Sum(p => System.Numerics.BitOperations.PopCount(p.RiverEdges));
            int mouths = near.Count(p => Water(p) && p.RiverEdges != 0);
            int land = near.Count(p => !Water(p));
            return edges * 10 + Math.Min(mouths, 2) * 25 + land * 2;
        }
        return state.Grid.Tiles.Select(state.Grid.HexOf).Where(h => state.PlotAt(h).RiverEdges != 0)
            .OrderByDescending(Score).ThenBy(h => state.Grid.TileAt(h).Value).FirstOrDefault();
    }

    private static int Score(GameSession session, Hex centre)
    {
        var state = session.Sim.State;
        var near = state.Grid.Range(centre, 5).Select(state.PlotAt).ToArray();
        int water = near.Count(p => session.Sim.Content.Get<Schema.TerrainDef>(p.Terrain).IsWater);
        bool Scatter(Plot plot, string set) => plot.Feature is { } feature
            && session.Sim.Content.Services.Get<ITileAppearanceService>().Any(s => s.Feature(feature)?.Scatter == set);
        int forest = near.Count(p => Scatter(p, "forest"));
        int jungle = near.Count(p => Scatter(p, "jungle"));
        int peaks = near.Count(p => p.Kind == PlotKind.Peak);
        int rivers = near.Count(p => p.RiverEdges != 0);
        int connected = ConnectedPeaks(session, centre).Count;
        // Prefer a compact connected ridge, not unrelated peaks at the edge of a wide search circle.
        return (water > 0 && forest > 0 && connected >= 3 ? 10000 : 0) + connected * 100
            + (water > 0 && forest > 0 && peaks > 1 ? 1000 : 0)
            + Math.Min(water, 8) * 10 + Math.Min(forest, 8) * 8 + Math.Min(jungle, 5) * 10
            + Math.Min(peaks, 6) * 8 + Math.Min(rivers, 6) * 5 + near.Select(p => p.Terrain).Distinct().Count() * 3;
    }

    private static HashSet<Hex> ConnectedPeaks(GameSession session, Hex centre)
    {
        var grid = session.Sim.State.Grid;
        var nearby = grid.Range(centre, 2).Where(h => session.Sim.State.PlotAt(h).Kind == PlotKind.Peak).ToHashSet();
        var connected = new HashSet<Hex> { centre };
        var pending = new Queue<Hex>();
        pending.Enqueue(centre);
        while (pending.TryDequeue(out var h))
            foreach (var neighbour in grid.Neighbors(h))
                if (nearby.Contains(neighbour) && connected.Add(neighbour)) pending.Enqueue(neighbour);
        return connected;
    }

    private static void PrepareRidge(GameSession session, Hex centre)
    {
        var state = session.Sim.State;
        var ridge = ConnectedPeaks(session, centre);
        // Sculpted peaks take the focus peak's generated mountain terrain, so no biome survives under a summit.
        string mountain = state.PlotAt(centre).Terrain;
        // Screenshot-only sculpting ensures a compact five-peak range even when the generator separates its peaks.
        while (ridge.Count < 5)
        {
            var candidates = ridge.SelectMany(state.Grid.Neighbors).Distinct().Where(h => !ridge.Contains(h))
                .Where(h => !session.Sim.Content.Get<Schema.TerrainDef>(state.PlotAt(h).Terrain).IsWater)
                .OrderBy(h => state.Grid.Distance(h, centre)).ThenBy(h => state.Grid.TileAt(h).Value).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("The terrain showcase needs five connected land tiles.");
            ridge.Add(candidates[0]);
        }
        foreach (var h in ridge)
        {
            state.PlotAt(h).Kind = PlotKind.Peak;
            state.PlotAt(h).Terrain = mountain;
            state.PlotAt(h).Feature = null;
        }
        Godot.GD.Print($"terrain showcase: {ridge.Count} connected peak tiles near {centre}");
    }

    public static void Prepare(GameSession session)
    {
        var state = session.Sim.State;
        var centre = Focus(session);
        PrepareRidge(session, centre);
        centre = Focus(session);
        foreach (var plot in state.Plots) plot.Visible.Add(session.Me.TeamId);
        bool Land(Hex h) => !session.Sim.Content.Get<Schema.TerrainDef>(state.PlotAt(h).Terrain).IsWater
            && state.PlotAt(h).Kind != PlotKind.Peak;
        var territory = state.Grid.Range(centre, 2).Where(Land).ToArray();
        foreach (var h in territory) state.PlotAt(h).Owner = GameSession.Human;
        if (territory.Length == 0) return;
        var road = new List<Hex> { territory[0] };
        for (int i = 0; i < 4; i++)
        {
            var next = state.Grid.Neighbors(road[^1]).Where(Land).Where(h => !road.Contains(h)).ToArray();
            if (next.Length == 0) break;
            road.Add(next[0]);
        }
        var route = session.Sim.Content.Get<Schema.RouteDef>().All.Where(r => !r.SeaTunnel)
            .OrderBy(r => r.Value).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
        if (route != null)
            foreach (var h in road) state.PlotAt(h).Route = route.Id;
    }
}
