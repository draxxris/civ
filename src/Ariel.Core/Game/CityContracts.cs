using System.Text.Json.Serialization;
using Ariel.Content;

namespace Ariel.Game;

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
    Ariel.Content.Schema.CultureLevelDef Level(Simulation sim, int city);
    int OwnerCulturePercent(Simulation sim, int city);
    int TeamCulturePercent(Simulation sim, int city);
}
public interface ICityWorkService
{
    string DefaultSpecialist(Simulation sim);
    int Radius(Simulation sim, int city);
    IReadOnlyList<TileId> WorkArea(Simulation sim, int city);
    string? Validate(Simulation sim, int city, IReadOnlyList<TileId> tiles, IReadOnlyDictionary<string, int> specialists);
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
/// <summary>Strongly-typed allocation-free key for paused city production progress.</summary>
public readonly record struct ProductionKey : IComparable<ProductionKey>
{
    public string Kind { get; }
    public string ContentId { get; }

    public ProductionKey(string kind, string contentId)
    {
        Kind = kind ?? string.Empty;
        ContentId = contentId ?? string.Empty;
    }

    public static ProductionKey Of(string kind, string contentId) => new(kind, contentId);

    [JsonIgnore]
    public string Key => $"{Kind}/{ContentId}";

    public override string ToString() => Key;

    public int CompareTo(ProductionKey other)
    {
        int cmp = string.CompareOrdinal(Kind, other.Kind);
        return cmp != 0 ? cmp : string.CompareOrdinal(ContentId, other.ContentId);
    }

    public static ProductionKey Parse(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        int slash = key.IndexOf('/');
        if (slash < 0) throw new FormatException($"Invalid production key: {key}");
        return new(key[..slash], key[(slash + 1)..]);
    }

    public static implicit operator string(ProductionKey key) => key.Key;
}

/// <summary>Queue and founding mutations owned by Cities. Commands call the same validation as previews.</summary>
public enum CityConquestOption { Annex, Puppet, Raze }
public interface ICityCommandService
{
    string? ValidateFound(Simulation sim, int player, int settler);
    void Found(Simulation sim, int player, int settler);
    string? ValidateConquestChoice(Simulation sim, int player, int city, CityConquestOption option);
    void ResolveConquestChoice(Simulation sim, int player, int city, CityConquestOption option);
    string? ValidateQueue(Simulation sim, int player, int city, string kind, string content, bool append, int? remove, int? removeOrderId = null);
    void Queue(Simulation sim, int city, string kind, string content, bool append, int? remove, int? removeOrderId = null);
    string? ValidateQueueMove(Simulation sim, int player, int city, int from, int to, int? orderId = null, int? targetOrderId = null);
    void MoveQueue(Simulation sim, int city, int from, int to, int? orderId = null, int? targetOrderId = null);
}
/// <summary>City political and governance relationship to its controlling player.</summary>
public enum CityGovernance
{
    /// <summary>Standard citizen control; queues and citizens are fully managed by the owner.</summary>
    Integrated = 0,
    /// <summary>Captured city awaiting conquest choice (Annex, Puppet, or Raze). Defaults to Puppet at turn end.</summary>
    PendingConquest = 1,
    /// <summary>Semi-autonomous puppet; citizens and production self-manage, cannot be hurried or drafted.</summary>
    Puppet = 2,
}

/// <summary>Atomic production completion payload evaluated at CityProduction and committed at CityCompletion.</summary>
public sealed record CompletedOrderBatch(ProductionOrder Order, long Cost, long Production, int ModifierPercent);

/// <summary>Saved feature-ruleset city state. Culture keeps fractional output separately from the legacy whole value.</summary>
public sealed class CityRuleState
{
    public int TurnAcquired { get; set; }
    public int HighestPopulation { get; set; } = 1;
    public int OccupationTurns { get; set; }
    public CityGovernance Governance { get; set; } = CityGovernance.Integrated;
    [JsonIgnore]
    public bool IsPuppet
    {
        get => Governance is CityGovernance.Puppet or CityGovernance.PendingConquest;
        set
        {
            if (value)
            {
                if (Governance != CityGovernance.PendingConquest) Governance = CityGovernance.Puppet;
            }
            else Governance = CityGovernance.Integrated;
        }
    }
    [JsonIgnore]
    public bool PendingConquestChoice
    {
        get => Governance == CityGovernance.PendingConquest;
        set
        {
            if (value) Governance = CityGovernance.PendingConquest;
            else if (Governance == CityGovernance.PendingConquest) Governance = CityGovernance.Integrated;
        }
    }
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

    public long GetPausedProgress(ProductionKey key) =>
        PausedProduction.GetValueOrDefault(key.Key);

    public long GetPausedProgress(string kind, string contentId) =>
        GetPausedProgress(ProductionKey.Of(kind, contentId));

    public void SetPausedProgress(ProductionKey key, long progress) =>
        PausedProduction[key.Key] = progress;

    public bool RemovePausedProgress(ProductionKey key) =>
        PausedProduction.Remove(key.Key);

    public CompletedOrderBatch? PendingCommit { get; set; }
    [JsonIgnore]
    public ProductionOrder? PendingCompletion
    {
        get => PendingCommit?.Order;
        set => PendingCommit = value is null ? null : new(value, 0, 0, 100);
    }
    [JsonIgnore]
    public long PendingCost
    {
        get => PendingCommit?.Cost ?? 0;
        set { if (PendingCommit is { } c) PendingCommit = c with { Cost = value }; }
    }
    [JsonIgnore]
    public long PendingProduction
    {
        get => PendingCommit?.Production ?? 0;
        set { if (PendingCommit is { } c) PendingCommit = c with { Production = value }; }
    }
    [JsonIgnore]
    public int PendingModifierPercent
    {
        get => PendingCommit?.ModifierPercent ?? 0;
        set { if (PendingCommit is { } c) PendingCommit = c with { ModifierPercent = value }; }
    }
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
    public TileId[] Tiles { get; init; } = [];
    public Hex[]? Hexes { get; init; }
    public SortedDictionary<string, int> Specialists { get; init; } = new(StringComparer.Ordinal);
    public bool Automatic { get; init; }
    public bool AvoidGrowth { get; init; }

    public IReadOnlyList<TileId> ResolvedTiles(Simulation sim)
    {
        if (Hexes is { Length: > 0 })
        {
            var list = new List<TileId>(Hexes.Length);
            foreach (var h in Hexes)
                if (sim.State.Grid.TryTile(h, out var t)) list.Add(t);
            return list;
        }
        return Tiles;
    }

    public override string? Validate(Simulation sim)
    {
        var city = sim.State.FindCity(CityId);
        if (city is null || city.Owner != Player) return "Not your city";
        if (city.Extensions.TryGet<CityRuleState>(sim.Content)?.IsPuppet == true) return "Puppet cities manage their own citizens";
        if (Automatic) return null;
        if (Hexes is { Length: > 0 })
        {
            var list = new List<TileId>(Hexes.Length);
            foreach (var h in Hexes)
            {
                if (!sim.State.Grid.TryTile(h, out var t)) return "Work plot is outside the map";
                list.Add(t);
            }
            return sim.Content.Services.Get<ICityWorkService>().Single().Validate(sim, CityId, list, Specialists);
        }
        return sim.Content.Services.Get<ICityWorkService>().Single().Validate(sim, CityId, Tiles, Specialists);
    }
    public override void Apply(Simulation sim)
    {
        var city = sim.State.CityById(CityId);
        var state = city.Extensions.Get<CityRuleState>(sim.Content) ?? new();
        city.Extensions.Set(sim.Content, state);
        state.ManualWork = !Automatic; state.AvoidGrowth = AvoidGrowth;
        var resolved = ResolvedTiles(sim);
        city.Worked = resolved.Distinct().OrderBy(t => t.Value).ToList();
        city.Specialists = new(Specialists, StringComparer.Ordinal);
        sim.Content.Services.Get<ICityWorkService>().Single().Assign(sim, CityId);
    }
}
public sealed class MoveCityQueueCommand : GameCommand
{
    public int CityId { get; init; }
    public int From { get; init; }
    public int To { get; init; }
    public int? OrderId { get; init; }
    public int? TargetOrderId { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().ValidateQueueMove(sim, Player, CityId, From, To, OrderId, TargetOrderId);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().MoveQueue(sim, CityId, From, To, OrderId, TargetOrderId);
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
    public int? RemoveOrderId { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().ValidateQueue(sim, Player, CityId, Kind, ContentId, Append, RemoveIndex, RemoveOrderId);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityCommandService>().Single().Queue(sim, CityId, Kind, ContentId, Append, RemoveIndex, RemoveOrderId);
}
