namespace Ariel.Game;

/// <summary>Plot height tiers. Water holds its depth, land its relief, and the only height a lift writes is flat land
/// raised to <see cref="Hills"/> as the high side of a terrace cliff. Water never rises, so water is never the high side.</summary>
public static class Levels
{
    /// <summary>Ocean and slimy ocean terrain.</summary>
    public const sbyte DeepWater = -2;
    /// <summary>Coast terrain and every lake, whatever its terrain.</summary>
    public const sbyte ShallowWater = -1;
    /// <summary>Flat land, and the default for a plot nobody has assigned a height to yet.</summary>
    public const sbyte Flat = 0;
    /// <summary>Hill relief, and flat land raised by the high side of a terrace cliff.</summary>
    public const sbyte Hills = 1;
    /// <summary>Peak relief, the top tier. A peak is never raised, so it stays here.</summary>
    public const sbyte Peak = 2;
}

/// <summary>Plot data at a row-major TileId. River bits follow Hex.Directions;
/// edits must use GameState.SetRiver to update both sides of shared edges.</summary>
public sealed class Plot
{
    /// <summary>Terrain content id.</summary>
    public string Terrain { get; set; } = "";
    /// <summary>Flatland, hills or peak, independent of terrain.</summary>
    public PlotKind Kind { get; set; }
    /// <summary>Stored height in tiers, one of the <see cref="Levels"/> constants. Authoritative: which side of a cliff is
    /// high is derived by comparing two heights, so no separate lift mask is stored. Edits must use GameState.SetCliff to
    /// keep the edge and the heights agreeing.</summary>
    public sbyte Height { get; set; }
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
    /// <summary>Six cliff-edge bits in Hex.Directions order, mirrored on adjacent plots. A cliff stands on the edge
    /// between two on-map plots, at least one of them land, and names one side as lifted. Edits must use
    /// GameState.SetCliff. The world rules own which edges are legal.</summary>
    public byte CliffEdges
    {
        get => _cliffEdges;
        set
        {
            if (value > 63) throw new ArgumentOutOfRangeException(nameof(value), "Cliff masks use only six bits");
            _cliffEdges = value;
        }
    }
    private byte _cliffEdges;
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
    // Revealed snapshots are immutable records, so the memory copy can share them.
    internal Plot Clone() => new()
    {
        Terrain = Terrain, Kind = Kind, Height = Height, IsLake = IsLake, ImprovementTurns = ImprovementTurns,
        DepletedMine = DepletedMine,
        BuildProgress = new(BuildProgress, BuildProgress.Comparer), Feature = Feature, Bonus = Bonus, Improvement = Improvement,
        Route = Route, _riverEdges = _riverEdges, _cliffEdges = _cliffEdges,
        Owner = Owner, Culture = new(Culture, Culture.Comparer),
        Revealed = TeamMask.FromWords(Revealed.Words), Visible = TeamMask.FromWords(Visible.Words),
        RevealedSnapshots = new(RevealedSnapshots, RevealedSnapshots.Comparer), AreaId = AreaId,
    };
}

/// <summary>Immutable plot memory; rivers, cliffs and city marks must not use unseen live data either.</summary>
/// <param name="CliffLiftEdges">Derived when the memory was captured: bit d is set when this plot stood higher than its
/// neighbour across the cliff on edge d. Stored so a memory of a cliff still says which way it drops.</param>
public sealed record RevealedPlotSnapshot(
    string Terrain, PlotKind Kind, bool IsLake, sbyte Height, string? Feature,
    string? Bonus, string? Improvement, string? Route, int? Owner,
    byte RiverEdges = 0, RevealedCitySnapshot? City = null, byte CliffEdges = 0, byte CliffLiftEdges = 0);

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
    internal Team Clone() => new()
    {
        Id = Id, Members = new(Members, Members.Comparer), Techs = new(Techs, Techs.Comparer), Researching = Researching,
        Research = new(Research, Research.Comparer), Wars = new(Wars, Wars.Comparer), Extensions = GameState.CloneExtensions(Extensions),
    };
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
