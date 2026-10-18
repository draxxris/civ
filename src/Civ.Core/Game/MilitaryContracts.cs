using Civ.Content;

namespace Civ.Game;

/// <summary>Movement points are integer sub-moves, scaled by RuleConstants.MoveDenominator.</summary>
public interface IMovementService
{
    string? CanEnter(Simulation sim, int unit, TileId from, TileId to, bool attack = false);
    int StepCost(Simulation sim, int unit, TileId from, TileId to);
    string? ValidatePath(Simulation sim, int player, int unit, IReadOnlyList<Hex> path);
    /// <summary>Traverse until moves run out or combat interrupts. Path excludes the starting plot.</summary>
    void Move(Simulation sim, int unit, IReadOnlyList<Hex> path);
}
public sealed record UnitPath(IReadOnlyList<Hex> Steps, long Cost);
/// <summary>Deterministic minimum-cost paths. Queries use no RNG and do not mutate state.</summary>
public interface IPathfindingService
{
    UnitPath? FindPath(Simulation sim, int unit, Hex destination, bool attack = false);
}
public sealed record CombatStrength(int Maximum, int Current, int Firepower, Ledger Ledger);
/// <summary>Odds are millionths. Success includes reaching the combat damage limit, not withdrawal.</summary>
public sealed record CombatPreview(int Attacker, int Defender, CombatStrength AttackStrength, CombatStrength DefenseStrength,
    int DefenderRoundOdds, int DieSides, int DamageToAttacker, int DamageToDefender, int SuccessOdds,
    int WithdrawalOdds, int AttackerFirstStrikes, int AttackerChanceFirstStrikes, int DefenderFirstStrikes,
    int DefenderChanceFirstStrikes, int WithdrawalPercent, int CombatLimit);
public enum CombatOutcome { AttackerWon, DefenderWon, Withdrawn, LimitedDamage, DefenderWithdrawn }
public sealed record CombatRound(int Number, int AttackerHealth, int DefenderHealth, bool FirstStrike);
public sealed record CollateralHit(int Unit, int Damage);
/// <summary>One fight. Unit fields are captured as it resolves, so they stay true after a loser is removed or a winner moves.</summary>
public sealed record CombatReport(int Turn, int Attacker, int Defender, CombatOutcome Outcome,
    IReadOnlyList<CombatRound> Rounds, IReadOnlyList<CollateralHit> Collateral,
    string AttackerType = "", int AttackerOwner = -1, Hex AttackerHex = default,
    string DefenderType = "", int DefenderOwner = -1, Hex DefenderHex = default, bool LoserDied = false, IReadOnlyList<CollateralHit>? Flanking = null);
public interface ICombatService
{
    int? BestDefender(Simulation sim, int attacker, Hex target);
    CombatPreview Preview(Simulation sim, int attacker, int defender);
    CombatReport Resolve(Simulation sim, int attacker, int defender);
    string? ValidateBombard(Simulation sim, int player, int unit, Hex target);
    int BombardCity(Simulation sim, int unit, Hex target);
    string? ValidateRangedStrike(Simulation sim, int player, int unit, Hex target);
    CombatReport RangedStrike(Simulation sim, int unit, Hex target);
    IReadOnlyList<CombatReport> History(Simulation sim);
}
public enum UnitActivity { Awake, Fortify, Sleep, Sentry, Intercept }
public enum AirMissionKind { Rebase, Recon, AirStrike, AirBomb }
public sealed record ReconSight(int Team, Hex Center, int Radius);
public sealed record AirMissionReport(AirMissionKind Kind, int Unit, Hex Target, int? Interceptor,
    int AttackerDamage, int InterceptorDamage, int? TargetUnit, int TargetDamage,
    int CityDefenseReduction, string? DestroyedImprovement);
public interface IAirMissionService
{
    int AirRange(Simulation sim, int unit);
    int InterceptionProbability(Simulation sim, int unit);
    string? Validate(Simulation sim, int player, int unit, AirMissionKind kind, Hex target);
    AirMissionReport Execute(Simulation sim, int unit, AirMissionKind kind, Hex target);
}
public enum UnitAction { Promote, Activity, Pillage, Upgrade, Disband }
public interface IUnitService
{
    int MaximumMoves(Simulation sim, int unit);
    int ExperienceNeeded(Simulation sim, int unit);
    int HealRate(Simulation sim, int unit);
    int CargoCapacity(Simulation sim, int transport);
    IReadOnlyList<int> CargoUnits(Simulation sim, int transport);
    string? ValidateLoad(Simulation sim, int player, int unit, int transport);
    void Load(Simulation sim, int unit, int transport);
    string? ValidateUnload(Simulation sim, int player, int unit, Hex? destination = null);
    void Unload(Simulation sim, int unit, Hex? destination = null);
    long UpgradePrice(Simulation sim, int unit, string target);
    string? Validate(Simulation sim, int player, int unit, UnitAction action, string argument);
    void Execute(Simulation sim, int unit, UnitAction action, string argument);
    void Initialize(Simulation sim, int unit);
}
public sealed record UnitSupportCount(int Total, int Military, int OutsideBorders);
/// <summary>Counts only. Economy owns free allowances and prices.</summary>
public interface IUnitSupportService
{
    UnitSupportCount Count(Simulation sim, int player);
}
/// <summary>Saved unit timers and activity. Earned levels do not count free promotions.</summary>
public sealed class MilitaryUnitState
{
    public int Level { get; set; } = 1;
    public int ExperienceRemainder { get; set; }
    public int FortifyTurns { get; set; }
    public bool Moved { get; set; }
    public bool Attacked { get; set; }
    public bool InterceptedThisTurn { get; set; }
    public UnitActivity Activity { get; set; }
    public int CreatedTurn { get; set; }
    public string? AttachedLeaderUnitType { get; set; }
    public int? TransportId { get; set; }
}
public sealed class MilitaryPlayerState
{
    public bool Barbarian { get; set; }
    public int WinsAgainstBarbarians { get; set; }
    public long GeneralPoints { get; set; }
    public int GreatGeneralsCreated { get; set; }
    public int GeneralThresholdModifier { get; set; }
}
/// <summary>Combat is atomic. Reports survive saving between commands within the turn.</summary>
public sealed class MilitaryGameState
{
    public List<CombatReport> Combats { get; set; } = [];
    public List<ReconSight> ReconSights { get; set; } = [];
}
/// <summary>Minimal ownership and razing boundary; Cities owns city state changes.</summary>
public interface ICityCaptureService
{
    void Capture(Simulation sim, int city, int player);
    string? ValidateRaze(Simulation sim, int city, int player);
    void Raze(Simulation sim, int city);
}
public sealed record CityBombarded(int Unit, int City, int DefenseReduction, int TotalDefenseDamage) : DomainEvent;
public sealed record CityCaptured(int City, int PreviousOwner, int Player) : DomainEvent;
public sealed record CityRazed(int City, int Player, Hex Position) : DomainEvent;
public sealed record PlotPillaged(int Unit, int Player, TileId Plot, string? Improvement, string? ReplacementImprovement, string? Route, int Gold) : DomainEvent;
public sealed record GoodyReceived(int Unit, string Goody) : DomainEvent;
