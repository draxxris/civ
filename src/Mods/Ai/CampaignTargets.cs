using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Shared geography for production, land campaigns and sealift.</summary>
internal sealed class CampaignTargets(AiContext ctx)
{
    public int Area(Hex hex) => ctx.Sim.State.PlotAt(hex).AreaId;
    public bool Water(TileId tile) => ctx.Sim.Content.Get<TerrainDef>(ctx.Sim.State.Plots[tile.Value].Terrain).IsWater;
    public bool Coastal(City city) => ctx.Grid.Neighbors(ctx.Tile(city.Position)).Any(Water);
    public IEnumerable<City> Enemies => ctx.EnemyCities;

    public City? SelectPrimaryTargetCity(int area) => Enemies.Where(c => Area(c.Position) == area)
        .OrderBy(c => DistanceFromHome(c)).ThenBy(c => c.Id).FirstOrDefault();
    public City? OverseasTarget(int area) => Enemies.Where(c => Area(c.Position) != area && Beaches(c).Any())
        .OrderBy(c => DistanceFromHome(c)).ThenBy(c => c.Id).FirstOrDefault();
    private int DistanceFromHome(City city) => ctx.NearestCity(city.Position) is { } home
        ? ctx.Grid.Distance(home.Position, city.Position) : 0;
    public City? Port(int area) => ctx.Cities.Where(c => Area(c.Position) == area && Coastal(c))
        .OrderBy(c => Enemies.Select(e => ctx.Grid.Distance(c.Position, e.Position)).DefaultIfEmpty(0).Min()).ThenBy(c => c.Id).FirstOrDefault();

    public IEnumerable<Hex> Beaches(City target) => ctx.Grid.Range(ctx.Tile(target.Position), 3)
        .Where(t => Land(t) && ctx.Sim.State.Plots[t.Value].AreaId == Area(target.Position) && ctx.Grid.Neighbors(t).Any(Water))
        .OrderBy(t => ctx.Grid.Distance(ctx.Grid.HexOf(t), target.Position)).ThenBy(t => t.Value).Select(ctx.Grid.HexOf);

    public bool Land(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        var terrain = ctx.Sim.Content.Get<TerrainDef>(plot.Terrain);
        if (terrain.IsWater || terrain.Impassable || plot.Kind == PlotKind.Peak) return false;
        return plot.Feature is not { } feature || !ctx.Sim.Content.Get<FeatureDef>(feature).Impassable;
    }
}
