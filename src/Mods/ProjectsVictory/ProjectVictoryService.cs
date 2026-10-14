using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using BuildingDef = Civ.Content.Schema.BuildingDef;
using TerrainDef = Civ.Content.Schema.TerrainDef;
using CultureLevelDef = Civ.Content.Schema.CultureLevelDef;

namespace Civ.Mods.ProjectsVictory;

internal sealed class ProjectVictoryService : IProjectVictoryService
{
    private static VictoryGameState Read(Simulation sim) => sim.State.Extensions.Get<VictoryGameState>(sim.Content) ?? new();
    private static TeamProjectState ReadTeam(Simulation sim, int team) => sim.State.TeamById(team).Extensions.Get<TeamProjectState>(sim.Content) ?? new();
    private static IEnumerable<VictoryDef> Victories(Simulation sim) => sim.Content.Get<VictoryDef>().All
        .Where(v => v.Enabled).OrderBy(v => v.Priority).ThenBy(v => v.Id, StringComparer.Ordinal);
    private static bool IsBarbarian(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true;
    private static IEnumerable<Player> Civilizations(Simulation sim) =>
        sim.State.Players.Where(p => !IsBarbarian(sim, p.Id)).OrderBy(p => p.Id);
    /// <summary>Candidate winning teams, highest combined score first. Ties go to the lower team id.</summary>
    private IEnumerable<IGrouping<int, Player>> TeamsByScore(Simulation sim) => Civilizations(sim).GroupBy(p => p.TeamId)
        .OrderByDescending(team => team.Sum(p => GetScore(sim, p.Id).Total)).ThenBy(team => team.Key);
    /// <summary>The team's highest-scoring member. Ties go to the lower player id.</summary>
    private int BestMember(Simulation sim, IEnumerable<int> members) =>
        members.OrderByDescending(id => GetScore(sim, id).Total).ThenBy(id => id).First();

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
        if (Read(sim).IsGameOver) Fail("The game is over");
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

    public void CompleteProject(Simulation sim, int cityId, string project)
    {
        var city = sim.State.CityById(cityId);
        int team = sim.State.PlayerById(city.Owner).TeamId;
        var state = ReadTeam(sim, team);
        state.Completed[project] = state.Completed.GetValueOrDefault(project) + 1;
        sim.State.TeamById(team).Extensions.Set(sim.Content, state);
        sim.Events.Publish(new ProjectCompleted(cityId, city.Owner, team, project));
    }

    private static int Wonders(Simulation sim, Player player) => sim.State.Cities.Where(c => c.Owner == player.Id)
        .SelectMany(c => c.Buildings).Count(b => sim.Content.Get<BuildingDef>(b).Limits is { NoLimit: false, World: >= 0 });

    public PlayerScore GetScore(Simulation sim, int playerId)
    {
        var player = sim.State.PlayerById(playerId);
        var rule = sim.Content.Get<VictoryDef>().All.Where(v => v.Rule == "time").OrderBy(v => v.Id, StringComparer.Ordinal).FirstOrDefault();
        int population = sim.State.Cities.Where(c => c.Owner == playerId).Sum(c => c.Population);
        int land = sim.State.Plots.Count(p => p.Owner == playerId);
        int techs = sim.State.TeamOf(playerId).Techs.Count;
        int wonders = Wonders(sim, player);
        int total = population * (rule?.ScorePerPopulation ?? 0) + land * (rule?.ScorePerLand ?? 0)
            + techs * (rule?.ScorePerTech ?? 0) + wonders * (rule?.ScorePerWonder ?? 0);
        return new(population, land, techs, wonders, total);
    }

    private static int MaxTurns(Simulation sim, VictoryDef def) =>
        (int)((long)def.MaxTurns * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).VictoryDelayPercent / 100);

    public VictoryStatus GetVictoryStatus(Simulation sim)
    {
        var state = Read(sim);
        var time = Victories(sim).FirstOrDefault(v => v.Rule == "time");
        return new(state.IsGameOver, state.WinningTeamId, state.WinningPlayerId, state.VictoryId, state.TurnWon, time is null ? 0 : MaxTurns(sim, time));
    }

    public VictoryStatus Evaluate(Simulation sim, bool turnEnd)
    {
        if (Read(sim).IsGameOver) return GetVictoryStatus(sim);
        foreach (var def in Victories(sim))
        {
            int? winner = def.Rule switch
            {
                "conquest" => Conquest(sim),
                "domination" => Domination(sim, def),
                "cultural" => Cultural(sim, def),
                "project" => ProjectWinner(sim, def),
                "time" => turnEnd ? Time(sim, def) : null,
                _ => null,
            };
            if (winner is { } player) { Record(sim, def, player); break; }
        }
        return GetVictoryStatus(sim);
    }

    private static void Record(Simulation sim, VictoryDef def, int player)
    {
        int team = sim.State.PlayerById(player).TeamId;
        sim.State.Extensions.Set(sim.Content, new VictoryGameState
        { IsGameOver = true, WinningTeamId = team, WinningPlayerId = player, VictoryId = def.Id, TurnWon = sim.State.Turn });
        sim.Events.Publish(new VictoryAchieved(team, player, def.Id, sim.State.Turn));
    }

    private static bool Alive(Simulation sim, int player) =>
        sim.State.Cities.Any(c => c.Owner == player) || sim.State.Units.Any(u => u.Owner == player);

    private int? Conquest(Simulation sim)
    {
        var civs = Civilizations(sim).ToArray();
        if (civs.Select(p => p.TeamId).Distinct().Count() < 2) return null;
        var alive = civs.Where(p => Alive(sim, p.Id)).ToArray();
        return alive.Select(p => p.TeamId).Distinct().Count() == 1 ? BestMember(sim, alive.Select(p => p.Id)) : null;
    }

    private int? Domination(Simulation sim, VictoryDef def)
    {
        var civs = Civilizations(sim).ToArray();
        if (civs.Select(p => p.TeamId).Distinct().Count() < 2) return null;
        long totalPopulation = sim.State.Cities.Sum(c => (long)c.Population);
        var landPlots = sim.State.Plots.Where(p => !(sim.Content.TryGet<TerrainDef>(p.Terrain, out var t) && t.IsWater)).ToArray();
        if (totalPopulation == 0 || landPlots.Length == 0) return null;
        foreach (var team in TeamsByScore(sim))
        {
            var members = team.Select(p => p.Id).ToHashSet();
            long population = sim.State.Cities.Where(c => members.Contains(c.Owner)).Sum(c => (long)c.Population);
            long land = landPlots.Count(p => p.Owner is { } o && members.Contains(o));
            if (population * 100 >= totalPopulation * def.PopulationPercent && land * 100 >= landPlots.Length * def.LandPercent)
                return BestMember(sim, members);
        }
        return null;
    }

    private int? Cultural(Simulation sim, VictoryDef def)
    {
        if (def.CultureLevel is null || def.CultureCities <= 0) return null;
        long threshold = sim.Content.Get<CultureLevelDef>(def.CultureLevel).SpeedThresholds[sim.Settings.GameSpeed];
        foreach (var team in TeamsByScore(sim))
        {
            var members = team.Select(p => p.Id).ToHashSet();
            if (sim.State.Cities.Count(c => members.Contains(c.Owner) && c.Culture >= threshold) >= def.CultureCities) return BestMember(sim, members);
        }
        return null;
    }

    private int? ProjectWinner(Simulation sim, VictoryDef def)
    {
        var required = sim.Content.Get<ProjectDef>().All.Where(p => p.Spaceship && p.Victory == def.Id)
            .Select(p => (p.Id, Count: p.VictoryMinThresholds.TryGetValue(def.Id, out int min) ? min : p.VictoryThresholds.GetValueOrDefault(def.Id, 1))).ToArray();
        if (required.Length == 0) return null;
        var service = sim.Content.Services.Get<IProjectVictoryService>().Single();
        foreach (var team in TeamsByScore(sim))
            if (required.All(r => service.TeamCount(sim, team.Key, r.Id) >= r.Count)) return BestMember(sim, team.Select(p => p.Id));
        return null;
    }

    private int? Time(Simulation sim, VictoryDef def)
    {
        if (def.MaxTurns <= 0 || sim.State.Turn < MaxTurns(sim, def)) return null;
        var winningTeam = TeamsByScore(sim).FirstOrDefault(team => team.Any(p => Alive(sim, p.Id)));
        // An eliminated teammate can hold land and outscore the living, so only living members may win.
        return winningTeam is null ? null : BestMember(sim, winningTeam.Where(p => Alive(sim, p.Id)).Select(p => p.Id));
    }
}
