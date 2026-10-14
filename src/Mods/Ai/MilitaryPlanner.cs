using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Soldier orders in priority order: garrisons, hunting nearby enemies, the war effort, exploring, then resting in cities.</summary>
internal sealed class MilitaryPlanner(AiContext ctx, ArmyPlan army, CityPlanner cities, AttackPlanner attack)
{
    private readonly UnitOrders _orders = new(ctx);
    private readonly ExplorerPlanner _explorer = new(ctx);
    private IReadOnlyList<CityView> Views => cities.Views;

    public void Plan()
    {
        foreach (var unit in Soldiers().ToList()) Promote(unit);
        AssignGarrisons();
        var free = Soldiers().Where(u => !ctx.Done.Contains(u.Id) && u.MovesLeft > 0).ToList();
        free = Hunt(free);
        free = attack.Plan(free, Views);
        free = ExploreWith(free);
        foreach (var unit in free) Rest(unit);
        foreach (var scout in ctx.UnitsWithRole(UnitRole.Explorer).Where(u => !ctx.Done.Contains(u.Id)).ToList()) Scout(scout);
        Upgrades();
    }

    private IEnumerable<Unit> Soldiers() => ctx.Units.Where(u => ctx.Alive(u) && ctx.Role(u) is UnitRole.Melee or UnitRole.Siege);

    private void AssignGarrisons()
    {
        foreach (var view in Views.OrderByDescending(v => v.Threat).ThenBy(v => v.City.Id))
        {
            int want = army.DesiredGarrison(view);
            var here = ctx.Sim.State.UnitsAt(view.City.Position).Where(u => u.Owner == ctx.PlayerId && ctx.Role(u) == UnitRole.Melee).OrderBy(u => u.Id).ToList();
            foreach (var unit in here.Take(want)) { ctx.Done.Add(unit.Id); _orders.Fortify(unit); }
        }
        // Reserve all existing garrisons before choosing reinforcements: never steal a city's last defender.
        foreach (var view in Views.OrderByDescending(v => v.Threat).ThenBy(v => v.City.Id))
        {
            int here = ctx.Sim.State.UnitsAt(view.City.Position).Count(u => u.Owner == ctx.PlayerId && ctx.Role(u) == UnitRole.Melee);
            for (int have = here; have < army.DesiredGarrison(view); have++)
                if (!Reinforce(view)) break;
        }
    }

    private bool Reinforce(CityView view)
    {
        var pick = Soldiers().Where(u => ctx.Role(u) == UnitRole.Melee && !ctx.Done.Contains(u.Id) && u.MovesLeft > 0 && u.Position != view.City.Position)
            .OrderBy(u => ctx.Grid.Distance(u.Position, view.City.Position)).ThenBy(u => u.Id).FirstOrDefault();
        if (pick is null) return false;
        ctx.Done.Add(pick.Id);
        ctx.MoveToward(pick, view.City.Position);
        return true;
    }

    private void Promote(Unit unit)
    {
        var service = ctx.Service<IUnitService>();
        if (unit.Experience < service.ExperienceNeeded(ctx.Sim, unit.Id)) return;
        var best = ctx.Sim.Content.Get<PromotionDef>().All.Where(p => service.Validate(ctx.Sim, ctx.PlayerId, unit.Id, UnitAction.Promote, p.Id) is null)
            .OrderByDescending(Value).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
        if (best is not null) ctx.Issue(new PromoteMilitaryUnitCommand { Player = ctx.PlayerId, Unit = unit.Id, Promotion = best.Id });
    }

    private static int Value(PromotionDef p) => p.CombatPercent * 3 + p.CityDefense + p.CityAttack / 2 + p.HillsDefense / 2 + p.ExperiencePercent / 4;

    /// <summary>Free soldiers attack enemies that stand on or near our land when the odds are good.</summary>
    private List<Unit> Hunt(List<Unit> free)
    {
        var remaining = new List<Unit>();
        foreach (var unit in free)
        {
            if (!ctx.Alive(unit)) continue;
            if (unit.Health < 70) { ctx.Done.Add(unit.Id); _orders.Fortify(unit); continue; }
            if (!HuntOne(unit)) remaining.Add(unit);
        }
        return remaining;
    }

    private bool HuntOne(Unit unit)
    {
        var best = ctx.Enemies.Where(e => ctx.Alive(e) && ctx.Grid.Distance(e.Position, unit.Position) <= 3 && OnOurLand(e.Position))
            .Select(e => (Enemy: e, Odds: _orders.Odds(unit, e.Position))).Where(x => x.Odds is { } o && o >= _orders.ThresholdFor(unit))
            .OrderByDescending(x => x.Odds).ThenBy(x => x.Enemy.Id).FirstOrDefault();
        if (best.Enemy is null) return false;
        ctx.Done.Add(unit.Id);
        if (ctx.Grid.Distance(unit.Position, best.Enemy.Position) > 1) _orders.Approach(unit, best.Enemy.Position);
        if (ctx.Alive(unit) && _orders.Odds(unit, best.Enemy.Position) >= _orders.ThresholdFor(unit)) _orders.Attack(unit, best.Enemy.Position);
        return true;
    }

    private bool OnOurLand(Hex position) => ctx.Sim.State.PlotAt(position).Owner == ctx.PlayerId
        || ctx.Cities.Any(c => ctx.Grid.Distance(c.Position, position) <= 3);

    /// <summary>With no scouts alive, one spare soldier explores early in the game.</summary>
    private List<Unit> ExploreWith(List<Unit> free)
    {
        if (ctx.Turn > 80 || free.Count == 0 || ctx.UnitsWithRole(UnitRole.Explorer).Any()) return free;
        var explorer = free.Where(u => ctx.Role(u) == UnitRole.Melee).OrderBy(u => u.Id).FirstOrDefault();
        if (explorer is null || !_explorer.Explore(explorer)) return free;
        ctx.Done.Add(explorer.Id);
        return free.Where(u => u.Id != explorer.Id).ToList();
    }

    private void Scout(Unit scout)
    {
        ctx.Done.Add(scout.Id);
        if (_explorer.Explore(scout)) return;
        if (ctx.CityAtUnit(scout) is null && ctx.NearestCity(scout.Position) is { } city) ctx.MoveToward(scout, city.Position);
        else _orders.Fortify(scout);
    }

    private void Rest(Unit unit)
    {
        ctx.Done.Add(unit.Id);
        if (ctx.CityAtUnit(unit) is not null) { _orders.Fortify(unit); return; }
        if (ctx.NearestCity(unit.Position) is { } city) ctx.MoveToward(unit, city.Position);
    }

    /// <summary>Upgrade units in cities when the treasury is comfortably above the reserve.</summary>
    private void Upgrades()
    {
        if (ctx.Turn % 3 != ctx.PlayerId % 3) return;
        var service = ctx.Service<IUnitService>();
        long spare = ctx.Self.Treasury - EconomyPlanner.Reserve(ctx.Cities.Count);
        foreach (var unit in Soldiers().Where(u => ctx.CityAtUnit(u) is not null).ToList())
        {
            if (spare <= 0) return;
            foreach (var target in ctx.Def(unit).Upgrades.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                long price = service.UpgradePrice(ctx.Sim, unit.Id, target);
                if (price > spare || !ctx.Issue(new UpgradeUnitCommand { Player = ctx.PlayerId, Unit = unit.Id, Target = target })) continue;
                spare -= price; break;
            }
        }
    }
}
