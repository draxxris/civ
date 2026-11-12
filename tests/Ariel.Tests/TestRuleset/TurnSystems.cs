using Ariel.Content;
using Ariel.Game;

namespace Ariel.BaseGame;

public sealed class ResearchSystem : ITurnSystem
{
    public string Id => "base:research";
    public int Order => 100;

    public void Run(Simulation sim)
    {
        foreach (var team in sim.State.Teams)
        {
            if (team.Researching == null) continue;
            foreach (int player in team.Members)
            {
                long commerce = sim.State.Cities.Where(c => c.Owner == player).Sum(c => (long)Yields.OfCity(sim, c).Commerce);
                team.ResearchProgress += commerce * (100 + Bonuses.Research(sim, player)) / 100;
            }
            var tech = sim.Content.Get<TechDef>(team.Researching);
            if (team.ResearchProgress >= tech.Cost)
            {
                team.Techs.Add(tech.Id);
                team.Research.Remove(tech.Id);
                team.Researching = null;
                sim.Events.Publish(new TechDiscovered(team.Id, tech.Id));
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
            if (Production.Of(sim, city) is not { } order) continue;
            int bonus = Bonuses.Production(sim, city.Owner);
            order.Progress += Yields.OfCity(sim, city).Production * (100 + bonus) / 100;

            if (order.Kind == ProductionKind.Building)
            {
                var def = sim.Content.Get<BuildingDef>(order.Item);
                if (order.Progress >= def.Cost)
                {
                    city.Buildings.Add(def.Id);
                    Production.Clear(sim, city);
                    sim.Events.Publish(new BuildingCompleted(city.Id, def.Id));
                }
            }
            else
            {
                var def = sim.Content.Get<UnitDef>(order.Item);
                if (order.Progress >= def.Cost)
                {
                    int xp = city.Buildings.Sum(b => sim.Content.Get<BuildingDef>(b).UnitExperience);
                    UnitRules.Spawn(sim, city.Owner, def.Id, city.Position, xp);
                    Production.Clear(sim, city);
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
                unit.Health = Math.Min(100, unit.Health + RestHeal);
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
                sim.Events.Publish(new CityGrew(city.Id, city.Population));
            }
            else if (city.FoodStored < 0)
            {
                if (city.Population > 1) { city.Population--; CityRules.RemoveCitizen(sim, city); }
                city.FoodStored = 0;
            }
        }
    }
}
