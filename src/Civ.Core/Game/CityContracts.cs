using Civ.Content;

namespace Civ.Game;

/// <summary>City output before sliders. Direct commerce is not raw commerce; percentages are additive bonuses.</summary>
public sealed record CityYieldResult(YieldVector BaseYields, YieldVector Yields, CommerceVector DirectCommerceHundredths,
    CommerceVector CommercePercent, Ledger Ledger);
public interface ICityYieldService
{
    CityYieldResult Calculate(Simulation sim, int city);
}
public sealed record CityWellbeing(int Happy, int Unhappy, int Angry, int Healthy, int Unhealthy, int FoodPenalty, Ledger Ledger);
/// <summary>Nonnegative pollution penalties, including building population effects but excluding citizen-count and terrain penalties.</summary>
public sealed record CityEnvironmentalUnhealth(long Buildings, long Bonuses, long Power);
public interface ICityWellbeingService
{
    CityWellbeing Calculate(Simulation sim, int city);
    /// <summary>Source penalties before healthy effects cancel them. Rulesets without pollution may return zero.</summary>
    CityEnvironmentalUnhealth EnvironmentalUnhealth(Simulation sim, int city) => new(0, 0, 0);
}
public sealed record CityGrowthResult(long Threshold, long Consumption, long Difference, int FoodKeptPercent, Ledger Ledger);
public interface ICityGrowthService
{
    CityGrowthResult Calculate(Simulation sim, int city);
}
/// <summary>Economy supplies FINAL culture in hundredths, including direct contributions and modifiers.
/// Without a provider Cities uses direct culture only. Providers must not mutate state.</summary>
public interface ICityCultureSource
{
    long CultureHundredths(Simulation sim, int city);
}
/// <summary>Immediate culture grants use the same border and historical-culture rules as city turns.</summary>
public interface ICityCultureService
{
    void AddCulture(Simulation sim, int city, long culture, int spreadTurns);
    int LevelIndex(Simulation sim, int city);
    Civ.Content.Schema.CultureLevelDef Level(Simulation sim, int city);
    int OwnerCulturePercent(Simulation sim, int city);
    int TeamCulturePercent(Simulation sim, int city);
}
public interface ICityWorkService
{
    string DefaultSpecialist(Simulation sim);
    int Radius(Simulation sim, int city);
    IReadOnlyList<TileId> WorkArea(Simulation sim, int city);
    string? Validate(Simulation sim, int city, IReadOnlyList<Hex> tiles, IReadOnlyDictionary<string, int> specialists);
    void Assign(Simulation sim, int city);
}
public sealed record CityMaintenanceResult(long DistanceHundredths, long CityCountHundredths, long TotalHundredths, Ledger Ledger, long CorporateHundredths = 0);
public interface ICityMaintenanceService
{
    CityMaintenanceResult Calculate(Simulation sim, int city);
}
/// <summary>Read-only building views include empire grants without changing a city's constructed buildings.</summary>
public interface ICityBuildingService
{
    IReadOnlyList<string> EffectiveBuildings(Simulation sim, int city);
    IReadOnlyList<string> ActiveBuildings(Simulation sim, int city);
    bool IsPowered(Simulation sim, int city);
    bool IsDirtyPower(Simulation sim, int city);
    /// <summary>Checks whether a candidate building supplies power with the city's current resources.</summary>
    bool ProvidesPower(Simulation sim, int city, string building);
}
public sealed record CityProductionResult(long Cost, long Production, int ModifierPercent, Ledger Ledger);
public interface ICityProductionService
{
    RequirementResult Validate(Simulation sim, int city, string kind, string content);
    CityProductionResult Calculate(Simulation sim, int city, string kind, string content);
}
public sealed record ConscriptResult(string UnitId, int PopulationCost, int MinPopulation, int Experience, int AngerTurns, int MaxPlayerDrafts, Ledger Ledger);
public interface ICityConscriptService
{
    ConscriptResult? Calculate(Simulation sim, int city);
    string? Validate(Simulation sim, int player, int city);
    void Conscript(Simulation sim, int city);
}
public sealed record CityGreatPeopleResult(long Threshold, long Rate, Ledger Ledger);
public interface ICityGreatPeopleService
{
    CityGreatPeopleResult Calculate(Simulation sim, int city);
}
/// <summary>Optional city predicates supplied to Foundation, without a dependency on the Cities assembly.</summary>
public interface ICityRequirementService
{
    string? Evaluate(Requirement.Leaf leaf, RequirementContext context);
}
/// <summary>Queue and founding mutations owned by Cities. Commands call the same validation as previews.</summary>
public enum CityConquestOption { Annex, Puppet, Raze }
public interface ICityCommandService
{
    string? ValidateFound(Simulation sim, int player, int settler);
    void Found(Simulation sim, int player, int settler);
    string? ValidateConquestChoice(Simulation sim, int player, int city, CityConquestOption option);
    void ResolveConquestChoice(Simulation sim, int player, int city, CityConquestOption option);
    string? ValidateQueue(Simulation sim, int player, int city, string kind, string content, bool append, int? remove);
    void Queue(Simulation sim, int city, string kind, string content, bool append, int? remove);
    string? ValidateQueueMove(Simulation sim, int player, int city, int from, int to);
    void MoveQueue(Simulation sim, int city, int from, int to);
}
/// <summary>Saved feature-ruleset city state. Culture keeps fractional output separately from the legacy whole value.</summary>
public sealed class CityRuleState
{
    public int TurnAcquired { get; set; }
    public int HighestPopulation { get; set; } = 1;
    public int OccupationTurns { get; set; }
    public bool IsPuppet { get; set; }
    public bool PendingConquestChoice { get; set; }
    public long FoodKept { get; set; }
    public long Overflow { get; set; }
    public long FeatureProduction { get; set; }
    public bool ManualWork { get; set; }
    public bool AvoidGrowth { get; set; }
    public bool Disorder { get; set; }
    public int DefenseDamage { get; set; }
    public bool BombardedThisTurn { get; set; }
    public int ConscriptAngerTurns { get; set; }
    public bool DraftedThisTurn { get; set; }
    public string? OriginalCivilization { get; set; }
    public long CultureRemainder { get; set; }
    public long LastCultureRateHundredths { get; set; }
    public SortedDictionary<int, long> CultureHundredths { get; set; } = new();
    public long GreatPeopleProgress { get; set; }
    public SortedDictionary<string, long> GreatPeopleWeights { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> PausedProduction { get; set; } = new(StringComparer.Ordinal);
    public ProductionOrder? PendingCompletion { get; set; }
    public long PendingCost { get; set; }
    public long PendingProduction { get; set; }
    public int PendingModifierPercent { get; set; }
}
/// <summary>Research can update CurrentEra. Null uses the starting era until era progression is implemented.</summary>
public sealed class CityPlayerState
{
    public string? CurrentEra { get; set; }
    public bool GoldenAge { get; set; }
    public int GreatPeopleBorn { get; set; }
    public int ConscriptCountThisTurn { get; set; }
    public int GreatPeopleThresholdModifier { get; set; }
}
public sealed class FoundCityWithSettlerCommand : GameCommand
{
    public int Settler { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().ValidateFound(sim, Player, Settler);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().Found(sim, Player, Settler);
}
public sealed class SetCityAssignmentsCommand : GameCommand
{
    public int CityId { get; init; }
    public Hex[] Tiles { get; init; } = [];
    public SortedDictionary<string, int> Specialists { get; init; } = new(StringComparer.Ordinal);
    public bool Automatic { get; init; }
    public bool AvoidGrowth { get; init; }
    public override string? Validate(Simulation sim)
    {
        var city = sim.State.FindCity(CityId);
        if (city is null || city.Owner != Player) return "Not your city";
        if (city.Extensions.TryGet<CityRuleState>(sim.Content)?.IsPuppet == true) return "Puppet cities manage their own citizens";
        return Automatic ? null : sim.Content.Services.Get<ICityWorkService>().Single().Validate(sim, CityId, Tiles, Specialists);
    }
    public override void Apply(Simulation sim)
    {
        var city = sim.State.CityById(CityId);
        var state = city.Extensions.Get<CityRuleState>(sim.Content) ?? new();
        city.Extensions.Set(sim.Content, state);
        state.ManualWork = !Automatic; state.AvoidGrowth = AvoidGrowth;
        city.Worked = Tiles.Select(sim.State.Grid.Normalize).OrderBy(h => h.Q).ThenBy(h => h.R).ToList();
        city.Specialists = new(Specialists, StringComparer.Ordinal);
        sim.Content.Services.Get<ICityWorkService>().Single().Assign(sim, CityId);
    }
}
public sealed class MoveCityQueueCommand : GameCommand
{
    public int CityId { get; init; }
    public int From { get; init; }
    public int To { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().ValidateQueueMove(sim, Player, CityId, From, To);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().MoveQueue(sim, CityId, From, To);
}
public sealed class ResolveCityConquestCommand : GameCommand
{
    public int CityId { get; init; }
    public CityConquestOption Option { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().ValidateConquestChoice(sim, Player, CityId, Option);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().ResolveConquestChoice(sim, Player, CityId, Option);
}
public sealed class ConscriptCityCommand : GameCommand
{
    public int CityId { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityConscriptService>().Single().Validate(sim, Player, CityId);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityConscriptService>().Single().Conscript(sim, CityId);
}
public sealed class SetCityProductionCommand : GameCommand
{
    public int CityId { get; init; }
    public string Kind { get; init; } = "";
    public string ContentId { get; init; } = "";
    public bool Append { get; init; }
    public int? RemoveIndex { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().ValidateQueue(sim, Player, CityId, Kind, ContentId, Append, RemoveIndex);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().Queue(sim, CityId, Kind, ContentId, Append, RemoveIndex);
}
