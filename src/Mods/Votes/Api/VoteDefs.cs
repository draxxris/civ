using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Votes;

/// <summary>A council that votes: the United Nations, the Apostolic Palace. It sits once a building that founds it
/// (<see cref="BuildingVotesDef"/>) stands anywhere, and meets every <see cref="Interval"/> turns.</summary>
public sealed class VoteSourceDef : GameContentDef
{
    /// <summary>Turns between sessions, scaled by the game speed's train percent.</summary>
    public int Interval { get; init; }
    /// <summary>A civic a player must run to be a full member, or null.</summary>
    [ContentReference(typeof(CivicDef))]
    public string? Civic { get; init; }
}

/// <summary>A resolution or an election a council can hold. Exactly one kind flag is set; a vote with none of them, or
/// with a kind this mod does not run (<see cref="NoNukes"/>, <see cref="AssignCity"/>), is never put to a council.</summary>
public sealed class VoteDef : GameContentDef
{
    /// <summary>The councils that may hold this vote, by <see cref="VoteSourceDef"/> id.</summary>
    [ContentReference(typeof(VoteSourceDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Sources { get; init; } = ImmutableDictionary<string, bool>.Empty;
    /// <summary>Percent of all possible votes needed to pass, rounded down.</summary>
    public int PopulationThreshold { get; init; }
    /// <summary>Voting members the council needs before it may hold this vote.</summary>
    public int MinVoters { get; init; }
    /// <summary>Extra votes, in percent, for a member whose state religion is the council's religion.</summary>
    public int StateReligionVotePercent { get; init; }
    /// <summary>One vote per city instead of one per citizen.</summary>
    public bool CityVoting { get; init; }
    /// <summary>One vote per civilization instead of one per citizen.</summary>
    public bool CivVoting { get; init; }
    /// <summary>Elect the council's secretary, who puts the resolutions.</summary>
    public bool SecretaryGeneral { get; init; }
    /// <summary>Elect the winner of the game.</summary>
    public bool Victory { get; init; }
    /// <summary>Every city may trade with every other without open borders, for good.</summary>
    public bool FreeTrade { get; init; }
    /// <summary>Ban nuclear weapons. Not run: the game has none.</summary>
    public bool NoNukes { get; init; }
    /// <summary>Every pair of voting members opens its borders.</summary>
    public bool OpenBorders { get; init; }
    /// <summary>Every pair of voting members signs a defensive pact.</summary>
    public bool DefensivePact { get; init; }
    /// <summary>Every voting member at war with the named player makes peace with it.</summary>
    public bool ForcePeace { get; init; }
    /// <summary>Sanctions: no voting member trades with the named player's team until it is a full member again.</summary>
    public bool ForceNoTrade { get; init; }
    /// <summary>Every voting member declares war on the named player.</summary>
    public bool ForceWar { get; init; }
    /// <summary>Hand a city to another player. Not run: the game has no way to transfer a city in peace.</summary>
    public bool AssignCity { get; init; }
    /// <summary>Civics every voting member must run while the resolution stands, by <see cref="CivicDef"/> id.</summary>
    [ContentReference(typeof(CivicDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ForceCivics { get; init; } = ImmutableDictionary<string, bool>.Empty;

    /// <summary>Whether the council may hold this vote.</summary>
    public bool HeldBy(string source) => Sources.GetValueOrDefault(source);
    /// <summary>An election chooses a team; every other vote is yes or no.</summary>
    [JsonIgnore]
    public bool IsElection => SecretaryGeneral || Victory;
    /// <summary>A resolution names one player.</summary>
    [JsonIgnore]
    public bool IsTargeted => ForcePeace || ForceNoTrade || ForceWar || AssignCity;
    /// <summary>The civics this resolution forces, in id order.</summary>
    [JsonIgnore]
    public IEnumerable<string> ForcedCivics => ForceCivics.Where(c => c.Value).Select(c => c.Key).Order(StringComparer.Ordinal);
    /// <summary>A passed resolution that keeps acting until the council dissolves.</summary>
    [JsonIgnore]
    public bool IsStanding => FreeTrade || ForceCivics.Any(c => c.Value);
    /// <summary>Whether this mod runs the vote at all.</summary>
    [JsonIgnore]
    public bool IsSupported => !NoNukes && !AssignCity
        && (IsElection || IsStanding || OpenBorders || DefensivePact || ForcePeace || ForceNoTrade || ForceWar);
}

/// <summary>What a building means to councils: it founds the named one. Any mod may attach one to a building.</summary>
public sealed class BuildingVotesDef : ContentAttachment<BuildingDef>
{
    [ContentReference(typeof(VoteSourceDef))]
    public string? VoteSource { get; init; }
}
