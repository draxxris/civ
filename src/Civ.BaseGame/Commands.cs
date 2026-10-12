using Civ.Game;

namespace Civ.BaseGame;

public sealed class FoundCityCommand : GameCommand
{
    public Hex Position { get; init; }
    /// <summary>Optional: a settler standing on <see cref="Position"/> that is consumed. 0 = found for free (setup, AI bootstrap).</summary>
    public int UnitId { get; init; }

    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "unknown player";
        if (UnitId != 0)
        {
            var u = sim.State.Units.FirstOrDefault(x => x.Id == UnitId);
            if (u == null || u.Owner != Player) return "not your unit";
            if (!sim.Content.Units.Get(u.Type).CanFoundCity) return "unit cannot found cities";
            if (u.Position != Position) return "settler must be on the city site";
        }
        if (!sim.State.Terrain.ContainsKey(Position)) return "off map";
        var t = sim.TerrainAt(Position);
        if (t.IsWater || !t.Passable) return "cannot found a city on " + t.Name;
        if (sim.State.Cities.Any(c => c.Position.DistanceTo(Position) < 3)) return "too close to another city";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        if (UnitId != 0) sim.State.Units.RemoveAll(x => x.Id == UnitId);
        int id = sim.State.NextCityId++;
        var city = new City { Id = id, Owner = Player, Name = $"City {id}", Position = Position };
        sim.State.Cities.Add(city);
        CityRules.AddCitizen(sim, city);
    }
}

public sealed class SetResearchCommand : GameCommand
{
    public string Tech { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "unknown player";
        if (!sim.Content.Techs.TryGet(Tech, out var def)) return "unknown tech";
        var p = sim.State.PlayerById(Player);
        if (p.Techs.Contains(Tech)) return "already known";
        if (def.Prerequisites.Any(r => !p.Techs.Contains(r))) return "missing prerequisites";
        if (p.Researching == Tech) return null; // already paid for
        return Resources.CheckAfford(sim, Player, null, def.ResourceCost);
    }

    /// <summary>Switching to a tech pays its resource cost up front; switching away does not refund it.</summary>
    public override void Apply(Simulation sim)
    {
        var p = sim.State.PlayerById(Player);
        if (p.Researching == Tech) return;
        Resources.Spend(sim, Player, null, sim.Content.Techs.Get(Tech).ResourceCost);
        p.Researching = Tech; p.ResearchProgress = 0;
    }
}

public sealed class StartBuildingCommand : GameCommand
{
    public int CityId { get; init; }
    public string Building { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        var city = sim.State.Cities.FirstOrDefault(c => c.Id == CityId);
        if (city == null || city.Owner != Player) return "not your city";
        if (!sim.Content.Buildings.TryGet(Building, out var def)) return "unknown building";
        if (city.Buildings.Contains(Building)) return "already built";
        if (def.RequiresTech != null && !sim.State.PlayerById(Player).Techs.Contains(def.RequiresTech)) return "tech not known";
        if (Institutions.CheckRequirement(sim, city, def.RequiresInstitution, def.RequiresInstitutionCategory) is { } why) return why;
        if (city.Building == Building) return null; // already paid for
        return Resources.CheckAfford(sim, Player, city, def.ResourceCost);
    }

    /// <summary>Starting a building pays its resource cost up front; switching away does not refund it.</summary>
    public override void Apply(Simulation sim)
    {
        var city = sim.State.Cities.First(c => c.Id == CityId);
        if (city.Building != Building)
        {
            Resources.Spend(sim, Player, city, sim.Content.Buildings.Get(Building).ResourceCost);
            city.Building = Building; city.BuildProgress = 0;
        }
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
        var city = sim.State.Cities.FirstOrDefault(c => c.Id == CityId);
        if (city == null || city.Owner != Player) return "not your city";
        return CityRules.ValidateAssignment(sim, city, Tiles, Specialists);
    }

    public override void Apply(Simulation sim) =>
        CityRules.Assign(sim.State.Cities.First(c => c.Id == CityId), Tiles, Specialists);
}

public sealed class SetCivicCommand : GameCommand
{
    public string Civic { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "unknown player";
        if (!sim.Content.Civics.TryGet(Civic, out var def)) return "unknown civic";
        if (def.RequiresTech != null && !sim.State.PlayerById(Player).Techs.Contains(def.RequiresTech)) return "tech not known";
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var def = sim.Content.Civics.Get(Civic);
        sim.State.PlayerById(Player).Civics[def.Category] = def.Id;
        foreach (var city in sim.State.Cities.Where(c => c.Owner == Player)) CityRules.EnforceSlots(sim, city);
    }
}
