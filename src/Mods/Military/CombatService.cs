using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

internal sealed class CombatService : ICombatService
{
    public int? BestDefender(Simulation sim, int attackerId, Hex target)
    {
        var attacker = sim.State.UnitById(attackerId);
        return sim.State.UnitsAt(target).Where(u => EligibleDefender(sim, attacker, u))
            .Where(u => MilitaryData.Constants(sim).MaxHitPoints - u.Health < MilitaryData.Definition(sim, attacker).CombatLimit)
            .Select(u => (Unit: u, Strength: CombatCalculator.Calculate(sim, attacker, u).DefenseStrength.Current))
            .OrderByDescending(x => x.Strength).ThenBy(x => x.Unit.Id).Select(x => (int?)x.Unit.Id).FirstOrDefault();
    }
    private static bool EligibleDefender(Simulation sim, Unit attacker, Unit unit) => MilitaryData.Enemy(sim, attacker.Owner, unit.Owner)
        && unit.Health > 0 && MilitaryData.Definition(sim, unit).Strength > 0 && MilitaryData.Definition(sim, unit).Domain != "military:domain_air";
    public CombatPreview Preview(Simulation sim, int attacker, int defender) => CombatCalculator.Calculate(sim, sim.State.UnitById(attacker), sim.State.UnitById(defender));
    public IReadOnlyList<CombatReport> History(Simulation sim) => sim.State.Extensions.Get<MilitaryGameState>(sim.Content)?.Combats.AsReadOnly() ?? (IReadOnlyList<CombatReport>)[];
    public CombatReport Resolve(Simulation sim, int attackerId, int defenderId)
    {
        var a = sim.State.UnitById(attackerId); var d = sim.State.UnitById(defenderId);
        ValidateCombat(sim, a, d);
        var preview = Preview(sim, attackerId, defenderId);
        var collateral = Collateral(sim, a, d);
        int netFirst = preview.AttackerFirstStrikes + Chance(sim, preview.AttackerChanceFirstStrikes)
            - preview.DefenderFirstStrikes - Chance(sim, preview.DefenderChanceFirstStrikes);
        int firstA = Math.Max(0, netFirst);
        int firstD = Math.Max(0, -netFirst);
        var rounds = new List<CombatRound>();
        CombatOutcome? outcome = null;
        while (outcome is null)
        {
            bool defenderHit = sim.Rng.Next(preview.DieSides) < preview.DefenderRoundOdds;
            if (defenderHit && firstA == 0) outcome = HitAttacker(sim, a, preview);
            if (!defenderHit && firstD == 0) outcome = HitDefender(sim, d, preview);
            rounds.Add(new(rounds.Count + 1, a.Health, d.Health, firstA > 0 || firstD > 0));
            firstA = Math.Max(0, firstA - 1); firstD = Math.Max(0, firstD - 1);
        }
        Finish(sim, a, d, preview, outcome.Value);
        var report = new CombatReport(sim.State.Turn, a.Id, d.Id, outcome.Value, rounds.AsReadOnly(), collateral);
        HistoryState(sim).Combats.Add(report);
        int winner = outcome == CombatOutcome.DefenderWon ? d.Id : a.Id;
        sim.Events.Publish(new CombatResolved(a.Id, d.Id, winner, outcome == CombatOutcome.DefenderWon ? a.Owner : d.Owner));
        MilitaryData.Sight(sim);
        return report;
    }
    private static void ValidateCombat(Simulation sim, Unit a, Unit d)
    {
        if (a.MovesLeft <= 0 || a.Health <= 0) throw new InvalidOperationException("Attacker cannot act");
        if (!EligibleDefender(sim, a, d)) throw new InvalidOperationException("Invalid defender");
        var movement = sim.Content.Services.Get<IMovementService>().Single();
        var error = movement.CanEnter(sim, a.Id, sim.State.Grid.TileAt(a.Position), sim.State.Grid.TileAt(d.Position), true);
        if (error is not null) throw new InvalidOperationException(error);
        if (MilitaryData.Constants(sim).MaxHitPoints - d.Health >= MilitaryData.Definition(sim, a).CombatLimit)
            throw new InvalidOperationException("Defender is already at combat limit");
    }
    private static int Chance(Simulation sim, int chance) => chance == 0 ? 0 : sim.Rng.Next(chance + 1);
    private static CombatOutcome? HitAttacker(Simulation sim, Unit a, CombatPreview preview)
    {
        if (a.Health <= preview.DamageToAttacker && sim.Rng.Next(100) < preview.WithdrawalPercent) return CombatOutcome.Withdrawn;
        a.Health = Math.Max(0, a.Health - preview.DamageToAttacker);
        return a.Health == 0 ? CombatOutcome.DefenderWon : null;
    }
    private static CombatOutcome? HitDefender(Simulation sim, Unit d, CombatPreview preview)
    {
        int maximum = MilitaryData.Constants(sim).MaxHitPoints;
        if (Math.Min(maximum, maximum - d.Health + preview.DamageToDefender) > preview.CombatLimit)
        {
            d.Health = maximum - preview.CombatLimit;
            return CombatOutcome.LimitedDamage;
        }
        d.Health = Math.Max(0, d.Health - preview.DamageToDefender);
        return d.Health == 0 ? CombatOutcome.AttackerWon : null;
    }
    private static void Finish(Simulation sim, Unit a, Unit d, CombatPreview preview, CombatOutcome outcome)
    {
        var state = MilitaryData.Edit(sim, a);
        state.Attacked = true; state.Moved = true; state.FortifyTurns = 0; state.Activity = UnitActivity.Awake;
        int cost = sim.Content.Services.Get<IMovementService>().Single().StepCost(sim, a.Id, sim.State.Grid.TileAt(a.Position), sim.State.Grid.TileAt(d.Position));
        a.MovesLeft = Math.Max(0, a.MovesLeft - Math.Max(cost, MilitaryData.Constants(sim).MoveDenominator));
        if (outcome == CombatOutcome.Withdrawn || outcome == CombatOutcome.LimitedDamage)
        {
            Experience(sim, a, d, d.Position, MilitaryData.Raw(sim, "EXPERIENCE_FROM_WITHDRAWL", 3));
            return;
        }
        AwardVictory(sim, a, d, d.Position, preview, outcome);
    }
    private static void AwardVictory(Simulation sim, Unit a, Unit d, Hex combatPlot, CombatPreview preview, CombatOutcome outcome)
    {
        bool attackWon = outcome == CombatOutcome.AttackerWon;
        var winner = attackWon ? a : d; var loser = attackWon ? d : a;
        var constants = MilitaryData.Constants(sim);
        int baseExperience = attackWon ? MilitaryData.Definition(sim, d).AttackExperience : MilitaryData.Definition(sim, a).DefenseExperience;
        int loserStrength = attackWon ? preview.DefenseStrength.Current : preview.AttackStrength.Current;
        int winnerStrength = attackWon ? preview.AttackStrength.Current : preview.DefenseStrength.Current;
        int experience = Math.Clamp(baseExperience * loserStrength / Math.Max(1, winnerStrength), constants.MinExperiencePerCombat, constants.MaxExperiencePerCombat);
        Experience(sim, winner, loser, combatPlot, experience);
        if (MilitaryData.Player(sim, loser.Owner).Barbarian) MilitaryData.EditPlayer(sim, winner.Owner).WinsAgainstBarbarians++;
        sim.State.RemoveUnit(loser.Id);
        if (MilitaryData.Definition(sim, a).Suicide && sim.State.FindUnit(a.Id) is not null) sim.State.RemoveUnit(a.Id);
    }
    private static void Experience(Simulation sim, Unit winner, Unit opponent, Hex combatPlot, int amount)
    {
        int cap = int.MaxValue;
        if (MilitaryData.Player(sim, opponent.Owner).Barbarian) cap = MilitaryData.Raw(sim, "BARBARIAN_MAX_XP_VALUE", 40);
        if (MilitaryData.Definition(sim, opponent).Animal) cap = MilitaryData.Raw(sim, "ANIMAL_MAX_XP_VALUE", 20);
        var state = MilitaryData.Edit(sim, winner);
        int change = amount * 100;
        var effects = MilitaryData.Effects(sim, winner);
        if (sim.State.PlotAt(combatPlot).Owner == winner.Owner)
            change += change * MilitaryData.Effect(effects, "experience_in_borders_percent") / 100;
        int percent = MilitaryData.Effect(effects, "experience_percent");
        change = change * Math.Max(0, 100 + percent) / 100;
        long previous = winner.Experience * 100L + state.ExperienceRemainder;
        long next = previous < cap * 100L ? Math.Min(cap * 100L, previous + change) : previous;
        winner.Experience = (int)(next / 100); state.ExperienceRemainder = (int)(next % 100);
        if (!MilitaryData.Player(sim, opponent.Owner).Barbarian) AwardGeneralPoints(sim, winner, opponent, combatPlot, amount);
    }
    private static void AwardGeneralPoints(Simulation sim, Unit winner, Unit opponent, Hex combatPlot, int amount)
    {
        var ledger = Civ.Content.ModifierSink.Collect(new(sim, Civ.Content.ModifierScope.Player, winner.Owner)).Ledger;
        long percent = 100 + ledger.Total("military:general_rate_percent");
        if (sim.State.PlotAt(combatPlot).Owner == winner.Owner) percent += ledger.Total("military:domestic_general_rate_percent");
        MilitaryData.EditPlayer(sim, winner.Owner).GeneralPoints += (int)(amount * Math.Max(0, percent) / 100);
    }
    private static IReadOnlyList<CollateralHit> Collateral(Simulation sim, Unit a, Unit primary)
    {
        var def = MilitaryData.Definition(sim, a);
        int extra = MilitaryData.Effect(MilitaryData.Effects(sim, a), "collateral_percent");
        int strength = def.Strength * def.CollateralDamage / 100;
        if (strength == 0 && extra == 0) return [];
        var targets = sim.State.UnitsAt(primary.Position).Where(u => u.Id != primary.Id && EligibleDefender(sim, a, u))
            .Select(u => (Unit: u, Score: u.Health * (1 + sim.Rng.Next(10000))))
            .OrderByDescending(t => t.Score).ThenBy(t => t.Unit.Id).Take(def.CollateralDamageMaxUnits).ToArray();
        var hits = new List<CollateralHit>();
        foreach (var target in targets)
        {
            int damage = CollateralDamage(sim, a, target.Unit, strength, extra);
            target.Unit.Health -= damage;
            if (damage > 0) hits.Add(new(target.Unit.Id, damage));
        }
        return hits.AsReadOnly();
    }
    private static int CollateralDamage(Simulation sim, Unit a, Unit target, int strength, int extra)
    {
        var ad = MilitaryData.Definition(sim, a); var td = MilitaryData.Definition(sim, target);
        if (td.CollateralImmuneCombatTypes.GetValueOrDefault(ad.CombatType ?? "") != 0) return 0;
        int factor = (strength + td.Strength + 1) / 2;
        int damage = MilitaryData.Constants(sim).CollateralCombatDamage * (strength + factor) / Math.Max(1, td.Strength + factor);
        damage *= 100 + extra;
        damage = damage * Math.Max(0, 100 - MilitaryData.Effect(MilitaryData.Effects(sim, target), "collateral_protection")) / 100;
        damage = Math.Max(0, damage / 100);
        int cap = Math.Min(ad.CollateralDamageLimit, ad.CollateralDamageLimit * (strength + factor) / Math.Max(1, td.Strength + factor));
        int current = MilitaryData.Constants(sim).MaxHitPoints - target.Health;
        return Math.Max(0, Math.Min(damage, cap - current));
    }
    internal static MilitaryGameState HistoryState(Simulation sim)
    {
        var state = sim.State.Extensions.Get<MilitaryGameState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }
}
