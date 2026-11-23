using Ariel.Content;

namespace Ariel.Mods.Corporations;

/// <summary>The numbers of the Realistic Guilds/Corporations mode (<see cref="CorporationMode"/>) that the source does
/// not define, one row in <c>data/corporation-rules.json</c>. Turn counts are nominal and scaled to the game's pace
/// (<see cref="Ariel.Game.GamePace.Scale"/>). The source's own spread constants stay in the rule constants
/// (<c>CORPORATION_SPREAD_RAND</c>, <c>CORPORATION_SPREAD_DISTANCE_DIVISOR</c>, <c>CORPORATION_RESOURCE_BASE_INFLUENCE</c>,
/// <c>FREE_TRADE_CORPORATION_SPREAD_MOD</c>), and each corporation's spread factor is its
/// <see cref="CorporationDef.NaturalSpreadPercent"/>. Every value is checked when the mod loads.</summary>
public sealed class CorporationRulesDef : ContentDef
{
    /// <summary>Mean turns until an unfounded guild or corporation is founded by one nation that can found it. Each turn
    /// each such nation founds it with probability 1 / (this, scaled to the pace): a geometric wait whose mean is this.</summary>
    public int FoundingMeanTurns { get; init; }
    /// <summary>Turns a nation whose headquarters closed for lack of the resource must wait before that guild or
    /// corporation can be founded by it again; other nations may found it at once.</summary>
    public int FoundingCooldownTurns { get; init; }
    /// <summary>Turns a branch outside the headquarters city lasts after its city loses every connected copy of the
    /// resources it consumes; it closes if the resource has not come back by then.</summary>
    public int BranchDecayTurns { get; init; }
    /// <summary>Turns a headquarters lasts after its nation has no city with a connected copy of the resources (for a
    /// guild that consumes none, too few of its trade's buildings); then the guild or corporation is dissolved.</summary>
    public int HeadquartersDecayTurns { get; init; }
    /// <summary>Percent a branch's spread chance into a city rises for each copy of the consumed resources the city
    /// can use: its nation's supply, as the city's network reaches it.</summary>
    public int SupplyPercentPerCopy { get; init; }
    /// <summary>The most copies <see cref="SupplyPercentPerCopy"/> counts.</summary>
    public int SupplyCopiesCap { get; init; }
    /// <summary>Subtracted from the distance term of the spread chance, as in the source's spread divisor
    /// (max(1, divisor x distance / map's longest distance - this)).</summary>
    public int SpreadDistanceFloor { get; init; }
    /// <summary>A city's influence for a corporation is divided by this for each competing corporation active there.</summary>
    public int CompetitorInfluenceDivisor { get; init; }
    /// <summary>The spread chance is divided by 1 + (corporations already in the city / this).</summary>
    public int CrowdingDivisor { get; init; }

    /// <summary>Why the values cannot run the rules, or nothing.</summary>
    public IEnumerable<string> Problems()
    {
        if (FoundingMeanTurns < 1) yield return $"FoundingMeanTurns {FoundingMeanTurns} must be at least 1";
        if (FoundingCooldownTurns < 0) yield return $"FoundingCooldownTurns {FoundingCooldownTurns} must not be negative";
        if (BranchDecayTurns < 1) yield return $"BranchDecayTurns {BranchDecayTurns} must be at least 1";
        if (HeadquartersDecayTurns < 1) yield return $"HeadquartersDecayTurns {HeadquartersDecayTurns} must be at least 1";
        if (SupplyPercentPerCopy < 0) yield return $"SupplyPercentPerCopy {SupplyPercentPerCopy} must not be negative";
        if (SupplyCopiesCap < 0) yield return $"SupplyCopiesCap {SupplyCopiesCap} must not be negative";
        if (SpreadDistanceFloor < 0) yield return $"SpreadDistanceFloor {SpreadDistanceFloor} must not be negative";
        if (CompetitorInfluenceDivisor < 1) yield return $"CompetitorInfluenceDivisor {CompetitorInfluenceDivisor} must be at least 1";
        if (CrowdingDivisor < 1) yield return $"CrowdingDivisor {CrowdingDivisor} must be at least 1";
    }
}
