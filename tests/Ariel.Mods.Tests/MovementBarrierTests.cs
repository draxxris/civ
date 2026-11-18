using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A mod bars units from plots by registering an <see cref="IMovementBarrier"/>; base movement asks it for
/// every unit's move, not only barbarians', and never for an attack.</summary>
public sealed class MovementBarrierTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void A_registered_barrier_bars_a_civilization_unit_from_a_plot_it_could_otherwise_enter_but_not_an_attack()
    {
        var barrier = new PlotBarrier();
        var probe = new ModSource
        {
            Root = mods.Root, Manifest = new() { Id = "barrier_probe", Version = "1", Dependencies = ["military"] },
            Instance = new Probe(barrier),
        };
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(probe)));
        Assert.Contains(barrier, sim.Rules().MovementBarriers);
        var grid = sim.State.Grid;
        var movement = sim.Rules().Movement;
        var start = new TileId(40);
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Position = grid.HexOf(start), Type = "military:warrior", MovesLeft = 60 };
        sim.State.AddUnit(unit);
        Assert.False(PlayerRules.IsBarbarian(sim, unit.Owner));

        var open = grid.Neighbors(start).First(t => movement.CanEnter(sim, unit.Id, start, t) is null);
        barrier.Barred = grid.HexOf(open);
        Assert.Equal("Movement barred", movement.CanEnter(sim, unit.Id, start, open));
        Assert.NotEqual("Movement barred", movement.CanEnter(sim, unit.Id, start, open, attack: true));
        Assert.Contains(barrier.Asked, a => a == unit.Id);
    }

    private sealed class Probe(PlotBarrier barrier) : IMod
    {
        public void Register(IModContext context) => context.RegisterService<IMovementBarrier>("plot", barrier);
    }

    private sealed class PlotBarrier : IMovementBarrier
    {
        public Hex? Barred { get; set; }
        public List<int> Asked { get; } = [];
        public bool Blocks(Simulation sim, int unit, Hex target)
        {
            Asked.Add(unit);
            return target == Barred;
        }
    }
}
