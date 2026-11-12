using System.Text.Json.Serialization;

namespace Ariel.Game;

/// <summary>Player-owned state; research and diplomacy live on Team.</summary>
public sealed class Player
{
    /// <summary>Stable player id.</summary>
    public int Id { get; init; }
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Civilization definition id.</summary>
    public string Civilization { get; init; } = "";
    /// <summary>Selected leader id, or empty when the ruleset has no leaders.</summary>
    public string Leader { get; init; } = "";
    /// <summary>Initial location selected by the ruleset setup, or null when it selects none.</summary>
    public TileId? StartingPlot { get; set; }
    /// <summary>Controlling AI service id, or null for a human. Changes mid-game only through
    /// <see cref="SetPlayerControlCommand"/>, when a multiplayer seat is handed to the AI or back.</summary>
    public string? Ai { get; set; }
    /// <summary>Owning team id.</summary>
    public int TeamId { get; set; }
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
    internal Player Clone() => new()
    {
        Id = Id, Name = Name, Civilization = Civilization, Leader = Leader, StartingPlot = StartingPlot, Ai = Ai, TeamId = TeamId,
        Culture = Culture, Treasury = Treasury, Civics = new(Civics, Civics.Comparer), EndedTurn = EndedTurn,
        Extensions = GameState.CloneExtensions(Extensions),
    };
}

/// <summary>City data. Position is canonical axial; indexes resolve it through the grid.</summary>
public sealed class City
{
    /// <summary>Stable city id.</summary>
    public int Id { get; set; }
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
    /// <summary>Next unique production order id for this city.</summary>
    public int NextOrderId { get; set; } = 1;
    /// <summary>Ordered production queue.</summary>
    public List<ProductionOrder> ProductionQueue { get; set; } = new();

    /// <summary>Finds an active production order by its canonical identifier, or null if not present.</summary>
    public ProductionOrder? FindOrder(int orderId)
    {
        NormalizeOrderIds();
        return ProductionQueue.FirstOrDefault(o => o.Id == orderId);
    }

    /// <summary>Returns the index of an order by its canonical identifier, or -1 if not present.</summary>
    public int IndexOfOrder(int orderId)
    {
        NormalizeOrderIds();
        return ProductionQueue.FindIndex(o => o.Id == orderId);
    }

    /// <summary>Appends a production order to the queue, assigning an order ID if none is set.</summary>
    public ProductionOrder EnqueueOrder(string kind, string contentId, long progress = 0, int id = 0)
    {
        int orderId = id > 0 ? id : NextOrderId++;
        if (orderId >= NextOrderId) NextOrderId = orderId + 1;
        var order = new ProductionOrder(orderId, kind, contentId, progress);
        ProductionQueue.Add(order);
        return order;
    }

    /// <summary>Removes an order by its canonical identifier, returning true if removed.</summary>
    public bool RemoveOrder(int orderId)
    {
        int index = IndexOfOrder(orderId);
        if (index >= 0)
        {
            ProductionQueue.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>Ensures all orders in the queue have unique, positive IDs.</summary>
    public void NormalizeOrderIds()
    {
        for (int i = 0; i < ProductionQueue.Count; i++)
        {
            if (ProductionQueue[i].Id <= 0)
            {
                ProductionQueue[i] = ProductionQueue[i] with { Id = NextOrderId++ };
            }
            else if (ProductionQueue[i].Id >= NextOrderId)
            {
                NextOrderId = ProductionQueue[i].Id + 1;
            }
        }
    }
    /// <summary>Citizen count.</summary>
    public int Population { get; set; } = 1;
    /// <summary>Stored food.</summary>
    public long FoodStored { get; set; }
    /// <summary>Worked plots in canonical ascending TileId order; center is worked for free.</summary>
    public List<TileId> Worked { get; set; } = new();

    /// <summary>Whether this city is working the specified plot.</summary>
    public bool IsWorking(TileId tile) => Worked.Contains(tile);

    /// <summary>Sorts worked plots in canonical ascending TileId order and removes duplicates.</summary>
    public void NormalizeWorked()
    {
        if (Worked.Count <= 1) return;
        Worked.Sort();
        for (int i = Worked.Count - 1; i > 0; i--)
            if (Worked[i] == Worked[i - 1]) Worked.RemoveAt(i);
    }

    /// <summary>Specialist counts by id.</summary>
    public SortedDictionary<string, int> Specialists { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Accumulated city culture.</summary>
    public long Culture { get; set; }
    /// <summary>Mod-owned typed state.</summary>
    public ExtensionState Extensions { get; set; } = new();
    // Production orders are immutable records, so the queue copy can share them.
    internal City Clone() => new()
    {
        Id = Id, Owner = Owner, Name = Name, Position = Position, Buildings = new(Buildings), NextOrderId = NextOrderId,
        ProductionQueue = new(ProductionQueue), Population = Population, FoodStored = FoodStored, Worked = new(Worked),
        Specialists = new(Specialists, Specialists.Comparer), Culture = Culture, Extensions = GameState.CloneExtensions(Extensions),
    };
}

/// <summary>Unit data. Use GameState.MoveUnit when changing position.</summary>
public sealed class Unit
{
    /// <summary>Stable unit id.</summary>
    public int Id { get; set; }
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
    internal Unit Clone() => new()
    {
        Id = Id, Owner = Owner, Type = Type, Position = Position, Experience = Experience, Health = Health, MovesLeft = MovesLeft,
        Promotions = new(Promotions, Promotions.Comparer), Extensions = GameState.CloneExtensions(Extensions),
    };
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
    /// <summary>Who moves when within the current turn.</summary>
    public TurnOrder TurnOrder { get; set; } = new();
    private HexGrid? _grid;
    private readonly Dictionary<int, Player> _players = new();
    private readonly Dictionary<int, Team> _teams = new();
    private readonly Dictionary<int, Unit> _units = new();
    private readonly Dictionary<int, City> _cities = new();
    private readonly Dictionary<TileId, City> _cityPlots = new();
    private readonly Dictionary<TileId, SortedDictionary<int, Unit>> _unitPlots = new();
    /// <summary>Geometry derived from the map specification.</summary>
    [JsonIgnore] public HexGrid Grid => _grid ??= new HexGrid(Map);
    private AreaSizes? _areaSizes;
    private sealed record AreaSizes(long Revision, Plot[] Plots, Dictionary<int, int> Counts);
    /// <summary>Number of plots in a connected area, or zero for an unknown area. Derived from <see cref="Plot.AreaId"/>
    /// and rebuilt only after an area id changes, so repeated queries do not scan the map.</summary>
    public int AreaSize(int areaId)
    {
        var cache = _areaSizes;
        long revision = Plot.AreaRevision;
        if (cache is null || cache.Revision != revision || !ReferenceEquals(cache.Plots, Plots))
        {
            // Read the revision before counting: a write during the count leaves this entry stale and it is rebuilt.
            var counts = new Dictionary<int, int>();
            foreach (var plot in Plots) counts[plot.AreaId] = counts.GetValueOrDefault(plot.AreaId) + 1;
            _areaSizes = cache = new(revision, Plots, counts);
        }
        return cache.Counts.GetValueOrDefault(areaId);
    }
    /// <summary>Get a player by id.</summary>
    public Player PlayerById(int id) => _players[id];
    /// <summary>Find a player without throwing.</summary>
    public Player? FindPlayer(int id) => _players.GetValueOrDefault(id);
    /// <summary>Get a team by id.</summary>
    public Team TeamById(int id) => _teams[id];
    /// <summary>Find a team without throwing.</summary>
    public Team? FindTeam(int id) => _teams.GetValueOrDefault(id);
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
    /// <summary>Find the city on a valid tile.</summary>
    public City? CityAtTile(TileId tile) => _cityPlots.GetValueOrDefault(tile);
    /// <summary>Find the city on a valid axial coordinate, including wrap images.</summary>
    public City? CityAt(Hex pos) => Grid.TryTile(pos, out var t) ? _cityPlots.GetValueOrDefault(t) : null;
    /// <summary>Find the city working this plot, if any. City center tiles are not counted as worked plots.</summary>
    public City? WorkingCity(TileId tile)
    {
        for (int i = 0; i < Cities.Count; i++)
            if (Cities[i].Worked.Contains(tile)) return Cities[i];
        return null;
    }
    /// <summary>Whether this plot is worked by any city.</summary>
    public bool IsPlotWorked(TileId tile) => WorkingCity(tile) is not null;
    /// <summary>Units on a plot, in ascending id order.</summary>
    public IEnumerable<Unit> UnitsAt(Hex pos) => Grid.TryTile(pos, out var t) && _unitPlots.TryGetValue(t, out var units) ? units.Values : [];
    /// <summary>Mutable plot at a valid axial coordinate.</summary>
    public Plot PlotAt(Hex pos) => Plots[Grid.TileAt(pos).Value];
    /// <summary>Add a team to the state and update indexes. Duplicate ids throw before any state changes.</summary>
    public void AddTeam(Team team)
    {
        if (_teams.ContainsKey(team.Id)) throw new ArgumentException($"Team id {team.Id} is already in use", nameof(team));
        _teams.Add(team.Id, team);
        Teams.Add(team);
    }
    /// <summary>Add a player and register them with their team. Missing teams or duplicate ids throw before state changes.</summary>
    public void AddPlayer(Player player)
    {
        if (_players.ContainsKey(player.Id)) throw new ArgumentException($"Player id {player.Id} is already in use", nameof(player));
        if (!_teams.TryGetValue(player.TeamId, out var team))
            throw new ArgumentException($"Team id {player.TeamId} does not exist", nameof(player));
        _players.Add(player.Id, player);
        team.Members.Add(player.Id);
        Players.Add(player);
    }
    /// <summary>Change a player's team and synchronize bidirectional team membership atomically.</summary>
    public void SetPlayerTeam(int playerId, int newTeamId)
    {
        if (!_players.TryGetValue(playerId, out var player))
            throw new ArgumentException($"Player id {playerId} does not exist", nameof(playerId));
        if (player.TeamId == newTeamId) return;
        if (!_teams.TryGetValue(newTeamId, out var newTeam))
            throw new ArgumentException($"Target team id {newTeamId} does not exist", nameof(newTeamId));
        if (_teams.TryGetValue(player.TeamId, out var oldTeam))
            oldTeam.Members.Remove(playerId);
        player.TeamId = newTeamId;
        newTeam.Members.Add(playerId);
    }
    /// <summary>Add a city and update its indexes. Duplicate ids or plots throw before any state changes.
    /// Unassigned ids (<= 0) are allocated from NextCityId; explicit ids advance NextCityId monotonically.</summary>
    public void AddCity(City city)
    {
        var position = Grid.Normalize(city.Position);
        var tile = Grid.TileAt(position);
        if (city.Id <= 0)
        {
            city.Id = NextCityId++;
        }
        else
        {
            if (_cities.ContainsKey(city.Id)) throw new ArgumentException($"City id {city.Id} is already in use", nameof(city));
            if (city.Id >= NextCityId) NextCityId = checked(city.Id + 1);
        }
        if (_cityPlots.ContainsKey(tile)) throw new ArgumentException($"Plot {tile.Value} already has a city", nameof(city));
        city.SetPosition(position);
        IndexCity(city);
        Cities.Add(city);
    }
    private void IndexCity(City city) { _cities.Add(city.Id, city); _cityPlots.Add(Grid.TileAt(city.Position), city); }
    /// <summary>Remove a city and its indexes.</summary>
    public void RemoveCity(int id) { var c = _cities[id]; _cities.Remove(id); _cityPlots.Remove(Grid.TileAt(c.Position)); Cities.Remove(c); }
    /// <summary>Add a unit at a canonicalized position. Duplicate ids throw before any state changes.
    /// Unassigned ids (<= 0) are allocated from NextUnitId; explicit ids advance NextUnitId monotonically.</summary>
    public void AddUnit(Unit unit)
    {
        var position = Grid.Normalize(unit.Position);
        if (unit.Id <= 0)
        {
            unit.Id = NextUnitId++;
        }
        else
        {
            if (_units.ContainsKey(unit.Id)) throw new ArgumentException($"Unit id {unit.Id} is already in use", nameof(unit));
            if (unit.Id >= NextUnitId) NextUnitId = checked(unit.Id + 1);
        }
        unit.SetPosition(position);
        IndexUnit(unit);
        Units.Add(unit);
    }
    private void IndexUnit(Unit unit)
    {
        _units.Add(unit.Id, unit);
        AddUnitPlot(Grid.TileAt(unit.Position), unit);
    }
    private void AddUnitPlot(TileId tile, Unit unit)
    {
        if (!_unitPlots.TryGetValue(tile, out var units)) _unitPlots[tile] = units = new();
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
    /// <summary>Move a unit and update the spatial index. Unknown units and off-map targets throw before any state changes.</summary>
    public void MoveUnit(int id, Hex to)
    {
        var u = _units[id];
        var position = Grid.Normalize(to);
        var tile = Grid.TileAt(position);
        RemoveUnitPlot(u);
        u.SetPosition(position);
        AddUnitPlot(tile, u);
    }
    /// <summary>Set a symmetric war/peace relation. Self-war is invalid.</summary>
    public void SetWar(int a, int b, bool war)
    {
        if (a == b) throw new ArgumentException("A team cannot be at war with itself");
        var x = TeamById(a); var y = TeamById(b);
        if (war) { x.Wars.Add(b); y.Wars.Add(a); } else { x.Wars.Remove(b); y.Wars.Remove(a); }
    }
    /// <summary>Set a river bit and its opposite on the neighboring plot. Off-map edges have no neighbor, so they never carry a river.</summary>
    public void SetRiver(TileId tile, int direction, bool present)
    {
        if (direction < 0 || direction >= 6) throw new ArgumentOutOfRangeException(nameof(direction));
        void Set(TileId t, int d, bool on) { var p = Plots[t.Value]; p.RiverEdges = (byte)(on ? p.RiverEdges | (1 << d) : p.RiverEdges & ~(1 << d)); }
        if (Grid.Neighbor(tile, direction) is not { } neighbor) { Set(tile, direction, false); return; } // clears any stale off-map bit
        Set(tile, direction, present);
        Set(neighbor, (direction + 3) % 6, present);
    }
    /// <summary>Set a cliff on one edge and its mirror on the neighboring plot, raising the named plot to stand one tier
    /// above the other. A height is only ever raised and never lowered by a cliff, so a peak stays at the top tier and
    /// clearing a cliff leaves both heights alone: another cliff on the same side may still justify them. Off-map edges
    /// never carry a cliff, and water never stands on the high side, which is a world rule callers keep.</summary>
    public void SetCliff(TileId high, int direction, bool present)
    {
        if (direction < 0 || direction >= 6) throw new ArgumentOutOfRangeException(nameof(direction));
        void Bit(TileId t, int d, bool on)
        {
            var p = Plots[t.Value];
            p.CliffEdges = (byte)(on ? p.CliffEdges | (1 << d) : p.CliffEdges & ~(1 << d));
        }
        if (Grid.Neighbor(high, direction) is not { } low)
        {
            Bit(high, direction, false);
            return;
        }
        int mirror = (direction + 3) % 6;
        Bit(high, direction, present);
        Bit(low, mirror, present);
        if (!present) return;
        var raised = Plots[high.Value];
        int oneTierAboveLow = Math.Min(Plots[low.Value].Height + 1, Levels.Peak);
        if (raised.Height < oneTierAboveLow) raised.Height = (sbyte)oneTierAboveLow;
    }
    /// <summary>Six bits in Hex.Directions order, bit d set when this plot is the higher side of the cliff on edge d.
    /// Derived from the two plots' heights, so it can never disagree with them, and never set on an off-map edge.</summary>
    public byte CliffLifts(TileId tile)
    {
        int height = Plots[tile.Value].Height;
        byte lifts = 0;
        for (int direction = 0; direction < 6; direction++)
        {
            if ((Plots[tile.Value].CliffEdges & (1 << direction)) == 0) continue;
            if (Grid.Neighbor(tile, direction) is not { } next) continue;
            if (Plots[next.Value].Height < height) lifts |= (byte)(1 << direction);
        }
        return lifts;
    }
    /// <summary>Whether a river runs along the specified edge direction of a tile.</summary>
    public bool HasRiver(TileId tile, int direction)
    {
        if (direction < 0 || direction >= 6) return false;
        if ((Plots[tile.Value].RiverEdges & (1 << direction)) != 0) return true;
        if (Grid.Neighbor(tile, direction) is { } next)
            return (Plots[next.Value].RiverEdges & (1 << ((direction + 3) % 6))) != 0;
        return false;
    }
    /// <summary>Whether a river runs along the shared edge between two tiles.</summary>
    public bool HasRiver(TileId a, TileId b)
    {
        for (int d = 0; d < 6; d++)
            if (Grid.Neighbor(a, d) == b) return HasRiver(a, d);
        return false;
    }
    /// <summary>Whether a river runs along the specified canonical edge.</summary>
    public bool HasRiver(HexEdge edge) => HasRiver(edge.First, edge.Second);
    /// <summary>Whether an impassable cliff stands on the specified edge direction of a tile.</summary>
    public bool HasCliff(TileId tile, int direction)
    {
        if (direction < 0 || direction >= 6) return false;
        if ((Plots[tile.Value].CliffEdges & (1 << direction)) != 0) return true;
        if (Grid.Neighbor(tile, direction) is { } next)
            return (Plots[next.Value].CliffEdges & (1 << ((direction + 3) % 6))) != 0;
        return false;
    }
    /// <summary>Whether an impassable cliff stands on the shared edge between two tiles.</summary>
    public bool HasCliff(TileId a, TileId b)
    {
        for (int d = 0; d < 6; d++)
            if (Grid.Neighbor(a, d) == b) return HasCliff(a, d);
        return false;
    }
    /// <summary>Whether an impassable cliff stands on the specified canonical edge.</summary>
    public bool HasCliff(HexEdge edge) => HasCliff(edge.First, edge.Second);
    /// <summary>Whether a cliff drops down from `from` to `to` (`from` is strictly higher than `to`).</summary>
    public bool IsCliffDrop(TileId from, TileId to)
    {
        for (int d = 0; d < 6; d++)
        {
            if (Grid.Neighbor(from, d) == to)
                return HasCliff(from, d) && (CliffLifts(from) & (1 << d)) != 0;
        }
        return false;
    }
    /// <summary>Whether a cliff rises as a wall facing `from` toward `to` (`to` is strictly higher than `from`).</summary>
    public bool IsCliffWall(TileId from, TileId to) => IsCliffDrop(to, from);
    /// <summary>Independent in-memory copy of all authoritative state with rebuilt lookup indexes. It hashes like the
    /// source; immutable data (map geometry, revealed plot memories, production orders) is shared, everything else is copied.</summary>
    public GameState Clone()
    {
        var copy = new GameState
        {
            Turn = Turn, Map = Map, NextCityId = NextCityId, NextUnitId = NextUnitId,
            Plots = Array.ConvertAll(Plots, p => p.Clone()),
            Players = Players.ConvertAll(p => p.Clone()),
            Teams = Teams.ConvertAll(t => t.Clone()),
            Cities = Cities.ConvertAll(c => c.Clone()),
            Units = Units.ConvertAll(u => u.Clone()),
            Extensions = CloneExtensions(Extensions),
            TurnOrder = TurnOrder.Clone(),
            _grid = Grid,
        };
        copy.IndexEntities();
        return copy;
    }
    /// <summary>Take over the contents of an unshared copy made by <see cref="Clone"/>. Entity objects are replaced by
    /// the copy's objects, so references held from before the call no longer belong to this state.</summary>
    internal void RestoreFrom(GameState copy)
    {
        Turn = copy.Turn; Map = copy.Map; NextCityId = copy.NextCityId; NextUnitId = copy.NextUnitId;
        Plots = copy.Plots; Players = copy.Players; Teams = copy.Teams; Cities = copy.Cities; Units = copy.Units;
        Extensions = copy.Extensions;
        TurnOrder = copy.TurnOrder;
        _grid = copy.Grid;
        IndexEntities();
    }
    private void IndexEntities()
    {
        _players.Clear(); _teams.Clear(); _units.Clear(); _cities.Clear(); _cityPlots.Clear(); _unitPlots.Clear();
        foreach (var t in Teams) _teams.Add(t.Id, t);
        foreach (var p in Players) _players.Add(p.Id, p);
        foreach (var c in Cities) IndexCity(c);
        foreach (var u in Units) IndexUnit(u);
    }
    internal static ExtensionState CloneExtensions(ExtensionState state) => state.Values.Count == 0 ? new() : state.Clone();
    /// <summary>Rebuild caches after deserialization or setup. Restores ordinal collection comparers.</summary>
    public void RebuildIndexes()
    {
        _grid = new HexGrid(Map);
        if (Plots.Length != Grid.Count) throw new InvalidDataException("Plot count does not match map dimensions");
        _players.Clear(); _teams.Clear(); _units.Clear(); _cities.Clear(); _cityPlots.Clear(); _unitPlots.Clear();
        foreach (var plot in Plots) { plot.BuildProgress = new(plot.BuildProgress, StringComparer.Ordinal); plot.Culture = new(plot.Culture); }
        foreach (var t in Teams)
        {
            t.Techs = new(t.Techs, StringComparer.Ordinal);
            t.Research = new(t.Research, StringComparer.Ordinal);
            t.Wars = new(t.Wars);
            t.Members = new(t.Members);
            if (!_teams.TryAdd(t.Id, t)) throw new InvalidDataException($"Duplicate team id {t.Id}");
        }
        foreach (var p in Players)
        {
            p.Civics = new(p.Civics, StringComparer.Ordinal);
            if (!_teams.TryGetValue(p.TeamId, out var team))
                throw new InvalidDataException($"Player {p.Id} references nonexistent team {p.TeamId}");
            if (!_players.TryAdd(p.Id, p)) throw new InvalidDataException($"Duplicate player id {p.Id}");
            team.Members.Add(p.Id);
        }
        foreach (var t in Teams)
        {
            t.Members.RemoveWhere(id => !_players.TryGetValue(id, out var p) || p.TeamId != t.Id);
        }
        (TurnOrder ?? throw new InvalidDataException("Missing turn order")).Validate(_players.ContainsKey);
        foreach (var c in Cities)
        {
            c.Specialists = new(c.Specialists, StringComparer.Ordinal);
            if (c.Id <= 0) c.Id = NextCityId++;
            else if (c.Id >= NextCityId) NextCityId = checked(c.Id + 1);
            if (!_cities.TryAdd(c.Id, c)) throw new ArgumentException($"Duplicate city id {c.Id}");
            var tile = Grid.TileAt(c.Position);
            if (!_cityPlots.TryAdd(tile, c)) throw new ArgumentException($"Plot {tile.Value} already has a city");
        }
        foreach (var u in Units)
        {
            u.Promotions = new(u.Promotions, StringComparer.Ordinal);
            if (u.Id <= 0) u.Id = NextUnitId++;
            else if (u.Id >= NextUnitId) NextUnitId = checked(u.Id + 1);
            if (!_units.TryAdd(u.Id, u)) throw new ArgumentException($"Duplicate unit id {u.Id}");
            AddUnitPlot(Grid.TileAt(u.Position), u);
        }
    }
}
