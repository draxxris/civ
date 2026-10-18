using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class PillageModifierTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Theory]
    [InlineData(-33)]
    [InlineData(-100)]
    [InlineData(-150)]
    public void Negative_pillage_modifiers_preserve_signed_truncation_treasury_and_replay(int percent)
    {
        var source = new ModSource { Root = mods.Root, Manifest = new() { Id = "pillage_probe", Version = "1", References = ["military", "world"] }, Instance = new Probe(percent) };
        var content = ModLoader.Build(mods.Sources.Append(source));
        var sim = GameFactory.Scenario(content);
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = sim.State.Grid.HexOf(new(39)) };
        sim.State.AddUnit(unit);
        content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        unit.Promotions.Add("pillage_probe:penalty");
        sim.State.SetWar(0, 1, true);
        var plot = sim.State.PlotAt(unit.Position);
        plot.Owner = 1; plot.Improvement = "pillage_probe:deposit";
        plot.Route = "world:route_route_road";
        long treasury = sim.State.PlayerById(0).Treasury = 100;
        var (w, x, y, z) = sim.Rng.State;
        var expectedRng = DeterministicRng.Restore(w, x, y, z);
        int raw = expectedRng.Next(101) + expectedRng.Next(101);
        Assert.True(raw > 0);
        Assert.NotEqual(0, raw % 100); // Distinguishes signed truncation from scaling the final multiplier.
        int expected = raw + raw * percent / 100;
        var loaded = SaveGame.Load(content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, loaded })
        {
            Assert.True(game.TryApply(new PillageCommand { Player = 0, Unit = unit.Id }, out var error), error);
            var message = Assert.Single(game.Events.Published.OfType<PlotPillaged>());
            Assert.Equal(new PlotPillaged(unit.Id, 0, new(39), "pillage_probe:deposit", "world:city_ruins", null, expected), message);
            Assert.Equal(treasury + expected, game.State.PlayerById(0).Treasury);
            Assert.Equal("world:city_ruins", game.State.Plots[39].Improvement);
            Assert.Equal("world:route_route_road", game.State.Plots[39].Route);
            Assert.Equal(expectedRng.State, game.Rng.State);
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    private sealed class Probe(int percent) : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.PromotionDef>().Register(new() { Id = "pillage_probe:penalty", PillageChange = percent });
            context.Content<S.ImprovementDef>().Register(new() { Id = "pillage_probe:deposit", PillageGold = 101, ImprovementPillage = "world:city_ruins" });
        }
    }
}
