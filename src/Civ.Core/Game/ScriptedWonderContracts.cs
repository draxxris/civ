namespace Civ.Game;

/// <summary>Read-only scripted wonder status. Queries do not create state or consume randomness.</summary>
public interface IScriptedWonderService
{
    IReadOnlyList<ScriptedWonderStatus> DescribeCity(Simulation sim, int cityId);
    bool IsActive(Simulation sim, int cityId, string building);
    /// <summary>Building classes granted while an owned or team-shared constructed wonder is active.</summary>
    IReadOnlyList<string> EmpireFreeBuildings(Simulation sim, int player);
    /// <summary>True when an active wonder bars this barbarian land unit from entering the target hex.</summary>
    bool BlocksBarbarianEntry(Simulation sim, int barbarianUnitId, Hex targetHex) => false;
}

public sealed record ScriptedWonderStatus(string Building, bool Active, bool CompletionApplied,
    int ElapsedTurns, int SpawnInterval, string? SpawnUnit);

/// <summary>City-local timers survive capture, saves and obsolete periods. Inactive timers pause.</summary>
public sealed class ScriptedWonderCityState
{
    public SortedDictionary<string, ScriptedWonderProgress> Wonders { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ScriptedWonderProgress
{
    public bool CompletionApplied { get; set; }
    public int ElapsedTurns { get; set; }
    public int LastProcessedTurn { get; set; }
}

public sealed record ScriptedWonderEffectApplied(int City, string Building, string Effect, long Amount) : DomainEvent;
