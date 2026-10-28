using Ariel;
using Ariel.Client;
using Ariel.Game;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>The setup screen's coastline must be the coastline the world will have. The preview reads the land step of
/// generation from a fresh stream, so it has to agree with a full generation for the same seed, without consuming the
/// game's own stream.</summary>
public sealed class MapPreviewTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Theory]
    [InlineData("foundation:worldsize_duel", 1UL)]
    [InlineData("foundation:worldsize_standard", 42UL)]
    [InlineData("foundation:worldsize_gigantic", 987654UL)]
    [Trait("Category", "WorldGen")]
    public void Every_shipped_layout_previews_the_world_it_generates(string worldSize, ulong seed)
    {
        var catalog = mods.Catalog;
        var content = catalog.Content;
        foreach (var choice in catalog.MapGenerators)
        {
            var options = catalog.DefaultOptions() with { MapGenerator = choice.Id, WorldSize = worldSize, Seed = seed, Opponents = 0 };
            var map = options.ToSettings(catalog).ResolveMap(content);
            var generator = content.MapGenerators.Get(choice.Id);
            var preview = generator.PreviewLand(map, seed, content);
            var plots = Enumerable.Range(0, map.Width * map.Height).Select(_ => new Plot()).ToArray();
            generator.Generate(map, plots, new DeterministicRng(seed), content, players: 1);
            Assert.Equal(plots.Length, preview.Length);
            for (int tile = 0; tile < plots.Length; tile++)
                Assert.True(!content.Get<S.TerrainDef>(plots[tile].Terrain).IsWater == preview[tile],
                    $"{choice.Id} {worldSize} seed {seed} tile {tile}");
        }
    }

    /// <summary>The picture is a view of a silhouette: it fits the box, keeps the map's proportions and its colours.</summary>
    [Fact]
    public void Rendered_picture_fits_its_box_and_keeps_land_and_water_apart()
    {
        var water = new Rgb(0.35f, 0.68f, 0.87f);
        var land = new Rgb(0.11f, 0.26f, 0.14f);
        const int columns = 10, rows = 6;
        var mask = new bool[columns * rows];
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns / 2; col++) mask[row * columns + col] = true;
        var image = MapSilhouetteImage.Render(new(new MapSpec(columns, rows), mask), 200, 120, water, land, samples: 2);

        Assert.True(image.Width <= 200 && image.Height <= 120);
        Assert.Equal(image.Width * image.Height * 4, image.Rgba.Length);
        double expected = (Math.Sqrt(3) * (columns + 0.5)) / (1.5 * (rows - 1) + 2);
        Assert.InRange((double)image.Width / image.Height, expected * 0.98, expected * 1.02);
        // Half of the columns are land on the left of the map and water on the right of it.
        Assert.Equal(Tone(land), Pixel(image, image.Width / 8, image.Height / 2));
        Assert.Equal(Tone(water), Pixel(image, image.Width * 7 / 8, image.Height / 2));
    }

    /// <summary>A wrapping map repeats every W columns, so its picture is one period wide and has no seam: every
    /// pixel of the middle row samples the map rather than falling outside it.</summary>
    [Fact]
    public void Wrapped_maps_render_one_period_and_leave_no_seam()
    {
        var water = new Rgb(0.35f, 0.68f, 0.87f);
        var land = new Rgb(0.11f, 0.26f, 0.14f);
        const int columns = 10, rows = 6;
        var mask = new bool[columns * rows];
        mask[0] = true;
        var image = MapSilhouetteImage.Render(new(new MapSpec(columns, rows, Wrap: true), mask), 200, 120, water, land, samples: 2);

        double expected = (Math.Sqrt(3) * columns) / (1.5 * (rows - 1) + 2);
        Assert.InRange((double)image.Width / image.Height, expected * 0.98, expected * 1.02);
        for (int x = 0; x < image.Width; x++) Assert.Equal(255, Pixel(image, x, image.Height / 2).A);
    }

    private static (byte R, byte G, byte B, byte A) Pixel(MapImage image, int x, int y)
    {
        int at = (y * image.Width + x) * 4;
        return (image.Rgba[at], image.Rgba[at + 1], image.Rgba[at + 2], image.Rgba[at + 3]);
    }

    private static (byte R, byte G, byte B, byte A) Tone(Rgb colour) =>
        ((byte)Math.Round(colour.R * 255), (byte)Math.Round(colour.G * 255), (byte)Math.Round(colour.B * 255), 255);
}
