using Civ.Content;
using Civ.Game;

namespace Civ.Mods.Tests;

/// <summary>Headless full-ruleset harness. Frozen content is shared; state and RNG are never shared.</summary>
public static class GameFactory
{
    public static Simulation Create(ContentDb content, ulong seed = 1, params string[] leaders)
    {
        if (leaders.Length == 0) leaders = ["peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar"];
        return new(content, new SimulationSettings
        {
            Seed = seed, WorldSize = "foundation:worldsize_standard", MapGenerator = "world:continents",
            GameSpeed = "foundation:gamespeed_normal", Handicap = "foundation:handicap_noble",
            StartingEra = "foundation:era_ancient", PlayerCount = leaders.Length, Leaders = leaders,
        });
    }
    /// <summary>Remove political inputs from fixtures that isolate another rules service.</summary>
    public static void NeutralPolitics(Simulation sim)
    {
        sim.State.Players = sim.State.Players.Select(p => new Player
        {
            Id = p.Id, TeamId = p.TeamId, Civilization = p.Civilization, Name = p.Name,
            Treasury = p.Treasury, Ai = p.Ai, StartingPlot = p.StartingPlot,
        }).ToList();
        sim.State.RebuildIndexes();
    }
    public static Simulation Scenario(ContentDb content)
    {
        var sim = Create(content);
        sim.State.Map = new(12, 8, true);
        sim.State.Plots = Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass", Owner = 0 }).ToArray();
        sim.State.Units.Clear();
        sim.State.NextUnitId = 1;
        foreach (var player in sim.State.Players) player.StartingPlot = null;
        foreach (var team in sim.State.Teams) team.Techs.Clear();
        sim.State.RebuildIndexes();
        return sim;
    }
}
