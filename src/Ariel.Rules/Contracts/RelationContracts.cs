using System.Text.Json.Serialization;

namespace Ariel.Game;

/// <summary>An unordered pair of teams, the key of a relation.</summary>
public readonly record struct TeamPair : IComparable<TeamPair>
{
    public int First { get; }
    public int Second { get; }

    public TeamPair(int a, int b)
    {
        First = Math.Min(a, b);
        Second = Math.Max(a, b);
    }

    public static TeamPair Of(int a, int b) => new(a, b);

    [JsonIgnore]
    public string Key => $"{First}/{Second}";

    public override string ToString() => Key;

    public int CompareTo(TeamPair other)
    {
        int cmp = First.CompareTo(other.First);
        return cmp != 0 ? cmp : Second.CompareTo(other.Second);
    }

    public static implicit operator string(TeamPair pair) => pair.Key;
}

/// <summary>One player's view of another, the key of what the first remembers about the second.</summary>
public readonly record struct PlayerDirection : IComparable<PlayerDirection>
{
    public int FromPlayer { get; }
    public int TowardPlayer { get; }

    public PlayerDirection(int fromPlayer, int towardPlayer)
    {
        FromPlayer = fromPlayer;
        TowardPlayer = towardPlayer;
    }

    public static PlayerDirection Of(int from, int toward) => new(from, toward);

    [JsonIgnore]
    public string Key => $"{FromPlayer}/{TowardPlayer}";

    public override string ToString() => Key;

    public int CompareTo(PlayerDirection other)
    {
        int cmp = FromPlayer.CompareTo(other.FromPlayer);
        return cmp != 0 ? cmp : TowardPlayer.CompareTo(other.TowardPlayer);
    }

    public static implicit operator string(PlayerDirection dir) => dir.Key;
}

/// <summary>How one player regards another, from worst to best. The base game does not compute it: a mod answers
/// through <see cref="IDispositions"/>, and without one every player is <see cref="Cautious"/>.</summary>
public enum Disposition { Furious, Annoyed, Cautious, Pleased, Friendly }

/// <summary>What two teams share since they met. Exists from first contact on; teams that never met have none.</summary>
public sealed class TeamRelation
{
    public int First { get; set; }
    public int Second { get; set; }
    public int ContactTurn { get; set; }
    /// <summary>The turn the current peace began: first contact, or the last peace treaty.</summary>
    public int PeaceStarted { get; set; }
    /// <summary>The turn of the last peace treaty, which bars a new war for <see cref="RelationRules.PeaceCooldownTurns"/>.</summary>
    public int? TreatyStarted { get; set; }
    /// <summary>The turn the current war began, or null at peace.</summary>
    public int? WarStarted { get; set; }
    public int FirstOffensiveWars { get; set; }
    public int SecondOffensiveWars { get; set; }

    [JsonIgnore]
    public bool AtWar => WarStarted is not null;

    public bool HasBindingPeace(int currentTurn, int bindingTurns = RelationRules.PeaceCooldownTurns) =>
        WarStarted is null && TreatyStarted is { } signed && currentTurn - signed < bindingTurns;

    /// <summary>Wars the team declared on the other one itself, not wars it was called into.</summary>
    public int OffensiveWarsBy(int team) =>
        team == First ? FirstOffensiveWars : team == Second ? SecondOffensiveWars : 0;

    internal void StartWar(int turn, int aggressor, bool offensive)
    {
        WarStarted = turn;
        if (!offensive) return;
        if (aggressor == First) FirstOffensiveWars++;
        else if (aggressor == Second) SecondOffensiveWars++;
    }

    internal void MakePeace(int turn)
    {
        WarStarted = null;
        PeaceStarted = turn;
        TreatyStarted = turn;
    }
}

/// <summary>Saved relations between teams and what players remember of each other. No caches and no RNG.</summary>
public sealed class RelationsState
{
    /// <summary>Relations of teams that have met, by <see cref="TeamPair.Key"/>.</summary>
    public SortedDictionary<string, TeamRelation> Relations { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Counts per memory kind id, by <see cref="PlayerDirection.Key"/> of the remembering player.</summary>
    public SortedDictionary<string, SortedDictionary<string, int>> Memories { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The last turn memories faded. Turn 0 never fades.</summary>
    public int LastFadeTurn { get; set; }

    public TeamRelation? GetRelation(int teamA, int teamB) => Relations.GetValueOrDefault(TeamPair.Of(teamA, teamB).Key);
    public SortedDictionary<string, int>? GetMemories(int fromPlayer, int towardPlayer) =>
        Memories.GetValueOrDefault(PlayerDirection.Of(fromPlayer, towardPlayer).Key);
}

/// <summary>Numbers every war and peace obeys.</summary>
public static class RelationRules
{
    /// <summary>Turns a war must last before peace can be made.</summary>
    public const int MinimumWarTurns = 12;
    /// <summary>Turns a peace treaty bars a new war between the same teams.</summary>
    public const int PeaceCooldownTurns = 10;
}

/// <summary>Memory kinds the base game records. Their decay and default attitude weight are data
/// (<c>Relations/data/memory-kind.json</c>, <c>Religion/data/memory-kind.json</c>).</summary>
public static class BaseMemories
{
    /// <summary>Remembered by every member of the defending team toward every member of the aggressor's.</summary>
    public const string DeclaredWar = "relations:declared_war";
    /// <summary>Remembered by third parties at least <see cref="Disposition.Pleased"/> with a defender.</summary>
    public const string DeclaredWarOnFriend = "relations:declared_war_on_friend";
    /// <summary>Remembered by the player a razed city was captured from.</summary>
    public const string RazedCity = "relations:razed_city";
    /// <summary>Remembered by followers of a religion whose holy city was razed.</summary>
    public const string RazedHolyCity = "relations:razed_holy_city";
    /// <summary>Remembered by followers of a religion an inquisition removed, and by owners of its holy city.</summary>
    public const string Inquisition = "religion:inquisition";
}

/// <summary>Two teams met.</summary>
public sealed record TeamsMet(int FirstTeam, int SecondTeam) : DomainEvent;
/// <summary>A war started. <paramref name="Offensive"/> is false for a team called into a war by an agreement.</summary>
public sealed record WarDeclared(int AggressorTeam, int DefenderTeam, bool Offensive) : DomainEvent;
/// <summary>A war ended in a peace treaty.</summary>
public sealed record PeaceSigned(int FirstTeam, int SecondTeam) : DomainEvent;

/// <summary>Relations between teams and what players remember of each other: the base game's diplomacy. Contact,
/// war and peace, border passage and the memory ledger live here; agreements and attitudes are asked of mods through
/// <see cref="ITeamAgreements"/> and <see cref="IDispositions"/>. Queries are RNG-free and do not mutate state. Team
/// ids are not player ids.</summary>
public interface IRelationsService
{
    bool HasMet(Simulation sim, int teamA, int teamB);
    /// <summary>Teams meet. Barbarians meet no one, and a team always knows itself.</summary>
    void EstablishContact(Simulation sim, int teamA, int teamB);
    /// <summary>Every team meets the teams whose units or borders it currently sees or touches.</summary>
    void DiscoverContacts(Simulation sim);
    /// <summary>The relation of two teams that have met, or null.</summary>
    TeamRelation? Relation(Simulation sim, int teamA, int teamB);

    string? ValidateWar(Simulation sim, int player, int target);
    /// <summary>The player's team declares war on the target's. Teams an agreement obliges (<see cref="ITeamAgreements.CallToArms"/>)
    /// join the defender; the defenders and the aggressor's friends remember it.</summary>
    void DeclareWar(Simulation sim, int player, int target);
    string? ValidatePeace(Simulation sim, int player, int target);
    void MakePeace(Simulation sim, int player, int target);

    /// <summary>Whether the team's units may enter the other team's territory in peace: its own, or by an agreement.</summary>
    bool MayEnter(Simulation sim, int team, int ownerTeam);
    /// <summary>Whether an agreement pledges the two teams to defend each other.</summary>
    bool Allied(Simulation sim, int teamA, int teamB);
    /// <summary>How one player regards another; <see cref="Disposition.Cautious"/> without a mod that judges it.</summary>
    Disposition DispositionToward(Simulation sim, int fromPlayer, int towardPlayer);

    /// <summary>The player remembers one more act of this kind by the other. Teammates and barbarians remember nothing of
    /// each other, and a kind with a <see cref="Ariel.Content.Schema.MemoryKindDef.Limit"/> stops counting there. The kind
    /// must be a registered <see cref="Ariel.Content.Schema.MemoryKindDef"/>.</summary>
    void Remember(Simulation sim, int fromPlayer, int towardPlayer, string kind);
    /// <summary>Remembered counts by memory kind id, in ordinal id order; empty when nothing is remembered.</summary>
    IReadOnlyDictionary<string, int> Memories(Simulation sim, int fromPlayer, int towardPlayer);
}

/// <summary>Agreements between teams, owned by the mod that negotiates them (the Diplomacy mod's open borders and
/// defensive pacts). The base game only asks; without such a mod no agreement exists.</summary>
public interface ITeamAgreements
{
    /// <summary>Whether an agreement lets the team's units into the other team's territory. Only asked at peace.</summary>
    bool OpenBorders(Simulation sim, int team, int other);
    /// <summary>Whether an agreement pledges the two teams to defend each other.</summary>
    bool Allied(Simulation sim, int teamA, int teamB);
    /// <summary>Called once when <paramref name="aggressor"/> declares war on <paramref name="defender"/>, before the war
    /// starts: settles the agreements the declaration breaks and returns, in order, the teams obliged to join the
    /// defender.</summary>
    IReadOnlyList<int> CallToArms(Simulation sim, int aggressor, int defender);
}

/// <summary>How one player regards another, answered by a mod that judges it (the Diplomacy mod's attitude).</summary>
public interface IDispositions
{
    Disposition Toward(Simulation sim, int fromPlayer, int towardPlayer);
}

/// <summary>Declare war on another player's team. The one way a war starts.</summary>
public sealed class DeclareWarCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Relations.ValidateWar(sim, Player, Target);
    public override void Apply(Simulation sim) => sim.Rules().Relations.DeclareWar(sim, Player, Target);
}

/// <summary>End a war that has lasted <see cref="RelationRules.MinimumWarTurns"/>. The treaty bars a new war between
/// the teams for <see cref="RelationRules.PeaceCooldownTurns"/>.</summary>
public sealed class MakePeaceCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Relations.ValidatePeace(sim, Player, Target);
    public override void Apply(Simulation sim) => sim.Rules().Relations.MakePeace(sim, Player, Target);
}
