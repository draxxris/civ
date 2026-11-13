using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Mods.Trade;

public sealed class TradeMod : IPreparableMod
{
    private static readonly ModJsonFile<CivicTradeDef[]> Civics = new("data/civic-trade.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Civics];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Civics);
        var service = new TradeRouteService();
        context.RegisterService<ITradeRouteService>("routes", service);
        context.RegisterService<IModifierSource>("route_yields", new TradeEffects(service));
        context.RegisterService<ICityTradeLinks>("trade_links", service);
    }
}
internal sealed class TradeEffects(ITradeRouteService service) : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        var result = service.Calculate(context.Sim, city.Id);
        sink.AddYield("trade:routes", result.Yields);
    }
}
