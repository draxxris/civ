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
    public void The_shipped_mods_offer_artillery_and_great_people_orders_through_sources()
    {
        Assert.True(mods.Content.Rules().UnitActions.Count >= 2);
        Assert.Equal(2, mods.Content.Rules().SupportFire.Count);
    }

    [Fact]
    public void Without_the_ranged_combat_mod_no_fire_is_offered_and_an_ai_war_still_plays()
    {
        var content = ModLoader.Build(mods.Sources.Where(s => s.Manifest.Id != "ranged_combat").Reverse());
        Assert.Empty(content.Rules().SupportFire);
        Assert.False(content.CommandTypes.Contains("ranged_combat:bombard_city"));

        var sim = GameFactory.Scenario(content);
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Grid.HexOf(new(40)), Population = 3 };
        city.Buildings.Add("cities:walls"); sim.State.AddCity(city);
        var siege = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:catapult", Position = sim.State.Grid.HexOf(new(39)), MovesLeft = 60 };
        sim.State.AddUnit(siege);
        sim.State.SetWar(0, 1, true);
        Assert.DoesNotContain(new GameSession(sim).DescribeUnit(siege)!.Actions, a => a.Id.StartsWith("bombard:", StringComparison.Ordinal));
        for (int turn = 0; turn < 3; turn++) sim.AdvanceTurn();
        Assert.Empty(sim.Events.Published.OfType<CityBombarded>());
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
