using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.RangedCombat;

/// <summary>Fire from a distance: bombarding an adjacent city's defences and striking a tile within a unit's range.
/// The base game keeps what such fire acts on (city defences, collateral damage, the combat record); this mod owns the
/// two orders, their rules, and how the unit panel and assault planners reach them.</summary>
public sealed class RangedCombatMod : IMod
{
    public void Register(IModContext context)
    {
        context.RegisterCommand<BombardCityCommand>("bombard_city");
        context.RegisterCommand<RangedStrikeCommand>("ranged_strike");
        context.RegisterService<IUnitActionSource>("artillery", new ArtilleryActions());
        // Assaults fire in this order: every bombardment, then every ranged strike.
        context.RegisterService<ISupportFire>("bombard", new BombardFire());
        context.RegisterService<ISupportFire>("ranged_strike", new RangedFire());
    }
}

/// <summary>Reduce the defences of an adjacent enemy city.</summary>
public sealed class BombardCityCommand : GameCommand
{
    public int Unit { get; init; }
    public Hex Target { get; init; }
    public override string? Validate(Simulation sim) => RangedRules.ValidateBombard(sim, Player, Unit, Target);
    public override void Apply(Simulation sim) => RangedRules.BombardCity(sim, Unit, Target);
}

/// <summary>Fire at whatever defends a tile in range, without a fight in return.</summary>
public sealed class RangedStrikeCommand : GameCommand
{
    public int Unit { get; init; }
    public Hex Target { get; init; }
    public override string? Validate(Simulation sim) => RangedRules.ValidateRangedStrike(sim, Player, Unit, Target);
    public override void Apply(Simulation sim) => RangedRules.RangedStrike(sim, Unit, Target);
}
