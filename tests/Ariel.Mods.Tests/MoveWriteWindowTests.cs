using Ariel.Content;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>What a unit move declares it writes (<see cref="UnitStateDomains.MoveWrites"/>), and when it writes more:
/// a first reveal widens it by <see cref="UnitStateDomains.Revealed"/> only, and moving a blockading unit widens it to
/// everything, because readers of city ledgers see units through blockades. The unit commands the computer players
/// issue every turn keep the same contract: a promotion writes units and sight, and an activity writes units, widening
/// fully when a blockade starts or ends. An upgrade also writes the treasury and the unit list's order, and widens
/// fully for a blockader.</summary>
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

    [Fact]
    public void A_promotion_writes_units_and_sight_and_an_activity_widens_fully_only_for_a_blockade()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.VerifyWrites = true;
        var unit = Add(sim, 39);
        var domains = sim.Content.StateDomains;
        ulong unitsAndSight = domains.Mask(UnitStateDomains.UnitsAndSight, out bool unknown);
        Assert.False(unknown);
        ulong units = domains.Mask(UnitStateDomains.UnitsOnly, out _);
        int team = sim.TeamOf(0).Id;
        Assert.True(sim.TryApply(new Script(s =>
        {
            // Every plot already revealed, so the promotion's sight finds nothing new: it writes only its domains, as
            // verification also checks, promotions included.
            foreach (var plot in s.State.Plots) plot.Revealed.Add(team);
            unit.Experience = 2;
            long stamp = s.WriteStamp;
            Assert.True(s.TryApply(new PromoteMilitaryUnitCommand { Player = 0, Unit = unit.Id, Promotion = "military:combat1" }, out var error), error);
            Assert.Contains("military:combat1", unit.Promotions);
            Assert.False(s.WrittenSince(unitsAndSight, stamp));

            stamp = s.WriteStamp;
            Assert.True(s.TryApply(new SetUnitActivityCommand { Player = 0, Unit = unit.Id, Activity = UnitActivity.Sleep }, out error), error);
            Assert.False(s.WrittenSince(units, stamp));

            // Ending a blockade changes what the resource network reads: everything counts as written.
            unit.Extensions.Get<MilitaryUnitState>(s.Content)!.Activity = UnitActivity.Blockade;
            stamp = s.WriteStamp;
            Assert.True(s.TryApply(new SetUnitActivityCommand { Player = 0, Unit = unit.Id, Activity = UnitActivity.Sleep }, out error), error);
            Assert.True(s.WrittenSince(domains.AllMask, stamp));
        }) { Player = 0 }, out var failure), failure);
    }

    /// <summary>The upgrade's rule run inside a window that declares <paramref name="declared"/>, as the command's own
    /// window would; verification throws when it closes if the rule wrote anything else.</summary>
    private static void Upgrade(Simulation sim, Unit unit, IReadOnlyList<string> declared)
    {
        using (sim.OpenWriteWindow(declared, "upgrade"))
            sim.Content.Services.Get<IUnitService>().Single().Execute(sim, unit.Id, UnitAction.Upgrade, "military:axeman");
    }

    [Fact]
    public void An_upgrade_writes_the_unit_sight_treasury_and_unit_order_and_widens_fully_for_a_blockader()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.VerifyWrites = true;
        var first = Add(sim, 39);
        var second = Add(sim, 40);
        var third = Add(sim, 41);
        var domains = sim.Content.StateDomains;
        ulong upgrade = domains.Mask(UnitStateDomains.UpgradeWrites, out bool unknown);
        Assert.False(unknown);
        Assert.Equal(new UpgradeUnitCommand().WriteDomains, UnitStateDomains.UpgradeWrites);
        IReadOnlyList<string> withoutOrder = UnitStateDomains.UpgradeWrites.Where(d => d != KernelStateDomains.UnitOrder).ToArray();
        int team = sim.TeamOf(0).Id;
        sim.State.PlayerById(0).Treasury = 10_000;
        Assert.True(sim.TryApply(new Script(s =>
        {
            foreach (var plot in s.State.Plots) plot.Revealed.Add(team);
            // Experience above the cap is cut, which writes the unit's experience as well as its type.
            first.Experience = 1_000;
            long stamp = s.WriteStamp;
            Upgrade(s, first, UnitStateDomains.UpgradeWrites);
            var upgraded = s.State.UnitById(first.Id);
            Assert.Equal("military:axeman", upgraded.Type);
            Assert.True(upgraded.Experience < 1_000);
            Assert.Equal([second.Id, third.Id, first.Id], s.State.Units.Select(u => u.Id).ToArray());
            Assert.False(s.WrittenSince(upgrade, stamp));

            // Without the order, moving the unit to the end of the list is a write outside the declaration.
            var moved = Assert.Throws<InvalidOperationException>(() => Upgrade(s, s.State.UnitById(second.Id), withoutOrder));
            Assert.Contains("upgrade", moved.Message);

            // The order domain holds the list's order only, never its membership: a disband declared with it fails.
            var removed = Assert.Throws<InvalidOperationException>(() =>
            {
                using (s.OpenWriteWindow(UnitStateDomains.UpgradeWrites, "disband"))
                    s.Content.Services.Get<IUnitService>().Single().Execute(s, third.Id, UnitAction.Disband, "");
            });
            Assert.Contains("disband", removed.Message);
        }) { Player = 0 }, out var error), error);

        // A blockade reads its unit's type, so upgrading a blockading unit counts as writing everything.
        var blockader = Add(sim, 42);
        Assert.True(sim.TryApply(new Script(s =>
        {
            blockader.Extensions.Get<MilitaryUnitState>(s.Content)!.Activity = UnitActivity.Blockade;
            long stamp = s.WriteStamp;
            Upgrade(s, blockader, UnitStateDomains.UpgradeWrites);
            Assert.True(s.WrittenSince(domains.AllMask, stamp));
        }) { Player = 0 }, out error), error);
    }
}
