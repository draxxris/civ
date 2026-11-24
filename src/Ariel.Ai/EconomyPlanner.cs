using System.Globalization;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>Commerce sliders: keep income non-negative with a treasury buffer, otherwise maximize research. The search
/// prices each trial on a forecast of the economy (<see cref="IPlayerEconomyService.Forecast"/>), since the rates
/// change nothing else, and issues one command for the rates it settles on.</summary>
internal sealed class EconomyPlanner(AiContext ctx)
{
    private const int MaxIterations = 100;
    /// <summary>The commerce types the planner ever sets a rate on: gold and research, and culture for unrest. Every other
    /// loaded type stays at zero.</summary>
    internal static readonly string[] Funded = [FoundationCommerce.GoldKey, FoundationCommerce.ResearchKey, FoundationCommerce.CultureKey];
    private CommerceVector Rates(long gold, long research, long culture = 0) => ctx.Sim.Rules().Commerce.Zero
        .With(FoundationCommerce.GoldKey, gold).With(FoundationCommerce.ResearchKey, research).With(FoundationCommerce.CultureKey, culture);
    private int Step => Math.Max(1, int.TryParse(ctx.Sim.Content.Get<Ariel.Content.Schema.RuleConstantsDef>().All.Single()
        .Raw.GetValueOrDefault("COMMERCE_PERCENT_CHANGE_INCREMENTS", "5"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 5);

    public static long Reserve(int cities) => 40 + 10L * cities;

    public void Plan()
    {
        if (ctx.Cities.Count == 0) return;
        var economy = ctx.Service<IPlayerEconomyService>();
        var rates = economy.Rates(ctx.Sim, ctx.PlayerId);
        rates = LegalStart(economy, Normalize(rates));
        long target = TargetIncome();
        var forecast = economy.Forecast(ctx.Sim, ctx.PlayerId);
        long net = forecast.NetGold(rates);
        for (int i = 0; i < MaxIterations; i++)
        {
            var next = Shift(rates, net < target ? Step : -Step);
            if (!TryShift(economy, forecast, rates, next, target, out var nextNet)) break;
            rates = next; net = nextNet;
        }
        rates = WithCulture(economy, forecast, rates, target);
        Apply(economy, rates);
    }

    internal const int CultureSteps = 2;

    /// <summary>Unrest wants culture: while a city is in disorder (not from anarchy or occupation) or has more angry
    /// citizens than content ones, move a little research into culture, keeping income on target.</summary>
    private CommerceVector WithCulture(IPlayerEconomyService economy, IEconomyForecast forecast, CommerceVector rates, long target)
    {
        if (!Unrest()) return rates;
        long culture = Math.Min(rates.Research, (long)Step * CultureSteps);
        if (culture <= 0) return rates;
        var next = Rates(rates.Gold, rates.Research - culture, culture);
        if (economy.ValidateRates(ctx.Sim, ctx.PlayerId, next) is not null) return rates;
        return forecast.NetGold(next) >= Math.Min(target, forecast.NetGold(rates)) ? next : rates;
    }

    private bool Unrest() => !GovernmentStatus.IsAnarchy(ctx.Sim, ctx.PlayerId) && ctx.Cities.Any(city =>
        city.Extensions.TryGet<CityRuleState>(ctx.Sim.Content) is { Disorder: true, OccupationTurns: 0 }
        || ctx.ViewOf(city).HappyMargin < 0);

    private long TargetIncome()
    {
        long reserve = Reserve(ctx.Cities.Count), treasury = ctx.Self.Treasury;
        if (treasury < reserve) return 2 + ctx.Cities.Count / 2;
        return 0;
    }

    private bool TryShift(IPlayerEconomyService economy, IEconomyForecast forecast, CommerceVector rates, CommerceVector next,
        long target, out long net)
    {
        net = 0;
        if (economy.ValidateRates(ctx.Sim, ctx.PlayerId, next) is not null) return false;
        net = forecast.NetGold(next);
        return next.Gold >= rates.Gold || net >= target;
    }

    private void Apply(IPlayerEconomyService economy, CommerceVector rates)
    {
        if (economy.Rates(ctx.Sim, ctx.PlayerId) != rates) ctx.Issue(new SetCommerceRatesCommand { Player = ctx.PlayerId, Rates = rates });
    }

    private CommerceVector LegalStart(IPlayerEconomyService economy, CommerceVector rates)
    {
        if (economy.ValidateRates(ctx.Sim, ctx.PlayerId, rates) is null) return rates;
        for (int gold = 0; gold <= 100; gold += Step)
        {
            var next = Rates(gold, 100 - gold);
            if (economy.ValidateRates(ctx.Sim, ctx.PlayerId, next) is null) return next;
        }
        return economy.Rates(ctx.Sim, ctx.PlayerId);
    }

    private CommerceVector Shift(CommerceVector r, int gold) => Rates(r.Gold + gold, r.Research - gold);

    /// <summary>Only gold and research: everything not on research moves to gold, in whole steps.</summary>
    private CommerceVector Normalize(CommerceVector r)
    {
        long other = 0;
        foreach (var (key, amount) in r) if (key != FoundationCommerce.ResearchKey) other += amount;
        long gold = Math.Clamp(other / Step * Step, 0, 100);
        return Rates(gold, 100 - gold);
    }
}
