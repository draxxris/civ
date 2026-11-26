using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Computer players play a game on content that lacks a feature mod, for each mod whose absence the base game
/// must survive. The cheap checks that such content holds nothing of the missing mod live with each mod's own tests;
/// this plays the games, which take tens of turns each, so it runs with the end-to-end tests.</summary>
public sealed class AbsentModGameTests
{
    private static string Rules => Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder);

    private static ContentDb Without(string mod) => ModLoader.Build(new LoadedMods().Sources.Where(s => s.Manifest.Id != mod));

    [Fact]
    [Trait("Category", "E2E")]
    public void The_ai_plays_on_without_each_feature_mod()
    {
        // The shipped mods without AirCombat: no aircraft is ever trained.
        var game = Play(Without("air_combat"), seed: 5, players: 2, turns: 30, techs: false);
        Assert.DoesNotContain(game.State.Units, u => game.Content.Get<S.UnitDef>(u.Type).Domain == "military:domain_air");

        // Without Barbarians: no hostile player appears, and huts still pay out, but never with hostile villagers, who
        // would need a hostile faction to belong to.
        var goodies = new List<GoodyReceived>();
        game = Play(Without("barbarians"), seed: 5, players: 2, turns: 30, techs: false,
            turn => goodies.AddRange(turn.Events.PreviousTurn.OfType<GoodyReceived>()));
        Assert.Null(GameFactory.BarbarianPlayer(game));
        Assert.Equal(2, game.State.Players.Count);
        Assert.All(goodies, g => Assert.Null(game.Content.Get<S.GoodyDef>(g.Goody).BarbarianUnit));

        // Without Religion.
        Play(Without("religion"), seed: 5, players: 2, turns: 30, techs: false);

        // The base game and the planner probe, with no feature mod at all.
        Play(GameContent.Build(Rules, ModLoader.FromDirectories([Path.Combine(AppContext.BaseDirectory, "test-mods", "AiPlanner")])),
            seed: 5, players: 2, turns: 30, techs: true);

        // The base game alone.
        Play(GameContent.Build(Rules, []), seed: 3, players: 3, turns: 40, techs: true);
    }

    /// <summary>Plays an AI game for the given turns; every computer player must hold a city at the end, and with
    /// <paramref name="techs"/> must have researched something.</summary>
    private static Simulation Play(ContentDb content, ulong seed, int players, int turns, bool techs, Action<Simulation>? afterTurn = null)
    {
        var sim = GameFactory.CreateAi(content, seed: seed, players: players);
        for (int turn = 0; turn < turns; turn++)
        {
            sim.AdvanceTurn();
            afterTurn?.Invoke(sim);
        }
        foreach (var player in sim.State.Players.Where(p => p.Ai == "ai:standard"))
        {
            Assert.True(sim.State.Cities.Count(c => c.Owner == player.Id) >= 1, $"player {player.Id} has no city");
            if (techs) Assert.True(sim.TeamOf(player.Id).Techs.Count > 0, $"player {player.Id} researched nothing");
        }
        return sim;
    }
}
