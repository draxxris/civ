using System.Text.Json.Nodes;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Trade;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

public sealed class TradeRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    // xunit builds a new test class instance per test, so the probe content must be static to be built once.
    private static readonly Lazy<ContentDb> ProbeContent = new(() =>
    {
        var loaded = new LoadedMods();
        var original = loaded.Content.Get<S.RuleConstantsDef>().All.Single();
        var source = new ModSource { Manifest = new() { Id = "trade_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation"] }, Root = loaded.Root, Instance = new Probe(original) };
        return ModLoader.Build(loaded.Sources.Append(source));
    });
    private Simulation Scenario()
    {
        var sim = GameFactory.Scenario(ProbeContent.Value); GameFactory.NeutralPolitics(sim);
        foreach (var plot in sim.State.Plots) { plot.Route = "world:route_route_road"; plot.Revealed.Add(0); }
        foreach (var (tile, owner, pop) in new[] { (0, 0, 1), (4, 0, 10), (7, 1, 10), (9, 0, 2) })
        {
            var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = pop };
            sim.State.AddCity(city); sim.State.Plots[tile].Owner = owner;
            city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true, AvoidGrowth = true });
        }
        // Foreign routes need contact and open borders.
        sim.Rules().Relations.EstablishContact(sim, sim.TeamOf(0).Id, sim.TeamOf(1).Id);
        sim.Rules().Diplomacy.SignAgreement(sim, 0, 1, DiplomaticAgreement.OpenBorders);
        return sim;
    }
    private static ITradeRouteService Service(Simulation sim) => sim.Content.Services.Get<ITradeRouteService>().Single();
    [Fact]
    public void Highest_value_routes_are_unique_and_foreign_peace_networks_obey_war_and_removed_routes()
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
    public void Missing_world_size_falls_back_when_calculating_routes()
    {
        var sim = Scenario();
        var save = JsonNode.Parse(SaveGame.Save(sim))!;
        save["Settings"]!["WorldSize"] = null;
        sim = SaveGame.Load(sim.Content, save.ToJsonString());
        Assert.Null(sim.Settings.WorldSize);
        Assert.Contains(Service(sim).Calculate(sim, 1).Routes, r => r.PartnerCity == 3);
    }
    [Fact]
    public void Profit_uses_population_distance_foreign_and_capital_modifiers_and_flows_through_sliders()
    {
        var sim = Scenario(); var city = sim.State.Cities[0]; city.Buildings.Add("cities:palace");
        var result = Service(sim).Calculate(sim, city.Id);
        var foreign = Assert.Single(result.Routes, r => r.Foreign);
        int worldPercent = sim.Content.Attachment<WorldSizeTradeDef>(sim.Settings.WorldSize!)!.TradeProfitPercent;
        int profitPercent = sim.Content.Attachment<RuleConstantsTradeDef>(sim.Content.Get<S.RuleConstantsDef>().All.Single().Id)!.TradeProfitPercent;
        long basis = Math.Max(100, Math.Min(500, 5 * worldPercent) * profitPercent / 100);
        Assert.Equal(basis * 225 / 100, foreign.ProfitHundredths);
        Assert.Equal(foreign.ProfitHundredths, foreign.YieldsHundredths.Commerce);
        var yields = sim.Rules().CityYields.Calculate(sim, city.Id);
        Assert.Equal(result.Yields.Commerce, yields.Ledger.Lines.Where(l => l.Source == "trade:routes" && l.Key == "yield.flat.commerce").Sum(l => l.Amount));
        var economy = sim.Rules().Economy; economy.SetRates(sim, 0, new(50, 50, 0, 0));
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
    [Fact]
    public void The_civic_valuer_prices_a_foreign_trade_ban_by_the_routes_it_costs_whether_or_not_it_is_held()
    {
        var sim = Scenario();
        var valuer = sim.Content.Services.Get<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>().Single(v => v.Id == "trade:civic_valuer");
        int Adjust(string civic) => valuer.Adjust((Ariel.Ai.AiContext)AiBindings.NewAi(sim, "AiContext", sim, 0),
            new Ariel.Ai.CivicSubject(sim.Content.Get<S.CivicDef>(civic)));
        long Points() => sim.State.Cities.Where(c => c.Owner == 0)
            .Sum(c => Service(sim).Calculate(sim, c.Id).Routes.Sum(r => Ariel.Ai.PlannerPoints.Yields(r.YieldsHundredths)));
        const string foreignPolicy = "government:civicoption_foreign_policy", society = "government:civicoption_society";
        const string protectionism = "government:protectionism";
        var civics = sim.State.PlayerById(0).Civics;
        civics.Remove(foreignPolicy);
        civics.Remove(society);

        long open = Points();
        civics[foreignPolicy] = protectionism;
        long banned = Points();
        Assert.True(banned < open, "the ban closes the foreign route");
        // Held or not, the ban is worth what the routes lose with it, the domestic route that takes the slot included.
        Assert.Equal((banned - open) / 100, Adjust(protectionism));
        civics.Remove(foreignPolicy);
        Assert.Equal((banned - open) / 100, Adjust(protectionism));

        // A ban another option already imposes costs nothing more; a civic with only unread trade fields is worth nothing.
        civics[society] = "government:hive";
        Assert.Equal(0, Adjust(protectionism));
        civics.Remove(society);
        Assert.Equal(0, Adjust("government:mercantilism"));
        sim.State.SetWar(0, 1, true);
        Assert.Equal(0, Adjust(protectionism)); // no foreign route to lose
    }
    [Fact]
    public void TradeRouteKey_and_CityPair_provide_typed_keys_and_query_operations()
    {
        var key = TradeRouteKey.Of(1, 3);
        Assert.Equal("1->3", key.Key);
        Assert.Equal("1->3", key.ToString());
        Assert.Equal(key, TradeRouteKey.Parse("1->3"));
        Assert.Equal(CityPair.Of(1, 3), key.Unordered);

        var pair = CityPair.Of(3, 1);
        Assert.Equal(1, pair.First);
        Assert.Equal(3, pair.Second);
        Assert.Equal("1<->3", pair.Key);
        Assert.Equal("1<->3", pair.ToString());
        Assert.Equal(pair, CityPair.Parse("1<->3"));
        Assert.True(pair.Contains(1));
        Assert.True(pair.Contains(3));
        Assert.False(pair.Contains(2));

        var sim = Scenario();
        var trade = Service(sim);
        var result = trade.Calculate(sim, 1);
        Assert.NotEmpty(result.Routes);

        var route = result.Routes[0];
        Assert.Equal(1, route.SourceCity);
        Assert.Equal(TradeRouteKey.Of(1, route.PartnerCity), route.Key);
        Assert.Equal(CityPair.Of(1, route.PartnerCity), route.Cities);

        Assert.True(trade.HasRoute(sim, 1, 3));
        Assert.True(trade.HasBilateralRoute(sim, 1, 3));
        Assert.True(trade.HasBilateralRoute(sim, 3, 1));
        Assert.False(trade.HasRoute(sim, 1, 999));
        Assert.False(trade.HasBilateralRoute(sim, 1, 999));

        var player0Routes = trade.ActiveRoutes(sim, 0);
        Assert.Contains(player0Routes, r => r.Key == TradeRouteKey.Of(1, 3));

        var allRoutes = trade.AllActiveRoutes(sim);
        Assert.Contains(allRoutes, r => r.Key == TradeRouteKey.Of(1, 3));
    }
    private sealed class Probe(S.RuleConstantsDef original) : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.RuleConstantsDef>().Override(new() { Id = original.Id, Values = original.Values, Raw = original.Raw.SetItem("INITIAL_TRADE_ROUTES", "2").SetItem("MAX_TRADE_ROUTES", "3") });
            context.Content<S.BuildingDef>().Register(new() { Id = "trade_probe:hub", DefaultBuilding = "trade_probe:hub" });
            context.Content<BuildingTradeDef>().Register(new() { Id = "trade_probe:hub_trade", Target = "trade_probe:hub", TradeRoutes = 10 });
        }
    }
}
