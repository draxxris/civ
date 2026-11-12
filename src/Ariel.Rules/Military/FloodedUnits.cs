using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Military;

/// <summary>Units left standing on land that warming turned to water. The stranded stack moves as one to a random
/// adjacent plot every one of its units could enter by a normal step; with no such plot, the stack is lost.</summary>
internal static class FloodedUnits
{
    internal static void Evacuate(Simulation sim, MovementService movement, GlobalWarmingOccurred flood)
    {
        if (Water(sim, flood.PreviousTerrain) || !Water(sim, flood.NewTerrain)) return;
        var from = flood.Plot;
        var plot = sim.State.Plots[from.Value];
        // Cargo rides with its carrier, which is stranded or not by its own domain.
        var stranded = sim.State.UnitsAt(sim.State.Grid.HexOf(from))
            .Where(u => MilitaryData.Read(sim, u).TransportId is null && !CanStand(sim, u, plot))
            .OrderBy(u => u.Id).ToArray();
        if (stranded.Length == 0) return;
        var queries = stranded.Select(u => movement.Query(sim, u.Id)).ToArray();
        var refuges = sim.State.Grid.Neighbors(from).Where(to => Refuge(sim, stranded, queries, from, to))
            .OrderBy(t => t.Value).ToArray();
        if (refuges.Length == 0)
            foreach (var unit in stranded) MilitaryData.RemoveUnit(sim, unit.Id);
        else
        {
            var target = sim.State.Grid.HexOf(refuges[sim.Rng.Next(refuges.Length)]);
            var builds = sim.Content.Services.Get<IWorkerBuildService>().SingleOrDefault();
            foreach (var unit in stranded)
            {
                MilitaryData.Edit(sim, unit).BreakActivity();
                builds?.Cancel(sim, unit.Owner, unit.Id);
                MilitaryData.MoveUnit(sim, unit.Id, target);
            }
        }
        MilitaryData.Sight(sim);
    }

    private static bool Water(Simulation sim, string terrain) => sim.Content.Get<S.TerrainDef>(terrain).IsWater;

    private static bool CanStand(Simulation sim, Unit unit, Plot plot) =>
        MovementService.TerrainEntry(sim, unit, MilitaryData.Definition(sim, unit), plot, MilitaryData.Effects(sim, unit)) is null;

    /// <summary>A plot the whole stack could step onto without fighting, boarding or taking a city.</summary>
    private static bool Refuge(Simulation sim, Unit[] stack, MovementService.MovementQuery[] queries, TileId from, TileId to)
    {
        var plot = sim.State.Plots[to.Value];
        if (sim.State.CityAt(sim.State.Grid.HexOf(to)) is { } city && stack.Any(u => MilitaryData.Enemy(sim, u.Owner, city.Owner)))
            return false;
        for (int i = 0; i < stack.Length; i++)
        {
            // CanEnter accepts water with a transport waiting; a refuge must hold the unit on its own.
            if (!CanStand(sim, stack[i], plot) || queries[i].CanEnter(from, to, attack: false) is not null) return false;
        }
        return true;
    }
}
