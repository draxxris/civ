using Civ.Content;

namespace Civ.Game;

/// <summary>Route profit and yields retain hundredths until city route aggregation.</summary>
public sealed record TradeRouteResult(int PartnerCity, bool Foreign, bool Overseas, long ProfitHundredths,
    YieldVector YieldsHundredths, Ledger Ledger);
public sealed record CityTradeResult(int Slots, IReadOnlyList<TradeRouteResult> Routes, YieldVector Yields, Ledger Ledger);
/// <summary>Active routes are derived from current connectivity. Queries are read-only and RNG-free.</summary>
public interface ITradeRouteService
{
    int MaxRoutes(Simulation sim, int city);
    CityTradeResult Calculate(Simulation sim, int city);
    TradeRouteResult CalculateProfit(Simulation sim, int city, int partnerCity);
}
