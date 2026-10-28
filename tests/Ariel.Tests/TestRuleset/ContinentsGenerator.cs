using Ariel.Content;
using Ariel.Game;

namespace Ariel.BaseGame;

/// <summary>A small deterministic radial elevation generator for the test ruleset.</summary>
public sealed class ContinentsGenerator : IMapGenerator
{
    public string Id => "base:continents";

    public void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content)
    {
        var grid = new HexGrid(spec);
        var ratings = Ratings(grid, rng);
        foreach (var tile in grid.Tiles)
        {
            plots[tile.Value].Terrain = ratings[tile.Value] switch
            {
                < 0 => "base:ocean",
                >= 90 => "base:mountains",
                >= 75 => "base:hills",
                >= 45 => "base:grassland",
                >= 20 => "base:plains",
                _ => "base:desert",
            };
        }
    }

    public bool[] PreviewLand(MapSpec spec, ulong seed, ContentDb content)
    {
        var grid = new HexGrid(spec);
        return Ratings(grid, new DeterministicRng(seed)).Select(rating => rating >= 0).ToArray();
    }

    /// <summary>One draw per tile in tile order, so a seed's ratings are the same for a preview and a generation.</summary>
    private static int[] Ratings(HexGrid grid, DeterministicRng rng)
    {
        var center = grid.TileAt(grid.Spec.Width / 2, grid.Spec.Height / 2);
        int radius = Math.Max(1, Math.Max(grid.Spec.Width, grid.Spec.Height) / 2);
        var ratings = new int[grid.Count];
        foreach (var tile in grid.Tiles) ratings[tile.Value] = rng.Next(100) - grid.Distance(tile, center) * 70 / radius;
        return ratings;
    }
}
