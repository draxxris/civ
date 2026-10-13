using Civ.Content;
using Civ.Game;

namespace Civ.BaseGame;

/// <summary>Small deterministic test AI: settle, research cheap techs, build, train and fight nearby opponents.</summary>
public sealed class BasicAi : IPlayerAI
{
    public string Id => "base:basic";

    public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
    {
        if (!sim.State.Cities.Any(c => c.Owner == playerId) && Sites.Best(sim, playerId) is { } site)
            yield return new FoundCityCommand { Player = playerId, Position = site };
        if (Research(sim, playerId) is { } research) yield return research;
        foreach (var city in sim.State.Cities.Where(c => c.Owner == playerId && c.Building == null))
            if (Production(sim, city) is { } production) yield return production;
        foreach (var unit in sim.State.Units.Where(u => u.Owner == playerId).OrderBy(u => u.Id).ToList())
        {
            if (Promotion(sim, unit) is { } promotion) yield return promotion;
            if (Attack(sim, unit) is { } attack) yield return attack;
        }
    }

    private static GameCommand? Research(Simulation sim, int player)
    {
        var team = sim.TeamOf(player);
        if (team.Researching != null) return null;
        var tech = sim.Content.Techs.All
            .Where(t => !team.Techs.Contains(t.Id) && t.Prerequisites.All(team.Techs.Contains)
                && sim.Content.Requirements.Evaluate(t.Requirements, new(sim, player)).Success)
            .OrderBy(t => t.Cost).ThenBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault();
        return tech == null ? null : new SetResearchCommand { Player = player, Tech = tech.Id };
    }
    private static GameCommand? Production(Simulation sim, City city)
    {
        var building = sim.Content.Buildings.All
            .Where(b => !city.Buildings.Contains(b.Id) && (b.RequiresTech == null || sim.KnowsTech(city.Owner, b.RequiresTech)))
            .OrderBy(b => b.Cost).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
        if (building != null) return new StartBuildingCommand { Player = city.Owner, CityId = city.Id, Building = building.Id };
        if (city.Training != null || HasEnoughMilitary(sim, city.Owner)) return null;
        var unit = sim.Content.Units.All
            .Where(u => u.Strength > 0 && (u.RequiresTech == null || sim.KnowsTech(city.Owner, u.RequiresTech)))
            .OrderByDescending(u => u.Strength).ThenBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
        return unit == null ? null : new StartTrainingCommand { Player = city.Owner, CityId = city.Id, Unit = unit.Id };
    }
    private static bool HasEnoughMilitary(Simulation sim, int player) =>
        sim.State.Units.Count(u => u.Owner == player && sim.Content.Units.Get(u.Type).Strength > 0)
        >= sim.State.Cities.Count(c => c.Owner == player);

    private static GameCommand? Promotion(Simulation sim, Unit unit)
    {
        if (UnitRules.UnspentPromotions(sim, unit) <= 0) return null;
        var promo = UnitRules.AvailablePromotions(sim, unit).FirstOrDefault();
        return promo == null ? null : new PromoteUnitCommand { Player = unit.Owner, UnitId = unit.Id, Promotion = promo.Id };
    }
    private static GameCommand? Attack(Simulation sim, Unit unit)
    {
        if (sim.Content.Units.Get(unit.Type).Strength <= 0 || unit.MovesLeft <= 0) return null;
        foreach (var neighbor in sim.State.Grid.Neighbors(unit.Position))
        {
            var target = UnitRules.BestDefender(sim, neighbor, unit.Owner);
            if (target != null && UnitRules.Strength(sim, unit, true) >= UnitRules.Strength(sim, target, false))
                return new AttackCommand { Player = unit.Owner, UnitId = unit.Id, Target = neighbor };
        }
        return null;
    }
}

public static class Sites
{
    /// <summary>The legal city site with the best surrounding yields (ties by coordinates), or null if none.</summary>
    public static Hex? Best(Simulation sim, int playerId) =>
        sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf)
            .Select(h => (hex: h, score: Score(sim, h)))
            .OrderByDescending(x => x.score).ThenBy(x => x.hex.Q).ThenBy(x => x.hex.R)
            .Select(x => (Hex?)x.hex)
            .FirstOrDefault(h => new FoundCityCommand { Player = playerId, Position = h!.Value }.Validate(sim) == null);

    private static int Score(Simulation sim, Hex hex) =>
        sim.State.Grid.Range(hex, CityRules.Radius).Sum(h => CityRules.TileScore(sim, h));

    /// <summary>A settler and warrior at the best site.</summary>
    public static void PlaceStartingUnits(Simulation sim, int playerId)
    {
        var site = Best(sim, playerId);
        if (site == null) return;
        UnitRules.Spawn(sim, playerId, "base:settler", site.Value);
        UnitRules.Spawn(sim, playerId, "base:warrior", site.Value);
    }
}
