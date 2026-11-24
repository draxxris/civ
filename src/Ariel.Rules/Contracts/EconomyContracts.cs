using Ariel.Content;

namespace Ariel.Game;

public sealed record CityCommerceResult(CommerceVector Hundredths, Ledger Ledger);
public sealed record PlayerEconomyResult(CommerceVector CommerceHundredths, long Income, long Expenses, long NetGold, Ledger Ledger);
public interface IPlayerEconomyService
{
    CommerceVector Rates(Simulation sim, int player);
    string? ValidateRates(Simulation sim, int player, CommerceVector rates);
    void SetRates(Simulation sim, int player, CommerceVector rates);
    CityCommerceResult CalculateCity(Simulation sim, int city);
    PlayerEconomyResult Calculate(Simulation sim, int player);
    /// <summary>The player's economy as it stands, with the commerce rates left open: everything the rates do not
    /// change is read once. Read-only; valid until the state changes.</summary>
    IEconomyForecast Forecast(Simulation sim, int player);
}
/// <summary>A player's economy at fixed state for hypothetical commerce rates (<see cref="IPlayerEconomyService.Forecast"/>).</summary>
public interface IEconomyForecast
{
    /// <summary>The net gold <see cref="IPlayerEconomyService.Calculate"/> would report with these rates set, without
    /// setting them. The rates need not be legal.</summary>
    long NetGold(CommerceVector rates);
}
public interface IPlayerEspionageSource
{
    long EspionageHundredths(Simulation sim, int player);
}
/// <summary>Where a player's espionage commerce goes, answered by the feature that spends it, or null without one
/// (<see cref="GameRules.EspionageSpending"/>). That feature banks the points itself; the economy lists the forecast
/// in the player's ledger. Without one, the economy banks the output in
/// <see cref="EconomyPlayerState.EspionageHundredths"/>.</summary>
public interface IEspionageSpending
{
    /// <summary>Forecast whole points per target team for this much espionage output (hundredths). Does not change
    /// state.</summary>
    IReadOnlyDictionary<int, long> Spending(Simulation sim, int player, long espionageHundredths);
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
/// <summary>The Economy module's state domains (<see cref="StateDomain"/>).</summary>
public static class EconomyStateDomains
{
    /// <summary>A player's gold and settlement: the treasury, and the Economy player state (rates, unpaid gold, the last
    /// settlement, the inflation ratio, hurry and strike counts, banked espionage).</summary>
    public const string Treasury = "economy:treasury";
    /// <summary>A city's hurry anger: the Economy city state.</summary>
    public const string HurryAnger = "economy:hurry_anger";
    /// <summary>Declarations, one list each, so the kernel resolves each once.</summary>
    public static readonly IReadOnlyList<string> TreasuryOnly = [Treasury], HurryAngerOnly = [HurryAnger];
}
/// <summary>A player's economy. Its commerce vectors carry every loaded commerce type: the economy creates the state
/// from <see cref="CommerceRoster.InitialRates"/> and <see cref="CommerceRoster.Zero"/>.</summary>
public sealed class EconomyPlayerState
{
    /// <summary>Slider percent by commerce type.</summary>
    public CommerceVector Rates { get; set; }
    public long AccruedCostRatioTimes10000 { get; set; } = 10000;
    public long UnpaidGold { get; set; }
    public int HurriedCount { get; set; }
    public int StrikeTurns { get; set; }
    public long EspionageHundredths { get; set; }
    public int SettledTurn { get; set; }
    /// <summary>Commerce in hundredths by type at the last settlement (<see cref="SettledTurn"/>).</summary>
    public CommerceVector SettledCommerceHundredths { get; set; }
}
public sealed class EconomyCityState
{
    public int HurryAngerTurns { get; set; }
}
/// <summary>Set the commerce slider: percent by commerce type. A type the rates leave out is set to zero.</summary>
public sealed class SetCommerceRatesCommand : GameCommand
{
    public CommerceVector Rates { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Economy.ValidateRates(sim, Player, Rates);
    public override void Apply(Simulation sim) => sim.Rules().Economy.SetRates(sim, Player, Rates);
}
public sealed class HurryCityCommand : GameCommand
{
    public int CityId { get; init; }
    public string HurryId { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Rules().Hurry.Validate(sim, Player, CityId, HurryId);
    public override void Apply(Simulation sim) => sim.Rules().Hurry.Hurry(sim, CityId, HurryId);
}
