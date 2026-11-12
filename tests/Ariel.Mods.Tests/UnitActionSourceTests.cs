using Ariel.Client;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A mod adds orders to the unit panel by registering a source; the client needs no change to offer them.</summary>
public sealed class UnitActionSourceTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void A_registered_source_adds_its_valid_orders_to_the_panel_and_they_run()
    {
        var probe = new ModSource
        {
            Root = mods.Root, Manifest = new() { Id = "order_probe", Version = "1", Dependencies = ["military"] }, Instance = new Probe(),
        };
        var session = new GameSession(GameFactory.Create(ModLoader.Build(mods.Sources.Append(probe)), 3));
        var unit = session.Sim.State.Units.First(u => u.Owner == session.LocalPlayer);
        session.SelectUnit(unit.Id);

        var actions = session.DescribeUnit(unit)!.Actions;
        var offered = Assert.Single(actions, a => a.Id.StartsWith("order_probe:", StringComparison.Ordinal));
        Assert.Equal("Rest", offered.Label);
        Assert.Equal("From a mod", offered.Tooltip);

        session.RunAction("order_probe:rest");
        Assert.Equal(UnitActivity.Sleep, unit.Extensions.Get<MilitaryUnitState>(session.Sim.Content)!.Activity);
    }

    [Fact]
    public void The_base_game_offers_artillery_and_great_people_orders_through_sources()
    {
        Assert.True(mods.Content.Rules().UnitActions.Count >= 2);
        Assert.Equal(2, mods.Content.Rules().SupportFire.Count);
    }

    private sealed class Probe : IMod
    {
        public void Register(IModContext context) => context.RegisterService<IUnitActionSource>("orders", new Orders());
    }

    private sealed class Orders : IUnitActionSource
    {
        public IEnumerable<UnitActionOffer> Offers(Simulation sim, int player, Unit unit)
        {
            yield return new("order_probe:rest", "Rest",
                new SetUnitActivityCommand { Player = player, Unit = unit.Id, Activity = UnitActivity.Sleep }, "From a mod");
            // Another player's order fails validation, so the panel must leave it out.
            yield return new("order_probe:not_mine", "Not mine",
                new SetUnitActivityCommand { Player = player + 1, Unit = unit.Id, Activity = UnitActivity.Sleep });
        }
    }
}
