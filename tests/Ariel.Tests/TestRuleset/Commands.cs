using Ariel.Game;

namespace Ariel.BaseGame;

public sealed class FoundCityCommand : GameCommand
{
    public Hex Position { get; init; }

    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "unknown player";
        if (!sim.State.Grid.Contains(Position)) return "off map";
        var t = sim.TerrainAt(Position);
        if (t.IsWater || !t.Passable) return "cannot found a city on " + t.Name;
        if (sim.State.Grid.Range(Position, 2).Any(h => sim.State.CityAt(h) != null)) return "too close to another city";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        int id = sim.State.NextCityId++;
        var city = new City { Id = id, Owner = Player, Name = $"City {id}", Position = Position };
        sim.State.AddCity(city);
        CityRules.AddCitizen(sim, city);
        sim.Events.Publish(new CityFounded(id, Player, Position));
    }
}

public sealed class SetResearchCommand : GameCommand
{
    public string Tech { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "unknown player";
        if (!sim.Content.Techs.TryGet(Tech, out var def)) return "unknown tech";
        var p = sim.TeamOf(Player);
        if (p.Techs.Contains(Tech)) return "already known";
        if (def.Prerequisites.Any(r => !p.Techs.Contains(r)) || !sim.Content.Requirements.Evaluate(def.Requirements, new(sim, Player)).Success) return "missing prerequisites";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var p = sim.TeamOf(Player);
        p.Researching = Tech;
    }
}

public sealed class StartBuildingCommand : GameCommand
{
    public int CityId { get; init; }
    public string Building { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        var city = sim.State.FindCity(CityId);
        if (city == null || city.Owner != Player) return "not your city";
        if (!sim.Content.Buildings.TryGet(Building, out var def)) return "unknown building";
        if (city.Buildings.Contains(Building)) return "already built";
        if (def.RequiresTech != null && !sim.KnowsTech(Player, def.RequiresTech)) return "tech not known";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var city = sim.State.CityById(CityId);
        if (city.Building != Building) { city.Building = Building; city.BuildProgress = 0; }
        city.Training = null; city.TrainProgress = 0;
    }
}

/// <summary>Replaces a city's whole citizen assignment: which tiles are worked and how many of each specialist.</summary>
public sealed class SetCityWorkCommand : GameCommand
{
    public int CityId { get; init; }
    public Hex[] Tiles { get; init; } = [];
    public Dictionary<string, int> Specialists { get; init; } = new();

    public override string? Validate(Simulation sim)
    {
        var city = sim.State.FindCity(CityId);
        if (city == null || city.Owner != Player) return "not your city";
        return CityRules.ValidateAssignment(sim, city, Tiles, Specialists);
    }

    public override void Apply(Simulation sim) =>
        CityRules.Assign(sim.State.CityById(CityId), Tiles, Specialists);
}

public sealed class SetCivicCommand : GameCommand
{
    public string Civic { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "unknown player";
        if (!sim.Content.Civics.TryGet(Civic, out var def)) return "unknown civic";
        if (def.RequiresTech != null && !sim.KnowsTech(Player, def.RequiresTech)) return "tech not known";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var def = sim.Content.Civics.Get(Civic);
        sim.State.PlayerById(Player).Civics[def.Category] = def.Id;
        foreach (var city in sim.State.Cities.Where(c => c.Owner == Player)) CityRules.EnforceSlots(sim, city);
    }
}
