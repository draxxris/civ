using System.Collections.Concurrent;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Everything one planner needs for one player's turn: state shortcuts, personality and a command sink.
/// Built fresh each turn. Saved intent belongs to player extensions, not to this derived query context.</summary>
internal sealed class AiContext
{
    private Segment _segment;

    // A segment has no authoritative writes. Parallel scoring joins before Issue or Refresh replaces it.
    private sealed class Segment
    {
        public readonly Lazy<List<City>> Cities;
        public readonly Lazy<List<Unit>> Units;
        public readonly Lazy<List<Unit>> Enemies;
        public readonly Lazy<City[]> EnemyCities;
        public readonly Lazy<Dictionary<UnitRole, int>> UnitCounts;
        public readonly Lazy<Dictionary<UnitRole, int>> QueuedCounts;
        public readonly Lazy<int[]> ThreatStrength;
        public readonly Lazy<CampaignTargets> Targets;
        public readonly ConcurrentDictionary<(TileId Tile, int Radius), int> Threats = new();
        public readonly ConcurrentDictionary<int, Lazy<CityView>> Views = new();
        public readonly ConcurrentDictionary<TileId, City?> NearestCities = new();
        public readonly ConcurrentDictionary<(int City, string Kind, string Id), Lazy<bool>> Production = new();
        private readonly Dictionary<(int Unit, TileId Goal, bool Attack), Lazy<UnitPath?>> _paths = new();
        private readonly object _pathsLock = new();

        public Segment(AiContext ctx)
        {
            Cities = new(() => ctx.Sim.State.Cities.Where(c => c.Owner == ctx.PlayerId).ToList());
            Units = new(() => ctx.Sim.State.Units.Where(u => u.Owner == ctx.PlayerId).ToList());
            Enemies = new(() => ctx.Sim.State.Units.Where(u => u.Health > 0 && ctx.IsEnemy(u.Owner)).ToList());
            EnemyCities = new(() => ctx.Sim.State.Cities.Where(c => ctx.IsEnemy(c.Owner)
                && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id)).ToArray());
            UnitCounts = new(() => Units.Value.GroupBy(ctx.Role).ToDictionary(g => g.Key, g => g.Count()));
            QueuedCounts = new(() => Cities.Value.SelectMany(c => c.ProductionQueue).Where(o => o.Kind == "cities:unit")
                .GroupBy(o => ctx.Catalog.Roles.GetValueOrDefault(o.ContentId)).ToDictionary(g => g.Key, g => g.Count()));
            ThreatStrength = new(() => BuildThreatStrength(ctx, Enemies.Value));
            Targets = new(() => new CampaignTargets(ctx));
        }

        public UnitPath? FindPath(AiContext ctx, Unit unit, TileId goal, bool attack)
        {
            // This segment fixes the entire unit and world state, not a partial movement fingerprint.
            var key = (unit.Id, goal, attack);
            UnitPath? Search() => ctx.Service<IPathfindingService>().FindPath(ctx.Sim, unit.Id, ctx.Grid.HexOf(goal), attack);
            Lazy<UnitPath?>? path;
            lock (_pathsLock)
            {
                // Retain only queried destinations: eight per owned unit, capped at 256 per segment.
                int limit = (int)Math.Min(256L, Math.Max(1L, Units.Value.Count) * 8);
                if (!_paths.TryGetValue(key, out path) && _paths.Count < limit)
                {
                    path = new(Search);
                    _paths.Add(key, path);
                }
            }
            // Search outside the lock. Lazy also reuses null results and coalesces concurrent identical queries.
            return path is null ? Search() : path.Value;
        }

        private static int[] BuildThreatStrength(AiContext ctx, IEnumerable<Unit> enemies)
        {
            // Supported maps have a bounded plot array; a dense strength bucket avoids per-query unit scans.
            var strength = new int[ctx.Grid.Count];
            foreach (var enemy in enemies)
            {
                int tile = ctx.Tile(enemy.Position).Value;
                strength[tile] = checked(strength[tile] + Math.Max(1, ctx.Def(enemy).Strength));
            }
            return strength;
        }
    }
    public Simulation Sim { get; }
    public int PlayerId { get; }
    public Player Self { get; }
    public Team Team { get; }
    public LeaderDef? Leader { get; }
    public AiCatalog Catalog { get; }
    public List<City> Cities => _segment.Cities.Value;
    public List<Unit> Units => _segment.Units.Value;
    public int Revision { get; private set; }
    public CampaignTargets Targets => _segment.Targets.Value;
    public IReadOnlyList<City> EnemyCities => _segment.EnemyCities.Value;
    /// <summary>Units that already acted this turn.</summary>
    public HashSet<int> Done { get; } = new();
    public int IssuedCommands { get; private set; }

    public AiContext(Simulation sim, int player)
    {
        Sim = sim; PlayerId = player;
        Self = sim.State.PlayerById(player); Team = sim.State.TeamById(Self.TeamId);
        Leader = sim.Content.Services.Get<IPeoplesService>().SingleOrDefault()?.Leader(sim, player);
        Catalog = AiCatalog.For(sim.Content);
        _segment = new(this);
    }

    /// <summary>Discard every derived entity view and query after a mutation, including direct service calls.</summary>
    public void Refresh()
    {
        _segment = new(this);
        Revision++;
    }

    public HexGrid Grid => Sim.State.Grid;
    public int Turn => Sim.State.Turn;
    public T Service<T>() where T : class => Sim.Content.Services.Get<T>().Single();
    public UnitDef Def(Unit unit) => Sim.Content.Get<UnitDef>(unit.Type);
    public UnitRole Role(Unit unit) => Catalog.Roles[unit.Type];
    public IReadOnlyList<Unit> Enemies => _segment.Enemies.Value;
    public int CountUnits(UnitRole role) => _segment.UnitCounts.Value.GetValueOrDefault(role);
    public int CountQueued(UnitRole role) => _segment.QueuedCounts.Value.GetValueOrDefault(role);
    public CityView ViewOf(City city) => _segment.Views.GetOrAdd(city.Id,
        _ => new Lazy<CityView>(() => CityView.Build(this, city))).Value;
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
        if (ok)
        {
            IssuedCommands++;
            // Command application includes synchronous event callbacks (capture, culture, assignments, war).
            Refresh();
        }
        return ok;
    }

    public IEnumerable<Unit> UnitsWithRole(UnitRole role) => Units.Where(u => Sim.State.FindUnit(u.Id) is not null && Role(u) == role);
    public bool Alive(Unit unit) => Sim.State.FindUnit(unit.Id) is not null;
    public City? CityAtUnit(Unit unit) => Sim.State.CityAt(unit.Position) is { } c && c.Owner == PlayerId ? c : null;
    public City? NearestCity(Hex from) => _segment.NearestCities.GetOrAdd(Tile(from),
        tile => Cities.MinBy(c => (Grid.Distance(Tile(c.Position), tile), c.Id)));

    /// <summary>Reuse read-only production eligibility until a successful command or explicit refresh.
    /// Issue still validates the command, independently of this scoring cache.</summary>
    public bool CanProduce(int city, string kind, string id) => _segment.Production.GetOrAdd((city, kind, id),
        key => new Lazy<bool>(() => Service<ICityProductionService>().Validate(Sim, key.City, key.Kind, key.Id).Success)).Value;
    public MilitaryUnitState MilitaryState(Unit unit) => unit.Extensions.Get<MilitaryUnitState>(Sim.Content) ?? new();

    /// <summary>Exact path query in the unchanged segment. Direct mutations require Refresh before the next query.</summary>
    public UnitPath? FindPath(Unit unit, Hex target, bool attack = false) => Grid.TryTile(target, out var goal)
        ? _segment.FindPath(this, unit, goal, attack) : null;

    /// <summary>Move along a pathfinder route, truncated to the current turn by the movement rules themselves.</summary>
    public bool MoveToward(Unit unit, Hex target, bool attack = false)
    {
        if (unit.MovesLeft <= 0 || unit.Position == target) return false;
        var path = FindPath(unit, target, attack);
        if (path is null || path.Steps.Count == 0) return false;
        return Issue(new MoveUnitPathCommand { Player = PlayerId, Unit = unit.Id, Path = path.Steps.ToArray() });
    }

    /// <summary>Hostile strength (sum of unit strengths) within a radius of a position.</summary>
    public int ThreatAround(Hex position, int radius)
    {
        if (radius < 0) return 0;
        var segment = _segment;
        return segment.Threats.GetOrAdd((Tile(position), radius), query =>
        {
            var strength = segment.ThreatStrength.Value;
            int total = 0;
            foreach (var tile in Grid.Range(query.Tile, query.Radius)) total = checked(total + strength[tile.Value]);
            return total;
        });
    }

    /// <summary>Sum of own military unit strength weighted by health, a simple integer power estimate.</summary>
    public static long Power(Simulation sim, int player)
    {
        long power = 0;
        foreach (var u in sim.State.Units)
            if (u.Owner == player) power += sim.Content.Get<UnitDef>(u.Type).Strength * u.Health / 10;
        return power + 20L * sim.State.Cities.Count(c => c.Owner == player);
    }
}
