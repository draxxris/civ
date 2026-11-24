using Ariel.Content;
using Ariel.Game;
using UnitDef = Ariel.Content.Schema.UnitDef;

namespace Ariel.Rules.Economy;

internal sealed class PlayerEconomy(CityCommerce commerce) : IPlayerEconomyService, IPlayerResearchSource
{
    public long ResearchHundredths(Simulation sim, int player) =>
        CommerceHundredths(sim, player, sim.Content.Commerce[FoundationCommerce.ResearchKey]);
    public long CommerceHundredths(Simulation sim, int player, CommerceType type)
    {
        var state = EconomyData.Read(sim, player);
        return state.SettledTurn == sim.State.Turn ? state.SettledCommerceHundredths[type] : Calculate(sim, player).CommerceHundredths[type];
    }
    public CommerceVector Rates(Simulation sim, int player) => EconomyData.Read(sim, player).Rates;
    public string? ValidateRates(Simulation sim, int player, CommerceVector rates)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        var commerce = sim.Rules().Commerce;
        foreach (var (key, _) in rates)
            if (!commerce.Types.TryGet(key, out _)) return "Unknown commerce type: " + key;
        var parts = commerce.Parts(rates);
        if (parts.Any(p => p < 0 || p > 100) || parts.Sum() != 100) return "Commerce rates must be 0..100 and sum to 100";
        int step = Math.Max(1, EconomyData.Raw(sim, "COMMERCE_PERCENT_CHANGE_INCREMENTS", 5));
        if (parts.Any(p => p % step != 0)) return "Commerce rates must use steps of " + step;
        var effects = EconomyData.Effects(sim, player).Ledger;
        var maxRateKeys = commerce.Names("economy:max_rate:", "");
        for (int i = 0; i < parts.Length; i++)
        {
            // Negative contributions reduce the default maximum. Future civic policies use the same hook.
            long maximum = Math.Clamp(100 + effects.Total(maxRateKeys[i]), 0, 100);
            if (parts[i] > maximum) return "Commerce rate exceeds maximum: " + commerce.Types[i].Key;
        }
        return null;
    }
    // Stored with every loaded type, so the state names each one whatever the command left out.
    public void SetRates(Simulation sim, int player, CommerceVector rates) => EconomyData.Edit(sim, player).Rates = sim.Rules().Commerce.Zero + rates;
    public CityCommerceResult CalculateCity(Simulation sim, int city) => commerce.Calculate(sim, city, Rates(sim, sim.State.CityById(city).Owner));
    public PlayerEconomyResult Calculate(Simulation sim, int player)
    {
        // Only reads: every city's commerce shares one span's modifier sums and city scope.
        using var span = sim.OpenReadSpan();
        var lines = new List<LedgerLine>();
        var commerce = sim.Rules().Commerce;
        var keys = commerce.Names("economy:", "_hundredths");
        var total = commerce.Zero;
        foreach (var city in EconomyData.Cities(sim, player))
        {
            var result = CalculateCity(sim, city.Id);
            total += result.Hundredths;
            string source = "economy:city:" + city.Id;
            for (int i = 0; i < keys.Length; i++) lines.Add(new(source, keys[i], result.Hundredths[commerce.Types[i]]));
        }
        long expenses = RecurringCosts.Calculate(sim, player, lines);
        var (dealIncome, dealExpenses) = DealGold(sim, player);
        long income = total.Gold / 100 + dealIncome;
        expenses += dealExpenses;
        lines.Add(new("economy:exchanges", "economy:deal_income", dealIncome));
        lines.Add(new("economy:exchanges", "economy:deal_expenses", dealExpenses));
        lines.Add(new("economy:total", "economy:income", income));
        lines.Add(new("economy:rules", "economy:expenses", expenses));
        lines.Add(new("economy:total", "economy:net_gold", income - expenses));
        return new(total, income, expenses, income - expenses, new(lines));
    }
    public IEconomyForecast Forecast(Simulation sim, int player)
    {
        var cities = EconomyData.Cities(sim, player).Select(city => commerce.Read(sim, city.Id)).ToArray();
        long expenses = RecurringCosts.Calculate(sim, player, []);
        var (dealIncome, dealExpenses) = DealGold(sim, player);
        return new Forecaster(cities, dealIncome, expenses + dealExpenses);
    }
    /// <summary>Calculate's net gold, from the same per-city arithmetic (<see cref="CityCommerce.Split"/>) and the same
    /// rate-independent costs: income is the gold hundredths' whole part plus deal income, minus every expense.</summary>
    private sealed class Forecaster(CityCommerce.Inputs[] cities, long dealIncome, long expenses) : IEconomyForecast
    {
        public long NetGold(CommerceVector rates)
        {
            long gold = 0;
            foreach (var city in cities) gold += CityCommerce.Split(city, rates).Result[city.Commerce[FoundationCommerce.GoldKey].Index];
            return gold / 100 + dealIncome - expenses;
        }
    }
    private static (long Income, long Expenses) DealGold(Simulation sim, int player) =>
        sim.Rules().Diplomacy.GoldPerTurn(sim, player);
    internal void Settle(Simulation sim, int player)
    {
        var owner = sim.State.PlayerById(player);
        var state = EconomyData.Edit(sim, player);
        var result = Calculate(sim, player);
        long expenses = result.Expenses;
        long dealIncome = result.Ledger.Total("economy:deal_income");
        long domesticExpenses = expenses - result.Ledger.Total("economy:deal_expenses");
        long netGold = result.NetGold;
        var total = result.CommerceHundredths;
        int step = Math.Max(1, EconomyData.Raw(sim, "COMMERCE_PERCENT_CHANGE_INCREMENTS", 5));
        // Types are cut in their deficit order (science first) into the remainder type. Each adjustment is legal under
        // the same policy as a command.
        var commerce = sim.Rules().Commerce;
        foreach (var cut in commerce.DeficitCuts)
        {
            int i = cut.Index, into = commerce.Remainder.Index;
            while (owner.Treasury + netGold < 0)
            {
                var parts = commerce.Parts(state.Rates);
                if (parts[i] == 0) break;
                long shift = Math.Min(step, parts[i]);
                parts[i] -= shift; parts[into] += shift;
                var next = commerce.Vector(parts);
                if (ValidateRates(sim, player, next) is not null) break;
                state.Rates = next;
                // Sliders change output, not recurring costs. Keep the expense snapshot for this settlement.
                total = CommerceTotal(sim, player);
                netGold = total.Gold / 100 + dealIncome - expenses;
            }
        }
        // The exchanges mod transfers deal gold after every player's Gold phase. Keep any domestic debt
        // until those transfers arrive, rather than forgiving it at the zero-treasury clamp.
        long treasury = owner.Treasury + total.Gold / 100 - domesticExpenses;
        state.UnpaidGold = Math.Max(0, -treasury);
        owner.Treasury = Math.Max(0, treasury);
        state.SettledTurn = sim.State.Turn;
        state.SettledCommerceHundredths = total;
    }
    internal void ReconcileGold(Simulation sim, int player)
    {
        var owner = sim.State.PlayerById(player);
        var state = EconomyData.Edit(sim, player);
        // Evaluate strikes only after exchanges transfer actual deal payments. Expected income
        // can fail to arrive when the payer cannot afford a deal.
        long treasury = owner.Treasury - state.UnpaidGold;
        state.UnpaidGold = Math.Max(0, -treasury);
        owner.Treasury = Math.Max(0, treasury);
        if (treasury >= 0)
        {
            state.StrikeTurns = 0;
            return;
        }
        if (!PlayerRules.IsNation(sim, player) || !EconomyData.Cities(sim, player).Any()) return;
        state.StrikeTurns++;
        if (state.StrikeTurns > 1) DisbandForDeficit(sim, player, state.StrikeTurns / 2);
    }
    private void DisbandForDeficit(Simulation sim, int player, int limit)
    {
        bool disbanded = false;
        for (int i = 0; i < limit; i++)
        {
            var unit = SelectDisbandUnit(sim, player);
            if (unit is null) break;
            // A transport's cargo goes ashore where it can; what cannot is lost with it.
            var removed = sim.Rules().Units?.Disband(sim, unit.Id);
            if (removed is null) sim.State.RemoveUnit(unit.Id);
            foreach (int id in removed ?? [unit.Id])
                sim.Events.Publish(new UnitDisbandedForDeficit(id, player, unit.Position));
            disbanded = true;
            if (Calculate(sim, player).NetGold >= 0) break;
        }
        if (disbanded) sim.Rules().Visibility?.Recompute(sim);
    }
    private static Unit? SelectDisbandUnit(Simulation sim, int player)
    {
        bool protectGarrisons = EconomyData.Civics(sim, player).Sum(c => c.HappyPerMilitaryUnit) > 0;
        Unit? best = null;
        long bestValue = long.MaxValue;
        foreach (var unit in sim.State.Units.Where(u => u.Owner == player).OrderBy(u => u.Id))
        {
            var def = sim.Content.Get<UnitDef>(unit.Type);
            if (def.GoldenAge || def.Cost <= 0) continue;
            if (protectGarrisons && IsSoleHappyGarrison(sim, unit, def)) continue;
            long value = DisbandValue(sim, unit, def);
            if (value >= bestValue) continue;
            best = unit;
            bestValue = value;
        }
        return best;
    }
    private static bool IsSoleHappyGarrison(Simulation sim, Unit unit, UnitDef def)
    {
        if (!def.MilitaryHappiness || sim.State.CityAt(unit.Position) is null) return false;
        return sim.State.UnitsAt(unit.Position).Count(u => u.Owner == unit.Owner
            && sim.Content.Get<UnitDef>(u.Type).MilitaryHappiness) == 1;
    }
    // CvPlayer::disbandUnit: low scores are removed first. Military tracks earned levels separately
    // from free promotions, so use its level when available.
    private static long DisbandValue(Simulation sim, Unit unit, UnitDef def)
    {
        int level = unit.Extensions.TryGet<MilitaryUnitState>(sim.Content)?.Level ?? 1 + unit.Promotions.Count;
        long value = 10000L + sim.Rng.Next(1000) + def.Cost * 5L + unit.Experience * 20L + level * 100L;
        value = DisbandPlotValue(sim, unit, def, value);
        value *= DisbandAiMultipliers.GetValueOrDefault(def.DefaultUnitAi ?? "", 1);
        if (def.ExtraCost > 0) value /= def.ExtraCost + 1L;
        return value;
    }
    private static long DisbandPlotValue(Simulation sim, Unit unit, UnitDef def, long value)
    {
        if (def.Strength > 0 && !def.NoDefensiveBonus && sim.State.CityAt(unit.Position) is not null) value *= 2;
        var plot = sim.State.PlotAt(unit.Position);
        if (plot.Owner is { } owner && sim.TeamOf(owner).Id == sim.TeamOf(unit.Owner).Id) value *= 3;
        if (plot.Owner == unit.Owner) value /= 6;
        return value;
    }
    private static readonly Dictionary<string, int> DisbandAiMultipliers = new(StringComparer.Ordinal)
    {
        ["military:unitai_subdued_animal"] = 3,
        ["military:unitai_hunter"] = 10,
        ["military:unitai_settle"] = 20,
        ["military:unitai_worker"] = 10,
        ["military:unitai_attack"] = 2,
        ["military:unitai_attack_city"] = 2,
        ["military:unitai_collateral"] = 2,
        ["military:unitai_pillage"] = 2,
        ["military:unitai_reserve"] = 2,
        ["military:unitai_counter"] = 2,
        ["military:unitai_pillage_counter"] = 2,
        ["military:unitai_city_defense"] = 6,
        ["military:unitai_city_counter"] = 6,
        ["military:unitai_city_special"] = 6,
        ["military:unitai_paradrop"] = 6,
        ["military:unitai_explore"] = 15,
        ["military:unitai_missionary"] = 8,
        ["military:unitai_spy"] = 12,
        ["military:unitai_icbm"] = 4,
        ["military:unitai_worker_sea"] = 18,
        ["military:unitai_explore_sea"] = 25,
        ["military:unitai_assault_sea"] = 5,
        ["military:unitai_settler_sea"] = 5,
        ["military:unitai_missionary_sea"] = 5,
        ["military:unitai_spy_sea"] = 5,
        ["military:unitai_carrier_sea"] = 5,
        ["military:unitai_missile_carrier_sea"] = 5,
        ["military:unitai_defense_air"] = 3,
        ["military:unitai_carrier_air"] = 3,
        ["military:unitai_missile_air"] = 3
    };
    private CommerceVector CommerceTotal(Simulation sim, int player)
    {
        var total = sim.Rules().Commerce.Zero;
        foreach (var city in EconomyData.Cities(sim, player)) total += CalculateCity(sim, city.Id).Hundredths;
        return total;
    }
}

internal sealed class EconomyPhase(string name, int order, Action<Simulation, int> run, IReadOnlyList<string>? writes = null) : IPlayerTurnSystem
{
    public string Id => "economy:" + name;
    public int Order => order;
    public IReadOnlyList<string>? WriteDomains => writes;
    public void Run(Simulation sim, int player) => run(sim, player);
    public void Run(Simulation sim) { foreach (var player in sim.State.Players.OrderBy(p => p.Id)) run(sim, player.Id); }
}
