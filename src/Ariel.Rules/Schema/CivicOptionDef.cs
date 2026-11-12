using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>CivicOption content. All authoritative numbers are integers.</summary>
public sealed class CivicOptionDef : GameContentDef
{


    /// <summary>Initial choice when the civilization does not declare this option.</summary>
    [ContentReference(typeof(CivicDef))]
    public string? DefaultCivic { get; init; }
}
