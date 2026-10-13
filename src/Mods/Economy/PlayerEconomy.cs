using Civ.Content;
using Civ.Game;

namespace Civ.Mods.Economy;

internal sealed class PlayerEconomy(CityCommerce commerce) : IPlayerEconomyService, IPlayerResearchSource
{
    public long ResearchHundredths(Simulation sim, int player)
    {
        var state = EconomyData.Read(sim, player);
        return state.SettledTurn == sim.State.Turn ? state.SettledCommerceHundredths.Research : Calculate(sim, player).CommerceHundredths.Research;
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
        long expenses = RecurringCosts.Calculate(sim, player, lines);
        long income = total.Gold / 100;
        lines.Add(new("economy:total", "economy:income", income));
        lines.Add(new("economy:total", "economy:net_gold", income - expenses));
        return new(total, income, expenses, income - expenses, new(lines));
    }
    internal void Settle(Simulation sim, int player)
    {
        var owner = sim.State.PlayerById(player);
        var state = EconomyData.Edit(sim, player);
        var result = Calculate(sim, player);
        long expenses = result.Expenses;
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
                netGold = total.Gold / 100 - expenses;
            }
        }
        long treasury = owner.Treasury + netGold;
        state.UnpaidGold = Math.Max(0, -treasury);
        owner.Treasury = Math.Max(0, treasury);
        state.SettledTurn = sim.State.Turn;
        state.SettledCommerceHundredths = total;
    }
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
