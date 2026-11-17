using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Votes;

/// <summary>The computer player in councils: it votes in every open session it sits in and, as secretary, puts the
/// resolution most likely to pass among those it wants (CvTeamAI::AI_chooseElection, CvPlayerAI::AI_diploVote).</summary>
internal sealed class CouncilPlanner : IAiPlanner
{
    public string Id => "votes:planner";
    /// <summary>After deals are answered, so agreements signed this turn count.</summary>
    public int Order => AiPlannerOrder.Diplomacy + 50;

    public void Plan(AiContext ctx)
    {
        if (ctx.Sim.Service<IVotesService>() is not { } votes) return;
        foreach (var source in votes.Councils(ctx.Sim))
        {
            if (votes.Council(ctx.Sim, source) is { AwaitingProposal: true } asked && asked.Secretary == ctx.Team.Id)
                Propose(ctx, votes, source, asked);
            if (votes.Council(ctx.Sim, source) is not { Session: { } session } council
                || session.Ballots.ContainsKey(ctx.PlayerId) || !votes.IsVotingMember(ctx.Sim, ctx.PlayerId, source)) continue;
            var ballot = CouncilJudgement.Decide(ctx, votes, source, council, session.Resolution, projecting: false);
            ctx.Issue(new CastVoteCommand { Player = ctx.PlayerId, Source = source, Choice = ballot.Choice, Candidate = ballot.Candidate });
        }
    }

    /// <summary>Of the resolutions this leader would vote for, the one the members are expected to back with the most
    /// votes, if that is enough to pass it. Each member's vote is projected from its own point of view.</summary>
    private static void Propose(AiContext ctx, IVotesService votes, string source, CouncilView council)
    {
        var members = ctx.Sim.State.Players.Select(p => p.Id).Where(p => p != ctx.PlayerId && votes.IsVotingMember(ctx.Sim, p, source))
            .Select(p => AiContext.Observe(ctx.Sim, p)).ToArray();
        Resolution? best = null; int bestSupport = -1;
        foreach (var resolution in votes.Proposals(ctx.Sim, source))
        {
            var own = CouncilJudgement.Decide(ctx, votes, source, council, resolution, projecting: true);
            if (!Backs(own, ctx.Team.Id)) continue;
            int support = votes.Votes(ctx.Sim, ctx.PlayerId, source, resolution.Vote);
            foreach (var member in members)
                if (Backs(CouncilJudgement.Decide(member, votes, source, council, resolution, projecting: true), ctx.Team.Id))
                    support += votes.Votes(ctx.Sim, member.PlayerId, source, resolution.Vote);
            if (support < votes.Required(ctx.Sim, source, resolution.Vote) || support <= bestSupport) continue;
            (best, bestSupport) = (resolution, support);
        }
        if (best is not null)
            ctx.Issue(new ProposeResolutionCommand { Player = ctx.PlayerId, Source = source, Vote = best.Vote, Target = best.Target });
    }

    /// <summary>A yes, or in an election a vote for the proposer's team.</summary>
    private static bool Backs(Ballot ballot, int team) => ballot.Choice == VoteChoice.Yes && (ballot.Candidate < 0 || ballot.Candidate == team);
}

/// <summary>How one leader votes on one resolution. A projection (<c>projecting</c>) answers without the RNG and never
/// defies, so a secretary can weigh what others will do without changing the game.</summary>
internal static class CouncilJudgement
{
    /// <summary>A war score a team is far enough ahead with to defy a call for peace (the war planner's winning score).</summary>
    private const int WinningBig = 150;

    internal static Ballot Decide(AiContext ctx, IVotesService votes, string source, CouncilView council, Resolution resolution, bool projecting)
    {
        var def = ctx.Sim.Content.Get<VoteDef>(resolution.Vote);
        if (def.IsElection) return Elect(ctx, votes, source, def);
        if (council.Secretary == ctx.Team.Id) return new(VoteChoice.Yes);
        bool friendly = council.Secretary is { } secretary && Attitude(ctx, secretary) == Disposition.Friendly;
        bool mayDefy = !projecting && votes.MayDefy(ctx.Sim, ctx.PlayerId, source, resolution);
        var choice = def switch
        {
            { IsStanding: true } when def.ForcedCivics.Any() => Civics(ctx, def, friendly, mayDefy),
            { FreeTrade: true } => VoteChoice.Yes,
            { OpenBorders: true } => friendly || Agrees(ctx, votes, source, ctx.Leader?.OpenBordersRefuseAttitudeThreshold) ? VoteChoice.Yes : VoteChoice.No,
            { DefensivePact: true } => friendly || Agrees(ctx, votes, source, ctx.Leader?.DefensivePactRefuseAttitudeThreshold) ? VoteChoice.Yes : VoteChoice.No,
            { ForcePeace: true } => Peace(ctx, resolution.Target, friendly, mayDefy),
            { ForceNoTrade: true } => Sanction(ctx, council, resolution.Target, friendly, mayDefy),
            { ForceWar: true } => War(ctx, resolution.Target, friendly, mayDefy),
            _ => VoteChoice.Abstain,
        };
        return new(choice);
    }

    private static Disposition Attitude(AiContext ctx, int team) =>
        ctx.Sim.Rules().Relations.DispositionToward(ctx.Sim, ctx.PlayerId, CouncilRules.Lead(ctx.Sim, team));

    /// <summary>Its own team when it stands; otherwise the candidate it likes best, if it likes it enough: Pleased for a
    /// secretary, Friendly to hand over the game.</summary>
    private static Ballot Elect(AiContext ctx, IVotesService votes, string source, VoteDef def)
    {
        var candidates = votes.Candidates(ctx.Sim, source);
        if (candidates.Contains(ctx.Team.Id)) return new(VoteChoice.Yes, ctx.Team.Id);
        var need = def.Victory ? Disposition.Friendly : Disposition.Pleased;
        var best = candidates.Select(t => (Team: t, Attitude: Attitude(ctx, t)))
            .OrderByDescending(x => x.Attitude).ThenBy(x => x.Team).FirstOrDefault();
        return candidates.Count > 0 && best.Attitude >= need ? new(VoteChoice.Yes, best.Team) : new(VoteChoice.Abstain);
    }

    /// <summary>Every other voting team is one this leader would sign the agreement with.</summary>
    private static bool Agrees(AiContext ctx, IVotesService votes, string source, string? threshold) =>
        CouncilRules.VotingTeams(ctx.Sim, source).Where(t => t != ctx.Team.Id)
            .All(t => !DispositionThresholds.AtOrBelow(Attitude(ctx, t), threshold));

    /// <summary>Against a forced civic clearly worse than the one it runs, and defiant when it is far worse.</summary>
    private static VoteChoice Civics(AiContext ctx, VoteDef def, bool friendly, bool mayDefy)
    {
        foreach (var id in def.ForcedCivics)
        {
            var forced = ctx.Sim.Content.Get<CivicDef>(id);
            if (ctx.Self.Civics.GetValueOrDefault(forced.Option!) is not { } heldId || heldId == id) continue;
            long held = ctx.CivicValue(ctx.Sim.Content.Get<CivicDef>(heldId)), offered = ctx.CivicValue(forced);
            long margin = Math.Max(10, Math.Abs(offered));
            if (friendly) offered += margin / 5;
            if (held <= offered + margin / 5) continue;
            return mayDefy && held > offered + margin * (40 + ctx.Sim.Rng.Next(80)) / 100 ? VoteChoice.Defy : VoteChoice.No;
        }
        return VoteChoice.Yes;
    }

    /// <summary>For peace when its side is losing; a third party stops a winner it dislikes or rescues a loser it likes.</summary>
    private static VoteChoice Peace(AiContext ctx, int target, bool friendly, bool mayDefy)
    {
        int targetTeam = ctx.Sim.State.PlayerById(target).TeamId;
        if (targetTeam == ctx.Team.Id)
        {
            if (ctx.Team.Wars.Any(ctx.WantsPeace)) return VoteChoice.Yes;
            bool winningBig = ctx.Team.Wars.Any(e => ctx.WarScore(e) >= WinningBig);
            return mayDefy && winningBig && ctx.Sim.Rng.Next(Math.Max(1, ctx.Leader?.BasePeaceWeight ?? 1)) == 0 ? VoteChoice.Defy : VoteChoice.No;
        }
        if (ctx.Team.Wars.Contains(targetTeam)) return ctx.WantsPeace(targetTeam) ? VoteChoice.Yes : VoteChoice.No;
        long theirs = TeamStrength(ctx, targetTeam);
        long enemies = ctx.Sim.State.TeamById(targetTeam).Wars.Select(e => TeamStrength(ctx, e)).DefaultIfEmpty(0).Max();
        var attitude = Attitude(ctx, targetTeam);
        bool yes = theirs > enemies ? attitude <= Disposition.Cautious : attitude >= Disposition.Pleased;
        return yes || friendly ? VoteChoice.Yes : VoteChoice.No;
    }

    /// <summary>Against sanctions on itself, defiantly; for them on a player it is wary of and would stop trading with.</summary>
    private static VoteChoice Sanction(AiContext ctx, CouncilView council, int target, bool friendly, bool mayDefy)
    {
        int targetTeam = ctx.Sim.State.PlayerById(target).TeamId;
        if (targetTeam == ctx.Team.Id) return mayDefy ? VoteChoice.Defy : VoteChoice.No;
        if (friendly) return VoteChoice.Yes;
        var attitude = Attitude(ctx, targetTeam);
        bool refuses = (council.Secretary is { } secretary
                && DispositionThresholds.AtOrBelow(Attitude(ctx, secretary), ctx.Leader?.StopTradingRefuseAttitudeThreshold))
            || DispositionThresholds.Above(attitude, ctx.Leader?.StopTradingThemRefuseAttitudeThreshold);
        return !refuses && attitude <= Disposition.Cautious ? VoteChoice.Yes : VoteChoice.No;
    }

    /// <summary>Against a war on itself, defiantly; for a war it already fights, or on a weaker player it resents.</summary>
    private static VoteChoice War(AiContext ctx, int target, bool friendly, bool mayDefy)
    {
        int targetTeam = ctx.Sim.State.PlayerById(target).TeamId;
        if (targetTeam == ctx.Team.Id) return mayDefy ? VoteChoice.Defy : VoteChoice.No;
        if (ctx.Team.Wars.Contains(targetTeam)) return VoteChoice.Yes;
        var attitude = Attitude(ctx, targetTeam);
        bool yes = (attitude <= Disposition.Annoyed && TeamStrength(ctx, ctx.Team.Id) >= TeamStrength(ctx, targetTeam))
            || (friendly && attitude <= Disposition.Cautious);
        return yes ? VoteChoice.Yes : VoteChoice.No;
    }

    private static long TeamStrength(AiContext ctx, int team) => CouncilRules.Members(ctx.Sim, team).Sum(ctx.Strength);
}
