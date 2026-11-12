using Ariel.Content;
using Ariel.Game;

namespace Ariel.BaseGame;

/// <summary>The test ruleset's answers to the kernel: one player per seat with a cycled civilization, the
/// civilization's starting techs and each category's default civic. Maps are the literal setting, with no free units
/// or sight to derive.</summary>
internal sealed class BaseRuleset : IRuleset
{
    public MapSpec ResolveMap(ContentDb content, SimulationSettings settings) => settings.Map;

    public void CreatePlayers(Simulation sim)
    {
        var civs = sim.Content.Get<CivilizationDef>().All.ToList();
        for (int i = 0; i < sim.Settings.PlayerCount; i++) AddPlayer(sim, i, ChooseCivilization(sim, i, civs));
    }

    public void Complete(Simulation sim) { }

    public void CompleteScenario(Simulation sim) { }

    public void Validate(Simulation sim)
    {
        foreach (var plot in sim.State.Plots) _ = sim.Content.Get<TerrainDef>(plot.Terrain);
        foreach (var unit in sim.State.Units) _ = sim.Content.Get<UnitDef>(unit.Type);
    }

    public bool OptionDefault(ContentDb content, string option) => false;

    public bool IsScout(Simulation sim, Unit unit) => sim.Content.TryGet<UnitDef>(unit.Type, out var def) && def.CanFoundCity;

    /// <summary>The configured civilization for a seat, else content's civilizations in ordinal id order, cycled.</summary>
    private static CivilizationDef? ChooseCivilization(Simulation sim, int player, IReadOnlyList<CivilizationDef> civs)
    {
        var chosen = sim.Settings.Civilizations;
        if (player < chosen.Length) return sim.Content.Get<CivilizationDef>(chosen[player]);
        return civs.Count == 0 ? null : civs[player % civs.Count];
    }

    private static void AddPlayer(Simulation sim, int id, CivilizationDef? civ)
    {
        var settings = sim.Settings;
        var ai = id < settings.PlayerAis.Length ? settings.PlayerAis[id] : null;
        if (ai != null) sim.Content.PlayerAIs.Get(ai);
        int teamId = id < settings.PlayerTeams.Length ? settings.PlayerTeams[id] : id;
        if (teamId < 0) throw new ArgumentException("Team ids must be nonnegative");
        var team = GetOrCreateTeam(sim.State, teamId);
        team.Members.Add(id);
        team.Techs.UnionWith(civ?.StartingTechs ?? []);
        var player = new Player
        {
            Id = id, TeamId = teamId, Name = civ?.Name ?? $"Player {id + 1}", Civilization = civ?.Id ?? "", Ai = ai,
        };
        foreach (var civic in sim.Content.Get<CivicDef>().All.Where(c => c.IsDefault)) player.Civics.TryAdd(civic.Category, civic.Id);
        sim.State.Players.Add(player);
    }

    private static Team GetOrCreateTeam(GameState state, int id)
    {
        var team = state.Teams.FirstOrDefault(t => t.Id == id);
        if (team != null) return team;
        team = new Team { Id = id };
        state.Teams.Add(team);
        return team;
    }
}

/// <summary>Lookups the test ruleset's rules share.</summary>
public static class RulesetContent
{
    /// <summary>Terrain definition on a valid plot, including normalized wrap coordinates.</summary>
    public static TerrainDef TerrainAt(this Simulation sim, Hex hex) => sim.Content.Get<TerrainDef>(sim.State.PlotAt(hex).Terrain);

    /// <summary>Civilization definition for a player, or null when the player has none.</summary>
    public static CivilizationDef? CivilizationOf(this Simulation sim, int playerId)
    {
        var id = sim.State.PlayerById(playerId).Civilization;
        return id.Length == 0 ? null : sim.Content.Get<CivilizationDef>(id);
    }
}
