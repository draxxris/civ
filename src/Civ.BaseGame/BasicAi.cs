using Civ.Content;
using Civ.Game;

namespace Civ.BaseGame;

/// <summary>
/// Deliberately simple default AI: found one city on the best nearby land, research the cheapest available tech,
/// build the cheapest available building. It is an ordinary registered <see cref="IPlayerAI"/>; mods can add smarter ones.
/// </summary>
public sealed class BasicAi : IPlayerAI
{
    public string Id => "base:basic";

    public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
    {
        var player = sim.State.PlayerById(playerId);

        if (!sim.State.Cities.Any(c => c.Owner == playerId))
        {
            var site = Sites.Best(sim, playerId);
            if (site != null)
                yield return new FoundCityCommand { Player = playerId, Position = site.Value };
        }

        if (player.Researching == null)
        {
            var tech = sim.Content.Techs.All
                .Where(t => new SetResearchCommand { Player = playerId, Tech = t.Id }.Validate(sim) == null)
                .OrderBy(t => t.Cost).ThenBy(t => t.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (tech != null) yield return new SetResearchCommand { Player = playerId, Tech = tech.Id };
        }

        foreach (var city in sim.State.Cities.Where(c => c.Owner == playerId && c.Building == null))
        {
            var building = sim.Content.Buildings.All
                .Where(b => new StartBuildingCommand { Player = playerId, CityId = city.Id, Building = b.Id }.Validate(sim) == null)
                .OrderBy(b => b.Cost).ThenBy(b => b.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (building != null) { yield return new StartBuildingCommand { Player = playerId, CityId = city.Id, Building = building.Id }; continue; }

            // Nothing left to build: keep one military unit per city.
            if (city.Training == null && sim.State.Units.Count(u => u.Owner == playerId && sim.Content.Units.Get(u.Type).Strength > 0)
                    < sim.State.Cities.Count(c => c.Owner == playerId))
            {
                var unit = sim.Content.Units.All
                    .Where(u => u.Strength > 0 && new StartTrainingCommand { Player = playerId, CityId = city.Id, Unit = u.Id }.Validate(sim) == null)
                    .OrderByDescending(u => u.Strength).ThenBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
                if (unit != null) yield return new StartTrainingCommand { Player = playerId, CityId = city.Id, Unit = unit.Id };
            }
        }

        foreach (var unit in sim.State.Units.Where(u => u.Owner == playerId).OrderBy(u => u.Id).ToList())
        {
            // Spend promotions on the first option by id, so combat lines come before the specialties.
            for (int i = UnitRules.UnspentPromotions(sim, unit); i > 0; i--)
            {
                var promo = UnitRules.AvailablePromotions(sim, unit).FirstOrDefault();
                if (promo == null) break;
                yield return new PromoteUnitCommand { Player = playerId, UnitId = unit.Id, Promotion = promo.Id };
                break; // the sim applies each command before asking again next turn
            }

            // Attack an adjacent enemy when it looks winnable.
            if (sim.Content.Units.Get(unit.Type).Strength <= 0 || unit.MovesLeft <= 0) continue;
            foreach (var n in unit.Position.Neighbors())
            {
                var target = UnitRules.BestDefender(sim, n, playerId);
                if (target != null && UnitRules.Strength(sim, unit, true) >= UnitRules.Strength(sim, target, false))
                {
                    yield return new AttackCommand { Player = playerId, UnitId = unit.Id, Target = n };
                    break;
                }
            }
        }
    }
}

public static class Sites
{
    /// <summary>The legal city site with the best surrounding yields (ties by coordinates), or null if none.</summary>
    public static Hex? Best(Simulation sim, int playerId) =>
        sim.State.Terrain.Keys
            .Select(h => (hex: h, score: Score(sim, h)))
            .OrderByDescending(x => x.score).ThenBy(x => x.hex.Q).ThenBy(x => x.hex.R)
            .Select(x => (Hex?)x.hex)
            .FirstOrDefault(h => new FoundCityCommand { Player = playerId, Position = h!.Value }.Validate(sim) == null);

    private static int Score(Simulation sim, Hex hex) =>
        Hex.Range(hex, CityRules.Radius).Where(sim.State.Terrain.ContainsKey).Sum(h => CityRules.TileScore(sim, h));

    /// <summary>A settler and a warrior at the best site, the way a normal game starts.</summary>
    public static void PlaceStartingUnits(Simulation sim, int playerId)
    {
        var site = Best(sim, playerId);
        if (site == null) return;
        UnitRules.Spawn(sim, playerId, "base:settler", site.Value);
        UnitRules.Spawn(sim, playerId, "base:warrior", site.Value);
    }
}
