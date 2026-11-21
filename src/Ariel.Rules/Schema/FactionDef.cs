using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Makes a civilization a faction: a player the game runs that is not a nation. The row is attached to the
/// civilization the faction plays as, so a player is a faction player exactly when its civilization has one
/// (<c>ContentDb.Attachment&lt;FactionDef&gt;(civilization)</c>), and no saved state says who the faction is. Each
/// property is one restriction the base rules apply; the feature that owns the faction decides how its units appear
/// and move. Barbarians are one row; any mod may add another (an invasion that arrives late, say) the same way.</summary>
public sealed class FactionDef : ContentAttachment<CivilizationDef>
{
    /// <summary>The leader the faction's player is created with.</summary>
    [ContentReference(typeof(LeaderDef))]
    public string Leader { get; init; } = "";
    /// <summary>Whether the faction meets other teams, trades, signs agreements and is remembered by leaders. A faction
    /// that does not negotiate is never met and never appears as a diplomatic partner.</summary>
    public bool Negotiates { get; init; }
    /// <summary>Whether the faction is at war with every other team for the whole game: war is declared when its player
    /// or any later team appears, and neither side can make peace.</summary>
    public bool AlwaysAtWar { get; init; } = true;
    /// <summary>What research costs the faction, in percent of the normal cost.</summary>
    public int ResearchCostPercent { get; init; } = 100;
    /// <summary>Research cost percentages that replace <see cref="ResearchCostPercent"/> while a game option is on. When
    /// several listed options are on, the highest applies.</summary>
    [ContentReference(typeof(GameOptionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ResearchCostPercentWithOption { get; init; } = ImmutableDictionary<string, int>.Empty;
    /// <summary>Whether the difficulty level's rules against untamed enemies apply to fights with the faction: the
    /// handicap's combat bonus and free early wins, the experience cap, and no general points.</summary>
    public bool HandicapFoe { get; init; }
    /// <summary>Whether uprisings join this faction: hostile villagers from huts and rebels from restless cities. When
    /// several loaded factions say so, the first by id fields them.</summary>
    public bool Uprisings { get; init; }
    /// <summary>How many turns (at normal speed) the faction holds a city before the city breaks away as a new nation
    /// (<c>ICityCaptureService.Secede</c>). Zero: its cities never break away.</summary>
    public int BreakawayTurns { get; init; }
}
