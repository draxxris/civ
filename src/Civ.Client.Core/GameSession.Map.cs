using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>A tooltip for a tile: a title and detail lines, limited to what the local team has seen.</summary>
public sealed record TileInfo(string Title, IReadOnlyList<string> Lines);

public sealed partial class GameSession
{
    public Sight SightOf(Plot plot) =>
        plot.Visible.Contains(Me.TeamId) ? Sight.Visible : plot.Revealed.Contains(Me.TeamId) ? Sight.Fogged : Sight.Hidden;

    public Sight SightOf(Hex hex) => SightOf(Sim.State.PlotAt(hex));

    /// <summary>Observed plot state only. Missing fog memory never falls back to live state.</summary>
    public RevealedPlotSnapshot? PerceivedPlot(TileId tile) =>
        Service<IVisibilityService>().SnapshotOf(Sim, tile, Me.TeamId);

    public RevealedPlotSnapshot? PerceivedPlot(Hex hex) => PerceivedPlot(Sim.State.Grid.TileAt(hex));

    /// <summary>The canonical tile under a map position (one world unit per hex size), or null off the map. Wrap images resolve to their tile.</summary>
    public Hex? Pick(double x, double z, double hexSize = 1)
    {
        var hex = Hex.FromPixel(x, z, hexSize);
        return Sim.State.Grid.TryTile(hex, out var tile) ? Sim.State.Grid.HexOf(tile) : null;
    }

    /// <summary>Horizontal distance after which the map repeats, in world units for hex size 1.</summary>
    public double WrapPeriod => Sim.State.Map.Wrap ? Sim.State.Map.Width * Math.Sqrt(3) : 0;

    public TileInfo? DescribeTile(Hex hex)
    {
        if (!Sim.State.Grid.TryTile(hex, out _)) return null;
        hex = Sim.State.Grid.Normalize(hex);
        var sight = SightOf(hex);
        if (PerceivedPlot(hex) is not { } plot) return new("Unexplored", []);
        var terrain = Sim.Content.Get<Schema.TerrainDef>(plot.Terrain).Name;
        var lines = new List<string>();
        AddPlotLines(plot, lines);
        AddOwnerLines(plot, lines);
        if (sight == Sight.Visible)
        {
            var y = YieldsOf(hex);
            lines.Add($"Yields: {y.Food} food, {y.Production} production, {y.Commerce} commerce");
            AddUnitLines(hex, lines);
        }
        return new(plot.Kind == PlotKind.Hills ? $"{plot.Kind} {terrain}" : terrain, lines);
    }

    private void AddPlotLines(RevealedPlotSnapshot plot, List<string> lines)
    {
        if (plot.Feature is { } f) lines.Add(Sim.Content.Get<Schema.FeatureDef>(f).Name);
        if (plot.Bonus is { } b && BonusVisible(b)) lines.Add("Resource: " + Sim.Content.Get<Schema.BonusDef>(b).Name);
        if (plot.Improvement is { } i) lines.Add(Sim.Content.Get<Schema.ImprovementDef>(i).Name);
        if (plot.Route is { } r) lines.Add(Sim.Content.Get<Schema.RouteDef>(r).Name);
        if (plot.RiverEdges != 0) lines.Add("River");

    }

    /// <summary>Resources are shown only after their reveal technology is known.</summary>
    public bool BonusVisible(string bonus)
    {
        var tech = Sim.Content.Get<Schema.BonusDef>(bonus).RevealTech;
        return tech == null || MyTeam.Techs.Contains(tech);
    }

    private void AddOwnerLines(RevealedPlotSnapshot plot, List<string> lines)
    {
        if (plot.Owner is int owner) lines.Add("Territory of " + Sim.State.PlayerById(owner).Name);
        if (plot.City is { } city) lines.Add($"City: {city.Name} (size {city.Population})");
    }

    private void AddUnitLines(Hex hex, List<string> lines)
    {
        foreach (var unit in Sim.State.UnitsAt(hex).Where(u => Service<IVisibilityService>().IsUnitVisible(Sim, u.Id, Me.TeamId)).OrderBy(u => u.Id))
            lines.Add($"{Sim.State.PlayerById(unit.Owner).Name}: {DefOf(unit).Name}");
    }

    /// <summary>Units visible to the local team, including detected stealth units.</summary>
    public IEnumerable<Unit> VisibleUnits() =>
        Sim.State.Units.Where(u => Service<IVisibilityService>().IsUnitVisible(Sim, u.Id, Me.TeamId));

    /// <summary>Map-only cities from observations, including remembered cities no longer in live state.</summary>
    public IEnumerable<City> KnownCities()
    {
        foreach (var tile in Sim.State.Grid.Tiles)
        {
            if (PerceivedPlot(tile)?.City is not { } city) continue;
            yield return new City { Id = city.Id, Owner = city.Owner, Name = city.Name,
                Position = Sim.State.Grid.HexOf(tile), Population = city.Population, Buildings = city.Buildings.ToList() };
        }
    }
}
