using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Small shared unit actions: fortify, attack with an odds threshold, retreat.</summary>
internal sealed class UnitOrders(AiContext ctx)
{
    public void Fortify(Unit unit)
    {
        if (ctx.MilitaryState(unit).Activity == UnitActivity.Fortify) return;
        ctx.Issue(new SetUnitActivityCommand { Player = ctx.PlayerId, Unit = unit.Id, Activity = UnitActivity.Fortify });
    }

    /// <summary>Success odds in millionths against the best defender on the tile, or null when nothing defends it.</summary>
    public int? Odds(Unit attacker, Hex target)
    {
        var combat = ctx.Service<ICombatService>();
        if (combat.BestDefender(ctx.Sim, attacker.Id, target) is not { } defender) return null;
        return combat.Preview(ctx.Sim, attacker.Id, defender).SuccessOdds;
    }

    /// <summary>Minimum acceptable attack odds, lowered a little by aggressive leaders.</summary>
    public int Threshold => 600_000 - (ctx.Leader?.BaseAttackOddsChange ?? 0) * 10_000;
    public int ThresholdFor(Unit unit) => ctx.Role(unit) == UnitRole.Siege ? 350_000 : Threshold;

    /// <summary>Move up to the target, never onto it. Actual-position combat odds are checked before a separate attack.</summary>
    public bool Approach(Unit unit, Hex target)
    {
        if (unit.MovesLeft <= 0) return false;
        var path = ctx.Service<IPathfindingService>().FindPath(ctx.Sim, unit.Id, target, attack: true);
        if (path is null || path.Steps.Count <= 1) return false;
        return ctx.Issue(new MoveUnitPathCommand { Player = ctx.PlayerId, Unit = unit.Id, Path = path.Steps.SkipLast(1).ToArray() });
    }

    public bool Attack(Unit attacker, Hex target)
    {
        if (attacker.MovesLeft <= 0 || ctx.Grid.Distance(attacker.Position, target) > 1) return false;
        return ctx.Issue(new MoveUnitPathCommand { Player = ctx.PlayerId, Unit = attacker.Id, Path = [target] });
    }
}
