namespace Civ.Game;

/// <summary>Plot data at a row-major TileId. River bits follow Hex.Directions;
/// edits must use GameState.SetRiver to update both sides of shared edges.</summary>
public sealed class Plot
{
    /// <summary>Terrain content id.</summary>
    public string Terrain { get; set; } = "";
    /// <summary>Flatland, hills or peak, independent of terrain.</summary>
    public PlotKind Kind { get; set; }
    /// <summary>Small inland water area, assigned by the world rules.</summary>
    public bool IsLake { get; set; }
    /// <summary>Worked turns toward the next improvement upgrade.</summary>
    public int ImprovementTurns { get; set; }
    /// <summary>Blocks mine builds until a resource discovery or reveal permits rebuilding.</summary>
    public bool DepletedMine { get; set; }
    /// <summary>Shared worker progress by build id.</summary>
    public SortedDictionary<string, long> BuildProgress { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Optional feature content id.</summary>
    public string? Feature { get; set; }
    /// <summary>Optional resource content id.</summary>
    public string? Bonus { get; set; }
    /// <summary>Optional improvement content id.</summary>
    public string? Improvement { get; set; }
    /// <summary>Optional route content id.</summary>
    public string? Route { get; set; }
    /// <summary>Six river-edge bits, mirrored on adjacent plots.</summary>
    public byte RiverEdges
    {
        get => _riverEdges;
        set
        {
            if (value > 63) throw new ArgumentOutOfRangeException(nameof(value), "River masks use only six bits");
            _riverEdges = value;
        }
    }
    private byte _riverEdges;
    /// <summary>Controlling player, or null.</summary>
    public int? Owner { get; set; }
    /// <summary>Accumulated culture by player id.</summary>
    public SortedDictionary<int, long> Culture { get; set; } = new();
    /// <summary>Compact team-id bitset for explored plots.</summary>
    public TeamMask Revealed { get; set; } = new();
    /// <summary>Compact team-id bitset for current sight.</summary>
    public TeamMask Visible { get; set; } = new();
    /// <summary>Last observed state per team. Missing memory stays unknown until observation.</summary>
    public SortedDictionary<int, RevealedPlotSnapshot> RevealedSnapshots { get; set; } = new();
    /// <summary>Connected land or water area id, assigned by the world rules.</summary>
    public int AreaId { get; set; }
}

/// <summary>Immutable plot memory; rivers and city marks must not use unseen live data either.</summary>
public sealed record RevealedPlotSnapshot(
    string Terrain, PlotKind Kind, bool IsLake, string? Feature,
    string? Bonus, string? Improvement, string? Route, int? Owner,
    byte RiverEdges = 0, RevealedCitySnapshot? City = null);

/// <summary>Only the city fields used by map labels and settlement models.</summary>
public sealed record RevealedCitySnapshot(int Id, int Owner, string Name, int Population,
    System.Collections.Immutable.ImmutableArray<string> Buildings);

/// <summary>Shared research and diplomacy state. Each player belongs to exactly one team.</summary>
public sealed class Team
{
    /// <summary>Stable team id.</summary>
    public int Id { get; init; }
    /// <summary>Member player ids.</summary>
    public SortedSet<int> Members { get; set; } = new();
    /// <summary>Known technology ids, ordinal.</summary>
    public SortedSet<string> Techs { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Current team research target.</summary>
    public string? Researching { get; set; }
    /// <summary>Progress by technology; changing targets retains progress.</summary>
    public SortedDictionary<string, long> Research { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Team ids at war. Absence means peace; GameState.SetWar keeps the matrix symmetric.</summary>
    public SortedSet<int> Wars { get; set; } = new();
    /// <summary>Mod-owned typed state.</summary>
    public ExtensionState Extensions { get; set; } = new();
    /// <summary>Progress on the selected technology.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long ResearchProgress
    {
        get => Researching is { } id ? Research.GetValueOrDefault(id) : 0;
        set { if (Researching is { } id) Research[id] = value; }
    }
}

/// <summary>A future production queue entry; kind is a mod-owned namespaced key.</summary>
public sealed record ProductionOrder(string Kind, string ContentId, long Progress = 0);
