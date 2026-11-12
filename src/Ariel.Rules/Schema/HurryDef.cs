using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Hurry content. All authoritative numbers are integers.</summary>
public sealed class HurryDef : GameContentDef
{
    public bool Anger { get; init; } = false;
    public int GoldPerProduction { get; init; } = 0;
    public int ProductionPerPopulation { get; init; } = 0;
}
