using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Commerce content. All authoritative numbers are integers.</summary>
public sealed class CommerceDef : GameContentDef
{
    public int InitialPercent { get; init; } = 0;
}
