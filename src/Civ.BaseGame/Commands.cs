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
        sim.State.Cities.Add(new City { Id = id, Owner = Player, Name = $"City {id}", Position = Position });
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
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var p = sim.State.PlayerById(Player);
        if (p.Researching != Tech) { p.Researching = Tech; p.ResearchProgress = 0; }
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
        return null;
    }

    public override void Apply(Simulation sim)
    {
        var city = sim.State.Cities.First(c => c.Id == CityId);
        if (city.Building != Building) { city.Building = Building; city.BuildProgress = 0; }
        city.Training = null; city.TrainProgress = 0;
    }
}
