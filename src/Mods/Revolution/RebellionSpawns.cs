using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Revolution;

internal static class RebellionSpawns
{
    internal static void Trigger(Simulation sim, City city, RevolutionRulesDef rules)
    {
        var tiles = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position)).Distinct().OrderBy(t => t.Value)
            .Where(t => Walkable(sim, city, t)).Take(rules.RebelUnits).ToArray();
        // An enclosed city still suffers unrest and releases pressure; no illegal units are placed.
        int rebel = tiles.Length == 0 ? -1 : EnsureBarbarians(sim);
        var units = new List<int>();
        foreach (var tile in tiles)
        {
            var unit = new Unit { Id = sim.State.NextUnitId++, Owner = rebel,
                Type = rules.RebelUnit, Position = sim.State.Grid.HexOf(tile) };
            sim.State.AddUnit(unit);
            sim.Events.Publish(new UnitCreated(unit.Id, rebel, unit.Type, unit.Position));
            units.Add(unit.Id);
        }
        var state = RevolutionService.Edit(sim, city);
        // Pressure above the threshold is discarded, so a rebellion always leaves the city below it.
        int before = state.InstabilityIndex;
        state.InstabilityIndex = Math.Max(0, Math.Min(before, rules.RebellionThreshold) - rules.RebellionRelease);
        int released = before - state.InstabilityIndex;
        // The +1 makes the cooldown cover the next turn, as UnrestUntilTurn does.
        state.RebellionCooldownUntilTurn = sim.State.Turn + 1 + Duration(sim, rules.CooldownTurns);
        state.UnrestUntilTurn = sim.State.Turn + 1 + Duration(sim, rules.UnrestTurns);
        sim.Events.Publish(new CityRebellionTriggered(city.Id, city.Owner, rebel, units.AsReadOnly(), released));
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
    }

    private static int Duration(Simulation sim, int turns) => Math.Max(1,
        turns * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).ResearchPercent / 100);

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

    private static int EnsureBarbarians(Simulation sim)
    {
        var existing = sim.State.Players.OrderBy(p => p.Id).FirstOrDefault(p =>
            p.Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true);
        if (existing is not null) return existing.Id;
        int id = sim.State.Players.Max(p => p.Id) + 1;
        int team = sim.State.Teams.Max(t => t.Id) + 1;
        var player = new Player { Id = id, TeamId = team, Name = "Rebels", EndedTurn = true,
            Civilization = "peoples:barbarian", Leader = "peoples:leader_barbarian" };
        player.Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        sim.State.Players.Add(player);
        sim.State.Teams.Add(new Team { Id = team, Members = new() { id } });
        sim.State.RebuildIndexes();
        foreach (var other in sim.State.Teams.Where(t => t.Id != team)) sim.State.SetWar(team, other.Id, true);
        return id;
    }
}
