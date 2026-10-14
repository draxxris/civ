using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Trade;

internal sealed class TradeRouteService : ITradeRouteService
{
    public int MaxRoutes(Simulation sim, int cityId)
    {
        if (TradeData.Raw(sim, "MAX_TRADE_ROUTES", 8) <= 0) return 0;
        var city = sim.State.CityById(cityId);
        return Slots(sim, city, TradeData.Effects(sim, city));
    }
    private static int Slots(Simulation sim, City city, ModifierSink sink)
    {
        sink.Add("trade:initial", "trade:slots", TradeData.Raw(sim, "INITIAL_TRADE_ROUTES", 1));
        foreach (var tech in sim.TeamOf(city.Owner).Techs.Select(sim.Content.Get<S.TechDef>)) sink.Add(tech.Id, "trade:slots", tech.TradeRoutes);
        foreach (var civic in TradeData.Civics(sim, city.Owner)) sink.Add(civic.Id, "trade:slots", civic.TradeRoutes);
        foreach (var b in TradeData.Active(sim, city)) sink.Add(b.Id, "trade:slots", b.TradeRoutes);
        bool coastal = TradeData.Coastal(sim, city);
        foreach (var other in sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id))
            foreach (var b in TradeData.Active(sim, other))
            {
                sink.Add(b.Id, "trade:slots", b.GlobalTradeRoutes);
                if (coastal) sink.Add(b.Id, "trade:slots", b.CoastalTradeRoutes);
            }
        return (int)Math.Clamp(sink.Ledger.Total("trade:slots"), 0, Math.Max(0, TradeData.Raw(sim, "MAX_TRADE_ROUTES", 8)));
    }
    public CityTradeResult Calculate(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var empty = new ModifierSink();
        if (TradeData.Raw(sim, "MAX_TRADE_ROUTES", 8) <= 0) return new(0, [], default, empty.Ledger);
        var sink = TradeData.Effects(sim, city);
        int slots = Slots(sim, city, sink);
        if (slots == 0 || GovernmentStatus.IsDisorder(sim, city)) return new(slots, [], default, sink.Ledger);
        var connections = sim.Content.Services.Get<ICityConnectionService>().Single();
        bool noForeign = TradeData.Civics(sim, city.Owner).Any(c => c.NoForeignTrade);
        var routes = sim.State.Cities.Where(c => Candidate(sim, city, c, noForeign) && connections.Connected(sim, city.Id, c.Id))
            .Select(c => Route(sim, city, c, sink.Ledger))
            .OrderByDescending(r => r.ProfitHundredths).ThenBy(r => r.Foreign).ThenBy(r => r.PartnerCity).Take(slots).ToArray();
        var hundredths = routes.Aggregate(default(YieldVector), (sum, r) => sum + r.YieldsHundredths);
        var yields = new YieldVector(hundredths.Food / 100, hundredths.Production / 100, hundredths.Commerce / 100);
        foreach (var route in routes) sink.Add("trade:partner:" + route.PartnerCity, "trade:profit_hundredths", route.ProfitHundredths);
        sink.AddYield("trade:routes", yields);
        return new(slots, Array.AsReadOnly(routes), yields, sink.Ledger);
    }
    private static bool Candidate(Simulation sim, City source, City partner, bool noForeign)
    {
        if (source.Id == partner.Id || GovernmentStatus.IsDisorder(sim, partner)) return false;
        if (sim.TeamOf(source.Owner).Id == sim.TeamOf(partner.Owner).Id) return true;
var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        int mine = sim.TeamOf(source.Owner).Id, theirs = sim.TeamOf(partner.Owner).Id;
        return !noForeign && !TradeData.Civics(sim, partner.Owner).Any(c => c.NoForeignTrade)
            && !sim.TeamOf(source.Owner).Wars.Contains(theirs)
            && (diplomacy is null || diplomacy.HasMet(sim, mine, theirs) && diplomacy.HasOpenBorders(sim, mine, theirs));
    }
    private static TradeRouteResult Route(Simulation sim, City city, City partner, Ledger effects)
    {
        var sink = new ModifierSink();
        int distance = sim.State.Grid.Distance(sim.State.Grid.TileAt(city.Position), sim.State.Grid.TileAt(partner.Position));
        int worldPercent = sim.Content.Get<S.WorldSizeDef>(sim.Settings.WorldSize!).TradeProfitPercent;
        long population = (long)partner.Population * TradeData.Raw(sim, "THEIR_POPULATION_TRADE_PERCENT", 50);
        long distanceValue = (long)distance * worldPercent;
        long baseProfit = Math.Max(100, Math.Min(population, distanceValue) * TradeData.Raw(sim, "TRADE_PROFIT_PERCENT", TradeData.Constants(sim).Values.TradeProfitPercent) / 100);
        sink.Add("trade:population", "trade:population_limit_hundredths", population);
        sink.Add("trade:distance", "trade:distance_limit_hundredths", distanceValue);
        sink.Add("trade:base", "trade:base_profit_hundredths", baseProfit);
        bool foreign = sim.TeamOf(city.Owner).Id != sim.TeamOf(partner.Owner).Id;
        bool overseas = sim.State.PlotAt(city.Position).AreaId != sim.State.PlotAt(partner.Position).AreaId;
        AddProfitModifiers(sim, city, partner, foreign, overseas, sink);
        long modifier = 100 + sink.Ledger.Total("trade:profit_percent") + effects.Total("trade:profit_percent");
        if (foreign) modifier += effects.Total("trade:foreign_profit_percent");
        long profit = baseProfit * Math.Max(0, modifier) / 100;
        var percent = YieldModifiers(sim, city.Owner, effects);
        var yields = new YieldVector(profit * percent.Food / 100, profit * percent.Production / 100, profit * percent.Commerce / 100);
        sink.Add("trade:total", "trade:profit_hundredths", profit);
        sink.AddYield("trade:route_hundredths", yields);
        return new(partner.Id, foreign, overseas, profit, yields, sink.Ledger);
    }
    private static void AddProfitModifiers(Simulation sim, City city, City partner, bool foreign, bool overseas, ModifierSink sink)
    {
        sink.Add("trade:population", "trade:profit_percent", Math.Max(0, (city.Population + TradeData.Raw(sim, "OUR_POPULATION_TRADE_MODIFIER_OFFSET", -10)) * TradeData.Raw(sim, "OUR_POPULATION_TRADE_MODIFIER", 5)));
        foreach (var b in TradeData.Active(sim, city))
        {
            sink.Add(b.Id, "trade:profit_percent", b.TradeRouteModifier);
            if (foreign) sink.Add(b.Id, "trade:profit_percent", b.ForeignTradeRouteModifier);
        }
        var capital = sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id).FirstOrDefault(c => TradeData.Active(sim, c).Any(b => b.Capital));
        if (capital is not null && sim.Content.Services.Get<ICityConnectionService>().Single().Connected(sim, city.Id, capital.Id))
            sink.Add("trade:capital", "trade:profit_percent", TradeData.Raw(sim, "CAPITAL_TRADE_MODIFIER", 25));
        if (overseas) sink.Add("trade:overseas", "trade:profit_percent", TradeData.Raw(sim, "OVERSEAS_TRADE_MODIFIER", 50));
        if (!foreign) return;
        sink.Add("trade:peace", "trade:profit_percent", TradeData.Raw(sim, "FOREIGN_TRADE_MODIFIER", 100));
        var partnerCivics = sim.State.PlayerById(partner.Owner).Civics.Values.ToHashSet(StringComparer.Ordinal);
        foreach (var civic in TradeData.Civics(sim, city.Owner))
        {
            sink.Add(civic.Id, "trade:profit_percent", civic.ForeignTradeRouteModifier);
            if (partnerCivics.Contains(civic.Id)) sink.Add(civic.Id + ":shared", "trade:profit_percent", civic.SharedCivicTradeRouteModifier);
        }
    }
    private static YieldVector YieldModifiers(Simulation sim, int player, Ledger effects)
    {
        long Rate(string id) => sim.Content.Get<S.YieldDef>("foundation:yield_" + id).TradeModifier + effects.Total("trade:yield_percent:" + id);
        var result = new YieldVector(Rate("food"), Rate("production"), Rate("commerce"));
        foreach (var civic in TradeData.Civics(sim, player)) result += civic.TradeYieldModifiers;
        foreach (var trait in TradeData.Traits(sim, player)) result += trait.TradeYieldModifiers;
        return new(Math.Max(0, result.Food), Math.Max(0, result.Production), Math.Max(0, result.Commerce));
    }
}
