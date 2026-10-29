using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

internal static class Geography
{
    internal static S.HexOverridesDef Tuning(ContentDb db) => db.Get<S.HexOverridesDef>("foundation:hex_overrides");
    internal static bool Water(ContentDb db, Plot plot) => db.Get<S.TerrainDef>(plot.Terrain).IsWater;
    internal static bool Impassable(ContentDb db, Plot plot) => plot.Kind == PlotKind.Peak
        || db.Get<S.TerrainDef>(plot.Terrain).Impassable
        || (plot.Feature is { } f && db.Get<S.FeatureDef>(f).Impassable);
    internal static int Latitude(HexGrid grid, TileId tile) =>
        Math.Abs(2 * grid.OffsetOf(tile).Row - (grid.Spec.Height - 1)) * 90 / Math.Max(1, grid.Spec.Height - 1);
    internal static bool Coast(HexGrid grid, Plot[] plots, ContentDb db, TileId tile) =>
        grid.Neighbors(tile).Any(n => Water(db, plots[n.Value]));

    internal static void AssignAreas(HexGrid grid, Plot[] plots, ContentDb db)
    {
        foreach (var plot in plots) { plot.AreaId = -1; plot.IsLake = false; }
        int area = 0;
        foreach (var tile in grid.Tiles)
        {
            if (plots[tile.Value].AreaId >= 0) continue;
            var members = FillArea(grid, plots, db, tile, area++);
            bool lake = Water(db, plots[tile.Value]) && members.Count <= Tuning(db).LakeMaxSize
                && members.All(t => grid.OffsetOf(t).Row > 0 && grid.OffsetOf(t).Row < grid.Spec.Height - 1);
            if (lake) foreach (var member in members) { plots[member.Value].IsLake = true; plots[member.Value].Terrain = "world:coast"; }
        }
    }

    private static List<TileId> FillArea(HexGrid grid, Plot[] plots, ContentDb db, TileId start, int area)
    {
        var queue = new Queue<TileId>();
        var members = new List<TileId>();
        bool water = Water(db, plots[start.Value]);
        plots[start.Value].AreaId = area;
        queue.Enqueue(start);
        while (queue.TryDequeue(out var tile))
        {
            members.Add(tile);
            foreach (var neighbor in grid.Neighbors(tile))
            {
                var plot = plots[neighbor.Value];
                if (plot.AreaId >= 0 || Water(db, plot) != water) continue;
                plot.AreaId = area;
                queue.Enqueue(neighbor);
            }
        }
        return members;
    }
}
