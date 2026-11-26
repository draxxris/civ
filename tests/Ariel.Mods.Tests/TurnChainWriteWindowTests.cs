using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The per-player turn steps that write a few flags declare them: puppet work writes
/// <see cref="CityStateDomains.Work"/>, the draft reset <see cref="CityStateDomains.Drafts"/> and defense recovery
/// <see cref="CityStateDomains.Defense"/>. Computer games rarely set these fields, so a turn here sets every one and
/// verification checks each step wrote only what it declares.</summary>
public sealed class TurnChainWriteWindowTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Puppet_work_draft_reset_and_defense_recovery_write_only_their_declared_domains()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var puppet = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new(39)) };
        var bombarded = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new(44)) };
        sim.State.AddCity(puppet);
        sim.State.AddCity(bombarded);
        var puppetState = new CityRuleState { Governance = CityGovernance.Puppet, ManualWork = true, DraftedThisTurn = true };
        puppet.Extensions.Set(sim.Content, puppetState);
        var bombardedState = new CityRuleState { DefenseDamage = 40, BombardedThisTurn = true };
        bombarded.Extensions.Set(sim.Content, bombardedState);
        var owner = sim.State.PlayerById(0);
        owner.Extensions.Set(sim.Content, new CityPlayerState { ConscriptCountThisTurn = 2 });
        sim.VerifyWrites = true;

        sim.AdvanceTurn();

        Assert.False(puppetState.ManualWork);
        Assert.False(puppetState.DraftedThisTurn);
        Assert.Equal(0, owner.Extensions.Get<CityPlayerState>(sim.Content)!.ConscriptCountThisTurn);
        Assert.False(bombardedState.BombardedThisTurn);
        // Bombarded this turn: no recovery yet, only the flag clears.
        Assert.Equal(40, bombardedState.DefenseDamage);
        sim.AdvanceTurn();
        Assert.True(bombardedState.DefenseDamage < 40);
    }
}
