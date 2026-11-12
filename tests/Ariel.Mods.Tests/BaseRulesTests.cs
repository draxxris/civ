using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The base modules are the game: they must load and play with no mod installed.</summary>
public sealed class BaseRulesTests
{
    private static string RulesRoot => Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder);

    [Fact]
    public void Base_modules_build_and_play_turns_without_any_mod()
    {
        var sources = BaseRules.Sources(RulesRoot);
        Assert.Equal(BaseRules.Ids, sources.Select(s => s.Manifest.Id));
        var content = ModLoader.Build(sources);
        // A base module that named a mod as a content provider would fail the build above; this pins the list too.
        Assert.All(sources, s => Assert.All(s.Manifest.Dependencies.Concat(s.Manifest.References), id => Assert.True(BaseRules.IsBase(id), $"{s.Manifest.Id} requires mod '{id}'")));

        var sim = GameFactory.Create(content, seed: 3);
        for (int turn = 1; turn <= 5; turn++)
        {
            foreach (var player in sim.State.Players.Where(p => !p.EndedTurn).ToArray())
                Assert.True(sim.TrySubmit(new EndTurnCommand { Player = player.Id }, out var error), error);
        }
        Assert.Equal(6, sim.State.Turn);
    }

    [Fact]
    public void Staged_mods_cannot_claim_a_base_module_id()
    {
        var staged = GameContent.Staged(AppContext.BaseDirectory);
        Assert.Equal(BaseRules.Ids.Count, staged.Count(s => BaseRules.IsBase(s.Manifest.Id)));
        Assert.All(staged.Where(s => BaseRules.IsBase(s.Manifest.Id)), s => Assert.StartsWith(RulesRoot, s.Root));
    }
}
