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
        if (candidates.Length == 0)
        {
            int released = Math.Min(state.InstabilityIndex, rules.RebellionRelease);
            state.Suppress(released);
            state.RebellionCooldownUntilTurn = sim.State.Turn + 1 + GamePace.Scale(sim, rules.CooldownTurns);
            sim.Events.Publish(new CityRebellionTriggered(city.Id, city.Owner, -1, [], released));
            return;
        }
        Shuffle(sim, candidates);
        var tiles = candidates.Take(rules.RebelUnits).ToArray();
        int rebel = EnsureBarbarians(sim, rules);
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
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
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

    private static int EnsureBarbarians(Simulation sim, RevolutionRulesDef rules)
    {
        var existing = sim.State.Players.OrderBy(p => p.Id).FirstOrDefault(p => PlayerRules.IsBarbarian(sim, p.Id));
        if (existing is not null) return existing.Id;
        int id = sim.State.Players.Max(p => p.Id) + 1;
        int team = sim.State.Teams.Max(t => t.Id) + 1;
        var player = new Player { Id = id, TeamId = team, Name = "Rebels", EndedTurn = true,
            Civilization = rules.RebelCivilization, Leader = rules.RebelLeader };
        player.Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        sim.State.Players.Add(player);
        sim.State.Teams.Add(new Team { Id = team, Members = new() { id } });
        sim.State.RebuildIndexes();
        foreach (var other in sim.State.Teams.Where(t => t.Id != team)) sim.State.SetWar(team, other.Id, true);
        return id;
    }
}
