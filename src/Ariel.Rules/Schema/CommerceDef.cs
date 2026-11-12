using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Commerce content. All authoritative numbers are integers.</summary>
public sealed class CommerceDef : GameContentDef
{
    public bool FlexiblePercent { get; init; } = false;
    public int AiWeightPercent { get; init; } = 0;
    public int InitialHappiness { get; init; } = 0;
    public int InitialPercent { get; init; } = 0;
}
