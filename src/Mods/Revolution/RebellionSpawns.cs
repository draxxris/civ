using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Revolution;

internal static class RebellionSpawns
{
    internal static void Trigger(Simulation sim, City city, RevolutionRulesDef rules)
    {
        var candidates = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position)).Distinct().OrderBy(t => t.Value)
            .Where(t => Walkable(sim, city, t)).ToArray();
        var state = RevolutionService.Edit(sim, city);
        // Rebels belong to the hostile faction; without one (no Barbarians mod) a rebellion only releases pressure.
        if (candidates.Length == 0 || sim.Rules().HostileFaction is null)
        {
            int released = Math.Min(state.InstabilityIndex, rules.RebellionRelease);
            state.Suppress(released);
            state.RebellionCooldownUntilTurn = sim.State.Turn + 1 + GamePace.Scale(sim, rules.CooldownTurns);
            sim.Events.Publish(new CityRebellionTriggered(city.Id, city.Owner, -1, [], released));
            return;
        }
        Shuffle(sim, candidates);
        var tiles = candidates.Take(rules.RebelUnits).ToArray();
        int rebel = sim.Rules().HostileFaction!.Ensure(sim);
        var units = new List<int>();
        foreach (var tile in tiles)
        {
            var unit = new Unit { Id = sim.State.NextUnitId++, Owner = rebel,
                Type = rules.RebelUnit, Position = sim.State.Grid.HexOf(tile) };
            sim.State.AddUnit(unit);
            sim.Events.Publish(new UnitCreated(unit.Id, rebel, unit.Type, unit.Position));
            units.Add(unit.Id);
        }
        // Pressure above the threshold is discarded, so a rebellion always leaves the city below it.
        int before = state.InstabilityIndex;
        state.TriggerRebellion(sim.State.Turn, rules.RebellionThreshold, rules.RebellionRelease,
            GamePace.Scale(sim, rules.CooldownTurns), GamePace.Scale(sim, rules.UnrestTurns));
        int releasedUnits = before - state.InstabilityIndex;
        sim.Events.Publish(new CityRebellionTriggered(city.Id, city.Owner, rebel, units.AsReadOnly(), releasedUnits));
        sim.Rules().Visibility?.Recompute(sim);
    }

    /// <summary>Fisher-Yates over the seeded simulation rng: which neighbour rebels appear on is random per game
    /// but identical on replay and across save/load.</summary>
    private static void Shuffle(Simulation sim, TileId[] tiles)
    {
        for (int i = tiles.Length - 1; i > 0; i--)
        {
            int j = sim.Rng.Next(i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }
    }

    /// <summary>Rebels spawn on unowned tiles or the city owner's tiles, never inside a third party's borders.</summary>
    private static bool Walkable(Simulation sim, City city, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Owner is { } owner && owner != city.Owner) return false;
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        if (terrain.IsWater || terrain.Impassable || plot.Kind == PlotKind.Peak) return false;
        if (plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).Impassable) return false;
        var hex = sim.State.Grid.HexOf(tile);
        return sim.State.CityAt(hex) is null && !sim.State.UnitsAt(hex).Any();
    }
}
