using System.Globalization;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Commerce sliders: keep income non-negative with a treasury buffer, otherwise maximize research.</summary>
internal sealed class EconomyPlanner(AiContext ctx)
{
    private const int MaxIterations = 100;
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
        long net = Net(economy, rates);
        for (int i = 0; i < MaxIterations; i++)
        {
            var next = Shift(rates, net < target ? Step : -Step);
            if (!TryShift(economy, rates, next, target, out var nextNet)) break;
            rates = next; net = nextNet;
        }
        Apply(economy, rates);
    }

    private long TargetIncome()
    {
        long reserve = Reserve(ctx.Cities.Count), treasury = ctx.Self.Treasury;
        if (treasury < reserve) return 2 + ctx.Cities.Count / 2;
        return 0;
    }

    private bool TryShift(IPlayerEconomyService economy, CommerceVector rates, CommerceVector next, long target, out long net)
    {
        net = 0;
        if (economy.ValidateRates(ctx.Sim, ctx.PlayerId, next) is not null) return false;
        net = Net(economy, next);
        return next.Gold >= rates.Gold || net >= target;
    }

    private long Net(IPlayerEconomyService economy, CommerceVector rates)
    {
        Apply(economy, rates);
        return economy.Calculate(ctx.Sim, ctx.PlayerId).NetGold;
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
            var next = new CommerceVector(gold, 100 - gold, 0, 0);
            if (economy.ValidateRates(ctx.Sim, ctx.PlayerId, next) is null) return next;
        }
        return economy.Rates(ctx.Sim, ctx.PlayerId);
    }

    private static CommerceVector Shift(CommerceVector r, int gold) => new(r.Gold + gold, r.Research - gold, 0, 0);

    private CommerceVector Normalize(CommerceVector r)
    {
        long gold = Math.Clamp((r.Gold + r.Culture + r.Espionage) / Step * Step, 0, 100);
        return new(gold, 100 - gold, 0, 0);
    }
}
