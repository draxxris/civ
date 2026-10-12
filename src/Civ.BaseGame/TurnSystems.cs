using Civ.Content;
using Civ.Game;

namespace Civ.BaseGame;

public sealed class ResearchSystem : ITurnSystem
{
    public string Id => "base:research";
    public int Order => 100;

    public void Run(Simulation sim)
    {
        foreach (var player in sim.State.Players)
        {
            if (player.Researching == null) continue;
            long commerce = sim.State.Cities.Where(c => c.Owner == player.Id).Sum(c => (long)Yields.OfCity(sim, c).Commerce);
            commerce = commerce * (100 + (sim.CivilizationOf(player.Id)?.ResearchBonus ?? 0)) / 100;
            player.ResearchProgress += commerce;
            var tech = sim.Content.Techs.Get(player.Researching);
            if (player.ResearchProgress >= tech.Cost)
            {
                player.Techs.Add(tech.Id);
                player.Researching = null;
                player.ResearchProgress = 0;
            }
        }
    }
}

public sealed class ProductionSystem : ITurnSystem
{
    public string Id => "base:production";
    public int Order => 200;

    public void Run(Simulation sim)
    {
        foreach (var city in sim.State.Cities)
        {
            if (city.Building == null) continue;
            int bonus = sim.CivilizationOf(city.Owner)?.ProductionBonus ?? 0;
            city.BuildProgress += Yields.OfCity(sim, city).Production * (100 + bonus) / 100;
            var def = sim.Content.Buildings.Get(city.Building);
            if (city.BuildProgress >= def.Cost)
            {
                city.Buildings.Add(def.Id);
                city.Building = null;
                city.BuildProgress = 0;
            }
        }
    }
}

public sealed class CultureSystem : ITurnSystem
{
    public string Id => "base:culture";
    public int Order => 300;

    public void Run(Simulation sim)
    {
        foreach (var city in sim.State.Cities)
        {
            int bonus = sim.CivilizationOf(city.Owner)?.CultureBonus ?? 0;
            city.Culture += (1 + city.Buildings.Sum(b => sim.Content.Buildings.Get(b).Culture)) * (100 + bonus) / 100;
        }
        foreach (var player in sim.State.Players)
            player.Culture = sim.State.Cities.Where(c => c.Owner == player.Id).Sum(c => c.Culture);
    }
}
