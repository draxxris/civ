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
            commerce = commerce * (100 + Bonuses.Research(sim, player.Id)) / 100;
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
            if (city.Building == null && city.Training == null) continue;
            int bonus = Bonuses.Production(sim, city.Owner);
            int production = Yields.OfCity(sim, city).Production * (100 + bonus) / 100;

            if (city.Building != null)
            {
                city.BuildProgress += production;
                var def = sim.Content.Buildings.Get(city.Building);
                if (city.BuildProgress >= def.Cost)
                {
                    city.Buildings.Add(def.Id);
                    city.Building = null;
                    city.BuildProgress = 0;
                }
            }
            else
            {
                city.TrainProgress += production;
                var def = sim.Content.Units.Get(city.Training!);
                if (city.TrainProgress >= def.Cost)
                {
                    int xp = city.Buildings.Sum(b => sim.Content.Buildings.Get(b).UnitExperience);
                    UnitRules.Spawn(sim, city.Owner, def.Id, city.Position, xp);
                    city.Training = null;
                    city.TrainProgress = 0;
                }
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
            int bonus = Bonuses.Culture(sim, city.Owner);
            city.Culture += (1 + Yields.OfCity(sim, city).Culture) * (100 + bonus) / 100;
        }
        foreach (var player in sim.State.Players)
            player.Culture = sim.State.Cities.Where(c => c.Owner == player.Id).Sum(c => c.Culture);
    }
}

/// <summary>Heals units that rested all turn, then refills everyone's movement.</summary>
public sealed class UnitSystem : ITurnSystem
{
    public string Id => "base:units";
    public int Order => 50;

    public const int RestHeal = 10;

    public void Run(Simulation sim)
    {
        foreach (var unit in sim.State.Units)
        {
            int max = UnitRules.MaxMoves(sim, unit);
            if (unit.MovesLeft >= max)
                unit.Health = Math.Min(100, unit.Health + RestHeal + UnitRules.PromotionsOf(sim, unit).Sum(p => p.HealBonus));
            unit.MovesLeft = max;
        }
    }
}

/// <summary>Food box: surplus grows the city, a deficit starves it. Also drops specialists a city no longer has slots for.</summary>
public sealed class GrowthSystem : ITurnSystem
{
    public string Id => "base:growth";
    public int Order => 250;

    public void Run(Simulation sim)
    {
        foreach (var city in sim.State.Cities)
        {
            CityRules.EnforceSlots(sim, city);
            city.FoodStored += Yields.OfCity(sim, city).Food - CityRules.FoodPerCitizen * city.Population;
            if (city.FoodStored >= CityRules.FoodToGrow(city.Population))
            {
                city.Population++;
                city.FoodStored = 0;
                CityRules.AddCitizen(sim, city);
            }
            else if (city.FoodStored < 0)
            {
                if (city.Population > 1) { city.Population--; CityRules.RemoveCitizen(sim, city); }
                city.FoodStored = 0;
            }
        }
    }
}
