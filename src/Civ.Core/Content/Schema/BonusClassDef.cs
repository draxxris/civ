using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>BonusClass content. All authoritative numbers are integers.</summary>
public sealed class BonusClassDef : GameContentDef
{
    public int Unique { get; init; } = 0;
}
