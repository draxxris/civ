using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Everything one planner needs for one player's turn: state shortcuts, personality and a command sink.
/// Built fresh each turn, so the AI itself carries no state between turns.</summary>
internal sealed class AiContext
{
    private readonly Lazy<List<Unit>> _enemies;
    private readonly Lazy<HashSet<int>> _enemyPlayers;
    public Simulation Sim { get; }
    public int PlayerId { get; }
    public Player Self { get; }
    public Team Team { get; }
    public LeaderDef? Leader { get; }
    public AiCatalog Catalog { get; }
    public List<City> Cities { get; private set; }
    public List<Unit> Units { get; private set; }
    /// <summary>Units that already acted this turn.</summary>
    public HashSet<int> Done { get; } = new();
    public int IssuedCommands { get; private set; }

    public AiContext(Simulation sim, int player)
    {
        Sim = sim; PlayerId = player;
        Self = sim.State.PlayerById(player); Team = sim.State.TeamById(Self.TeamId);
        Leader = sim.Content.Services.Get<IPeoplesService>().SingleOrDefault()?.Leader(sim, player);
        Catalog = AiCatalog.For(sim.Content);
        (Cities, Units) = Snapshot();
        // Read-only planner scoring may touch these from several threads at once.
        _enemyPlayers = new(() => sim.State.Players.Where(p => IsEnemy(p.Id)).Select(p => p.Id).ToHashSet(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _enemies = new(() => sim.State.Units.Where(u => _enemyPlayers.Value.Contains(u.Owner)).ToList(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private (List<City>, List<Unit>) Snapshot() =>
        (Sim.State.Cities.Where(c => c.Owner == PlayerId).ToList(), Sim.State.Units.Where(u => u.Owner == PlayerId).ToList());

    /// <summary>Re-read own cities and units after commands that create or remove them (founding).</summary>
    public void Refresh() => (Cities, Units) = Snapshot();

    public HexGrid Grid => Sim.State.Grid;
    public int Turn => Sim.State.Turn;
    public T Service<T>() where T : class => Sim.Content.Services.Get<T>().Single();
    public UnitDef Def(Unit unit) => Sim.Content.Get<UnitDef>(unit.Type);
    public UnitRole Role(Unit unit) => Catalog.Roles[unit.Type];
    public IReadOnlyList<Unit> Enemies => _enemies.Value;
    public bool IsEnemy(int player) => player != PlayerId && Team.Wars.Contains(Sim.State.PlayerById(player).TeamId);
    public bool AtWarWithPlayers => Team.Wars.Any(t => Sim.State.TeamById(t).Members.Any(m => !WarRules.IsBarbarian(Sim, m)));
    public TileId Tile(Hex hex) => Grid.TileAt(hex);

    /// <summary>Flavor weight: a base of 10 plus the leader's own flavor, so every flavor counts a little.</summary>
    public int Weight(string flavor) => 10 + (Leader?.Flavors.GetValueOrDefault(flavor) ?? 0);
    /// <summary>Flavor-weighted value of a content flavor table, in roughly the same points as yields.</summary>
    public int FlavorScore(IReadOnlyDictionary<string, int> flavors)
    {
        int total = 0;
        foreach (var pair in flavors) total += pair.Value * Weight(pair.Key) / 5;
        return total;
    }

    /// <summary>Validate and apply through the normal command path.</summary>
    public bool Issue(GameCommand command)
    {
        bool ok = Sim.TryApply(command, out _);
        if (ok) IssuedCommands++;
        return ok;
    }

    public IEnumerable<Unit> UnitsWithRole(UnitRole role) => Units.Where(u => Sim.State.FindUnit(u.Id) is not null && Role(u) == role);
    public bool Alive(Unit unit) => Sim.State.FindUnit(unit.Id) is not null;
    public City? CityAtUnit(Unit unit) => Sim.State.CityAt(unit.Position) is { } c && c.Owner == PlayerId ? c : null;
    public City? NearestCity(Hex from) => Cities.OrderBy(c => Grid.Distance(c.Position, from)).ThenBy(c => c.Id).FirstOrDefault();
    public MilitaryUnitState MilitaryState(Unit unit) => unit.Extensions.Get<MilitaryUnitState>(Sim.Content) ?? new();

    /// <summary>Move along a pathfinder route, truncated to the current turn by the movement rules themselves.</summary>
    public bool MoveToward(Unit unit, Hex target, bool attack = false)
    {
        if (unit.MovesLeft <= 0 || unit.Position == target) return false;
        var path = Service<IPathfindingService>().FindPath(Sim, unit.Id, target, attack);
        if (path is null || path.Steps.Count == 0) return false;
        return Issue(new MoveUnitPathCommand { Player = PlayerId, Unit = unit.Id, Path = path.Steps.ToArray() });
    }

    /// <summary>Hostile strength (sum of unit strengths) within a radius of a position.</summary>
    public int ThreatAround(Hex position, int radius) => Enemies.Where(e => Grid.Distance(e.Position, position) <= radius)
        .Sum(e => Math.Max(1, Sim.Content.Get<UnitDef>(e.Type).Strength));

    /// <summary>Sum of own military unit strength weighted by health, a simple integer power estimate.</summary>
    public static long Power(Simulation sim, int player)
    {
        long power = 0;
        foreach (var u in sim.State.Units)
            if (u.Owner == player) power += sim.Content.Get<UnitDef>(u.Type).Strength * u.Health / 10;
        return power + 20L * sim.State.Cities.Count(c => c.Owner == player);
    }
}
