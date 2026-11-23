using Ariel.Client;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The experimental parallel AI planning preference: off by default, kept on this computer, and never a
/// change to what a game does.</summary>
public sealed class ParallelAiPlanningTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static string TempFile() =>
        Path.Combine(Directory.CreateDirectory("/tmp/ariel").FullName, $"preferences-{Guid.NewGuid():N}.json");

    private NewGameOptions DuelOptions() =>
        mods.Catalog.DefaultOptions() with { WorldSize = "foundation:worldsize_duel", Opponents = 1 };

    [Fact]
    public void It_is_off_by_default_and_plays_every_AI_in_turn()
    {
        var preferences = new ClientPreferences();
        Assert.False(preferences.ParallelAiPlanning);
        Assert.Equal(Simulation.SequentialAiWaveLimit, preferences.AiWaveLimit);
        Assert.Equal(1, GameFactory.CreateAi(mods.Content).AiWaveLimit);
        Assert.Equal(1, GameSession.Start(mods.Catalog, DuelOptions()).Sim.AiWaveLimit);
    }

    [Fact]
    public void It_is_exposed_as_an_experimental_setting_with_help()
    {
        Assert.Contains("experimental", ClientPreferences.ParallelAiPlanningLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("usually slower", ClientPreferences.ParallelAiPlanningHelp);
        Assert.Contains("never changes the outcome", ClientPreferences.ParallelAiPlanningHelp);
        // It is a preference of this computer, not one of the catalog's game options that a save or a lobby carries.
        Assert.DoesNotContain(mods.Catalog.GameOptions, o => o.Name.Contains("parallel", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false, 64, 1)]
    [InlineData(true, 1, 2)]
    [InlineData(true, 8, 2)]
    [InlineData(true, 16, 4)]
    [InlineData(true, 128, 4)]
    public void The_width_is_sequential_when_off_and_a_small_bound_when_on(bool on, int processors, int width) =>
        Assert.Equal(width, Simulation.AiWaveLimitFor(on, processors));

    [Fact]
    public void The_preference_persists_and_a_missing_or_damaged_file_means_off()
    {
        string path = TempFile();
        Assert.False(ClientPreferences.Load(path).ParallelAiPlanning);
        new ClientPreferences { ParallelAiPlanning = true }.Save(path);
        Assert.True(ClientPreferences.Load(path).ParallelAiPlanning);
        File.WriteAllText(path, "{ not json");
        Assert.False(ClientPreferences.Load(path).ParallelAiPlanning);
        File.Delete(path);
    }

    [Fact]
    public void The_width_follows_the_session_through_a_load_and_stays_out_of_saves_and_hashes()
    {
        var wide = GameSession.Start(mods.Catalog, DuelOptions(), aiWaveLimit: 4);
        var narrow = GameSession.Start(mods.Catalog, DuelOptions());
        Assert.Equal(4, wide.Sim.AiWaveLimit);
        Assert.Equal(narrow.Sim.ComputeHash(), wide.Sim.ComputeHash());
        string save = SaveGame.Save(wide.Sim);
        Assert.Equal(SaveGame.Save(narrow.Sim), save);
        Assert.Equal(1, SaveGame.Load(mods.Content, save).AiWaveLimit);

        Assert.True(wide.LoadFromJson(save));
        Assert.Equal(4, wide.Sim.AiWaveLimit);
        Assert.Equal(narrow.Sim.ComputeHash(), wide.Sim.ComputeHash());
    }

    [Fact]
    public void Four_AI_players_give_identical_hashes_for_thirty_turns_with_it_on_across_a_reload()
    {
        var sequential = GameFactory.CreateAi(mods.Content, seed: 31);
        var parallel = GameFactory.CreateAi(mods.Content, seed: 31);
        parallel.AiWaveLimit = Simulation.AiWaveLimitFor(true, 16);
        Assert.Equal(1, sequential.AiWaveLimit);
        Assert.Equal(4, parallel.AiWaveLimit);
        for (int turn = 0; turn < 30; turn++)
        {
            Assert.Equal(sequential.ComputeHash(), parallel.ComputeHash());
            if (turn == 15)
            {
                parallel = SaveGame.Load(mods.Content, SaveGame.Save(parallel));
                Assert.Equal(1, parallel.AiWaveLimit); // not saved
                parallel.AiWaveLimit = 4;
                Assert.Equal(sequential.ComputeHash(), parallel.ComputeHash());
            }
            sequential.AdvanceTurn(); parallel.AdvanceTurn();
        }
        Assert.Equal(sequential.ComputeHash(), parallel.ComputeHash());
    }
}
