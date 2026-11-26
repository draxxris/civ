using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Military;

/// <summary>Units left standing on land that a terrain change (<see cref="TerrainChanged"/>) turned to water. The stranded stack moves as one to a random
/// adjacent plot every one of its units could enter by a normal step; with no such plot, the stack is lost.</summary>
internal static class FloodedUnits
{
    internal static void Evacuate(Simulation sim, MovementService movement, TerrainChanged flood)
    {
        if (Water(sim, flood.PreviousTerrain) || !Water(sim, flood.NewTerrain)) return;
        var from = flood.Plot;
        // Cargo rides with its carrier, which is stranded or not by its own domain.
        var stranded = sim.State.UnitsAt(sim.State.Grid.HexOf(from))
            .Where(u => MilitaryData.Read(sim, u).TransportId is null && !CanStand(sim, u, from))
            .OrderBy(u => u.Id).ToArray();
        if (stranded.Length == 0) return;
        MoveOff(sim, movement, from, stranded);
        MilitaryData.Sight(sim);
    }

    /// <summary>Move a stack off its plot as one, to a random adjacent plot every unit could enter by a normal step;
    /// with no such plot the stack is lost. Shared by flooding and by units put out of a city that changed hands.</summary>
    internal static void MoveOff(Simulation sim, MovementService movement, TileId from, Unit[] stack)
    {
        var queries = stack.Select(u => movement.Query(sim, u.Id)).ToArray();
        var refuges = sim.State.Grid.Neighbors(from).Where(to => Refuge(sim, stack, queries, from, to))
            .OrderBy(t => t.Value).ToArray();
        if (refuges.Length == 0)
            foreach (var unit in stack) MilitaryData.RemoveUnit(sim, unit.Id);
        else
        {
            var target = sim.State.Grid.HexOf(refuges[sim.Rng.Next(refuges.Length)]);
            var builds = sim.Rules().WorkerBuilds;
            foreach (var unit in stack)
            {
                MilitaryData.Edit(sim, unit).BreakActivity();
                builds?.Cancel(sim, unit.Owner, unit.Id);
                MilitaryData.MoveUnit(sim, unit.Id, target);
            }
        }
    }

    private static bool Water(Simulation sim, string terrain) => sim.Content.Get<S.TerrainDef>(terrain).IsWater;

    private static bool CanStand(Simulation sim, Unit unit, TileId tile) =>
        MovementService.TerrainEntry(sim, unit, MilitaryData.Definition(sim, unit), tile, MilitaryData.Effects(sim, unit)) is null;

    /// <summary>A plot the whole stack could step onto without fighting, boarding or taking a city.</summary>
    private static bool Refuge(Simulation sim, Unit[] stack, MovementService.MovementQuery[] queries, TileId from, TileId to)
    {
        if (sim.State.CityAt(sim.State.Grid.HexOf(to)) is { } city && stack.Any(u => MilitaryData.Enemy(sim, u.Owner, city.Owner)))
            return false;
        for (int i = 0; i < stack.Length; i++)
        {
            // CanEnter accepts water with a transport waiting; a refuge must hold the unit on its own.
            if (!CanStand(sim, stack[i], to) || queries[i].CanEnter(from, to, attack: false) is not null) return false;
        }
        return true;
    }
}
