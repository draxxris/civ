using Ariel.Game;

namespace Ariel.Mods.Tests;

/// <summary>The Ranged Combat mod's orders, as its unit action source offers them. The mod is loaded from its folder,
/// so tests reach its commands the way a client does instead of naming their types.</summary>
internal static class Artillery
{
    /// <summary>The order to bombard the city on a tile.</summary>
    public static GameCommand Bombard(Simulation sim, Unit unit, Hex target) => Order(sim, unit, target, "bombard");

    /// <summary>The order to fire at whatever defends a tile.</summary>
    public static GameCommand Strike(Simulation sim, Unit unit, Hex target) => Order(sim, unit, target, "ranged_strike");

    private static GameCommand Order(Simulation sim, Unit unit, Hex target, string kind)
    {
        string id = $"{kind}:{sim.State.Grid.TileAt(target).Value}";
        return sim.Rules().UnitActions.SelectMany(source => source.Offers(sim, unit.Owner, unit)).First(offer => offer.Id == id).Command;
    }
}
