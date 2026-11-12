using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Tests;

/// <summary>The smallest ruleset a hand-made content set needs to start a game without the base modules: the map is
/// the literal setting, every seat gets a player on its own team with a cycled civilization, and options read their
/// content default. Tests that probe one contract against custom content register it beside their definitions.</summary>
internal sealed class ProbeRuleset : IRuleset
{
    /// <summary>Register the probe ruleset as the content set's only ruleset.</summary>
    public static void Register(IModContext context)
    {
        // Players and plots are checked against these kinds, so they exist even when the content set defines none.
        _ = context.Content<CivilizationDef>();
        _ = context.Content<TerrainDef>();
        context.RegisterService<IRuleset>("ruleset", new ProbeRuleset());
    }

    public MapSpec ResolveMap(ContentDb content, SimulationSettings settings) => settings.Map;

    public void CreatePlayers(Simulation sim)
    {
        var civs = sim.Content.Get<CivilizationDef>().All.ToList();
        for (int id = 0; id < sim.Settings.PlayerCount; id++)
        {
            var civ = civs.Count == 0 ? null : civs[id % civs.Count];
            var team = new Team { Id = id };
            team.Members.Add(id);
            sim.State.Teams.Add(team);
            sim.State.Players.Add(new Player
            {
                Id = id, TeamId = id, Name = civ?.Name ?? $"Player {id + 1}", Civilization = civ?.Id ?? "",
            });
        }
    }

    public void Complete(Simulation sim) { }

    public void CompleteScenario(Simulation sim) { }

    public void Validate(Simulation sim)
    {
        foreach (var plot in sim.State.Plots) _ = sim.Content.Get<TerrainDef>(plot.Terrain);
    }

    public bool OptionDefault(ContentDb content, string option) =>
        content.TryGet<GameOptionDef>(option, out var definition) && definition.Default;

    public bool IsScout(Simulation sim, Unit unit) => false;
}
