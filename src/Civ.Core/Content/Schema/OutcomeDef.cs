using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Eligibility and chance modifiers for unit outcomes. Execution belongs to military rules.</summary>
public sealed class OutcomeDef : GameContentDef
{
    public string Message { get; init; } = "";
    [ContentReference(typeof(TechDef))]
    public string? ObsoleteTech { get; init; }
    public bool FriendlyTerritory { get; init; } = true;
    public bool NeutralTerritory { get; init; } = true;
    public bool HostileTerritory { get; init; } = true;
    public bool BarbarianTerritory { get; init; }
    public bool City { get; init; }
    public bool NotCity { get; init; }
    public bool ToCoastalCity { get; init; }
    [ContentReference(typeof(PromotionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> PromotionChanceChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
}

/// <summary>An ordered chance branch, retaining all effect values used by the v1 profile.</summary>
public sealed record OutcomeEffect
{
    [ContentReference(typeof(OutcomeDef))]
    public string Outcome { get; init; } = "";
    public int Chance { get; init; }
    [ContentReference(typeof(UnitDef))]
    public string? Unit { get; init; }
    [ContentReference(typeof(BonusDef))]
    public string? Bonus { get; init; }
    public YieldVector Yields { get; init; }
    public bool DeliverUnitToCity { get; init; }
    /// <summary>Optional source boolean-define key. Missing native defines evaluate to false.</summary>
    public string? DeliveryPolicyKey { get; init; }
}
