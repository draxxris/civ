using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>What a unit move declares it writes (<see cref="UnitStateDomains.MoveWrites"/>), and when it writes more:
/// a first reveal widens it by <see cref="UnitStateDomains.Revealed"/> only, and moving a blockading unit widens it to
/// everything, because readers of city ledgers see units through blockades.</summary>
public sealed class MoveWriteWindowTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    /// <summary>Kernel work around the moves, so that write stamps persist between them; it edits plots directly, so it
    /// declares nothing.</summary>
    private sealed class Script(Action<Simulation> body) : GameCommand
    {
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim) => body(sim);
    }

    private static Unit Add(Simulation sim, int tile)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = sim.State.Grid.HexOf(new(tile)) };
        sim.State.AddUnit(unit);
        sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
        return unit;
    }

    private static bool Move(Simulation sim, Unit unit, int tile, out long stamp)
    {
        stamp = sim.WriteStamp;
        unit.MovesLeft = 300;
        return sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = unit.Id, Path = [sim.State.Grid.HexOf(new(tile))] }, out _);
    }

    [Fact]
    public void A_move_writes_units_and_sight_widens_by_reveals_and_widens_fully_for_a_blockader()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.VerifyWrites = true;
        var unit = Add(sim, 39);
        var domains = sim.Content.StateDomains;
        ulong moves = domains.Mask(UnitStateDomains.MoveWrites, out bool unknown);
        Assert.False(unknown);
        ulong revealed = domains.Mask(UnitStateDomains.RevealedOnly, out _);
        int team = sim.TeamOf(0).Id;
        Assert.True(sim.TryApply(new Script(s =>
        {
            // Every plot already revealed: the move writes only its declared domains, as verification also checks.
            foreach (var plot in s.State.Plots) plot.Revealed.Add(team);
            Assert.True(Move(s, unit, 40, out long stamp));
            Assert.False(s.WrittenSince(moves, stamp));

            // A plot seen for the first time widens the window by reveals and nothing else.
            foreach (var plot in s.State.Plots) plot.Revealed.Remove(team);
            Assert.True(Move(s, unit, 41, out stamp));
            Assert.True(s.WrittenSince(moves, stamp));
            Assert.False(s.WrittenSince(moves | revealed, stamp));

            // Moving a blockading unit ends its blockade, which the resource network reads: everything counts as written.
            foreach (var plot in s.State.Plots) plot.Revealed.Add(team);
            unit.Extensions.Get<MilitaryUnitState>(s.Content)!.Activity = UnitActivity.Blockade;
            Assert.True(Move(s, unit, 42, out stamp));
            Assert.True(s.WrittenSince(domains.AllMask, stamp));
        }) { Player = 0 }, out var error), error);
    }
}
