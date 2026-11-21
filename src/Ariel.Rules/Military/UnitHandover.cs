using Ariel.Game;

namespace Ariel.Rules.Military;

/// <summary>Units that change owner or are put off a plot when the plot's city changes hands without a fight.</summary>
internal sealed partial class UnitService
{
    public void Transfer(Simulation sim, int unitId, int player)
    {
        var unit = sim.State.UnitById(unitId);
        if (unit.Owner == player) return;
        foreach (int cargo in CargoUnits(sim, unitId).ToArray()) Rehome(sim, sim.State.UnitById(cargo), player);
        Rehome(sim, unit, player);
    }

    private static void Rehome(Simulation sim, Unit unit, int player)
    {
        sim.Rules().WorkerBuilds?.Cancel(sim, unit.Owner, unit.Id);
        MilitaryData.Edit(sim, unit).BreakActivity();
        // The owner is fixed for a unit's life, so the unit is replaced under its own id, as an upgrade does.
        var handed = new Unit { Id = unit.Id, Owner = player, Type = unit.Type, Position = unit.Position,
            Experience = unit.Experience, Health = unit.Health, MovesLeft = unit.MovesLeft,
            Promotions = new(unit.Promotions, StringComparer.Ordinal), Extensions = unit.Extensions };
        sim.State.RemoveUnit(unit.Id); sim.State.AddUnit(handed);
    }

    public bool Expel(Simulation sim, Hex plot, int player)
    {
        var stack = sim.State.UnitsAt(plot).Where(u => u.Owner == player && MilitaryData.Read(sim, u).TransportId is null)
            .OrderBy(u => u.Id).ToArray();
        if (stack.Length == 0) return false;
        FloodedUnits.MoveOff(sim, movement, sim.State.Grid.TileAt(plot), stack);
        return true;
    }
}
