using System.Text.Json.Serialization;
using Ariel.Content;

namespace Ariel.Game;

public sealed record ResearchCostResult(long Cost, Ledger Ledger);
public interface IResearchService
{
    ResearchCostResult CalculateCost(Simulation sim, int team, string tech);
    int RateModifier(Simulation sim, int team, string tech);
    /// <summary>Physical eligibility, independent of optional selection rules and random hands.</summary>
    RequirementResult Validate(Simulation sim, int player, string tech);
    /// <summary>Whether the player can select this technology now. Pure query.</summary>
    RequirementResult ValidateSelection(Simulation sim, int player, string tech);
    string? ValidateQueue(Simulation sim, int player, IReadOnlyList<string> queue, bool freeTech = false);
    void SetQueue(Simulation sim, int player, IReadOnlyList<string> queue, bool freeTech = false);
    /// <summary>Whole unmodified beakers. Overflow is normalized before moving between technologies.</summary>
    void Contribute(Simulation sim, int player, long beakers);
    /// <summary>Immediate unmodified research for a specific eligible technology, capped at remaining cost.</summary>
    void AddProgress(Simulation sim, int player, string tech, long beakers);
}
/// <summary>Economy supplies research settled before discoveries and city production.</summary>
public interface IPlayerResearchSource
{
    long ResearchHundredths(Simulation sim, int player);
}
public sealed class ResearchTeamState
{
    private List<string> _queue = [];

    public List<string> Queue
    {
        get => _queue;
        set => _queue = value != null ? SanitizeQueue(value) : [];
    }
    public long Overflow { get; set; }
    public int FreeTechs { get; set; }
    public SortedDictionary<string, int> Completions { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Explicit contact gate for the known-team rate bonus. Diplomacy can populate this set.</summary>
    public SortedSet<int> MetTeams { get; set; } = new();

    [JsonIgnore]
    public bool HasQueue => _queue.Count > 0;

    [JsonIgnore]
    public int QueueCount => _queue.Count;

    [JsonIgnore]
    public string? CurrentTarget => _queue.FirstOrDefault();

    public void Enqueue(string tech)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tech);
        _queue.Add(tech);
    }

    public bool RemoveFromQueue(string tech)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tech);
        return _queue.RemoveAll(t => string.Equals(t, tech, StringComparison.Ordinal)) > 0;
    }

    public string? PopQueue()
    {
        if (_queue.Count == 0) return null;
        string top = _queue[0];
        _queue.RemoveAt(0);
        return top;
    }

    public void ClearQueue() => _queue.Clear();

    public static List<string> SanitizeQueue(IEnumerable<string> items) =>
        items.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
}
public sealed class SetTeamResearchCommand : GameCommand
{
    public string Tech { get; init; } = "";
    public string[] Queue { get; init; } = [];
    public bool FreeTech { get; init; }
    private IReadOnlyList<string> Targets => Tech.Length == 0 ? Queue : new[] { Tech }.Concat(Queue).ToArray();
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IResearchService>().Single().ValidateQueue(sim, Player, Targets, FreeTech);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IResearchService>().Single().SetQueue(sim, Player, Targets, FreeTech);
}
