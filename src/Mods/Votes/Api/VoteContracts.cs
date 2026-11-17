using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Votes;

/// <summary>How a member votes. <see cref="Defy"/> is a no that refuses to obey: it fails a resolution that would pass,
/// and the defier stops being a full member until it next votes yes on a resolution that passes.</summary>
public enum VoteChoice { Abstain, Yes, No, Defy }

/// <summary>One member's vote: a choice on a resolution, or the team it elects (<see cref="Candidate"/>, with
/// <see cref="VoteChoice.Yes"/>) in an election. An abstention elects no one.</summary>
public sealed record Ballot(VoteChoice Choice, int Candidate = -1);

/// <summary>A vote put to a council and, for a targeted one (<see cref="VoteDef.IsTargeted"/>), the player it names.</summary>
public sealed record Resolution(string Vote, int Target = -1);

/// <summary>A vote the council is holding. Members vote until the turn after it opened ends; a member who has not voted
/// by then abstains.</summary>
public sealed class VoteSession
{
    public Resolution Resolution { get; set; } = new("");
    public int OpenedTurn { get; set; }
    /// <summary>Ballots cast, by player id.</summary>
    public SortedDictionary<int, Ballot> Ballots { get; set; } = [];
}

/// <summary>One council's saved state, from the turn it was founded.</summary>
public sealed class CouncilState
{
    public int FoundedTurn { get; set; }
    /// <summary>The religion the council belongs to, bound when a building that requires a state religion founded it:
    /// then only that religion's cities vote. Null for a secular council.</summary>
    public string? Religion { get; set; }
    /// <summary>Turns until the next session.</summary>
    public int Timer { get; set; }
    /// <summary>Sessions until the secretary stands for election again.</summary>
    public int SecretaryTimer { get; set; }
    /// <summary>The elected secretary's team, or null.</summary>
    public int? Secretary { get; set; }
    /// <summary>The turn the secretary was asked to put a resolution, while it has not; null otherwise.</summary>
    public int? AwaitingProposal { get; set; }
    public VoteSession? Session { get; set; }
    /// <summary>Standing resolutions that passed (free trade, forced civics), by <see cref="VoteDef"/> id.</summary>
    public SortedSet<string> Standing { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Players who defied a resolution and have not obeyed one since: voting members, never full members.</summary>
    public SortedSet<int> Defiant { get; set; } = [];
    /// <summary>Teams under the council's sanctions: voting members do not trade with them.</summary>
    public SortedSet<int> Sanctioned { get; set; } = [];
}

/// <summary>Saved councils, by <see cref="VoteSourceDef"/> id. A council is here from its founding on.</summary>
public sealed class VotesGameState
{
    public SortedDictionary<string, CouncilState> Councils { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A council's standing and the vote it is holding, for screens and planners. Ids are player and team ids.</summary>
public sealed record CouncilView(string Source, string? Religion, int? Secretary, int TurnsToSession,
    IReadOnlyList<string> Standing, IReadOnlyList<int> Sanctioned, IReadOnlyList<int> Defiant,
    VoteSession? Session, bool AwaitingProposal);

/// <summary>Councils, their members and their votes. Queries never change state or use the RNG.</summary>
public interface IVotesService
{
    /// <summary>The councils that sit, in id order.</summary>
    IReadOnlyList<string> Councils(Simulation sim);
    CouncilView? Council(Simulation sim, string source);
    /// <summary>The player's votes in the council: on <paramref name="vote"/> when given, otherwise its weight as a
    /// member (citizens, or citizens in cities of the council's religion).</summary>
    int Votes(Simulation sim, int player, string source, string? vote = null);
    /// <summary>A voting member has a vote in the council.</summary>
    bool IsVotingMember(Simulation sim, int player, string source);
    /// <summary>A full member also keeps the council's religion and civic and has not defied it.</summary>
    bool IsFullMember(Simulation sim, int player, string source);
    /// <summary>All votes the council's members hold on the vote.</summary>
    int Possible(Simulation sim, string source, string vote);
    /// <summary>The votes a resolution needs to pass, or a team to win an election.</summary>
    int Required(Simulation sim, string source, string vote);
    /// <summary>The teams that may stand in an election, in id order.</summary>
    IReadOnlyList<int> Candidates(Simulation sim, string source);
    /// <summary>The resolutions the secretary may put now, in vote id then target order: any vote the council holds
    /// but the secretary's own election, which comes when the term ends.</summary>
    IReadOnlyList<Resolution> Proposals(Simulation sim, string source);
    string? ValidateProposal(Simulation sim, int player, string source, Resolution resolution);
    void Propose(Simulation sim, int player, string source, Resolution resolution);
    string? ValidateBallot(Simulation sim, int player, string source, Ballot ballot);
    void Cast(Simulation sim, int player, string source, Ballot ballot);
    /// <summary>Whether the member may defy this resolution (it would be bound by it, and is not the secretary).</summary>
    bool MayDefy(Simulation sim, int player, string source, Resolution resolution);
}

/// <summary>Commands reach the service through the registry, so they only run where the Votes mod is loaded.</summary>
internal static class Councils
{
    internal static IVotesService Service(Simulation sim) => sim.Service<IVotesService>()
        ?? throw new InvalidOperationException("the Votes mod is not loaded");
}

/// <summary>The council's secretary puts a resolution to the members.</summary>
public sealed class ProposeResolutionCommand : GameCommand
{
    public string Source { get; init; } = "";
    public string Vote { get; init; } = "";
    public int Target { get; init; } = -1;
    public override string? Validate(Simulation sim) => Councils.Service(sim).ValidateProposal(sim, Player, Source, new(Vote, Target));
    public override void Apply(Simulation sim) => Councils.Service(sim).Propose(sim, Player, Source, new(Vote, Target));
}

/// <summary>A voting member casts its ballot in the council's open session. A later ballot replaces an earlier one.</summary>
public sealed class CastVoteCommand : GameCommand
{
    public string Source { get; init; } = "";
    public VoteChoice Choice { get; init; }
    public int Candidate { get; init; } = -1;
    public override string? Validate(Simulation sim) => Councils.Service(sim).ValidateBallot(sim, Player, Source, new(Choice, Candidate));
    public override void Apply(Simulation sim) => Councils.Service(sim).Cast(sim, Player, Source, new(Choice, Candidate));
}

/// <summary>A council sat for the first time.</summary>
public sealed record CouncilFounded(string Source, int Player) : DomainEvent;
/// <summary>A council opened a vote.</summary>
public sealed record SessionOpened(string Source, Resolution Resolution) : DomainEvent;
/// <summary>A council's vote ended. <paramref name="Winner"/> is the elected team, or -1.</summary>
public sealed record ResolutionDecided(string Source, Resolution Resolution, bool Passed, int Winner) : DomainEvent;
