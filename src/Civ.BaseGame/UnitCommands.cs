using Civ.Game;

namespace Civ.BaseGame;

public sealed class StartTrainingCommand : GameCommand
{
    public int CityId { get; init; }
    public string Unit { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        var city = sim.State.Cities.FirstOrDefault(c => c.Id == CityId);
        if (city == null || city.Owner != Player) return "not your city";
        if (!sim.Content.Units.TryGet(Unit, out var def)) return "unknown unit";
        if (def.RequiresTech != null && !sim.State.PlayerById(Player).Techs.Contains(def.RequiresTech)) return "tech not known";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var city = sim.State.Cities.First(c => c.Id == CityId);
        if (city.Training != Unit) { city.Training = Unit; city.TrainProgress = 0; }
        city.Building = null; city.BuildProgress = 0;
    }
}

public sealed class MoveUnitCommand : GameCommand
{
    public int UnitId { get; init; }
    public Hex To { get; init; }

    public override string? Validate(Simulation sim)
    {
        var u = sim.State.Units.FirstOrDefault(x => x.Id == UnitId);
        if (u == null || u.Owner != Player) return "not your unit";
        if (u.MovesLeft <= 0) return "no moves left";
        if (u.Position.DistanceTo(To) != 1) return "destination not adjacent";
        if (!sim.State.Terrain.ContainsKey(To)) return "off map";
        var t = sim.TerrainAt(To);
        if (t.IsWater || !t.Passable) return "impassable";
        if (sim.State.Units.Any(x => x.Position == To && x.Owner != Player)) return "enemy unit there (attack instead)";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var u = sim.State.UnitById(UnitId);
        u.MovesLeft = Math.Max(0, u.MovesLeft - sim.TerrainAt(To).MoveCost);
        u.Position = To;
    }
}

public sealed class AttackCommand : GameCommand
{
    public int UnitId { get; init; }
    public Hex Target { get; init; }

    public override string? Validate(Simulation sim)
    {
        var u = sim.State.Units.FirstOrDefault(x => x.Id == UnitId);
        if (u == null || u.Owner != Player) return "not your unit";
        if (sim.Content.Units.Get(u.Type).Strength <= 0) return "unit cannot attack";
        if (u.MovesLeft <= 0) return "no moves left";
        if (u.Position.DistanceTo(Target) != 1) return "target not adjacent";
        if (UnitRules.BestDefender(sim, Target, Player) == null) return "nothing to attack";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var attacker = sim.State.UnitById(UnitId);
        var defender = UnitRules.BestDefender(sim, Target, Player)!;
        attacker.MovesLeft = 0;
        UnitRules.Fight(sim, attacker, defender);
    }
}

public sealed class PromoteUnitCommand : GameCommand
{
    public int UnitId { get; init; }
    public string Promotion { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        var u = sim.State.Units.FirstOrDefault(x => x.Id == UnitId);
        if (u == null || u.Owner != Player) return "not your unit";
        if (!sim.Content.Promotions.TryGet(Promotion, out var promo)) return "unknown promotion";
        return UnitRules.CanPromote(sim, u, promo);
    }

    public override void Apply(Simulation sim)
    {
        var u = sim.State.UnitById(UnitId);
        u.Promotions.Add(Promotion);
        // A mobility-style promotion takes effect immediately for the rest of this turn.
        u.MovesLeft += sim.Content.Promotions.Get(Promotion).MovesBonus;
    }
}
