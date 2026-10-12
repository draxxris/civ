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
            var site = sim.State.Terrain.Keys
                .Select(h => (hex: h, score: Score(sim, h)))
                .OrderByDescending(x => x.score).ThenBy(x => x.hex.Q).ThenBy(x => x.hex.R)
                .Select(x => x.hex)
                .FirstOrDefault(h => new FoundCityCommand { Player = playerId, Position = h }.Validate(sim) == null);
            if (site != default || sim.State.Terrain.ContainsKey(site))
                yield return new FoundCityCommand { Player = playerId, Position = site };
        }

        if (player.Researching == null)
        {
            var tech = sim.Content.Techs.All
                .Where(t => !player.Techs.Contains(t.Id) && t.Prerequisites.All(player.Techs.Contains))
                .OrderBy(t => t.Cost).ThenBy(t => t.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (tech != null) yield return new SetResearchCommand { Player = playerId, Tech = tech.Id };
        }

        foreach (var city in sim.State.Cities.Where(c => c.Owner == playerId && c.Building == null))
        {
            var building = sim.Content.Buildings.All
                .Where(b => !city.Buildings.Contains(b.Id) && (b.RequiresTech == null || player.Techs.Contains(b.RequiresTech)))
                .OrderBy(b => b.Cost).ThenBy(b => b.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (building != null) yield return new StartBuildingCommand { Player = playerId, CityId = city.Id, Building = building.Id };
        }
    }

    private static int Score(Simulation sim, Hex hex)
    {
        int total = 0;
        foreach (var h in Hex.Range(hex, 1))
        {
            if (!sim.State.Terrain.ContainsKey(h)) continue;
            var t = sim.TerrainAt(h);
            total += t.Food + t.Production + t.Commerce;
        }
        return total;
    }
}
