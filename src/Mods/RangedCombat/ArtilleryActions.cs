using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.RangedCombat;

/// <summary>Bombardment and ranged strikes as a unit's panel offers them: one order of each kind per tile in reach.</summary>
internal sealed class ArtilleryActions : IUnitActionSource
{
    public IEnumerable<UnitActionOffer> Offers(Simulation sim, int player, Unit unit)
    {
        var def = sim.Content.Get<UnitDef>(unit.Type);
        var grid = sim.State.Grid;
        foreach (var tile in grid.Range(grid.TileAt(unit.Position), Math.Max(1, def.BombRange)).OrderBy(t => t.Value))
        {
            var target = grid.HexOf(tile);
            string name = sim.State.CityAt(target)?.Name ?? target.ToString();
            yield return new($"bombard:{tile.Value}", $"Bombard: {name}",
                new BombardCityCommand { Player = player, Unit = unit.Id, Target = target });
            if (def.BombRange <= 0) continue;
            yield return new($"ranged_strike:{tile.Value}", $"Ranged strike: {name}",
                new RangedStrikeCommand { Player = player, Unit = unit.Id, Target = target });
        }
    }
}

/// <summary>Bombardment of an adjacent city's defences, for units with a bombard rate.</summary>
internal sealed class BombardFire : ISupportFire
{
    public GameCommand? Aim(Simulation sim, Unit unit, Hex target) => sim.Content.Get<UnitDef>(unit.Type).BombardRate > 0
        ? new BombardCityCommand { Player = unit.Owner, Unit = unit.Id, Target = target }
        : null;
}

/// <summary>A ranged strike at whatever defends a tile, for units with a firing range.</summary>
internal sealed class RangedFire : ISupportFire
{
    public GameCommand? Aim(Simulation sim, Unit unit, Hex target) => sim.Content.Get<UnitDef>(unit.Type).BombRange > 0
        ? new RangedStrikeCommand { Player = unit.Owner, Unit = unit.Id, Target = target }
        : null;
}
