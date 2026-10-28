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
        var center = grid.TileAt(spec.Width / 2, spec.Height / 2);
        int radius = Math.Max(1, Math.Max(spec.Width, spec.Height) / 2);
        foreach (var tile in grid.Tiles)
        {
            int v = rng.Next(100) - grid.Distance(tile, center) * 70 / radius;
            plots[tile.Value].Terrain = v switch
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
}
