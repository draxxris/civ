namespace Ariel.Content.Schema;

/// <summary>A commerce type: where the commerce slider sends a share of every city's commerce. A mod registers its own
/// rows and the economy runs every loaded type the same way, reading what differs between them from these properties.
/// All authoritative numbers are integers.</summary>
public sealed class CommerceDef : GameContentDef, ICommerceType
{
    /// <inheritdoc/>
    public string Key { get; init; } = "";
    /// <inheritdoc/>
    public int Order { get; init; }
    /// <summary>Slider percent a new player starts with. The loaded types' initial percents sum to 100.</summary>
    public int InitialPercent { get; init; } = 0;
    /// <summary>Whether the type receives the hundredths the slider split leaves unallocated, and the percent the
    /// economy takes from other types to cover a deficit. Exactly one loaded type does (gold).</summary>
    public bool TakesRemainder { get; init; }
    /// <summary>Whether a city's output of the type never falls below zero (research, culture); otherwise negative
    /// direct commerce can make it negative.</summary>
    public bool FloorAtZero { get; init; }
    /// <summary>When settlement finds the treasury short, it moves slider percent from the types that have a value here
    /// to the remainder type, lowest value first, one step at a time. Null: never cut.</summary>
    public int? DeficitCut { get; init; }
    /// <summary>Computer player points for one unit of the type, on the scale of the planner's yield points.</summary>
    public int AiPoints { get; init; }
}
