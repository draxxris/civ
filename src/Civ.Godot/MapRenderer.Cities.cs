using Civ.Client.Terrain;
using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class MapRenderer
{

    /// <summary>Buildings keep this far from rivers.</summary>
    private const double RiverClearance = 0.04;

    /// <summary>One site per known city that is not hidden: its buildings, laid out round its walls and roads.</summary>
    private List<CitySite> CitySites()
    {
        var state = _session.Sim.State;
        var cities = _session.KnownCities().ToList();
        var cityTiles = cities.Select(c => state.Grid.TileAt(c.Position)).ToHashSet();
        var sites = new List<CitySite>();
        foreach (var city in cities)
        {
            var sight = _session.SightOf(city.Position);
            if (sight == Sight.Hidden) continue;
            var tile = state.Grid.TileAt(city.Position);
            var centre = PositionOf(city.Position);
            var look = LookOf(city.Owner);
            var style = look.Style;
            var walls = _improvements.CityRing(city) is { } ring ? _improvements.WallsOf(ring) : null;
            var input = new CityLayoutInput(CitySeed(city), city.Population, style, walls, RoadDirections(tile),
                (x, z) => Buildable(tile, cityTiles, centre.X + (float)x, centre.Z + (float)z));
            sites.Add(new CitySite(centre, CityLayout.Plan(input), style, look.Ground, walls != null,
                sight == Sight.Visible ? 1f : 0.55f));
        }
        return sites;
    }

    /// <summary>The look of a player's cities in its current era, from the mods; huts on dirt when none says.</summary>
    private CityAppearance LookOf(int player)
    {
        var sim = _session.Sim;
        string era = sim.State.PlayerById(player).Extensions.Get<CityPlayerState>(sim.Content)?.CurrentEra ?? sim.Settings.StartingEra;
        return sim.Content.Services.Get<ICityAppearanceService>().Select(s => s.Era(era)).FirstOrDefault(a => a != null)
            ?? new CityAppearance(CityStyle.Huts, CityGround.Dirt);
    }

    private static int CitySeed(City city) =>
        unchecked(city.Id * 73856093 ^ city.Position.Q * 19349663 ^ city.Position.R * 83492791);

    /// <summary>Directions (Hex.Directions order) toward neighbours with a road or railroad.</summary>
    private int RoadDirections(TileId tile)
    {
        var state = _session.Sim.State;
        int mask = 0;
        for (int d = 0; d < 6; d++)
            if (state.Grid.Neighbor(tile, d) is TileId n && n != tile && state.Plots[n.Value].Route != null) mask |= 1 << d;
        return mask;
    }

    /// <summary>True when a building may stand at the map point: away from sea, lakes and rivers. (Flat land can lie
    /// close to height 0, so a height test would empty low cities.) On a neighbour tile the
    /// point must also be seen, not a peak, free of another city, of improvement models and of trees.</summary>
    private bool Buildable(TileId cityTile, HashSet<TileId> cityTiles, float x, float z)
    {
        if (TerrainField.NearWater(x, z) || TerrainField.Rivers.Near(x, z, RiverClearance)) return false;
        var state = _session.Sim.State;
        if (!state.Grid.TryTile(Hex.FromPixel(x, z, 1), out var tile)) return false;
        if (tile == cityTile) return true;
        if (cityTiles.Contains(tile)) return false;
        var plot = state.Plots[tile.Value];
        if (_session.SightOf(plot) == Sight.Hidden || plot.Kind == PlotKind.Peak) return false;
        if (plot.Improvement is { } improvement && _improvements.Has(improvement)) return false;
        return plot.Feature is not { } feature || _terrain.Palette.ScatterFor(feature, plot.Terrain)?.Scatter is null;
    }
}
