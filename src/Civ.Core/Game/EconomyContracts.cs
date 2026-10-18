using Civ.Content;

namespace Civ.Game;

public sealed record CityCommerceResult(CommerceVector Hundredths, Ledger Ledger);
public sealed record PlayerEconomyResult(CommerceVector CommerceHundredths, long Income, long Expenses, long NetGold, Ledger Ledger);
public interface IPlayerEconomyService
{
    CommerceVector Rates(Simulation sim, int player);
    string? ValidateRates(Simulation sim, int player, CommerceVector rates);
    void SetRates(Simulation sim, int player, CommerceVector rates);
    CityCommerceResult CalculateCity(Simulation sim, int city);
    PlayerEconomyResult Calculate(Simulation sim, int player);
}
public interface IPlayerEspionageSource
{
    long EspionageHundredths(Simulation sim, int player);
}
/// <summary>Infinite queue items. Conversion uses current production, never stored overflow.</summary>
public interface ICityProcessService
{
    RequirementResult Validate(Simulation sim, int city, string process);
    CommerceVector Convert(Simulation sim, int city, string process, long production);
}
public sealed record HurryResult(long Gold, int Population, long Production, Ledger Ledger);
public interface ICityHurryService
{
    HurryResult Calculate(Simulation sim, int city, string hurry);
    string? Validate(Simulation sim, int player, int city, string hurry);
    void Hurry(Simulation sim, int city, string hurry);
}
public sealed class EconomyPlayerState
{
    public CommerceVector Rates { get; set; } = new(50, 50, 0, 0);
    public long AccruedCostRatioTimes10000 { get; set; } = 10000;
    public long UnpaidGold { get; set; }
    public int HurriedCount { get; set; }
    public int StrikeTurns { get; set; }
    public long EspionageHundredths { get; set; }
    public int SettledTurn { get; set; }
    public CommerceVector SettledCommerceHundredths { get; set; }
}
public sealed class EconomyCityState
{
    public int HurryAngerTurns { get; set; }
}
public sealed class SetCommerceRatesCommand : GameCommand
{
    public CommerceVector Rates { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IPlayerEconomyService>().Single().ValidateRates(sim, Player, Rates);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IPlayerEconomyService>().Single().SetRates(sim, Player, Rates);
}
public sealed class HurryCityCommand : GameCommand
{
    public int CityId { get; init; }
    public string HurryId { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityHurryService>().Single().Validate(sim, Player, CityId, HurryId);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityHurryService>().Single().Hurry(sim, CityId, HurryId);
}
