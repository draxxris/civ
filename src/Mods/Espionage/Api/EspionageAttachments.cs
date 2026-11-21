using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Espionage;

/// <summary>What a unit means to espionage, kept in this mod's own rows (see "Content attachments" in
/// docs/DESIGN.md): whether it runs spy missions, and whether it defends its team's cities against them. A unit
/// without a row is neither: read it as <see cref="None"/>.</summary>
public sealed class UnitEspionageDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitEspionageDef None = new();
    /// <summary>The unit runs spy missions.</summary>
    public bool Spy { get; init; }
    /// <summary>The unit counts as a defender against spy missions in its team's cities.</summary>
    public bool CounterSpy { get; init; }
}
