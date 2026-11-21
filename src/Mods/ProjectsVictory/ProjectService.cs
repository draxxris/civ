using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.ProjectsVictory;

internal sealed class ProjectService : IProjectService
{
    private static TeamProjectState ReadTeam(Simulation sim, int team) => sim.State.TeamById(team).Extensions.Get<TeamProjectState>(sim.Content) ?? new();

    public int TeamCount(Simulation sim, int team, string project) => ReadTeam(sim, team).Completed.GetValueOrDefault(project);
    public int WorldCount(Simulation sim, string project) => sim.State.Teams.Sum(t => TeamCount(sim, t.Id, project));

    public RequirementResult ValidateProject(Simulation sim, int cityId, string id)
    {
        var failures = new List<RequirementFailure>();
        void Fail(string reason) => failures.Add(new(null, reason));
        if (!sim.Content.TryGet<ProjectDef>(id, out var def)) { Fail("Unknown project"); return new(false, failures); }
        var city = sim.State.CityById(cityId);
        int team = sim.State.PlayerById(city.Owner).TeamId;
        if (def.Cost < 0) Fail("Project cannot be built");
        if (sim.Rules().Victory.Status(sim).IsGameOver) Fail("The game is over");
        if (def.TechPrereq is { } tech && !sim.KnowsTech(city.Owner, tech)) Fail("Requires technology: " + tech);
        failures.AddRange(sim.Content.Requirements.Evaluate(def.Requirements, new(sim, city.Owner, city, sim.State.Grid.TileAt(city.Position))).Failures);
        if (def.MaxGlobalInstances >= 0 && WorldCount(sim, id) >= def.MaxGlobalInstances) Fail("WorldLimitReached");
        if (def.MaxTeamInstances >= 0 && TeamCount(sim, team, id) >= def.MaxTeamInstances) Fail("TeamLimitReached");
        foreach (var limit in def.VictoryThresholds.Values)
            if (TeamCount(sim, team, id) >= limit) Fail("VictoryThresholdReached");
        foreach (var (required, count) in def.PrereqProjects)
            if (TeamCount(sim, team, required) < count) Fail("Requires project: " + required);
        if (def.AnyonePrereqProject is { } anyone && WorldCount(sim, anyone) <= 0) Fail("Requires project built by any team: " + anyone);
        if (def.Victory is { } victory && sim.Content.TryGet<VictoryDef>(victory, out var linked) && !linked.Enabled) Fail("Victory is disabled");
        return new(failures.Count == 0, failures);
    }

    /// <summary>Copies of the project the team may still build, capped by every limit <see cref="ValidateProject"/> enforces.
    /// <see cref="int.MaxValue"/> when nothing caps it. <c>Shared</c> is true when the tightest cap binds the whole team
    /// rather than only this player, because a copy a teammate queues spends the same room.</summary>
    internal (int Copies, bool Shared) ProjectRoom(Simulation sim, int cityId, string id)
    {
        var def = sim.Content.Get<ProjectDef>(id);
        int team = sim.State.PlayerById(sim.State.CityById(cityId).Owner).TeamId;
        int remaining = int.MaxValue, teamRemaining = int.MaxValue;
        if (def.MaxGlobalInstances >= 0) remaining = Math.Min(remaining, def.MaxGlobalInstances - WorldCount(sim, id));
        if (def.MaxTeamInstances >= 0) teamRemaining = Math.Min(teamRemaining, def.MaxTeamInstances - TeamCount(sim, team, id));
        foreach (var limit in def.VictoryThresholds.Values) teamRemaining = Math.Min(teamRemaining, limit - TeamCount(sim, team, id));
        remaining = Math.Min(remaining, teamRemaining);
        // A world cap is spent by whoever finishes first, so another player's queue never spends this team's room; a team
        // cap is spent by every queue on the team, so a teammate's queued copy does.
        return (remaining, teamRemaining <= remaining);
    }

    public void CompleteProject(Simulation sim, int cityId, string project)
    {
        var city = sim.State.CityById(cityId);
        int team = sim.State.PlayerById(city.Owner).TeamId;
        var state = ReadTeam(sim, team);
        state.Completed[project] = state.Completed.GetValueOrDefault(project) + 1;
        sim.State.TeamById(team).Extensions.Set(sim.Content, state);
        sim.Events.Publish(new ProjectCompleted(cityId, city.Owner, team, project));
    }
}

/// <summary>The space race: the first team, highest score first, to complete every spaceship project linked to the
/// victory, each as many times as the victory's minimum (or else its threshold, or else once) asks.</summary>
internal sealed class SpaceshipVictory(ProjectService projects) : IVictoryCondition
{
    public string Id => ProjectVictories.Spaceship;

    public int? Winner(Simulation sim, VictoryDef victory, IVictoryStandings standings, bool turnEnd)
    {
        var required = sim.Content.Get<ProjectDef>().All.Where(p => p.Spaceship && p.Victory == victory.Id)
            .Select(p => (p.Id, Count: p.VictoryMinThresholds.TryGetValue(victory.Id, out int min) ? min : p.VictoryThresholds.GetValueOrDefault(victory.Id, 1)))
            .ToArray();
        if (required.Length == 0) return null;
        foreach (int team in standings.Teams)
            if (required.All(r => projects.TeamCount(sim, team, r.Id) >= r.Count)) return standings.Winner(team);
        return null;
    }
}
