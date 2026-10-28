using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Trade;

public sealed class TradeMod : IMod
{
    public void Register(IModContext context)
    {
        var service = new TradeRouteService();
        context.RegisterService<ITradeRouteService>("routes", service);
        context.RegisterService<IModifierSource>("route_yields", new TradeEffects(service));
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
