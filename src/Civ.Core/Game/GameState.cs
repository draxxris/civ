using System.Text.Json.Serialization;

namespace Civ.Game;

/// <summary>Player-owned state; research and diplomacy live on Team.</summary>
public sealed class Player
{
    /// <summary>Stable player id.</summary>
    public int Id { get; init; }
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Civilization definition id.</summary>
    public string Civilization { get; init; } = "";
    /// <summary>Controlling AI service id, or null for a human.</summary>
    public string? Ai { get; init; }
    /// <summary>Owning team id.</summary>
    public int TeamId { get; init; }
    /// <summary>Accumulated culture.</summary>
    public long Culture { get; set; }
    /// <summary>Treasury in integer gold.</summary>
    public long Treasury { get; set; }
    /// <summary>Held civic per category.</summary>
    public SortedDictionary<string, string> Civics { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Whether this player finished the current turn.</summary>
    public bool EndedTurn { get; set; }
    /// <summary>Mod-owned typed state.</summary>
    public ExtensionState Extensions { get; set; } = new();
}

/// <summary>City data. Position is canonical axial; indexes resolve it through the grid.</summary>
public sealed class City
{
    /// <summary>Stable city id.</summary>
    public int Id { get; init; }
    /// <summary>Owner player id.</summary>
    public int Owner { get; init; }
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Canonical map position.</summary>
    public Hex Position { get => _position; init => _position = value; }
    private Hex _position;
    internal void SetPosition(Hex position) => _position = position;
    /// <summary>Completed building ids.</summary>
    public List<string> Buildings { get; set; } = new();
    /// <summary>Current building (small test ruleset).</summary>
    public string? Building { get; set; }
    /// <summary>Current building progress.</summary>
    public long BuildProgress { get; set; }
    /// <summary>Unit currently being trained, exclusive with Building.</summary>
    public string? Training { get; set; }
    /// <summary>Current unit progress.</summary>
    public long TrainProgress { get; set; }
    /// <summary>Ordered production queue for feature rulesets.</summary>
    public List<ProductionOrder> ProductionQueue { get; set; } = new();
    /// <summary>Citizen count.</summary>
    public int Population { get; set; } = 1;
    /// <summary>Stored food.</summary>
    public long FoodStored { get; set; }
    /// <summary>Worked plots in canonical (Q,R) order; center is worked for free.</summary>
    public List<Hex> Worked { get; set; } = new();
    /// <summary>Specialist counts by id.</summary>
    public SortedDictionary<string, int> Specialists { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Accumulated city culture.</summary>
    public long Culture { get; set; }
    /// <summary>Mod-owned typed state.</summary>
    public ExtensionState Extensions { get; set; } = new();
}

/// <summary>Unit data. Use GameState.MoveUnit when changing position.</summary>
public sealed class Unit
{
    /// <summary>Stable unit id.</summary>
    public int Id { get; init; }
    /// <summary>Owner player id.</summary>
    public int Owner { get; init; }
    /// <summary>Unit definition id.</summary>
    public string Type { get; init; } = "";
    /// <summary>Canonical position. Runtime changes must go through MoveUnit.</summary>
    public Hex Position { get => _position; init => _position = value; }
    private Hex _position;
    internal void SetPosition(Hex position) => _position = position;
    /// <summary>Combat experience.</summary>
    public int Experience { get; set; }
    /// <summary>Integer health points.</summary>
    public int Health { get; set; } = 100;
    /// <summary>Remaining movement points.</summary>
    public int MovesLeft { get; set; }
    /// <summary>Promotion ids.</summary>
    public SortedSet<string> Promotions { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Mod-owned typed state.</summary>
    public ExtensionState Extensions { get; set; } = new();
}

/// <summary>Plain authoritative data with rebuildable, non-serialized lookup caches.
/// Use Add/Remove/Move methods for runtime entity edits; rebuild after bulk loading.</summary>
public sealed class GameState
{
    /// <summary>Current turn, starting at one.</summary>
    public int Turn { get; set; } = 1;
    /// <summary>Map topology.</summary>
    public MapSpec Map { get; set; } = new(1, 1);
    /// <summary>Plot array indexed by TileId.Value.</summary>
    public Plot[] Plots { get; set; } = [];
    /// <summary>Players in id order.</summary>
    public List<Player> Players { get; set; } = new();
    /// <summary>Teams in id order.</summary>
    public List<Team> Teams { get; set; } = new();
    /// <summary>Cities in id order. Bulk edits require RebuildIndexes.</summary>
    public List<City> Cities { get; set; } = new();
    /// <summary>Next monotonic city id.</summary>
    public int NextCityId { get; set; } = 1;
    /// <summary>Units in id order. Bulk edits require RebuildIndexes.</summary>
    public List<Unit> Units { get; set; } = new();
    /// <summary>Next monotonic unit id.</summary>
    public int NextUnitId { get; set; } = 1;
    /// <summary>Game-wide mod state.</summary>
    public ExtensionState Extensions { get; set; } = new();
    private HexGrid? _grid;
    private readonly Dictionary<int, Player> _players = new();
    private readonly Dictionary<int, Team> _teams = new();
    private readonly Dictionary<int, Unit> _units = new();
    private readonly Dictionary<int, City> _cities = new();
    private readonly Dictionary<TileId, City> _cityPlots = new();
    private readonly Dictionary<TileId, SortedDictionary<int, Unit>> _unitPlots = new();
    /// <summary>Geometry derived from the map specification.</summary>
    [JsonIgnore] public HexGrid Grid => _grid ??= new HexGrid(Map);
    /// <summary>Get a player by id.</summary>
    public Player PlayerById(int id) => _players[id];
    /// <summary>Get a team by id.</summary>
    public Team TeamById(int id) => _teams[id];
    /// <summary>Get the team owning a player.</summary>
    public Team TeamOf(int playerId) => TeamById(PlayerById(playerId).TeamId);
    /// <summary>Get a unit by id.</summary>
    public Unit UnitById(int id) => _units[id];
    /// <summary>Find a unit without throwing.</summary>
    public Unit? FindUnit(int id) => _units.GetValueOrDefault(id);
    /// <summary>Get a city by id.</summary>
    public City CityById(int id) => _cities[id];
    /// <summary>Find a city without throwing.</summary>
    public City? FindCity(int id) => _cities.GetValueOrDefault(id);
    /// <summary>Find the city on a valid tile, including wrap images.</summary>
    public City? CityAt(Hex pos) => Grid.TryTile(pos, out var t) ? _cityPlots.GetValueOrDefault(t) : null;
    /// <summary>Units on a plot, in ascending id order.</summary>
    public IEnumerable<Unit> UnitsAt(Hex pos) => Grid.TryTile(pos, out var t) && _unitPlots.TryGetValue(t, out var units) ? units.Values : [];
    /// <summary>Mutable plot at a valid axial coordinate.</summary>
    public Plot PlotAt(Hex pos) => Plots[Grid.TileAt(pos).Value];
    /// <summary>Add a city and update its indexes.</summary>
    public void AddCity(City city) { city.SetPosition(Grid.Normalize(city.Position)); IndexCity(city); Cities.Add(city); }
    private void IndexCity(City city) { _cities.Add(city.Id, city); _cityPlots.Add(Grid.TileAt(city.Position), city); }
    /// <summary>Remove a city and its indexes.</summary>
    public void RemoveCity(int id) { var c = _cities[id]; _cities.Remove(id); _cityPlots.Remove(Grid.TileAt(c.Position)); Cities.Remove(c); }
    /// <summary>Add a unit at a canonicalized position.</summary>
    public void AddUnit(Unit unit) { unit.SetPosition(Grid.Normalize(unit.Position)); IndexUnit(unit); Units.Add(unit); }
    private void IndexUnit(Unit unit)
    {
        _units.Add(unit.Id, unit);
        var t = Grid.TileAt(unit.Position);
        if (!_unitPlots.TryGetValue(t, out var units)) _unitPlots[t] = units = new();
        units.Add(unit.Id, unit);
    }
    /// <summary>Remove a unit and all its indexes.</summary>
    public void RemoveUnit(int id)
    {
        var u = _units[id]; _units.Remove(id); RemoveUnitPlot(u); Units.Remove(u);
    }
    private void RemoveUnitPlot(Unit u)
    {
        var t = Grid.TileAt(u.Position); var units = _unitPlots[t]; units.Remove(u.Id);
        if (units.Count == 0) _unitPlots.Remove(t);
    }
    /// <summary>Move a unit and update the spatial index.</summary>
    public void MoveUnit(int id, Hex to)
    {
        var canonical = Grid.Normalize(to); var u = _units[id]; RemoveUnitPlot(u); u.SetPosition(canonical);
        var t = Grid.TileAt(canonical);
        if (!_unitPlots.TryGetValue(t, out var units)) _unitPlots[t] = units = new();
        units.Add(id, u);
    }
    /// <summary>Set a symmetric war/peace relation. Self-war is invalid.</summary>
    public void SetWar(int a, int b, bool war)
    {
        if (a == b) throw new ArgumentException("A team cannot be at war with itself");
        var x = TeamById(a); var y = TeamById(b);
        if (war) { x.Wars.Add(b); y.Wars.Add(a); } else { x.Wars.Remove(b); y.Wars.Remove(a); }
    }
    /// <summary>Set a river bit and its opposite on the neighboring plot; bounded edges belong to their plot.</summary>
    public void SetRiver(TileId tile, int direction, bool present)
    {
        if (direction < 0 || direction >= 6) throw new ArgumentOutOfRangeException(nameof(direction));
        void Set(TileId t, int d) { var p = Plots[t.Value]; p.RiverEdges = (byte)(present ? p.RiverEdges | (1 << d) : p.RiverEdges & ~(1 << d)); }
        Set(tile, direction);
        if (Grid.Neighbor(tile, direction) is { } n) Set(n, (direction + 3) % 6);
    }
    /// <summary>Rebuild caches after deserialization or setup. Restores ordinal collection comparers.</summary>
    public void RebuildIndexes()
    {
        _grid = new HexGrid(Map);
        if (Plots.Length != Grid.Count) throw new InvalidDataException("Plot count does not match map dimensions");
        _players.Clear(); _teams.Clear(); _units.Clear(); _cities.Clear(); _cityPlots.Clear(); _unitPlots.Clear();
        foreach (var p in Players) { p.Civics = new(p.Civics, StringComparer.Ordinal); _players.Add(p.Id, p); }
        foreach (var t in Teams) { t.Techs = new(t.Techs, StringComparer.Ordinal); t.Research = new(t.Research, StringComparer.Ordinal); _teams.Add(t.Id, t); }
        foreach (var c in Cities) { c.Specialists = new(c.Specialists, StringComparer.Ordinal); IndexCity(c); }
        foreach (var u in Units) { u.Promotions = new(u.Promotions, StringComparer.Ordinal); IndexUnit(u); }
    }
}
