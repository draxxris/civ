using Civ.Content;
using Civ.Game;

namespace Civ.BaseGame;

/// <summary>Unit stat, experience and combat rules. All integer math so every peer agrees.</summary>
public static class UnitRules
{
    public const int DamagePerRound = 20;

    public static IEnumerable<PromotionDef> PromotionsOf(Simulation sim, Unit u) =>
        u.Promotions.Select(id => sim.Content.Promotions.Get(id));

    public static int MaxMoves(Simulation sim, Unit u) =>
        sim.Content.Units.Get(u.Type).Moves + PromotionsOf(sim, u).Sum(p => p.MovesBonus);

    /// <summary>Number of experience thresholds reached.</summary>
    public static int Level(Simulation sim, Unit u) =>
        sim.Content.ExperienceLevels.All.Count(l => u.Experience >= l.Threshold);

    public static int UnspentPromotions(Simulation sim, Unit u) => Level(sim, u) - u.Promotions.Count;

    public static string? CanPromote(Simulation sim, Unit u, PromotionDef promo)
    {
        var def = sim.Content.Units.Get(u.Type);
        if (def.Strength <= 0) return "unit cannot be promoted";
        if (UnspentPromotions(sim, u) <= 0) return "no promotion available";
        if (u.Promotions.Contains(promo.Id)) return "already has promotion";
        if (promo.Classes.Length > 0 && !promo.Classes.Any(def.Classes.Contains)) return "unit class cannot take this promotion";
        if (promo.RequiresAny.Length > 0 && !promo.RequiresAny.Any(u.Promotions.Contains)) return "missing prerequisite promotion";
        return null;
    }

    public static IEnumerable<PromotionDef> AvailablePromotions(Simulation sim, Unit u) =>
        sim.Content.Promotions.All.Where(p => CanPromote(sim, u, p) == null);

    /// <summary>Strength x100 after promotions, terrain (defenders) and current health.</summary>
    public static int Strength(Simulation sim, Unit u, bool attacking)
    {
        var def = sim.Content.Units.Get(u.Type);
        int bonus = 0;
        foreach (var p in PromotionsOf(sim, u))
            bonus += p.StrengthBonus + (attacking ? p.AttackBonus : p.DefenseBonus);
        if (!attacking) bonus += sim.TerrainAt(u.Position).DefenseBonus;
        return def.Strength * 100 * (100 + bonus) / 100 * u.Health / 100;
    }

    /// <summary>The test ruleset permits combat between different teams, without diplomacy gating.</summary>
    public static bool IsOpponent(Simulation sim, int player, int other) =>
        sim.State.PlayerById(player).TeamId != sim.State.PlayerById(other).TeamId;

    public static Unit? BestDefender(Simulation sim, Hex hex, int attackerOwner) =>
        sim.State.UnitsAt(hex).Where(u => IsOpponent(sim, attackerOwner, u.Owner))
            .OrderByDescending(u => Strength(sim, u, false)).ThenBy(u => u.Id).FirstOrDefault();

    /// <summary>Fight to the death in 20-damage rounds, odds proportional to strength. Winner earns experience.</summary>
    public static void Fight(Simulation sim, Unit attacker, Unit defender)
    {
        int a = Math.Max(1, Strength(sim, attacker, true));
        int d = Math.Max(1, Strength(sim, defender, false));
        while (attacker.Health > 0 && defender.Health > 0)
        {
            if (sim.Rng.Next(a + d) < a) defender.Health -= DamagePerRound;
            else attacker.Health -= DamagePerRound;
        }
        if (defender.Health <= 0) { sim.State.RemoveUnit(defender.Id); attacker.Experience += 2; }
        else { sim.State.RemoveUnit(attacker.Id); defender.Experience += 1; }
        sim.Events.Publish(new CombatResolved(attacker.Id, defender.Id, defender.Health <= 0 ? attacker.Id : defender.Id));
    }

    public static Unit Spawn(Simulation sim, int owner, string type, Hex position, int experience = 0)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = position, Experience = experience };
        unit.MovesLeft = MaxMoves(sim, unit);
        sim.State.AddUnit(unit);
        sim.Events.Publish(new UnitCreated(unit.Id, owner, type, unit.Position));
        return unit;
    }
}
