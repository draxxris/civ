using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Trade;

/// <summary>What a civic's trade rules are worth to the computer player, added to the civic planner's score
/// (<see cref="CivicSubject"/>). The base AI does not know the <see cref="CivicTradeDef"/> attachment; this mod, which
/// owns the route rules, prices it: the route yields of the player's cities with the civic in its option, minus those
/// with that option empty, in <see cref="PlannerPoints"/>, as for yields every turn. The routes are chosen by the real
/// rules both times, so a ban on foreign trade costs only what domestic routes cannot make up, and a civic scores the
/// same whether the player runs it or not. Not priced: the connectedness and forced-trade fields, which no rule reads
/// yet, and any <c>trade:</c> modifier among the civic's own effects (no bundled civic has one).</summary>
internal sealed class TradeCivicValuer : IAiValuer<CivicSubject>
{
    public string Id => "trade:civic_valuer";

    public int Adjust(AiContext ctx, in CivicSubject subject)
    {
        var sim = ctx.Sim;
        var civic = subject.Civic;
        if (sim.Content.Attachment<CivicTradeDef>(civic.Id) is not { } trade || !Priced(trade)) return 0;

        // The player's other options as they are; this civic's option holds it, or nothing.
        var without = new List<CivicTradeDef>();
        foreach (var (option, held) in ctx.Self.Civics)
            if (option != civic.Option && sim.Content.Attachment<CivicTradeDef>(held) is { } other) without.Add(other);
        var with = new List<CivicTradeDef>(without) { trade };

        long hundredths = 0;
        foreach (var city in ctx.Cities)
            hundredths += Points(TradeRouteService.Calculate(sim, city, with)) - Points(TradeRouteService.Calculate(sim, city, without));
        return (int)Math.Clamp(hundredths / 100, -AiValuation.Limit, AiValuation.Limit);
    }

    /// <summary>Whether the attachment changes anything the route rules read.</summary>
    private static bool Priced(CivicTradeDef trade) => trade.NoForeignTrade || trade.Routes != 0 || trade.ForeignProfitPercent != 0
        || trade.SharedCivicProfitPercent != 0 || trade.YieldPercent != default;

    /// <summary>The routes' yields in planner points, in hundredths.</summary>
    private static long Points(CityTradeResult result)
    {
        long points = 0;
        foreach (var route in result.Routes) points += PlannerPoints.Yields(route.YieldsHundredths);
        return points;
    }
}
