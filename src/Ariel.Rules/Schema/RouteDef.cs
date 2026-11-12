using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Route content. All authoritative numbers are integers.</summary>
public sealed class RouteDef : GameContentDef
{
    public YieldVector Yields { get; init; } = default;
    public bool SeaTunnel { get; init; } = false;
    public int AdvancedStartCost { get; init; } = 0;
    public int AdvancedStartCostIncrease { get; init; } = 0;
    public int FlatMovement { get; init; } = 0;
    public int MovementCost { get; init; } = 0;
    public int Value { get; init; } = 0;
}
