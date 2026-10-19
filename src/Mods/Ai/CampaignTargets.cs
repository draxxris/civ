using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Shared geography for production, land campaigns and sealift.</summary>
internal sealed class CampaignTargets(AiContext ctx)
{
    private readonly object _gate = new();
    private int _revision = -1;
    private string _dependencies = "";
    public int Area(Hex hex) => ctx.Sim.State.PlotAt(hex).AreaId;
    public bool Water(TileId tile) => ctx.Sim.Content.Get<TerrainDef>(ctx.Sim.State.Plots[tile.Value].Terrain).IsWater;
    public bool Coastal(City city) => ctx.Grid.Neighbors(ctx.Tile(city.Position)).Any(Water);
    public bool PortAccess(City city) => BeachWater(ctx.Tile(city.Position)).Any();
    public IEnumerable<TileId> BeachWater(TileId land) => Enumerable.Range(0, 6)
        .Where(d => (ctx.Sim.State.Plots[land.Value].CliffEdges & (1 << d)) == 0)
        .Select(d => ctx.Grid.Neighbor(land, d)).OfType<TileId>().Where(Water);
    public IEnumerable<City> Enemies => ctx.EnemyCities;

    public City? SelectPrimaryTargetCity(int area)
    {
        var goal = AiIntent.Read(ctx).Campaigns.GetValueOrDefault(area);
        if (goal is not null && Reusable(goal, Dependencies())) return goal.City is { } id ? ctx.Sim.State.FindCity(id) : null;
        return ChoosePrimary(area);
    }

    // Read-only evaluation also runs during replay validation. Queries never issue commands or modify saved intent.
    internal SortedDictionary<int, CampaignGoal> Evaluate(IEnumerable<int> areas)
    {
        string dependencies = Dependencies();
        var held = AiIntent.Read(ctx).Campaigns;
        var result = new SortedDictionary<int, CampaignGoal>();
        foreach (int area in areas.Distinct().Order())
        {
            var goal = held.GetValueOrDefault(area);
            result[area] = Reusable(goal, dependencies) ? goal! :
                new(ctx.Turn, AiIntent.ReviewTurn(ctx), dependencies, ChoosePrimary(area)?.Id);
        }
        return result;
    }

    private bool Reusable(CampaignGoal? goal, string dependencies) => goal is not null
        && goal.Dependencies == dependencies && AiIntent.Reusable(ctx, goal.EvaluatedTurn, goal.ReviewTurn);

    private City? ChoosePrimary(int area) => Enemies.Where(c => Area(c.Position) == area)
        .OrderBy(c => DistanceFromHome(c)).ThenBy(c => c.Id).FirstOrDefault();

    private string Dependencies()
    {
        lock (_gate)
        {
            if (_revision == ctx.Revision) return _dependencies;
            _dependencies = CanonicalJson.Digest(new
            {
                ctx.Team.Wars, ctx.Team.Techs,
                Cities = ctx.Sim.State.Cities.OrderBy(c => c.Id).Select(c => new
                {
                    c.Id, c.Owner, c.Position, Area = Area(c.Position),
                    Revealed = ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id),
                }),
                Economy = AiIntent.EconomicStatus(ctx),
            });
            _revision = ctx.Revision;
            return _dependencies;
        }
    }
    public City? OverseasTarget(int area) => Enemies.Where(c => Area(c.Position) != area && Beaches(c).Any())
        .OrderBy(c => DistanceFromHome(c)).ThenBy(c => c.Id).FirstOrDefault();
    private int DistanceFromHome(City city) => ctx.NearestCity(city.Position) is { } home
        ? ctx.Grid.Distance(home.Position, city.Position) : 0;
    public City? Port(int area) => ctx.Cities.Where(c => Area(c.Position) == area && PortAccess(c))
        .OrderBy(c => Enemies.Select(e => ctx.Grid.Distance(c.Position, e.Position)).DefaultIfEmpty(0).Min()).ThenBy(c => c.Id).FirstOrDefault();

    public IEnumerable<Hex> Beaches(City target) => ctx.Grid.Range(ctx.Tile(target.Position), 3)
        .Where(t => Land(t) && ctx.Sim.State.Plots[t.Value].AreaId == Area(target.Position) && BeachWater(t).Any())
        .OrderBy(t => ctx.Grid.Distance(ctx.Grid.HexOf(t), target.Position)).ThenBy(t => t.Value).Select(ctx.Grid.HexOf);

    public bool Land(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        var terrain = ctx.Sim.Content.Get<TerrainDef>(plot.Terrain);
        if (terrain.IsWater || terrain.Impassable || plot.Kind == PlotKind.Peak) return false;
        return plot.Feature is not { } feature || !ctx.Sim.Content.Get<FeatureDef>(feature).Impassable;
    }
}
