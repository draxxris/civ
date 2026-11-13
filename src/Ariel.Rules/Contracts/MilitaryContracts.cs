using System.Text.Json.Serialization;
using Ariel.Content;

namespace Ariel.Game;

/// <summary>Movement points are integer sub-moves, scaled by RuleConstants.MoveDenominator.</summary>
public interface IMovementService
{
    string? CanEnter(Simulation sim, int unit, TileId from, TileId to, bool attack = false);
    int StepCost(Simulation sim, int unit, TileId from, TileId to);
    string? ValidatePath(Simulation sim, int player, int unit, IReadOnlyList<Hex> path);
    /// <summary>Traverse until moves run out or combat interrupts. Path excludes the starting plot.</summary>
    void Move(Simulation sim, int unit, IReadOnlyList<Hex> path);
}
/// <summary>
/// A route excluding the starting tile. StepTurns aligns with Steps: 1 means this turn, 2 means next turn.
/// Arrival estimates use current moves and a full allowance on later turns, with unchanged movement rules.
/// An attack's final arrival assumes success; combat and subsequent world changes can interrupt the route.
/// </summary>
public sealed record UnitPath(IReadOnlyList<Hex> Steps, long Cost, IReadOnlyList<int> StepTurns)
{
    public int Turns => StepTurns.Count == 0 ? 1 : StepTurns[^1];
}
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
    /// <summary>A city's defence bonus as it stands, after the damage its defences have taken. Zero or less means
    /// there is nothing left to knock down.</summary>
    int CityDefense(Simulation sim, int city);
    /// <summary>The percentage of bombardment a city's working buildings absorb.</summary>
    int BombardResistance(Simulation sim, int city);
    /// <summary>Knock a city's defences down by <paramref name="reduction"/> defence points on behalf of a unit, mark
    /// the city bombarded this turn so it does not recover, and publish <see cref="CityBombarded"/>. Returns the
    /// reduction applied.</summary>
    int DamageCityDefense(Simulation sim, int unit, int city, int reduction);
    /// <summary>Apply an attacker's collateral damage to the units stacked with a defender, and return the hits.
    /// Consumes random numbers.</summary>
    IReadOnlyList<CollateralHit> Collateral(Simulation sim, int attacker, int defender);
    /// <summary>Close a strike that was not a fight between two units, after its damage is applied: remove a
    /// defender it killed, add the report to the combat history, publish <see cref="CombatResolved"/> when a unit was
    /// hit, and refresh sight. A null defender is fire that only reached a city.</summary>
    CombatReport RecordStrike(Simulation sim, int attacker, int? defender, Hex target, IReadOnlyList<CollateralHit> collateral);
    IReadOnlyList<CombatReport> History(Simulation sim);
}
/// <summary>Optional naval rules. Queries use current units and wars, with no saved blockade counters.</summary>
public interface INavalBlockadeService
{
    bool IsBlockaded(Simulation sim, TileId tile, int team);
    /// <summary>Every tile where <see cref="IsBlockaded"/> is true for the team. Whole-map queries use this once instead of once per tile.</summary>
    IReadOnlySet<TileId> BlockadedTiles(Simulation sim, int team) =>
        sim.State.Grid.Tiles.Where(t => IsBlockaded(sim, t, team)).ToHashSet();
}
public enum UnitActivity { Awake, Fortify, Sleep, Sentry, Intercept, Blockade }
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
    /// <summary>Active aerial reconnaissance sectors for the current turn.</summary>
    IReadOnlyList<ReconSight> ActiveRecon(Simulation sim);
    /// <summary>Active aerial reconnaissance sectors for a team for the current turn.</summary>
    IReadOnlyList<ReconSight> ActiveRecon(Simulation sim, int team);
}
public enum UnitAction { Promote, Activity, Pillage, Upgrade, Disband }
public interface IUnitService
{
    int MaximumMoves(Simulation sim, int unit);
    /// <summary>The total a unit's promotions and other modifier sources give it for one military effect, by the
    /// effect's name without the module prefix (for example "bombard_rate").</summary>
    int Effect(Simulation sim, int unit, string effect);
    /// <summary>Use up a unit's attack for the turn at a cost in movement points: it counts as having moved and
    /// attacked, and stops fortifying or sleeping.</summary>
    void SpendAttack(Simulation sim, int unit, int moves);
    int ExperienceNeeded(Simulation sim, int unit);
    int HealRate(Simulation sim, int unit);
    int CargoCapacity(Simulation sim, int transport);
    IReadOnlyList<int> CargoUnits(Simulation sim, int transport);
    string? ValidateLoad(Simulation sim, int player, int unit, int transport);
    void Load(Simulation sim, int unit, int transport);
    string? ValidateUnload(Simulation sim, int player, int unit, Hex? destination = null);
    void Unload(Simulation sim, int unit, Hex? destination = null);
    /// <summary>Remove a unit its owner gave up. Its cargo goes ashore where it can stand, on the unit's plot or a
    /// neighbour (a ship beside land, an airship over land); cargo with nowhere to stand is lost with it. Returns every
    /// unit removed, the disbanded unit last.</summary>
    IReadOnlyList<int> Disband(Simulation sim, int unit);
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
    private int _fortifyTurns;
    /// <summary>Completed turn boundaries rested while fortified, granting defense bonuses on subsequent turns.</summary>
    public int FortifyTurns
    {
        get => _activity == UnitActivity.Fortify ? _fortifyTurns : 0;
        set
        {
            _fortifyTurns = value;
            if (value > 0 && _activity != UnitActivity.Fortify) _activity = UnitActivity.Fortify;
        }
    }
    public bool Moved { get; set; }
    public bool Attacked { get; set; }
    public bool InterceptedThisTurn { get; set; }
    private UnitActivity _activity;
    public UnitActivity Activity
    {
        get => _activity;
        set
        {
            _activity = value;
            if (value != UnitActivity.Fortify) _fortifyTurns = 0;
        }
    }
    public int CreatedTurn { get; set; }
    public string? AttachedLeaderUnitType { get; set; }
    public int? TransportId { get; set; }

    /// <summary>Wake the unit and clear any ongoing fortification counter.</summary>
    public void BreakActivity()
    {
        _activity = UnitActivity.Awake;
        _fortifyTurns = 0;
    }
}
/// <summary>The barbarian faction: one player on its own team, at war with every other team. Spawns, hostile
/// villagers and rebellions all field units for it.</summary>
public interface IBarbarianFaction
{
    /// <summary>The barbarian player, created on first use. Every caller gets the same player for the rest of the game.</summary>
    int Ensure(Simulation sim);
}
/// <summary>Combat counters of one player. Combat wins against civilizations earn <see cref="GeneralPoints"/>; the
/// feature that turns them into leaders (<see cref="ICombatLeaders"/>) spends them and keeps the other two counts.</summary>
public sealed class MilitaryPlayerState
{
    public int WinsAgainstBarbarians { get; set; }
    public long GeneralPoints { get; set; }
    public int GreatGeneralsCreated { get; set; }
    public int GeneralThresholdModifier { get; set; }
}
/// <summary>Leaders that emerge from the general points a player's combat wins earn, answered by the feature that has
/// such leaders, or null without one (<see cref="GameRules.CombatLeaders"/>). Combat adds the points to
/// <see cref="MilitaryPlayerState.GeneralPoints"/> and asks at once; without an answer the points just accumulate.</summary>
public interface ICombatLeaders
{
    /// <summary>The winner's player has just earned general points: spend every threshold they cover on a leader.
    /// Runs inside combat resolution, so any randomness it draws is part of the fight.</summary>
    void Emerge(Simulation sim, Unit winner);
}
/// <summary>Whether something bars a barbarian unit from a plot it could otherwise enter, such as a wonder that keeps
/// barbarians out of its owner's lands. Movement asks every implementation before a barbarian moves without
/// attacking; an attack is never barred. Queries never change state or use the RNG.</summary>
public interface IBarbarianBarrier
{
    /// <summary>Whether this barbarian unit may not enter the target plot.</summary>
    bool Blocks(Simulation sim, int barbarianUnit, Hex target);
}
/// <summary>Combat is atomic. Reports survive saving between commands within the turn.</summary>
public sealed class MilitaryGameState
{
    /// <summary>The one barbarian player, or null before the faction first appears. It never changes once set.</summary>
    [JsonInclude] public int? BarbarianPlayer { get; private set; }

    /// <summary>Record the barbarian player. Setting it again to the same player is a no-op; any other player throws.</summary>
    public void SetBarbarianPlayer(int player)
    {
        if (BarbarianPlayer is { } existing && existing != player)
            throw new InvalidOperationException($"Player {existing} is already the barbarian player");
        BarbarianPlayer = player;
    }
    public List<CombatReport> Combats { get; set; } = [];
    public List<ReconSight> ReconSights { get; set; } = [];

    /// <summary>
    /// Adds or updates an active recon sector for a team, deduplicating matching center hexes
    /// and retaining the larger radius.
    /// </summary>
    public bool AddReconSight(ReconSight sight)
    {
        ArgumentNullException.ThrowIfNull(sight);
        for (int i = 0; i < ReconSights.Count; i++)
        {
            var existing = ReconSights[i];
            if (existing.Team == sight.Team && existing.Center == sight.Center)
            {
                if (sight.Radius > existing.Radius)
                {
                    ReconSights[i] = sight;
                    return true;
                }
                return false;
            }
        }
        ReconSights.Add(sight);
        return true;
    }

    /// <summary>Adds or updates an active recon sector for a team.</summary>
    public bool AddReconSight(int team, Hex center, int radius) =>
        AddReconSight(new ReconSight(team, center, radius));
}
/// <summary>Minimal ownership and razing boundary; Cities owns city state changes.</summary>
public interface ICityCaptureService
{
    void Capture(Simulation sim, int city, int player);
    string? ValidateRaze(Simulation sim, int city, int player);
    void Raze(Simulation sim, int city);
}
public sealed record CityBombarded(int Unit, int City, int DefenseReduction, int TotalDefenseDamage) : DomainEvent;
public sealed record CityCaptured(int City, int PreviousOwner, int Player, int CaptureGold = 0, bool AutoRazed = false) : DomainEvent;
/// <summary>A city was destroyed by its owner <paramref name="Player"/>. <paramref name="CapturedFrom"/> is the player it was
/// last captured from, if any.</summary>
public sealed record CityRazed(int City, int Player, Hex Position, int? CapturedFrom = null) : DomainEvent;
public sealed record PlotPillaged(int Unit, int Player, TileId Plot, string? Improvement, string? ReplacementImprovement, string? Route, int Gold) : DomainEvent;
public sealed record GoodyReceived(int Unit, string Goody) : DomainEvent;

/// <summary>A combat finished; the loser was removed. Report is the fight as it resolved, so presentation can replay it after the turn rolls over.</summary>
public sealed record CombatResolved(int Attacker, int Defender, int Winner, int LoserOwner = -1, CombatReport? Report = null) : DomainEvent;
public sealed record AirMissionResolved(AirMissionReport Report) : DomainEvent;
