using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Mods.Trade;

public sealed class TradeMod : IPreparableMod
{
    private static readonly ModJsonFile<CivicTradeDef[]> Civics = new("data/civic-trade.json");
    private static readonly ModJsonFile<BuildingTradeDef[]> Buildings = new("data/building-trade.json");
    private static readonly ModJsonFile<TechTradeDef[]> Techs = new("data/tech-trade.json");
    private static readonly ModJsonFile<YieldTradeDef[]> Yields = new("data/yield-trade.json");
    private static readonly ModJsonFile<WorldSizeTradeDef[]> WorldSizes = new("data/world-size-trade.json");
    private static readonly ModJsonFile<RuleConstantsTradeDef[]> Constants = new("data/rule-constants-trade.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Civics, Buildings, Techs, Yields, WorldSizes, Constants];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Civics);
        ContentRegistration.Load(context, Buildings);
        ContentRegistration.Load(context, Techs);
        ContentRegistration.Load(context, Yields);
        ContentRegistration.Load(context, WorldSizes);
        ContentRegistration.Load(context, Constants);
        var service = new TradeRouteService();
        context.RegisterService<ITradeRouteService>("routes", service);
        context.RegisterService<Ariel.Presentation.ICityPanel>("city", new TradeCityPanel());
        context.RegisterService<IModifierSource>("route_yields", new TradeEffects(service));
        context.RegisterService<ICityTradeLinks>("trade_links", service);
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>("civic_valuer", new TradeCivicValuer());
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
