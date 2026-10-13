using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Process content. All authoritative numbers are integers.</summary>
public sealed class ProcessDef : GameContentDef
{
    public CommerceVector ProductionToCommerceModifiers { get; init; } = default;
}
