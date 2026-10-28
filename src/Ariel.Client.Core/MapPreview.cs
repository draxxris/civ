using Ariel.Content;
using Ariel.Game;

namespace Ariel.Client;

/// <summary>A world's coastline as the new-game screen shows it: the map it belongs to and one flag per tile, true
/// where the plot is land.</summary>
public sealed record MapSilhouette(MapSpec Map, bool[] Land)
{
    /// <summary>Land flags in tile order, matching <see cref="HexGrid.Count"/>.</summary>
    public int Count => Land.Length;
}

/// <summary>Engine-free previews of a setup choice's world. Nothing here mutates state, so a screen can run it on a
/// worker thread while the player keeps choosing.</summary>
public static class MapPreview
{
    /// <summary>The coastline the given choice generates. It calls the chosen generator's land step, which must match
    /// the map generation that follows, so the picture is the world the player will get.</summary>
    public static MapSilhouette Land(GameCatalog catalog, NewGameOptions options)
    {
        var content = catalog.Content;
        var settings = options.ToSettings(catalog);
        var map = settings.ResolveMap(content);
        var mask = content.MapGenerators.Get(settings.MapGenerator).PreviewLand(map, settings.Seed, content);
        if (mask is null || mask.Length != map.Width * map.Height)
            throw new InvalidDataException($"Map generator '{settings.MapGenerator}' previewed " +
                $"{mask?.Length ?? 0} plots for a {map.Width} by {map.Height} map.");
        return new(map, mask);
    }
}
