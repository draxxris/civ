using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Tests;

/// <summary>Headless full-ruleset harness, including GreatPeople and Trade from LoadedMods' payload discovery.
/// Frozen content is shared; state and RNG are never shared.</summary>
public static class GameFactory
{
    public static Simulation Create(ContentDb content, ulong seed = 1, params string[] leaders) =>
        new(content, Settings(seed, leaders));

    public static SimulationSettings Settings(ulong seed = 1, params string[] leaders)
    {
        if (leaders.Length == 0) leaders = ["peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar"];
        return new()
        {
            Seed = seed, WorldSize = "foundation:worldsize_standard", MapGenerator = "world:continents",
            GameSpeed = "foundation:gamespeed_normal", Handicap = "foundation:handicap_noble",
            StartingEra = "foundation:era_ancient", PlayerCount = leaders.Length, Leaders = leaders,
        };
    }
    /// <summary>Headless AI game with the same setup defaults as the normal feature harness.</summary>
    public static Simulation CreateAi(ContentDb content, ulong seed = 1, int players = 4,
        string world = "foundation:worldsize_small") => new(content, new SimulationSettings
    {
        Seed = seed, WorldSize = world, MapGenerator = "world:continents",
        GameSpeed = "foundation:gamespeed_normal", Handicap = "foundation:handicap_noble",
        StartingEra = "foundation:era_ancient", PlayerCount = players,
        Leaders = Enumerable.Range(0, players).Select(i => new[]
        {
            "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar",
            "peoples:leader_bismarck", "peoples:leader_gandhi",
        }[i % 4]).ToArray(),
        PlayerAis = Enumerable.Repeat<string?>("ai:standard", players).ToArray(),
    });

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
    public static Simulation Scenario(ContentDb content) => Simulation.CreateScenario(content, Settings(1, []),
        new(12, 8, true), Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass", Owner = 0 }).ToArray(),
        sim =>
        {
            foreach (var team in sim.State.Teams) team.Techs.Clear();
        });
}
