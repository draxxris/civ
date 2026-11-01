using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Soldier orders in priority order: garrisons, hunting nearby enemies, the war effort, exploring, then resting in cities.</summary>
internal enum PromotionRole { General, Defender, Attacker, Siege, Scout }

internal sealed class MilitaryPlanner(AiContext ctx, ArmyPlan army, CityPlanner cities, AttackPlanner attack)
{
    private const int ParallelPaths = 4;
    private readonly UnitOrders _orders = new(ctx);
    private readonly ExplorerPlanner _explorer = new(ctx);
    private IReadOnlyList<CityView> Views => cities.Views;

    public void Plan()
    {
        foreach (var unit in Promotable().ToList()) Promote(unit);
        AssignGarrisons();

        var navy = new NavalPlanner(ctx, _orders, attack);
        navy.Support();
        var free = Soldiers().Where(u => !ctx.Done.Contains(u.Id) && u.MovesLeft > 0).ToList();
        free = Heal(free);
        free = Escort(free);
        free = Hunt(free);
        free = attack.Plan(free, Views);
        free = Stage(free);
        free = navy.Sealift(free);
        navy.Plan();
        free = ExploreWith(free);
        foreach (var unit in free) Rest(unit);
        foreach (var scout in ctx.UnitsWithRole(UnitRole.Explorer).Where(u => !ctx.Done.Contains(u.Id)).ToList()) Scout(scout);
        Upgrades();
        // Fights this turn can earn another level: spend it now rather than leave it waiting a turn.
        foreach (var unit in Promotable().ToList()) Promote(unit);
    }

    /// <summary>Every unit still in play for this player: alive, not dead on health, and not loaded as cargo.</summary>
    private IEnumerable<Unit> Promotable() => ctx.Units.Where(u => ctx.Alive(u) && u.Health > 0
        && ctx.MilitaryState(u).TransportId is null);

    private IEnumerable<Unit> Soldiers() => Promotable().Where(u => ctx.Role(u) is UnitRole.Melee or UnitRole.Siege);

    private void AssignGarrisons()
    {
        var prioritized = Views.OrderByDescending(v => v.Threat).ThenBy(v => v.City.Id).ToArray();
        foreach (var view in prioritized)
        {
            int want = army.DesiredGarrison(view);
            var here = ctx.Sim.State.UnitsAt(view.City.Position).Where(u => u.Owner == ctx.PlayerId && ctx.Role(u) == UnitRole.Melee && ctx.MilitaryState(u).TransportId is null)
                .OrderByDescending(u => AiOptions.DefenderQuality(ctx.Def(u)) * u.Health).ThenBy(u => u.Id).Take(want).ToArray();
            foreach (var unit in here) { ctx.Done.Add(unit.Id); _orders.Fortify(unit); }
        }
        // Reserve all existing garrisons before choosing reinforcements: never steal a city's last defender.
        foreach (var view in prioritized)
        {
            int here = ctx.Sim.State.UnitsAt(view.City.Position).Count(u => u.Owner == ctx.PlayerId && ctx.Role(u) == UnitRole.Melee && ctx.MilitaryState(u).TransportId is null);
            for (int have = here; have < army.DesiredGarrison(view); have++)
                if (!Reinforce(view)) break;
        }
    }

    private bool Reinforce(CityView view)
    {
        int area = ctx.Sim.State.PlotAt(view.City.Position).AreaId;
        var candidates = Soldiers().Where(u => ctx.Role(u) == UnitRole.Melee && !ctx.Done.Contains(u.Id) && u.MovesLeft > 0 && u.Position != view.City.Position)
            .Where(u => ctx.Sim.State.PlotAt(u.Position).AreaId == area).ToArray();
        var paths = ParallelScoring.Select(ctx.Sim, candidates, ParallelPaths, u => ctx.FindPath(u, view.City.Position));
        var pick = candidates.Select((u, i) => (Unit: u, Path: paths[i])).Where(x => x.Path is not null)
            .OrderByDescending(x => ReinforcementValue(x.Unit, x.Path!, view)).ThenBy(x => x.Unit.Id).FirstOrDefault();
        if (pick.Unit is null) return false;
        ctx.Done.Add(pick.Unit.Id);
        // No mutation separates reachability, cost selection and this command: carry the selected route too.
        ctx.Issue(new MoveUnitPathCommand { Player = ctx.PlayerId, Unit = pick.Unit.Id, Path = pick.Path!.Steps.ToArray() });
        return true;
    }

    private long ReinforcementValue(Unit unit, UnitPath path, CityView target)
    {
        int sourceThreat = Views.FirstOrDefault(v => v.City.Position == unit.Position)?.Threat ?? 0;
        return (target.Threat - sourceThreat) * 1000L - path.Cost;
    }

    /// <summary>Spend any promotion the unit has earned. Every unit is asked, not just soldiers: a scout earns
    /// experience exploring, and leaving it unspent wastes the level it paid for.</summary>
    private void Promote(Unit unit)
    {
        var service = ctx.Service<IUnitService>();
        if (unit.Experience < service.ExperienceNeeded(ctx.Sim, unit.Id)) return;
        _promoting = unit;
        var best = ctx.Sim.Content.Get<PromotionDef>().All.Where(p => service.Validate(ctx.Sim, ctx.PlayerId, unit.Id, UnitAction.Promote, p.Id) is null)
            .OrderByDescending(Value).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
        if (best is not null) ctx.Issue(new PromoteMilitaryUnitCommand { Player = ctx.PlayerId, Unit = unit.Id, Promotion = best.Id });
    }

    private int Value(PromotionDef p) => Value(p, PromotionRoleOf(_promoting!));
    private Unit? _promoting;

    /// <summary>The job a promotion should serve: holding a city, taking one, softening a stack, or scouting.</summary>
    public PromotionRole PromotionRoleOf(Unit unit)
    {
        var def = ctx.Def(unit);
        return ctx.Role(unit) switch
        {
            UnitRole.Siege => PromotionRole.Siege,
            UnitRole.Explorer or UnitRole.NavalExplorer => PromotionRole.Scout,
            UnitRole.Melee when def.OnlyDefensive || (AiOptions.Defends(def) && !AiOptions.Attacks(def))
                || (ctx.CityAtUnit(unit) is not null && ctx.MilitaryState(unit).Activity == UnitActivity.Fortify) => PromotionRole.Defender,
            UnitRole.Melee => PromotionRole.Attacker,
            _ => PromotionRole.General,
        };
    }

    public static int Value(PromotionDef p, PromotionRole role)
    {
        int value = p.CombatPercent * 3 + p.ExperiencePercent / 4 + p.FirstStrikesChange * 15 + p.ChanceFirstStrikesChange * 8;
        return value + role switch
        {
            PromotionRole.Defender => p.CityDefense * 2 + p.HillsDefense + p.FirstStrikesChange * 10,
            PromotionRole.Attacker => p.CityAttack * 2 + p.HillsAttack / 2 + p.WithdrawalChange / 2 + p.MovesChange * 15,
            PromotionRole.Siege => p.CollateralDamageChange * 2 + p.BombardRateChange * 3 + p.CityAttack + p.WithdrawalChange,
            PromotionRole.Scout => p.MovesChange * 40 + p.VisibilityChange * 30 + p.WithdrawalChange / 2,
            _ => p.CityDefense / 2 + p.CityAttack / 2 + p.HillsDefense / 2 + p.MovesChange * 20,
        };
    }

    internal const int WoundedHealth = 70, HavenThreatRadius = 2;

    /// <summary>Wounded soldiers heal where they are only when no enemy is near; otherwise they fall back to the
    /// nearest reachable own city. Either way they leave hunting and campaigns until healed.</summary>
    private List<Unit> Heal(List<Unit> free)
    {
        var remaining = new List<Unit>();
        foreach (var unit in free)
        {
            if (!ctx.Alive(unit)) continue;
            if (unit.Health >= WoundedHealth) { remaining.Add(unit); continue; }
            ctx.Done.Add(unit.Id);
            if (ctx.CityAtUnit(unit) is not null || ctx.ThreatAround(unit.Position, HavenThreatRadius) == 0 || !Retreat(unit))
                _orders.Fortify(unit);
        }
        return remaining;
    }

    private bool Retreat(Unit unit)
    {
        int area = ctx.Sim.State.PlotAt(unit.Position).AreaId;
        foreach (var city in ctx.Cities.Where(c => ctx.Sim.State.PlotAt(c.Position).AreaId == area)
            .OrderBy(c => ctx.Grid.Distance(c.Position, unit.Position)).ThenBy(c => c.Id).Take(3))
            if (ctx.MoveToward(unit, city.Position))
            {
                if (ctx.Alive(unit) && unit.MovesLeft > 0) _orders.Fortify(unit);
                return true;
            }
        return false;
    }

    internal const int EscortReach = 4;

    /// <summary>Each settler that has left the cities gets one soldier on its tile. Settlers move first, so the escort
    /// follows to where the settler stopped and the pair spends the other players' turns together.</summary>
    private List<Unit> Escort(List<Unit> free)
    {
        var pool = free.Where(u => ctx.Role(u) == UnitRole.Melee).ToList();
        foreach (var settler in ctx.UnitsWithRole(UnitRole.Settler).Where(s => ctx.Alive(s) && ctx.CityAtUnit(s) is null).OrderBy(s => s.Id).ToList())
        {
            var guard = pool.Where(u => ctx.Alive(u) && ctx.Grid.Distance(u.Position, settler.Position) <= EscortReach)
                .OrderBy(u => ctx.Grid.Distance(u.Position, settler.Position)).ThenByDescending(u => ctx.Def(u).Strength).ThenBy(u => u.Id)
                .FirstOrDefault(u => u.Position == settler.Position || ctx.FindPath(u, settler.Position) is not null);
            if (guard is null) continue;
            pool.Remove(guard);
            ctx.Done.Add(guard.Id);
            if (guard.Position != settler.Position) ctx.MoveToward(guard, settler.Position);
        }
        return free.Where(u => !ctx.Done.Contains(u.Id)).ToList();
    }

    /// <summary>While a war is being prepared, the field army gathers at the border city nearest the target.</summary>
    private List<Unit> Stage(List<Unit> free)
    {
        if (ctx.AtWarWithPlayers || AiIntent.Read(ctx).War is not { } war) return free;
        if (new WarPlanner(ctx, army).BorderCity(war.Target) is not { } border) return free;
        foreach (var unit in free.Where(ctx.Alive))
        {
            ctx.Done.Add(unit.Id);
            if (ctx.Grid.Distance(unit.Position, border.Position) <= 1) _orders.Fortify(unit);
            else ctx.MoveToward(unit, border.Position);
        }
        return [];
    }

    /// <summary>Free soldiers attack enemies that stand on or near our land when the odds are good.</summary>
    private List<Unit> Hunt(List<Unit> free)
    {
        var remaining = new List<Unit>();
        foreach (var unit in free)
        {
            if (!ctx.Alive(unit)) continue;
            if (!HuntOne(unit)) remaining.Add(unit);
        }
        return remaining;
    }

    private bool HuntOne(Unit unit)
    {
        var best = ctx.Enemies.Where(e => ctx.Alive(e) && ctx.Grid.Distance(e.Position, unit.Position) <= 3 && OnOurLand(e.Position)
            && ctx.Sim.State.CityAt(e.Position) is null)
            .Select(e => (Enemy: e, Odds: _orders.Odds(unit, e.Position))).Where(x => x.Odds is { } o && o >= _orders.ThresholdFor(unit))
            .OrderByDescending(x => x.Odds).ThenBy(x => x.Enemy.Id).FirstOrDefault();
        if (best.Enemy is null) return false;
        ctx.Done.Add(unit.Id);
        if (ctx.Grid.Distance(unit.Position, best.Enemy.Position) > 1) _orders.Approach(unit, best.Enemy.Position);
        attack.Prepare(best.Enemy.Position);
        if (ctx.Alive(unit) && !ctx.MilitaryState(unit).Attacked && _orders.Odds(unit, best.Enemy.Position) >= _orders.ThresholdFor(unit))
            _orders.Attack(unit, best.Enemy.Position);
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

    /// <summary>Upgrade units in cities when the treasury is comfortably above the reserve: threatened cities first,
    /// then the most strength gained per gold. A threatened city does not wait for the three-turn review.</summary>
    private void Upgrades()
    {
        var threat = Views.ToDictionary(v => v.City.Id, v => v.Threat);
        bool urgent = threat.Values.Any(t => t > 0);
        if (!urgent && ctx.Turn % 3 != ctx.PlayerId % 3) return;
        var service = ctx.Service<IUnitService>();
        long spare = ctx.Self.Treasury - EconomyPlanner.Reserve(ctx.Cities.Count);
        if (spare <= 0) return;
        var options = new List<(Unit Unit, string Target, long Price, int Threat, long Value)>();
        foreach (var unit in Soldiers().Where(u => ctx.CityAtUnit(u) is not null).ToList())
            foreach (var target in ctx.Def(unit).Upgrades.Keys)
            {
                long price = service.UpgradePrice(ctx.Sim, unit.Id, target);
                if (UpgradeValue(ctx.Def(unit), ctx.Sim.Content.Get<UnitDef>(target), price) is not { } value) continue;
                options.Add((unit, target, price, threat.GetValueOrDefault(ctx.CityAtUnit(unit)!.Id), value));
            }
        var upgraded = new HashSet<int>();
        foreach (var option in options.OrderByDescending(o => o.Threat).ThenByDescending(o => o.Value).ThenBy(o => o.Unit.Id)
            .ThenBy(o => o.Target, StringComparer.Ordinal))
        {
            if (spare <= 0) return;
            if (upgraded.Contains(option.Unit.Id) || option.Price > spare || !ctx.Alive(option.Unit)) continue;
            if (!ctx.Issue(new UpgradeUnitCommand { Player = ctx.PlayerId, Unit = option.Unit.Id, Target = option.Target })) continue;
            upgraded.Add(option.Unit.Id);
            spare -= option.Price;
        }
    }

    /// <summary>Strength gained per hundred gold, or null when the upgrade adds no strength.</summary>
    public static long? UpgradeValue(UnitDef from, UnitDef to, long price)
    {
        int gain = to.Strength - from.Strength;
        return gain <= 0 ? null : gain * 100_000L / Math.Max(1, price);
    }
}
