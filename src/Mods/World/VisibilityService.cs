using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

internal sealed class VisibilityService : IVisibilityService, ITurnSystem
{
    public string Id => "world:visibility";
    public int Order => TurnPhase.Visibility;
    public void Run(Simulation sim) => Recompute(sim);
    public void Recompute(Simulation sim)
    {
        foreach (var plot in sim.State.Plots) plot.Visible.Clear();
        var tuning = Geography.Tuning(sim.Content);
        foreach (var unit in sim.State.Units.OrderBy(u => u.Id))
            if (!sim.Content.Get<S.UnitDef>(unit.Type).NoRevealMap) Reveal(sim, unit.Owner, unit.Position,
                Math.Max(0, tuning.UnitSight + unit.Promotions.Sum(p => sim.Content.Get<S.PromotionDef>(p).VisibilityChange)));
        foreach (var city in sim.State.Cities.OrderBy(c => c.Id)) Reveal(sim, city.Owner, city.Position, tuning.CitySight);
    }
    private static void Reveal(Simulation sim, int player, Hex position, int radius)
    {
        int team = sim.State.PlayerById(player).TeamId;
        foreach (var tile in sim.State.Grid.Range(sim.State.Grid.TileAt(position), radius))
        {
            sim.State.Plots[tile.Value].Visible.Add(team);
            sim.State.Plots[tile.Value].Revealed.Add(team);
        }
    }
}
