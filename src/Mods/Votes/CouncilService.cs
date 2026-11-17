using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Diplomacy;
using static Ariel.Mods.Votes.CouncilRules;

namespace Ariel.Mods.Votes;

/// <summary>The councils: their sessions (CvGame::doVoteSelection, doVoteResults), what a passed vote does
/// (CvGame::processVote) and the base game's questions they answer: whether sanctions forbid trade or a council's free
/// trade allows it (<see cref="ITradeTerms"/>), and whether a forced civic rules a civic out (<see cref="ICivicRestrictions"/>).
/// <para>A session opens at the end of a turn and closes at the end of the next: every member has one turn to vote,
/// and one who has not voted abstains. The secretary is asked for a resolution the same way, and loses the chance when
/// the next turn ends without one.</para></summary>
internal sealed class CouncilService : IVotesService, ITradeTerms, ICivicRestrictions
{
    public IReadOnlyList<string> Councils(Simulation sim) => Sitting(sim).Select(c => c.Source).ToArray();

    public CouncilView? Council(Simulation sim, string source)
    {
        if (CouncilRules.Council(sim, source) is not { } council) return null;
        return new(source, council.Religion, council.Secretary, council.Timer, council.Standing.ToArray(),
            council.Sanctioned.ToArray(), council.Defiant.ToArray(), council.Session, council.AwaitingProposal is not null);
    }

    public int Votes(Simulation sim, int player, string source, string? vote = null) => CouncilRules.Votes(sim, player, source, vote);
    public bool IsVotingMember(Simulation sim, int player, string source) => CouncilRules.IsVotingMember(sim, player, source);
    public bool IsFullMember(Simulation sim, int player, string source) => CouncilRules.IsFullMember(sim, player, source);
    public int Possible(Simulation sim, string source, string vote) => CouncilRules.Possible(sim, source, vote);
    public int Required(Simulation sim, string source, string vote) => CouncilRules.Required(sim, source, vote);
    public IReadOnlyList<int> Candidates(Simulation sim, string source) => CouncilRules.Candidates(sim, source);
    public bool MayDefy(Simulation sim, int player, string source, Resolution resolution) => CouncilRules.MayDefy(sim, player, source, resolution);

    public IReadOnlyList<Resolution> Proposals(Simulation sim, string source)
    {
        if (!Sits(sim, source)) return [];
        var targets = Civilizations(sim).ToArray();
        return sim.Content.Get<VoteDef>().All.Where(v => v.HeldBy(source) && v.IsSupported && !v.SecretaryGeneral)
            .OrderBy(v => v.Id, StringComparer.Ordinal)
            .SelectMany(v => v.IsTargeted ? targets.Select(t => new Resolution(v.Id, t)) : [new Resolution(v.Id)])
            .Where(r => Invalid(sim, source, r) is null).ToArray();
    }

    public string? ValidateProposal(Simulation sim, int player, string source, Resolution resolution)
    {
        if (CouncilRules.Council(sim, source) is not { } council) return "the council does not sit";
        if (council.Secretary != sim.State.PlayerById(player).TeamId) return "only the secretary puts resolutions";
        if (council.AwaitingProposal is null || council.Session is not null) return "the council is not taking resolutions";
        if (sim.Content.TryGet<VoteDef>(resolution.Vote, out var def) && def.SecretaryGeneral) return "the secretary is elected when the term ends";
        return Invalid(sim, source, resolution);
    }

    public void Propose(Simulation sim, int player, string source, Resolution resolution)
    {
        var council = Edit(sim).Councils[source];
        council.AwaitingProposal = null;
        Open(sim, source, council, resolution);
    }

    public string? ValidateBallot(Simulation sim, int player, string source, Ballot ballot)
    {
        if (CouncilRules.Council(sim, source)?.Session is not { } session) return "the council is not voting";
        if (!IsVotingMember(sim, player, source)) return "only voting members vote";
        var def = sim.Content.Get<VoteDef>(session.Resolution.Vote);
        if (def.IsElection)
            return ballot.Choice switch
            {
                VoteChoice.Abstain => null,
                VoteChoice.Yes when Candidates(sim, source).Contains(ballot.Candidate) => null,
                VoteChoice.Yes => "that team does not stand",
                _ => "an election takes a candidate or an abstention",
            };
        if (ballot.Candidate >= 0) return "a resolution takes no candidate";
        if (ballot.Choice == VoteChoice.Defy && !MayDefy(sim, player, source, session.Resolution)) return "this resolution cannot be defied";
        return null;
    }

    public void Cast(Simulation sim, int player, string source, Ballot ballot) =>
        Edit(sim).Councils[source].Session!.Ballots[player] = ballot;

    private static void Open(Simulation sim, string source, CouncilState council, Resolution resolution)
    {
        council.Session = new VoteSession { Resolution = resolution, OpenedTurn = sim.State.Turn };
        sim.Events.Publish(new SessionOpened(source, resolution));
    }

    /// <summary>One turn of every council: found it, keep its state true to the game, close the session the members
    /// have voted in, and open the next one when its time comes.</summary>
    internal void Advance(Simulation sim)
    {
        foreach (var source in sim.Content.Get<VoteSourceDef>().All.Select(s => s.Id).Order(StringComparer.Ordinal))
        {
            if (CouncilRules.Council(sim, source) is null && !Found(sim, source)) continue;
            if (!Sits(sim, source)) continue;
            var council = Edit(sim).Councils[source];
            Tidy(sim, source, council);
            if (council.Session is { } session && session.OpenedTurn < sim.State.Turn) Close(sim, source, council, session);
            if (council.AwaitingProposal is { } asked && asked < sim.State.Turn) council.AwaitingProposal = null;
            if (council.Timer > 0) { council.Timer--; continue; }
            if (council.Session is not null || council.AwaitingProposal is not null) continue;
            Convene(sim, source, council);
        }
    }

    /// <summary>The first seat founds the council. A council a state-religion building founds belongs to its builder's
    /// state religion (CvCity::setNumRealBuildingTimed).</summary>
    private static bool Found(Simulation sim, string source)
    {
        if (Seats(sim, source).FirstOrDefault() is not { } seat) return false;
        string? religion = null;
        var building = sim.Content.Get<BuildingVotesDef>().All.First(b => b.VoteSource == source && seat.Buildings.Contains(b.Target));
        if (sim.Content.Get<BuildingDef>(building.Target).RequiresStateReligion)
            if ((religion = Religions(sim)?.StateReligion(sim, seat.Owner)) is null) return false;
        Edit(sim).Councils[source] = new CouncilState { FoundedTurn = sim.State.Turn, Religion = religion };
        sim.Events.Publish(new CouncilFounded(source, seat.Owner));
        return true;
    }

    /// <summary>CvGame::updateSecretaryGeneral and friends: a secretary who is no longer a full member loses the office,
    /// a sanctioned team that is a full member again is free, the dead are forgotten, and forced civics are enforced on
    /// every voting member, including one that joined since the vote.</summary>
    private static void Tidy(Simulation sim, string source, CouncilState council)
    {
        if (council.Secretary is { } secretary && !TeamIsFull(sim, secretary, source))
            (council.Secretary, council.SecretaryTimer) = (null, 0);
        council.Sanctioned.RemoveWhere(t => !Members(sim, t).Any() || TeamIsFull(sim, t, source));
        council.Defiant.RemoveWhere(p => !Alive(sim, p));
        Enforce(sim, source, council);
    }

    private static void Enforce(Simulation sim, string source, CouncilState council)
    {
        var forced = council.Standing.SelectMany(v => sim.Content.Get<VoteDef>(v).ForcedCivics).ToArray();
        if (forced.Length == 0) return;
        foreach (int player in Civilizations(sim).Where(p => CouncilRules.IsVotingMember(sim, p, source)))
        {
            var civics = sim.State.PlayerById(player).Civics;
            var missing = forced.Where(c => !civics.ContainsValue(c))
                .GroupBy(c => sim.Content.Get<CivicDef>(c).Option, StringComparer.Ordinal).Select(g => g.First()).ToList();
            if (missing.Count > 0) sim.Rules().Government.Impose(sim, player, missing);
        }
    }

    /// <summary>CvGame::doVoteSelection: members meet each other; then either the secretary's term is up and an
    /// election opens, or the secretary is asked for a resolution.</summary>
    private static void Convene(Simulation sim, string source, CouncilState council)
    {
        council.Timer = Interval(sim, source);
        var relations = sim.Rules().Relations;
        var teams = VotingTeams(sim, source).ToArray();
        for (int i = 0; i < teams.Length; i++)
            for (int j = i + 1; j < teams.Length; j++) relations.EstablishContact(sim, teams[i], teams[j]);
        if (council.SecretaryTimer > 0) council.SecretaryTimer--;
        else
        {
            council.SecretaryTimer = SecretaryTerm(sim);
            var election = sim.Content.Get<VoteDef>().All.Where(v => v.HeldBy(source) && v.SecretaryGeneral)
                .OrderBy(v => v.Id, StringComparer.Ordinal).Select(v => new Resolution(v.Id)).FirstOrDefault();
            if (election is not null && Invalid(sim, source, election) is null) { Open(sim, source, council, election); return; }
        }
        if (council.Secretary is not null) council.AwaitingProposal = sim.State.Turn;
    }

    /// <summary>CvGame::doVoteResults. A vote the council can no longer hold is cancelled. An election goes to the
    /// candidate with the most votes if they reach the threshold. A resolution passes on enough yes votes unless a
    /// member defies it; defiers stop being full members, and the members who voted yes on a passed one are full
    /// members again.</summary>
    private static void Close(Simulation sim, string source, CouncilState council, VoteSession session)
    {
        council.Session = null;
        var resolution = session.Resolution;
        var def = sim.Content.Get<VoteDef>(resolution.Vote);
        bool valid = Invalid(sim, source, resolution) is null;
        int required = CouncilRules.Required(sim, source, def.Id);
        int Count(Func<Ballot, bool> pick) => session.Ballots.Where(b => pick(b.Value) && Alive(sim, b.Key))
            .Sum(b => CouncilRules.Votes(sim, b.Key, source, def.Id));
        bool passed = false; int winner = -1;
        if (!valid) { }
        else if (def.IsElection)
        {
            var tally = CouncilRules.Candidates(sim, source).Select(t => (Team: t, Votes: Count(b => b.Choice == VoteChoice.Yes && b.Candidate == t)))
                .OrderByDescending(x => x.Votes).ThenBy(x => x.Team).FirstOrDefault();
            passed = tally.Votes > 0 && tally.Votes >= required;
            if (passed) winner = tally.Team;
            if (def.SecretaryGeneral && passed) council.Secretary = winner;
            else if (def.SecretaryGeneral) (council.Secretary, council.SecretaryTimer) = (null, 0);
            if (def.Victory && passed) Victory(sim)?.AwardVote(sim, BestMember(sim, winner, source, def.Id));
        }
        else
        {
            passed = Count(b => b.Choice == VoteChoice.Yes) >= required;
            var defiers = session.Ballots.Where(b => b.Value.Choice == VoteChoice.Defy).Select(b => b.Key).ToArray();
            if (passed && defiers.Length > 0)
            {
                passed = false;
                council.Defiant.UnionWith(defiers);
            }
            if (passed)
            {
                council.Defiant.ExceptWith(session.Ballots.Where(b => b.Value.Choice == VoteChoice.Yes).Select(b => b.Key));
                Apply(sim, source, council, def, resolution);
            }
        }
        sim.Events.Publish(new ResolutionDecided(source, resolution, passed, winner));
    }

    private static int BestMember(Simulation sim, int team, string source, string vote) => Members(sim, team)
        .OrderByDescending(p => CouncilRules.Votes(sim, p, source, vote)).ThenBy(p => p).First();

    /// <summary>CvGame::processVote for a resolution that passed.</summary>
    private static void Apply(Simulation sim, string source, CouncilState council, VoteDef def, Resolution resolution)
    {
        var relations = sim.Rules().Relations;
        if (def.IsStanding)
        {
            council.Standing.Add(def.Id);
            Enforce(sim, source, council);
        }
        else if (def.OpenBorders || def.DefensivePact)
        {
            var agreement = def.OpenBorders ? DiplomaticAgreement.OpenBorders : DiplomaticAgreement.DefensivePact;
            var diplomacy = CouncilRules.Diplomacy(sim)!;
            foreach (var pair in MissingAgreements(sim, source, agreement).ToList())
            {
                int a = Lead(sim, pair.First), b = Lead(sim, pair.Second);
                if (diplomacy.ValidateAgreement(sim, a, b, agreement) is null) diplomacy.SignAgreement(sim, a, b, agreement);
            }
        }
        else if (def.ForcePeace)
        {
            int targetTeam = sim.State.PlayerById(resolution.Target).TeamId;
            foreach (int team in VotingTeams(sim, source).Where(t => sim.State.TeamById(t).Wars.Contains(targetTeam)).ToList())
                relations.MakePeace(sim, Lead(sim, team), resolution.Target);
        }
        else if (def.ForceNoTrade)
        {
            int targetTeam = sim.State.PlayerById(resolution.Target).TeamId;
            council.Sanctioned.Add(targetTeam);
            // CvPlayer::stopTradingWithTeam: the target remembers every member that now refuses its trade.
            if (sim.Content.TryGet<MemoryKindDef>(DiplomacyMemories.StoppedTrading, out _))
                foreach (int member in Civilizations(sim).Where(p => CouncilRules.IsVotingMember(sim, p, source) && sim.State.PlayerById(p).TeamId != targetTeam))
                    foreach (int victim in Members(sim, targetTeam))
                        relations.Remember(sim, victim, member, DiplomacyMemories.StoppedTrading);
        }
        else if (def.ForceWar)
        {
            int targetTeam = sim.State.PlayerById(resolution.Target).TeamId;
            foreach (int team in VotingTeams(sim, source).Where(t => t != targetTeam && !sim.State.TeamById(t).Wars.Contains(targetTeam)).ToList())
                if (relations.ValidateWar(sim, Lead(sim, team), resolution.Target) is null)
                    relations.DeclareWar(sim, Lead(sim, team), resolution.Target);
        }
    }

    /// <summary>Sanctions: a voting member and a sanctioned team do not trade.</summary>
    public bool Embargoed(Simulation sim, int teamA, int teamB)
    {
        foreach (var (source, council) in Read(sim).Councils)
        {
            if (council.Sanctioned.Count == 0) continue;
            bool aSanctioned = council.Sanctioned.Contains(teamA), bSanctioned = council.Sanctioned.Contains(teamB);
            if (!aSanctioned && !bSanctioned) continue;
            if (!Sits(sim, source)) continue;
            if ((aSanctioned && TeamVotes(sim, teamB, source)) || (bSanctioned && TeamVotes(sim, teamA, source))) return true;
        }
        return false;
    }

    /// <summary>A standing free-trade resolution opens every city to every other's trade (CvGame::isFreeTrade).</summary>
    public bool FreeTrade(Simulation sim, int teamA, int teamB)
    {
        foreach (var (source, council) in Read(sim).Councils)
            if (council.Standing.Any(v => sim.Content.Get<VoteDef>(v).FreeTrade) && Sits(sim, source)) return true;
        return false;
    }

    /// <summary>CvPlayer::canDoCivics: while a resolution forces a civic, a voting member may run no other in its option.</summary>
    public string? Refuse(Simulation sim, int player, CivicDef civic)
    {
        foreach (var (source, council) in Read(sim).Councils)
        {
            if (council.Standing.Count == 0) continue;
            foreach (var forced in council.Standing.SelectMany(v => sim.Content.Get<VoteDef>(v).ForcedCivics))
            {
                var def = sim.Content.Get<CivicDef>(forced);
                if (def.Option != civic.Option || def.Id == civic.Id) continue;
                if (!Sits(sim, source) || !IsVotingMember(sim, player, source)) continue;
                return $"{sim.Content.Get<VoteSourceDef>(source).Name} requires {def.Name}";
            }
        }
        return null;
    }
}

/// <summary>Runs the councils once a turn, after cities have built (a new seat founds its council the turn it is
/// finished) and before the game's end-of-turn checks.</summary>
internal sealed class CouncilSessions(CouncilService service) : ITurnSystem
{
    public string Id => "votes:sessions";
    public int Order => TurnPhase.GameEnd - 50;
    public void Run(Simulation sim) => service.Advance(sim);
}
