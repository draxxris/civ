using Civ.Game;

namespace Civ.Mods.Ai;

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
            long ratio = mine * 100 / Math.Max(1, AiContext.Power(ctx.Sim, other.Id));
            if (ratio < need || ratio <= bestRatio) continue;
            best = other.Id; bestRatio = ratio;
        }
        return best;
    }

    private bool Candidate(int player)
    {
        if (player == ctx.PlayerId || WarRules.IsBarbarian(ctx.Sim, player) || ctx.Sim.State.PlayerById(player).TeamId == ctx.Self.TeamId) return false;
        var areas = ctx.Cities.Select(c => ctx.Sim.State.PlotAt(c.Position).AreaId).ToHashSet();
        var theirs = ctx.Sim.State.Cities.Where(c => c.Owner == player && areas.Contains(ctx.Sim.State.PlotAt(c.Position).AreaId)
            && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id)).ToList();
        return theirs.Count > 0 && theirs.Min(c => ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position))) <= 14;
    }

    /// <summary>Asks for peace with enemy teams that are losing or have fought too long. Returns true when a treaty was signed.</summary>
    private bool MakePeace()
    {
        long mine = AiContext.Power(ctx.Sim, ctx.PlayerId);
        bool signed = false;
        foreach (var teamId in ctx.Team.Wars.ToList())
        {
            var enemies = ctx.Sim.State.TeamById(teamId).Members.Where(m => !WarRules.IsBarbarian(ctx.Sim, m)).ToList();
            if (enemies.Count == 0) continue;
            long theirs = enemies.Sum(m => AiContext.Power(ctx.Sim, m));
            bool losing = mine * 100 < theirs * 85;
            bool tired = ctx.Sim.State.Turn - WarRules.Read(ctx.Sim, ctx.Self.TeamId).WarStarted.GetValueOrDefault(teamId) > 40;
            if ((losing || tired) && ctx.Issue(new MakePeaceCommand { Player = ctx.PlayerId, Target = enemies[0] })) signed = true;
        }
        return signed;
    }
}
