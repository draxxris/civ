using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class TradeRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private Simulation Scenario()
    {
        var original = mods.Content.Get<S.RuleConstantsDef>().All.Single();
        var source = new ModSource { Manifest = new() { Id = "trade_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation"] }, Root = mods.Root, Instance = new Probe(original) };
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(source))); GameFactory.NeutralPolitics(sim);
        foreach (var plot in sim.State.Plots) { plot.Route = "world:route_route_road"; plot.Revealed.Add(0); }
        foreach (var (tile, owner, pop) in new[] { (0, 0, 1), (4, 0, 10), (7, 1, 10), (9, 0, 2) })
        {
            var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = pop };
            sim.State.AddCity(city); sim.State.Plots[tile].Owner = owner;
            city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true, AvoidGrowth = true });
        }
        return sim;
    }
    private static ITradeRouteService Service(Simulation sim) => sim.Content.Services.Get<ITradeRouteService>().Single();
    [Fact]
    public void Highest_value_routes_are_unique_and_foreign_peace_networks_obey_war_and_pillage()
    {
        var sim = Scenario(); var trade = Service(sim); ulong hash = sim.ComputeHash();
        var result = trade.Calculate(sim, 1);
        Assert.Equal(2, result.Slots); Assert.Equal(new[] { 3, 2 }, result.Routes.Select(r => r.PartnerCity));
        Assert.Equal(2, result.Routes.Select(r => r.PartnerCity).Distinct().Count());
        Assert.True(result.Routes[0].Foreign); Assert.Equal(hash, sim.ComputeHash());
        sim.State.SetWar(0, 1, true); Assert.DoesNotContain(trade.Calculate(sim, 1).Routes, r => r.PartnerCity == 3);
        sim.State.SetWar(0, 1, false);
        foreach (var plot in sim.State.Plots) plot.Route = null;
        Assert.Empty(trade.Calculate(sim, 1).Routes);
    }
    [Fact]
    public void Profit_uses_population_distance_foreign_and_capital_modifiers_and_flows_through_sliders()
    {
        var sim = Scenario(); var city = sim.State.Cities[0]; city.Buildings.Add("cities:palace");
        var result = Service(sim).Calculate(sim, city.Id);
        var foreign = Assert.Single(result.Routes.Where(r => r.Foreign));
        long basis = Math.Max(100, Math.Min(500, 5 * sim.Content.Get<S.WorldSizeDef>(sim.Settings.WorldSize!).TradeProfitPercent) * sim.Content.Get<S.RuleConstantsDef>().All.Single().Values.TradeProfitPercent / 100);
        Assert.Equal(basis * 225 / 100, foreign.ProfitHundredths);
        Assert.Equal(foreign.ProfitHundredths, foreign.YieldsHundredths.Commerce);
        var yields = sim.Content.Services.Get<ICityYieldService>().Single().Calculate(sim, city.Id);
        Assert.Equal(result.Yields.Commerce, yields.Ledger.Lines.Where(l => l.Source == "trade:routes" && l.Key == "yield.flat.commerce").Sum(l => l.Amount));
        var economy = sim.Content.Services.Get<IPlayerEconomyService>().Single(); economy.SetRates(sim, 0, new(50, 50, 0, 0));
        var output = economy.CalculateCity(sim, city.Id);
        Assert.True(output.Hundredths.Research > 0); Assert.True(output.Hundredths.Gold > 0);
    }
    [Fact]
    public void Building_slots_clamp_and_disorder_suppresses_routes_without_losing_capacity()
    {
        var sim = Scenario(); var city = sim.State.Cities[0]; city.Buildings.Add("trade_probe:hub");
        Assert.Equal(3, Service(sim).MaxRoutes(sim, city.Id));
        city.Extensions.Get<CityRuleState>(sim.Content)!.Disorder = true;
        var result = Service(sim).Calculate(sim, city.Id); Assert.Equal(3, result.Slots); Assert.Empty(result.Routes); Assert.Equal(default, result.Yields);
    }
    [Fact]
    public void Routes_and_slider_settlement_resume_identically_after_save_and_default_profile_can_disable_routes()
    {
        var sim = Scenario(); var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(Service(sim).Calculate(sim, 1).Routes.Select(r => r.PartnerCity), Service(loaded).Calculate(loaded, 1).Routes.Select(r => r.PartnerCity));
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        var disabled = GameFactory.Scenario(mods.Content); disabled.State.AddCity(new City { Id = 1, Owner = 0, Position = disabled.State.Grid.HexOf(new TileId(0)) });
        Assert.Equal(0, Service(disabled).MaxRoutes(disabled, 1));
    }
    private sealed class Probe(S.RuleConstantsDef original) : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.RuleConstantsDef>().Override(new() { Id = original.Id, Values = original.Values, Raw = original.Raw.SetItem("INITIAL_TRADE_ROUTES", "2").SetItem("MAX_TRADE_ROUTES", "3") });
            context.Content<S.BuildingDef>().Register(new() { Id = "trade_probe:hub", DefaultBuilding = "trade_probe:hub", TradeRoutes = 10 });
        }
    }
}
