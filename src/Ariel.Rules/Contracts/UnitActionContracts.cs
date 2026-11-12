namespace Ariel.Game;

/// <summary>One order a feature proposes for a unit: a stable id, the text of its button, and the command that carries
/// it out. The command is a candidate; whoever offers it to a player validates it first.</summary>
public sealed record UnitActionOffer(string Id, string Label, GameCommand Command, string Tooltip = "");

/// <summary>The orders a feature adds to a unit. A client lists every source's offers in registration order without
/// knowing the commands behind them, so a mod that adds an order registers a source instead of changing the client.</summary>
public interface IUnitActionSource
{
    /// <summary>Candidate orders for a unit of the given player, in a stable order. Sources may offer orders that
    /// fail validation now; they must not change state.</summary>
    IEnumerable<UnitActionOffer> Offers(Simulation sim, int player, Unit unit);
}

/// <summary>A way for a unit to hurt a tile without entering it, as an assault uses it before the close fight.
/// Planners ask every registered kind in registration order, so they need not know which kinds exist.</summary>
public interface ISupportFire
{
    /// <summary>The order that fires the unit at the target, or null when the unit has no such weapon. A non-null
    /// order may still fail validation, for a target out of reach or a unit that has already fired.</summary>
    GameCommand? Aim(Simulation sim, Unit unit, Hex target);
}
