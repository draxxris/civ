namespace Civ.Game;

public sealed class Player
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public SortedSet<string> Techs { get; } = new(StringComparer.Ordinal);
    public string? Researching { get; set; }
    public long ResearchProgress { get; set; }
    public long Culture { get; set; }
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
    public long Culture { get; set; }
}

/// <summary>Plain data. Everything that must stay in sync across peers lives here and is covered by the state hash.</summary>
public sealed class GameState
{
    public int Turn { get; set; } = 1;
    public Dictionary<Hex, string> Terrain { get; } = new();
    public List<Player> Players { get; } = new();
    public List<City> Cities { get; } = new();
    public int NextCityId { get; set; } = 1;

    public Player PlayerById(int id) => Players.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"player {id}");
    public City? CityAt(Hex pos) => Cities.FirstOrDefault(c => c.Position == pos);
}
