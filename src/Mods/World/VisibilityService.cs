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
        if (sim.State.Extensions.Get<MilitaryGameState>(sim.Content) is { } military)
            foreach (var sight in military.ReconSights) RevealTeam(sim, sight.Team, sight.Center, sight.Radius);
    }
    public bool IsUnitVisible(Simulation sim, int unitId, int observerTeam)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Health <= 0) return false;
        if (sim.TeamOf(unit.Owner).Id == observerTeam) return true;
        if (!sim.State.PlotAt(unit.Position).Visible.Contains(observerTeam)) return false;
        if (unit.Extensions.Get<MilitaryUnitState>(sim.Content)?.TransportId is not null) return false;
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        if (def.Invisibility is null && !def.Invisible) return true;
        return sim.State.Units.Any(detector => detector.Health > 0 && sim.TeamOf(detector.Owner).Id == observerTeam
            && Detects(sim, detector, unit, def));
    }
    private static bool Detects(Simulation sim, Unit detector, Unit target, S.UnitDef targetDef)
    {
        int distance = sim.State.Grid.Distance(detector.Position, target.Position);
        int sight = Math.Max(1, Geography.Tuning(sim.Content).UnitSight
            + detector.Promotions.Sum(p => sim.Content.Get<S.PromotionDef>(p).VisibilityChange));
        if (distance > sight) return false;
        var def = sim.Content.Get<S.UnitDef>(detector.Type);
        return targetDef.Invisibility is { } kind
            ? def.VisibleInvisibilityTypes.Contains(kind, StringComparer.Ordinal)
            : distance <= 1 && !def.OnlyDefensive;
    }
    private static void Reveal(Simulation sim, int player, Hex position, int radius)
    {
        RevealTeam(sim, sim.State.PlayerById(player).TeamId, position, radius);
    }
    private static void RevealTeam(Simulation sim, int team, Hex position, int radius)
    {
        foreach (var tile in sim.State.Grid.Range(sim.State.Grid.TileAt(position), radius))
        {
            sim.State.Plots[tile.Value].Visible.Add(team);
            sim.State.Plots[tile.Value].Revealed.Add(team);
        }
    }
}
