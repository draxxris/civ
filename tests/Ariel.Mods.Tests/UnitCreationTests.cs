using System.Collections.Immutable;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary><see cref="IUnitService.Create"/>, the one way rules and mods bring a unit into play: the id, the
/// initialisation, the caller's adjustments, sight and contacts, then the event.</summary>
public sealed class UnitCreationTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Scout = "creation_probe:scout";
    private const string FarSight = "creation_probe:far_sight";

    private Simulation Scenario()
    {
        var probe = new ModSource
        {
            Root = mods.Root, Manifest = new() { Id = "creation_probe", Version = "1", References = ["military"] },
            Instance = new Probe(),
        };
        return GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(probe)));
    }

    [Fact]
    public void A_created_unit_has_its_id_free_promotions_and_full_movement_before_the_caller_adjusts_it()
    {
        var sim = Scenario(); var units = sim.Rules().Units;
        int id = sim.State.NextUnitId; var position = sim.State.Grid.HexOf(new(40));
        bool prepared = false;
        var unit = units.Create(sim, 0, Scout, position, u =>
        {
            prepared = true;
            Assert.Contains(FarSight, u.Promotions);
            Assert.Equal(units.MaximumMoves(sim, u.Id), u.MovesLeft);
            Assert.DoesNotContain(sim.Events.Published.OfType<UnitCreated>(), e => e.Unit == u.Id);
            u.Experience = 7;
        });
        Assert.True(prepared);
        Assert.Equal(id, unit.Id); Assert.Equal(id + 1, sim.State.NextUnitId);
        Assert.Same(unit, sim.State.UnitById(id)); Assert.Equal(position, unit.Position);
        Assert.Equal(3 * sim.Content.Get<S.RuleConstantsDef>().All.Single().Values.MoveDenominator, units.MaximumMoves(sim, unit.Id));
        Assert.Equal(units.MaximumMoves(sim, unit.Id), unit.MovesLeft);
        Assert.Equal(7, unit.Experience);
        Assert.Equal(sim.State.Turn, unit.Extensions.Get<MilitaryUnitState>(sim.Content)!.CreatedTurn);
        Assert.Equal(new UnitCreated(id, 0, Scout, position), sim.Events.Published.OfType<UnitCreated>().Single(e => e.Unit == id));
    }

    [Fact]
    public void A_caller_can_hand_over_a_spent_unit()
    {
        var sim = Scenario();
        var unit = sim.Rules().Units.Create(sim, 0, Scout, sim.State.Grid.HexOf(new(40)), u => u.MovesLeft = 0);
        Assert.Equal(0, unit.MovesLeft);
    }

    [Fact]
    public void Sight_with_free_promotions_and_contacts_are_settled_before_the_unit_is_announced()
    {
        var sim = Scenario(); var grid = sim.State.Grid;
        int team = sim.TeamOf(1).Id, other = sim.TeamOf(0).Id;
        foreach (var plot in sim.State.Plots) { plot.Owner = null; plot.Visible.Remove(team); }
        // Three tiles off: neither neighbours nor in plain sight, only in the far sight the free promotion gives.
        var far = grid.HexOf(new(43));
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = far });
        Assert.Equal(3, grid.Distance(new TileId(40), new TileId(43)));
        Assert.False(sim.Rules().Relations.HasMet(sim, team, other));
        sim.Rules().Units.Create(sim, 1, Scout, grid.HexOf(new(40)));
        Assert.True(sim.State.Plots[43].Visible.Contains(team));
        Assert.True(sim.Rules().Relations.HasMet(sim, team, other));
    }

    private sealed class Probe : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.PromotionDef>().Register(new() { Id = FarSight, VisibilityChange = 2 });
            context.Content<S.UnitDef>().Register(new()
            {
                Id = Scout, DefaultUnit = Scout, Moves = 3, Domain = "military:domain_land",
                FreePromotions = ImmutableDictionary<string, bool>.Empty.Add(FarSight, true),
            });
        }
    }
}
