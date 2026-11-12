using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.World;

internal static class Geography
{
    internal static S.HexOverridesDef Tuning(ContentDb db) => db.Get<S.HexOverridesDef>("foundation:hex_overrides");
    internal static bool Water(ContentDb db, Plot plot) => db.Get<S.TerrainDef>(plot.Terrain).IsWater;

    /// <summary>Water terrains that stand a tier below a coast. Everything else that is water is shallow, because a lake
    /// is shallow whatever terrain it carries.</summary>
    private static readonly string[] DeepWater = ["world:ocean", "world:slimy_ocean"];

    /// <summary>The height a plot's terrain, lake flag and relief imply. Assets decide the water depth; relief decides the
    /// land tier, so flat land is 0, hills 1 and a peak 2.</summary>
    internal static sbyte HeightOf(ContentDb db, Plot plot) => Water(db, plot)
        ? plot.IsLake || !DeepWater.Contains(plot.Terrain) ? Levels.ShallowWater : Levels.DeepWater
        : plot.Kind switch { PlotKind.Peak => Levels.Peak, PlotKind.Hills => Levels.Hills, _ => Levels.Flat };

    /// <summary>Write every plot's height from its terrain, lake flag and relief. A generator runs this before it stands
    /// any cliff, so a regenerated map cannot keep a height the previous one raised.</summary>
    internal static void AssignHeights(ContentDb db, Plot[] plots)
    {
        foreach (var plot in plots) plot.Height = HeightOf(db, plot);
    }
    internal static bool Impassable(ContentDb db, Plot plot) => plot.Kind == PlotKind.Peak
        || db.Get<S.TerrainDef>(plot.Terrain).Impassable
        || (plot.Feature is { } f && db.Get<S.FeatureDef>(f).Impassable);
    internal static int Latitude(HexGrid grid, TileId tile) =>
        Math.Abs(2 * grid.OffsetOf(tile).Row - (grid.Spec.Height - 1)) * 90 / Math.Max(1, grid.Spec.Height - 1);
    internal static bool Coast(HexGrid grid, Plot[] plots, ContentDb db, TileId tile) =>
        grid.Neighbors(tile).Any(n => Water(db, plots[n.Value]));

    /// <summary>Whether replacing one terrain with another can change which plots are connected. Only a flip between
    /// land and water does; anything else leaves every area as it was.</summary>
    internal static bool ChangesAreas(ContentDb db, string before, string after) =>
        db.Get<S.TerrainDef>(before).IsWater != db.Get<S.TerrainDef>(after).IsWater;

    /// <summary>Flood-fill connected land and water areas and mark small enclosed water as lakes. Each area takes the
    /// smallest TileId among its plots as its id, so a terrain change renumbers only the areas it touches: an area whose
    /// plots did not change keeps its id however the rest of the map changed.</summary>
    internal static void AssignAreas(HexGrid grid, Plot[] plots, ContentDb db)
    {
        foreach (var plot in plots) { plot.AreaId = -1; plot.IsLake = false; }
        foreach (var tile in grid.Tiles)
        {
            if (plots[tile.Value].AreaId >= 0) continue;
            // Tiles run in ascending order, so the first unvisited plot of an area is its smallest.
            var members = FillArea(grid, plots, db, tile, tile.Value);
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
