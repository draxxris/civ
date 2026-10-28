using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Military;

internal static class GoodyService
{
    internal static void Receive(Simulation sim, Unit unit)
    {
        var plot = sim.State.PlotAt(unit.Position);
        if (plot.Improvement is not { } improvement || !sim.Content.Get<S.ImprovementDef>(improvement).Goody) return;
        if (MilitaryData.Player(sim, unit.Owner).Barbarian) return;
        plot.Improvement = null;
        var goodies = sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap).Goodies.Select(sim.Content.Get<S.GoodyDef>).Where(g => Eligible(sim, unit, g)).ToArray();
        if (goodies.Length == 0) return;
        var goody = goodies[sim.Rng.Next(goodies.Length)];
        int gold = goody.Gold + Random(sim, goody.GoldRand1) + Random(sim, goody.GoldRand2);
        sim.State.PlayerById(unit.Owner).Treasury += gold;
        unit.Experience += goody.Experience;
        unit.Health = Math.Min(MilitaryData.Constants(sim).MaxHitPoints, unit.Health + goody.Healing);
        if (goody.GrantedUnit is { } type) GrantUnit(sim, unit, type);
        if (goody.Tech) GrantTechnology(sim, unit);
        if (goody.MapRange > 0) RevealMap(sim, unit, goody);
        if (goody.BarbarianUnit is { } barbarian) HostileVillagers(sim, unit, goody, barbarian);
        sim.Events.Publish(new GoodyReceived(unit.Id, goody.Id));
    }
    private static int Random(Simulation sim, int range) => range <= 0 ? 0 : sim.Rng.Next(range);
    private static bool Eligible(Simulation sim, Unit unit, S.GoodyDef goody)
    {
        var def = MilitaryData.Definition(sim, unit);
        if (goody.Bad && def.NoBadGoodies) return false;
        if (MilitaryData.Constants(sim).MaxHitPoints - unit.Health < goody.DamagePrereq) return false;
        if (!ExperienceEligible(sim, def, goody)) return false;
        if (goody.Tech && !Technologies(sim, unit).Any()) return false;
        if (!GrantEligible(sim, goody)) return false;
        return goody.BarbarianUnit is null || HostileEligible(sim, unit);
    }
    private static bool ExperienceEligible(Simulation sim, S.UnitDef unit, S.GoodyDef goody) => goody.Experience <= 0 || sim.State.Turn > 10 && unit.Strength > 0;
    private static bool GrantEligible(Simulation sim, S.GoodyDef goody) => goody.GrantedUnit is not { } granted || sim.Content.Get<S.UnitDef>(granted).Strength == 0 || sim.State.Turn > 20;
    private static bool HostileEligible(Simulation sim, Unit unit)
    {
        var cities = sim.State.Cities.Where(c => c.Owner == unit.Owner).ToArray();
        if (cities.Length == 0) return false;
        return cities.Length > 1 || sim.State.Grid.Distance(unit.Position, cities[0].Position) > 7;
    }
    private static IEnumerable<S.TechDef> Technologies(Simulation sim, Unit unit) => sim.Content.Get<S.TechDef>().All.Where(t => t.GoodyTech && !t.Disable
        && !sim.KnowsTech(unit.Owner, t.Id) && sim.Content.Requirements.Evaluate(t.Requirements, new(sim, unit.Owner)).Success);
    private static void GrantTechnology(Simulation sim, Unit unit)
    {
        var techs = Technologies(sim, unit).ToArray();
        if (techs.Length == 0) return;
        var tech = techs[sim.Rng.Next(techs.Length)];
        var team = sim.TeamOf(unit.Owner);
        team.Techs.Add(tech.Id);
        if (team.Research.Remove(tech.Id, out long saved) && saved > 0)
        {
            var research = team.Extensions.Get<ResearchTeamState>(sim.Content) ?? new ResearchTeamState();
            research.Overflow += saved;
            team.Extensions.Set(sim.Content, research);
        }
        if (team.Researching == tech.Id) team.Researching = null;
        sim.Events.Publish(new TechDiscovered(sim.TeamOf(unit.Owner).Id, tech.Id));
    }
    private static void GrantUnit(Simulation sim, Unit unit, string type)
    {
        var civ = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(unit.Owner).Civilization);
        string? replacement = civ.UnitOverrides.GetValueOrDefault(sim.Content.Get<S.UnitDef>(type).DefaultUnit, type);
        if (replacement is not null) MilitaryData.Spawn(sim, unit.Owner, replacement, unit.Position);
    }
    private static void RevealMap(Simulation sim, Unit unit, S.GoodyDef goody)
    {
        var grid = sim.State.Grid;
        var centers = grid.Range(grid.TileAt(unit.Position), goody.MapOffset).ToArray();
        var center = centers[sim.Rng.Next(centers.Length)];
        foreach (var tile in grid.Range(center, goody.MapRange))
            if (sim.Rng.Next(100) < goody.MapProbability)
                sim.Content.Services.Get<IVisibilityService>().Single().RevealPlot(sim, tile, sim.TeamOf(unit.Owner).Id);
    }
    private static void HostileVillagers(Simulation sim, Unit unit, S.GoodyDef goody, string type)
    {
        int owner = BarbarianSpawns.EnsurePlayer(sim);
        var candidates = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(unit.Position)).Where(t =>
            !sim.Content.Get<S.TerrainDef>(sim.State.Plots[t.Value].Terrain).IsWater && sim.State.Plots[t.Value].Kind != PlotKind.Peak
            && !sim.State.UnitsAt(sim.State.Grid.HexOf(t)).Any()).ToArray();
        int spawned = 0;
        foreach (var tile in candidates)
            if (sim.Rng.Next(100) < goody.BarbarianUnitProbability || spawned < goody.MinBarbarians)
            { MilitaryData.Spawn(sim, owner, type, sim.State.Grid.HexOf(tile)); spawned++; }
    }
}
