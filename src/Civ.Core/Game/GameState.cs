namespace Civ.Game;

public sealed class Player
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Civilization { get; init; } = "";
    /// <summary>Id of the <see cref="Content.IPlayerAI"/> controlling this player, or null for a human.</summary>
    public string? Ai { get; init; }
    public SortedSet<string> Techs { get; } = new(StringComparer.Ordinal);
    public string? Researching { get; set; }
    public long ResearchProgress { get; set; }
    public long Culture { get; set; }
    /// <summary>Held civic per category.</summary>
    public SortedDictionary<string, string> Civics { get; } = new(StringComparer.Ordinal);
    /// <summary>Stockpiles of player-scoped mod resources, by resource id.</summary>
    public SortedDictionary<string, long> Resources { get; } = new(StringComparer.Ordinal);
    public bool EndedTurn { get; set; }
}

public sealed class City
{
    public int Id { get; init; }
    public int Owner { get; init; }
    public string Name { get; init; } = "";
    public Hex Position { get; init; }
    public List<string> Buildings { get; } = new();
    public string? Building { get; set; }
    public long BuildProgress { get; set; }
    /// <summary>Unit currently being trained (exclusive with <see cref="Building"/>).</summary>
    public string? Training { get; set; }
    public long TrainProgress { get; set; }
    public int Population { get; set; } = 1;
    public long FoodStored { get; set; }
    /// <summary>Tiles worked by citizens (the city centre is always worked for free), kept sorted by (Q, R).</summary>
    public List<Hex> Worked { get; } = new();
    /// <summary>Citizens not working a tile, by specialist id.</summary>
    public SortedDictionary<string, int> Specialists { get; } = new(StringComparer.Ordinal);
    public long Culture { get; set; }
    /// <summary>Stockpiles of city-scoped mod resources, by resource id.</summary>
    public SortedDictionary<string, long> Resources { get; } = new(StringComparer.Ordinal);
    /// <summary>Institutions (religions, guilds, corporations, ...) present here.</summary>
    public SortedSet<string> Institutions { get; } = new(StringComparer.Ordinal);
}

/// <summary>Where and by whom an institution was founded. Its first city is its holy city / headquarters.</summary>
public sealed class FoundedInstitution
{
    public int Founder { get; init; }
    public int HomeCity { get; init; }
    public int FoundedTurn { get; init; }
}

public sealed class Unit
{
    public int Id { get; init; }
    public int Owner { get; init; }
    public string Type { get; init; } = "";
    public Hex Position { get; set; }
    public int Experience { get; set; }
    public int Health { get; set; } = 100;
    public int MovesLeft { get; set; }
    public SortedSet<string> Promotions { get; } = new(StringComparer.Ordinal);
}

/// <summary>Plain data. Everything that must stay in sync across peers lives here and is covered by the state hash.</summary>
public sealed class GameState
{
    public int Turn { get; set; } = 1;
    public Dictionary<Hex, string> Terrain { get; } = new();
    public List<Player> Players { get; } = new();
    public List<City> Cities { get; } = new();
    public int NextCityId { get; set; } = 1;
    public List<Unit> Units { get; } = new();
    public int NextUnitId { get; set; } = 1;
    /// <summary>Institutions founded so far, by id. Each can be founded once per game.</summary>
    public SortedDictionary<string, FoundedInstitution> Institutions { get; } = new(StringComparer.Ordinal);

    public Player PlayerById(int id) => Players.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"player {id}");
    public Unit UnitById(int id) => Units.FirstOrDefault(u => u.Id == id) ?? throw new KeyNotFoundException($"unit {id}");
    public City? CityAt(Hex pos) => Cities.FirstOrDefault(c => c.Position == pos);
}
