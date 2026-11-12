using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Shared v1 definition metadata. Collections are immutable, and values use integer units.</summary>
public abstract class GameContentDef : ContentDef
{
    [ContentReference(typeof(EraDef))]
    public string? Era { get; init; }
    public string Help { get; init; } = "";
    public string Pedia { get; init; } = "";
    public string Strategy { get; init; } = "";
    /// <summary>Button or atlas reference; retained for the later asset conversion pass.</summary>
    public string Icon { get; init; } = "";
    /// <summary>Ordinal source field paths with non-default data that is not represented by this schema.</summary>
    public ImmutableArray<string> Unsupported { get; init; } = [];
}

/// <summary>Negative one means unlimited; zero disables construction for that scope.</summary>
public sealed record ClassLimits
{
    public int World { get; init; } = -1;
    public int Team { get; init; } = -1;
    public int Player { get; init; } = -1;
    public int ExtraPlayer { get; init; }
    public bool NoLimit { get; init; }
    public bool Monument { get; init; }
}

/// <summary>Calendar steps retain source order. Months and turn counts are exact integers.</summary>
public sealed record TurnIncrement(int Months, int Turns);

public sealed record ImprovementBonus
{
    public bool MakesValid { get; init; }
    public bool Connects { get; init; }
    public int DiscoveryRandomRange { get; init; }
    public int DepletionRandomRange { get; init; }
    public YieldVector Yields { get; init; }
}

public sealed record BuildFeature
{
    public Requirement? Requirements { get; init; }
    public int Time { get; init; }
    public int Production { get; init; }
    public bool Remove { get; init; }
    public bool AllowRemovalWithoutProduction { get; init; }
}

public sealed record SpecialistSlotsChange
{
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> Slots { get; init; } = ImmutableDictionary<string, int>.Empty;
}

public sealed record CivicAttitudeEffect(int Change, string Text);

/// <summary>Non-content predicates, with kind-specific count/parameter units. Evaluators fail closed until supported.</summary>
public sealed class ConditionDef : GameContentDef { }
