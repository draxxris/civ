using System.Globalization;
using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Barbarians;

/// <summary>Creates the barbarian player once, on its own team and at war with every other team, and records it
/// as the base game's one hostile player for the rest of the game. Answers the base game's <see cref="IHostileFaction"/>,
/// barring a barbarian from a plot when any registered <see cref="IBarbarianBarrier"/> does.</summary>
internal sealed class BarbarianFaction : IHostileFaction
{
    int IHostileFaction.Ensure(Simulation sim) => Ensure(sim);

    public bool Bars(Simulation sim, int unit, Hex target)
    {
        foreach (var barrier in sim.Content.Services.Get<IBarbarianBarrier>())
            if (barrier.Blocks(sim, unit, target)) return true;
        return false;
    }

    internal static int Ensure(Simulation sim)
    {
        if (PlayerRules.BarbarianPlayer(sim) is { } existing) return existing;
        int playerId = sim.State.Players.Max(p => p.Id) + 1;
        int teamId = sim.State.Teams.Max(t => t.Id) + 1;
        sim.State.AddTeam(new Team { Id = teamId });
        sim.State.AddPlayer(new Player { Id = playerId, TeamId = teamId, Civilization = "peoples:barbarian",
            Leader = "peoples:leader_barbarian", Name = "Barbarians", EndedTurn = true });
        Barbarian.GameState(sim).SetBarbarianPlayer(playerId);
        foreach (var team in sim.State.Teams.Where(t => t.Id != teamId)) sim.State.SetWar(teamId, team.Id, true);
        return playerId;
    }
}
/// <summary>Barbarian land units in unclaimed land and ships off unclaimed coasts, once enough cities exist.</summary>
internal sealed class BarbarianSpawns : ITurnSystem
{
    public string Id => "barbarians:spawns";
    /// <summary>Just after the spawn phase's own systems (random events), where this ran when it was part of the base
    /// game, so the order of random draws stays as it was.</summary>
    public int Order => TurnPhase.Spawns + 1;
    public void Run(Simulation sim)
    {
        if (!Ready(sim)) return;
        int player = BarbarianFaction.Ensure(sim);
        ShareTechnologies(sim, player);
        foreach (var area in sim.State.Grid.Tiles.GroupBy(t => sim.State.Plots[t.Value].AreaId).OrderBy(g => g.Key)) SpawnArea(sim, player, area.ToArray());
        SpawnWater(sim, player);
        Barbarian.Sight(sim);
    }
    private static void SpawnWater(Simulation sim, int player)
    {
        var candidates = sim.State.Grid.Tiles.Where(t => WaterSpawnPlot(sim, t)).ToList();
        int divisor = sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap).UnownedWaterTilesPerBarbarianUnit;
        if (divisor <= 0) divisor = 40;
        int existing = sim.State.Units.Count(u => u.Owner == player && u.Health > 0
            && Barbarian.Definition(sim, u).Domain == "military:domain_sea");
        int needed = candidates.Count / Math.Max(1, divisor * 3) - existing;
        if (needed <= 0) return;
        var best = NavalUnit(sim);
        if (best is null) return;
        for (int i = 0; i < needed && candidates.Count > 0; i++)
        {
            int index = sim.Rng.Next(candidates.Count);
            var tile = candidates[index]; candidates.RemoveAt(index);
            Barbarian.Spawn(sim, player, best.Id, sim.State.Grid.HexOf(tile));
        }
    }
    private static S.UnitDef? NavalUnit(Simulation sim)
    {
        var civ = sim.Content.Get<S.CivilizationDef>("peoples:barbarian");
        var civilians = sim.State.Players.Where(p => !PlayerRules.IsBarbarian(sim, p.Id)).ToArray();
        return sim.Content.Get<S.UnitDef>().All.Where(u => NavalCandidate(u)
            && civ.UnitOverrides.GetValueOrDefault(u.DefaultUnit, u.DefaultUnit) == u.Id)
            .Where(u => civilians.Any(p => sim.Content.Requirements.EvaluateKind(u.Requirements, new(sim, p.Id), "tech").Success))
            .OrderByDescending(u => u.Strength).ThenBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
    }
    private static bool NavalCandidate(S.UnitDef unit) => unit.Domain == "military:domain_sea" && unit.Strength > 0
        && unit.Cost >= 0 && !unit.Animal && !unit.OnlyDefensive;

    private static bool WaterSpawnPlot(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        if (!terrain.IsWater || terrain.Impassable || plot.IsLake || plot.Owner is not null) return false;
        if (plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).Impassable) return false;
        if (plot.Visible.Any(t => sim.State.TeamById(t).Members.Any(p => !PlayerRules.IsBarbarian(sim, p)))) return false;
        var position = sim.State.Grid.HexOf(tile);
        if (sim.State.CityAt(position) is not null || sim.State.UnitsAt(position).Any()) return false;
        return CoastalWater(sim, tile);
    }
    internal static bool CoastalWater(Simulation sim, TileId tile) => sim.State.Grid.Neighbors(tile)
        .Any(n => !sim.Content.Get<S.TerrainDef>(sim.State.Plots[n.Value].Terrain).IsWater);

    private static bool Ready(Simulation sim)
    {
        var handicap = sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap);
        int start = handicap.BarbarianCreationTurnsElapsed * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).BarbPercent / 100;
        var civilians = sim.State.Players.Where(p => !PlayerRules.IsBarbarian(sim, p.Id)).ToArray();
        bool eraAllows = civilians.Any(p => !sim.Content.Get<S.EraDef>(p.Extensions.Get<CityPlayerState>(sim.Content)?.CurrentEra ?? sim.Settings.StartingEra).NoBarbUnits);
        return eraAllows && sim.State.Turn - 1 >= start && sim.State.Cities.Count >= civilians.Length * 3 / 2;
    }
    private static void ShareTechnologies(Simulation sim, int barbarian)
    {
        var teams = sim.State.Teams.Where(t => t.Id != sim.TeamOf(barbarian).Id).ToArray();
        foreach (var tech in sim.Content.Get<S.TechDef>().All)
            if (teams.Count(t => t.Techs.Contains(tech.Id)) * 2 > teams.Length) sim.TeamOf(barbarian).Techs.Add(tech.Id);
    }
    private static void SpawnArea(Simulation sim, int player, TileId[] area)
    {
        if (area.Length == 0) return;
        if (sim.Content.Get<S.TerrainDef>(sim.State.Plots[area[0].Value].Terrain).IsWater) return; // Water is handled after the land areas.
        int divisor = sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap).UnownedTilesPerBarbarianUnit;
        if (divisor <= 0) return;
        int existing = sim.State.Units.Count(u => u.Owner == player && sim.State.PlotAt(u.Position).AreaId == sim.State.Plots[area[0].Value].AreaId);
        int deficit = area.Count(t => sim.State.Plots[t.Value].Owner is null) / divisor - existing;
        if (deficit <= 0) return;
        var candidates = area.Where(t => SpawnPlot(sim, t)).ToList();
        var units = SpawnUnits(sim, player).ToArray();
        if (units.Length == 0) return;
        for (int i = 0; i < deficit / 4 + 1 && candidates.Count > 0; i++)
        {
            int index = sim.Rng.Next(candidates.Count);
            var tile = candidates[index]; candidates.RemoveAt(index);
            var unit = units.Select(u => (Def: u, Score: 1 + sim.Rng.Next(1000) + (u.UnitAiTypes.GetValueOrDefault("military:unitai_attack") ? 200 : 0)))
                .OrderByDescending(u => u.Score).ThenBy(u => u.Def.Id, StringComparer.Ordinal).First().Def;
            Barbarian.Spawn(sim, player, unit.Id, sim.State.Grid.HexOf(tile));
        }
    }
    private static bool SpawnPlot(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Owner is not null || sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater || plot.Kind == PlotKind.Peak) return false;
        if (sim.Content.Get<S.TerrainDef>(plot.Terrain).Impassable) return false;
        if (plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).Impassable) return false;
        if (plot.Visible.Any(t => !sim.State.TeamById(t).Members.Any(p => PlayerRules.IsBarbarian(sim, p)))) return false;
        var position = sim.State.Grid.HexOf(tile);
        if (sim.State.UnitsAt(position).Any()) return false;
        int minimum = Barbarian.Raw(sim, "MIN_BARBARIAN_STARTING_DISTANCE", 4);
        return !sim.State.Units.Any(u => !PlayerRules.IsBarbarian(sim, u.Owner) && sim.State.Grid.Distance(u.Position, position) < minimum);
    }
    private static IEnumerable<S.UnitDef> SpawnUnits(Simulation sim, int player)
    {
        var civ = sim.Content.Get<S.CivilizationDef>("peoples:barbarian");
        return sim.Content.Get<S.UnitDef>().All.Where(u => u.DefaultUnit == u.Id && u.Strength > 0 && u.Cost >= 0 && !u.OnlyDefensive
            && !u.Animal && u.Domain == "military:domain_land" && civ.UnitOverrides.GetValueOrDefault(u.Id, u.Id) == u.Id)
            .Where(u => sim.Content.Requirements.Evaluate(u.Requirements, new(sim, player)).Success);
    }
}

/// <summary>Every barbarian unit's move at the start of the turn: attack a neighbour it likely beats, else march on
/// the nearest enemy city; ships hunt ships and raid fishing boats.</summary>
internal sealed class BarbarianBehavior : ITurnSystem
{
    public string Id => "barbarians:behavior";
    public int Order => TurnPhase.GameBegin;
    public void Run(Simulation sim)
    {
        foreach (var unit in sim.State.Units.Where(u => PlayerRules.IsBarbarian(sim, u.Owner)).OrderBy(u => u.Id).ToArray()) Act(sim, unit);
    }
    private static void ActSea(Simulation sim, Unit unit)
    {
        var grid = sim.State.Grid;
        var combat = sim.Rules().Combat;
        var movement = sim.Rules().Movement;
        var from = grid.TileAt(unit.Position);
        var enemy = grid.Neighbors(from).Where(t => movement.CanEnter(sim, unit.Id, from, t, true) is null)
            .Where(t => sim.State.UnitsAt(grid.HexOf(t)).Any(u => Barbarian.Enemy(sim, unit.Owner, u.Owner)
                && Barbarian.Definition(sim, u).Domain == "military:domain_sea"))
            .Select(t => (Tile: t, Defender: combat.BestDefender(sim, unit.Id, grid.HexOf(t))))
            .Where(x => x.Defender is not null)
            .Select(x => (x.Tile, Odds: combat.Preview(sim, unit.Id, x.Defender!.Value).SuccessOdds))
            .Where(x => x.Odds >= 500_000).OrderByDescending(x => x.Odds).ThenBy(x => x.Tile.Value).ToArray();
        if (enemy.Length > 0) { movement.Move(sim, unit.Id, [grid.HexOf(enemy[0].Tile)]); return; }
        if (PillageFish(sim, unit)) return;
        var targets = grid.Tiles.Where(t => SeaTarget(sim, unit, t))
            .OrderBy(t => grid.Distance(from, t)).ThenBy(t => t.Value);
        foreach (var target in targets)
            if (Sail(sim, unit, target)) { PillageFish(sim, unit); return; }
        foreach (var tile in grid.Neighbors(from).Where(t => BarbarianSpawns.CoastalWater(sim, t)).OrderBy(t => t.Value))
            if (movement.CanEnter(sim, unit.Id, from, tile) is null)
            { movement.Move(sim, unit.Id, [grid.HexOf(tile)]); return; }
    }
    private static bool SeaTarget(Simulation sim, Unit unit, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (!plot.Visible.Contains(sim.TeamOf(unit.Owner).Id) || !sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater) return false;
        if (Fishing(plot) && plot.Owner is { } owner && Barbarian.Enemy(sim, unit.Owner, owner)) return true;
        return sim.State.UnitsAt(sim.State.Grid.HexOf(tile)).Any(u => Barbarian.Enemy(sim, unit.Owner, u.Owner)
            && Barbarian.Definition(sim, u).Domain == "military:domain_sea");
    }
    private static bool Fishing(Plot plot) => plot.Improvement is "world:improvement_improvement_fishing_boats"
        or "world:improvement_improvement_whaling_boats";
    private static bool PillageFish(Simulation sim, Unit unit)
    {
        if (!Fishing(sim.State.PlotAt(unit.Position))) return false;
        return sim.TryApply(new PillageCommand { Player = unit.Owner, Unit = unit.Id }, out _);
    }
    private static bool Sail(Simulation sim, Unit unit, TileId target)
    {
        var path = sim.Rules().Paths.FindPath(sim, unit.Id, sim.State.Grid.HexOf(target));
        if (path is null || path.Steps.Count == 0) return false;
        sim.Rules().Movement.Move(sim, unit.Id, path.Steps);
        return true;
    }
    private static void Act(Simulation sim, Unit unit)
    {
        if (unit.Health <= 0 || sim.State.FindUnit(unit.Id) is null || unit.MovesLeft <= 0) return;
        if (Barbarian.Definition(sim, unit).Domain == "military:domain_sea") { ActSea(sim, unit); return; }
        var movement = sim.Rules().Movement;
        var combat = sim.Rules().Combat;
        var from = sim.State.Grid.TileAt(unit.Position);
        var targets = sim.State.Grid.Neighbors(from).Where(t => movement.CanEnter(sim, unit.Id, from, t, true) is null)
            .Select(t => (Tile: t, Defender: combat.BestDefender(sim, unit.Id, sim.State.Grid.HexOf(t)))).Where(t => t.Defender is not null)
            .Select(t => (t.Tile, Odds: combat.Preview(sim, unit.Id, t.Defender!.Value).SuccessOdds)).OrderByDescending(t => t.Odds).ThenBy(t => t.Tile.Value).ToArray();
        if (targets.Length > 0 && targets[0].Odds >= 500_000) { movement.Move(sim, unit.Id, [sim.State.Grid.HexOf(targets[0].Tile)]); return; }
        var city = sim.State.Cities.Where(c => Barbarian.Enemy(sim, unit.Owner, c.Owner)).OrderBy(c => sim.State.Grid.Distance(c.Position, unit.Position)).ThenBy(c => c.Id).FirstOrDefault();
        if (city is null) return;
        var path = sim.Rules().Paths.FindPath(sim, unit.Id, city.Position, true);
        if (path is not null && path.Steps.Count > 0) movement.Move(sim, unit.Id, path.Steps);
    }
}

/// <summary>The few base-game reads the faction shares.</summary>
internal static class Barbarian
{
    internal static S.UnitDef Definition(Simulation sim, Unit unit) => sim.Content.Get<S.UnitDef>(unit.Type);
    internal static int Raw(Simulation sim, string key, int fallback) =>
        sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : fallback;
    internal static bool Enemy(Simulation sim, int player, int other) => sim.TeamOf(player).Wars.Contains(sim.TeamOf(other).Id);
    internal static MilitaryGameState GameState(Simulation sim)
    {
        var state = sim.State.Extensions.Get<MilitaryGameState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }
    internal static Unit Spawn(Simulation sim, int owner, string type, Hex position)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = position };
        sim.State.AddUnit(unit); sim.Events.Publish(new UnitCreated(unit.Id, owner, type, position)); return unit;
    }
    internal static void Sight(Simulation sim)
    {
        sim.Rules().Visibility.Recompute(sim);
        sim.Rules().Relations.DiscoverContacts(sim);
    }
}
