using Ariel.Content;
using Ariel.Game;
using UnitDef = Ariel.Content.Schema.UnitDef;

namespace Ariel.Mods.Economy;

internal sealed class PlayerEconomy(CityCommerce commerce) : IPlayerEconomyService, IPlayerResearchSource, IPlayerEspionageSource
{
    public long ResearchHundredths(Simulation sim, int player)
    {
        var state = EconomyData.Read(sim, player);
        return state.SettledTurn == sim.State.Turn ? state.SettledCommerceHundredths.Research : Calculate(sim, player).CommerceHundredths.Research;
    }
    public long EspionageHundredths(Simulation sim, int player)
    {
        var state = EconomyData.Read(sim, player);
        return state.SettledTurn == sim.State.Turn ? state.SettledCommerceHundredths.Espionage : Calculate(sim, player).CommerceHundredths.Espionage;
    }
    public CommerceVector Rates(Simulation sim, int player) => EconomyData.Read(sim, player).Rates;
    public string? ValidateRates(Simulation sim, int player, CommerceVector rates)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        var parts = CityCommerce.Parts(rates);
        if (parts.Any(p => p < 0 || p > 100) || parts.Sum() != 100) return "Commerce rates must be 0..100 and sum to 100";
        int step = Math.Max(1, EconomyData.Raw(sim, "COMMERCE_PERCENT_CHANGE_INCREMENTS", 5));
        if (parts.Any(p => p % step != 0)) return "Commerce rates must use steps of " + step;
        var effects = EconomyData.Effects(sim, player).Ledger;
        for (int i = 0; i < parts.Length; i++)
        {
            // Negative contributions reduce the default maximum. Future civic policies use the same hook.
            long maximum = Math.Clamp(100 + effects.Total("economy:max_rate:" + CityCommerce.Types[i]), 0, 100);
            if (parts[i] > maximum) return "Commerce rate exceeds maximum: " + CityCommerce.Types[i];
        }
        return null;
    }
    public void SetRates(Simulation sim, int player, CommerceVector rates) => EconomyData.Edit(sim, player).Rates = rates;
    public CityCommerceResult CalculateCity(Simulation sim, int city) => commerce.Calculate(sim, city, Rates(sim, sim.State.CityById(city).Owner));
    public PlayerEconomyResult Calculate(Simulation sim, int player)
    {
        var lines = new List<LedgerLine>();
        CommerceVector total = default;
        foreach (var city in EconomyData.Cities(sim, player))
        {
            var result = CalculateCity(sim, city.Id);
            total += result.Hundredths;
            lines.Add(new("economy:city:" + city.Id, "economy:gold_hundredths", result.Hundredths.Gold));
            lines.Add(new("economy:city:" + city.Id, "economy:research_hundredths", result.Hundredths.Research));
            lines.Add(new("economy:city:" + city.Id, "economy:culture_hundredths", result.Hundredths.Culture));
            lines.Add(new("economy:city:" + city.Id, "economy:espionage_hundredths", result.Hundredths.Espionage));
        }
        if (sim.Content.Services.Get<IEspionageService>().SingleOrDefault() is { } espionage)
            foreach (var share in espionage.Spending(sim, player, total.Espionage))
                lines.Add(new("economy:espionage_target:" + share.Key, "economy:espionage_spending", share.Value));
        long expenses = RecurringCosts.Calculate(sim, player, lines);
        var (dealIncome, dealExpenses) = DealGold(sim, player);
        long income = total.Gold / 100 + dealIncome;
        expenses += dealExpenses;
        lines.Add(new("diplomacy:deals", "economy:deal_income", dealIncome));
        lines.Add(new("diplomacy:deals", "economy:deal_expenses", dealExpenses));
        lines.Add(new("economy:total", "economy:income", income));
        lines.Add(new("economy:rules", "economy:expenses", expenses));
        lines.Add(new("economy:total", "economy:net_gold", income - expenses));
        return new(total, income, expenses, income - expenses, new(lines));
    }
    private static (long Income, long Expenses) DealGold(Simulation sim, int player)
    {
        var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        if (diplomacy is null) return default;
        long income = 0, expenses = 0;
        foreach (var deal in diplomacy.Deals(sim))
        {
            if (deal.Status != DiplomaticDealStatus.Active) continue;
            if (deal.Proposer != player && deal.Recipient != player) continue;
            if (sim.TeamOf(deal.Proposer).Wars.Contains(sim.TeamOf(deal.Recipient).Id)) continue;
            foreach (var item in deal.Items.Where(i => i.Kind == TradeItemKind.GoldPerTurn))
            {
                if (item.FromPlayer == player) expenses += item.Amount;
                else income += item.Amount;
            }
        }
        return (income, expenses);
    }
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
        // Science is reduced first. Each adjustment is legal under the same policy as a command.
        for (int i = 1; i < 4; i++)
        {
            while (owner.Treasury + netGold < 0)
            {
                var parts = CityCommerce.Parts(state.Rates);
                if (parts[i] == 0) break;
                long shift = Math.Min(step, parts[i]);
                parts[i] -= shift; parts[0] += shift;
                var next = CityCommerce.Vector(parts);
                if (ValidateRates(sim, player, next) is not null) break;
                state.Rates = next;
                // Sliders change output, not recurring costs. Keep the expense snapshot for this settlement.
                total = CommerceTotal(sim, player);
                netGold = total.Gold / 100 + dealIncome - expenses;
            }
        }
        // Diplomacy transfers deal gold after every player's Gold phase. Keep any domestic debt
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
        // Evaluate strikes only after Diplomacy transfers actual deal payments. Expected income
        // can fail to arrive when the payer cannot afford a deal.
        long treasury = owner.Treasury - state.UnpaidGold;
        state.UnpaidGold = Math.Max(0, -treasury);
        owner.Treasury = Math.Max(0, treasury);
        if (treasury >= 0)
        {
            state.StrikeTurns = 0;
            return;
        }
        if (IsBarbarian(sim, player) || !EconomyData.Cities(sim, player).Any()) return;
        state.StrikeTurns++;
        if (state.StrikeTurns > 1) DisbandForDeficit(sim, player, state.StrikeTurns / 2);
    }
    private static bool IsBarbarian(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true;
    private void DisbandForDeficit(Simulation sim, int player, int limit)
    {
        bool disbanded = false;
        for (int i = 0; i < limit; i++)
        {
            var unit = SelectDisbandUnit(sim, player);
            if (unit is null) break;
            sim.State.RemoveUnit(unit.Id);
            sim.Events.Publish(new UnitDisbandedForDeficit(unit.Id, player, unit.Position));
            disbanded = true;
            if (Calculate(sim, player).NetGold >= 0) break;
        }
        if (disbanded) sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
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
        CommerceVector total = default;
        foreach (var city in EconomyData.Cities(sim, player)) total += CalculateCity(sim, city.Id).Hundredths;
        return total;
    }
}

internal sealed class EconomyPhase(string name, int order, Action<Simulation, int> run) : IPlayerTurnSystem
{
    public string Id => "economy:" + name;
    public int Order => order;
    public void Run(Simulation sim, int player) => run(sim, player);
    public void Run(Simulation sim) { foreach (var player in sim.State.Players.OrderBy(p => p.Id)) run(sim, player.Id); }
}
