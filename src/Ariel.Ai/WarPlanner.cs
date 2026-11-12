using Ariel.Game;

namespace Ariel.Ai;

/// <summary>Declares limited wars against clearly weaker neighbours when the leader's personality allows,
/// and ends wars that go badly.</summary>
internal sealed class WarPlanner(AiContext ctx, ArmyPlan army)
{
    private const int CheckInterval = 5, MinimumTurn = 45;

    public void Plan()
    {
        // A treaty signed this turn also blocks new wars this turn, so one turn never holds both peace and war.
        if (MakePeace()) return;
        if (ctx.Turn < MinimumTurn || (ctx.Turn + ctx.PlayerId) % CheckInterval != 0 || ctx.AtWarWithPlayers) return;
        if (army.Soldiers < 5 + ctx.Cities.Count) return;
        var target = ChooseTarget();
        if (target is { } victim && Roll(victim)) ctx.Issue(new DeclareWarCommand { Player = ctx.PlayerId, Target = victim });
    }

    /// <summary>Higher MaxWarRand means a more reluctant leader. Overwhelming advantage increases the urgency.</summary>
    private bool Roll(int target)
    {
        long theirs = Math.Max(1, AiContext.Power(ctx.Sim, target));
        int advantage = (int)Math.Clamp(AiContext.Power(ctx.Sim, ctx.PlayerId) / theirs, 1, 8);
        int delay = Math.Max(2, (ctx.Leader?.MaxWarRand ?? 100) / (8 * advantage));
        return ctx.Sim.Rng.Next(delay) == 0;
    }

    private int? ChooseTarget()
    {
        long mine = AiContext.Power(ctx.Sim, ctx.PlayerId);
        int need = 150 + 8 * (ctx.Leader?.BasePeaceWeight ?? 3);
        int? best = null; long bestRatio = 0;
        foreach (var other in ctx.Sim.State.Players.Where(p => Candidate(p.Id)))
        {
            if (AttitudeNeed(other.Id) is not { } shift) continue;
            long ratio = mine * 100 / Math.Max(1, AiContext.Power(ctx.Sim, other.Id));
            if (ratio < need + shift || ratio <= bestRatio) continue;
            best = other.Id; bestRatio = ratio;
        }
        return best;
    }

    /// <summary>Extra power advantage (percent) wanted before attacking a player, from how this leader regards it:
    /// a grudge lowers the bar, goodwill raises it, and a friend or an ally is never attacked.</summary>
    public int? AttitudeNeed(int player)
    {
        var relations = ctx.Sim.Rules().Relations;
        if (relations.Allied(ctx.Sim, ctx.Self.TeamId, ctx.Sim.State.PlayerById(player).TeamId)) return null;
        return relations.DispositionToward(ctx.Sim, ctx.PlayerId, player) switch
        {
            Disposition.Furious => -40,
            Disposition.Annoyed => -20,
            Disposition.Pleased => 40,
            Disposition.Friendly => null,
            _ => 0,
        };
    }

    private bool Candidate(int player)
    {
        if (player == ctx.PlayerId || PlayerRules.IsBarbarian(ctx.Sim, player) || ctx.Sim.State.PlayerById(player).TeamId == ctx.Self.TeamId) return false;
        var areas = ctx.Cities.Select(c => ctx.Sim.State.PlotAt(c.Position).AreaId).ToHashSet();
        var theirs = ctx.Sim.State.Cities.Where(c => c.Owner == player && areas.Contains(ctx.Sim.State.PlotAt(c.Position).AreaId)
            && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id)).ToList();
        return theirs.Count > 0 && theirs.Min(c => ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position))) <= 14;
    }

    /// <summary>Asks for peace with enemy teams that are losing or have fought too long. Returns true when a treaty was signed.</summary>
    private bool MakePeace()
    {
        long mine = ctx.Team.Members.Where(m => !PlayerRules.IsBarbarian(ctx.Sim, m)).Sum(m => AiContext.Power(ctx.Sim, m));
        bool signed = false;
        foreach (var teamId in ctx.Team.Wars.ToList())
        {
            var enemies = ctx.Sim.State.TeamById(teamId).Members.Where(m => !PlayerRules.IsBarbarian(ctx.Sim, m)).ToList();
            if (enemies.Count == 0) continue;
            long theirs = enemies.Sum(m => AiContext.Power(ctx.Sim, m));
            bool losing = mine * 100 < theirs * 85;
            bool tired = ctx.Sim.State.Turn - (ctx.Sim.Rules().Relations.Relation(ctx.Sim, ctx.Self.TeamId, teamId)?.WarStarted ?? 0) > 40;
            if ((losing || tired) && ctx.Issue(new MakePeaceCommand { Player = ctx.PlayerId, Target = enemies[0] })) signed = true;
        }
        return signed;
    }
}
