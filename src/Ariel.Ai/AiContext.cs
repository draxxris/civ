using System.Collections.Concurrent;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>Everything one planner needs for one player's turn: state shortcuts, personality and a command sink.
/// Built fresh each turn. Saved intent belongs to player extensions, not to this derived query context.
/// <para>The public members are the API a planner contributed through <see cref="IAiPlanner"/> works with: the
/// player and its team, cached views of its cities and units that every successful <see cref="Issue"/> renews, the
/// command sink, and the ledger of units that already acted this turn. Everything else stays with the built-in
/// planners.</para></summary>
public sealed class AiContext
{
    private Segment _segment;
    /// <summary>The threat grid every segment refills in place: one map-sized array for the whole turn instead of
    /// one per issued command, which on large maps is a large-object-heap allocation each time.</summary>
    private int[]? _threat;
    /// <summary>The same for the strength of foreign troops at peace with us (<see cref="PressureAround"/>).</summary>
    private int[]? _pressure;

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
        public readonly Lazy<int[]> PressureStrength;
        public readonly Lazy<City[]> CampaignCities;
        public readonly Lazy<CampaignTargets> Targets;
        public readonly Lazy<IReadOnlyDictionary<string, long>> HeldResources;
        public readonly ConcurrentDictionary<(TileId Tile, int Radius), int> Threats = new();
        public readonly ConcurrentDictionary<(TileId Tile, int Radius), int> Pressures = new();
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
            ThreatStrength = new(() => BuildStrength(ctx, ctx._threat ??= new int[ctx.Grid.Count], Enemies.Value));
            PressureStrength = new(() => BuildStrength(ctx, ctx._pressure ??= new int[ctx.Grid.Count], ctx.Sim.State.Units.Where(u =>
                u.Health > 0 && ctx.Foreign(u.Owner) && ctx.Def(u).Strength > 0 && ctx.Def(u).Domain == "military:domain_land"
                && ctx.Sim.State.PlotAt(u.Position).Visible.Contains(ctx.Team.Id))));
            CampaignCities = new(() => BuildCampaignCities(ctx));
            Targets = new(() => new CampaignTargets(ctx));
            HeldResources = new(() => BuildHeldResources(ctx));
        }

        /// <summary>Copies of every resource on the trade network at the capital, imports included (what a city supplies
        /// itself cannot be traded), as <see cref="ResourceSubject.Held"/>
        /// counts them. Built only when a resource valuer is registered.</summary>
        private static IReadOnlyDictionary<string, long> BuildHeldResources(AiContext ctx)
        {
            var capital = ctx.Cities.OrderByDescending(c => c.Buildings.Any(b => ctx.Sim.Content.Get<BuildingDef>(b).Capital))
                .ThenBy(c => c.Id).FirstOrDefault();
            if (capital is null) return new Dictionary<string, long>(StringComparer.Ordinal);
            var resources = ctx.Sim.Content.Get<BonusDef>().All.Select(b => b.Id).ToArray();
            return ResourceAccess.TradeCounts(ctx.Sim, ctx.PlayerId, capital.Id, resources);
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

        /// <summary>Enemy cities that a land campaign has chosen as its goal, or that the player's own soldiers stand
        /// within <see cref="CampaignReach"/> of.</summary>
        private static City[] BuildCampaignCities(AiContext ctx)
        {
            var goals = AiIntent.Read(ctx).Campaigns.Values.Select(g => g.City).OfType<int>().ToHashSet();
            var army = ctx.Units.Where(u => u.Health > 0 && ctx.Role(u) is UnitRole.Melee or UnitRole.Siege).ToArray();
            return ctx.EnemyCities.Where(c => goals.Contains(c.Id)
                || army.Any(u => ctx.Grid.Distance(u.Position, c.Position) <= CampaignReach)).ToArray();
        }

        private static int[] BuildStrength(AiContext ctx, int[] strength, IEnumerable<Unit> units)
        {
            // Supported maps have a bounded plot array; a dense strength bucket avoids per-query unit scans.
            // Reusing it is safe because segments never overlap: scoring joins before Issue or Refresh replaces one,
            // and each kind of strength has its own buffer.
            Array.Clear(strength);
            foreach (var unit in units)
            {
                int tile = ctx.Tile(unit.Position).Value;
                strength[tile] = checked(strength[tile] + Math.Max(1, ctx.Def(unit).Strength));
            }
            return strength;
        }
    }
    public Simulation Sim { get; }
    public int PlayerId { get; }
    public Player Self { get; }
    public Team Team { get; }
    /// <summary>The leader this player plays, whose personality (flavors, refusal thresholds, temperament) colours its
    /// choices; null in a ruleset without leaders.</summary>
    public LeaderDef? Leader { get; }
    internal AiCatalog Catalog { get; }
    /// <summary>The player's cities as of the last successful command or <see cref="Refresh"/>.</summary>
    public IReadOnlyList<City> Cities => _segment.Cities.Value;
    /// <summary>The player's units as of the last successful command or <see cref="Refresh"/>; one may have died
    /// since, which <see cref="Alive"/> tells.</summary>
    public IReadOnlyList<Unit> Units => _segment.Units.Value;
    internal int Revision { get; private set; }
    internal CampaignTargets Targets => _segment.Targets.Value;
    /// <summary>Cities of players this player is at war with, on plots its team has revealed.</summary>
    public IReadOnlyList<City> EnemyCities => _segment.EnemyCities.Value;
    /// <summary>The enemy cities the land army is going after: a campaign's goal, or a city the player's own soldiers
    /// stand within <see cref="CampaignReach"/> tiles of. Support arms (aircraft, say) aim here rather than at every
    /// enemy city they can reach.</summary>
    public IReadOnlyList<City> CampaignCities => _segment.CampaignCities.Value;
    /// <summary>How near its soldiers must stand for an enemy city to count among <see cref="CampaignCities"/>.</summary>
    public const int CampaignReach = 3;
    /// <summary>Units that already acted this turn.</summary>
    internal HashSet<int> Done { get; } = new();
    /// <summary>Whether a planner earlier in this turn already gave <paramref name="unit"/> its orders.</summary>
    public bool HasActed(Unit unit) => Done.Contains(unit.Id);
    /// <summary>Claim <paramref name="unit"/> for this turn, so later planners leave it alone.</summary>
    public void MarkActed(Unit unit) => Done.Add(unit.Id);
    /// <summary>The treasury the economy planner keeps in hand for this empire; spend only what lies above it.</summary>
    public long GoldReserve => EconomyPlanner.Reserve(Cities.Count);
    internal int IssuedCommands { get; private set; }

    // Work the built-in planners share within one turn, created on first use.
    private SitePlanner? _sites;
    private ArmyPlan? _army;
    private CityPlanner? _production;
    internal SitePlanner Sites => _sites ??= new(this);
    internal ArmyPlan Army => _army ??= new(this);
    /// <summary>This turn's production plan; military and hurry orders read the choices it made.</summary>
    internal CityPlanner Production => _production ??= new(this, Sites, Army);

    internal AiContext(Simulation sim, int player)
    {
        Sim = sim; PlayerId = player;
        Self = sim.State.PlayerById(player); Team = sim.State.TeamById(Self.TeamId);
        Leader = sim.Rules().Peoples?.Leader(sim, player);
        Catalog = AiCatalog.For(sim.Content);
        _segment = new(this);
    }

    /// <summary>A context for asking what something is worth to <paramref name="player"/> outside its turn, as a screen
    /// previewing a deal for a computer player's answer does. It reads the game only: <see cref="Issue"/> throws.</summary>
    public static AiContext Observe(Simulation sim, int player) => new(sim, player) { _observer = true };

    private bool _observer;

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
    internal UnitRole Role(Unit unit) => Catalog.Roles[unit.Type];
    /// <summary>Living units of players this player is at war with, seen or not.</summary>
    public IReadOnlyList<Unit> Enemies => _segment.Enemies.Value;
    /// <summary>The land units that fight: the player's melee and siege units, not counting those queued.</summary>
    public int LandArmy => CountUnits(UnitRole.Melee) + CountUnits(UnitRole.Siege);
    /// <summary>The unit <paramref name="city"/> should train among <paramref name="candidates"/>: the highest
    /// <paramref name="score"/> plus the leader's personality and what unit valuers add, ties by ordinal id, among the
    /// units the player can plausibly build and the city can produce; null when none qualifies.</summary>
    public UnitDef? BestUnit(City city, Func<UnitDef, bool> candidates, Func<UnitDef, int> score) =>
        Production.Options.BestAmong(city, candidates, score);
    internal int CountUnits(UnitRole role) => _segment.UnitCounts.Value.GetValueOrDefault(role);
    internal int CountQueued(UnitRole role) => _segment.QueuedCounts.Value.GetValueOrDefault(role);
    internal CityView ViewOf(City city) => _segment.Views.GetOrAdd(city.Id,
        _ => new Lazy<CityView>(() => CityView.Build(this, city))).Value;
    internal bool IsEnemy(int player) => player != PlayerId && Team.Wars.Contains(Sim.State.PlayerById(player).TeamId);
    /// <summary>Another civilization's player this player is at peace with: not itself, a teammate, an enemy or a barbarian.</summary>
    internal bool Foreign(int player) => player != PlayerId && !IsEnemy(player) && Sim.State.PlayerById(player).TeamId != Team.Id
        && PlayerRules.IsNation(Sim, player);
    internal bool AtWarWithPlayers => Team.Wars.Any(t => Sim.State.TeamById(t).Members.Any(m => PlayerRules.IsNation(Sim, m)));
    public TileId Tile(Hex hex) => Grid.TileAt(hex);

    /// <summary>Flavor weight: a base of 10 plus the leader's own flavor, so every flavor counts a little.</summary>
    internal int Weight(string flavor) => 10 + (Leader?.Flavors.GetValueOrDefault(flavor) ?? 0);
    /// <summary>Flavor-weighted value of a content flavor table, in roughly the same points as yields.</summary>
    internal int FlavorScore(IReadOnlyDictionary<string, int> flavors)
    {
        int total = 0;
        foreach (var pair in flavors) total += pair.Value * Weight(pair.Key) / 5;
        return total;
    }

    /// <summary>What every registered <see cref="IAiValuer{TSubject}"/> of resources adds to the AI's worth of
    /// <paramref name="subject"/>, by ordinal id, clamped to ±<see cref="AiValuation.Limit"/>; 0 when none is registered.
    /// A planner valuing resources (the diplomacy planner, for deals) asks this so one registration answers
    /// every "what is this resource worth to me" question.</summary>
    public int Adjustment(in ResourceSubject subject) => Sum(Catalog.ResourceValuers, in subject);
    /// <summary>What the registered building valuers add to the worth of <paramref name="subject"/>; 0 when none is registered.</summary>
    public int Adjustment(in BuildingSubject subject) => Sum(Catalog.BuildingValuers, in subject);
    /// <summary>What the registered unit valuers add to the worth of <paramref name="subject"/>; 0 when none is registered.</summary>
    public int Adjustment(in UnitSubject subject) => Sum(Catalog.UnitValuers, in subject);
    /// <summary>What the registered technology valuers add to the worth of <paramref name="subject"/>; 0 when none is registered.</summary>
    public int Adjustment(in TechSubject subject) => Sum(Catalog.TechValuers, in subject);
    /// <summary>What the registered civic valuers add to the worth of <paramref name="subject"/>; 0 when none is registered.</summary>
    public int Adjustment(in CivicSubject subject) => Sum(Catalog.CivicValuers, in subject);
    /// <summary>What the registered city valuers add to the worth of taking <paramref name="subject"/>; 0 when none is registered.</summary>
    public int Adjustment(in CitySubject subject) => Sum(Catalog.CityValuers, in subject);

    private int Sum<TSubject>(IAiValuer<TSubject>[] valuers, in TSubject subject) where TSubject : struct
    {
        if (valuers.Length == 0) return 0;
        long total = 0;
        foreach (var valuer in valuers) total += valuer.Adjust(this, in subject);
        return (int)Math.Clamp(total, -AiValuation.Limit, AiValuation.Limit);
    }

    /// <summary>The resource adjustment for one more source of <paramref name="resource"/> from <paramref name="plot"/>,
    /// counting what the player holds only when a resource valuer is registered.</summary>
    internal int ResourceAdjustment(BonusDef resource, TileId? plot) => Catalog.ResourceValuers.Length == 0 ? 0
        : Adjustment(new ResourceSubject(resource, _segment.HeldResources.Value.GetValueOrDefault(resource.Id), plot));

    /// <summary>Validate and apply through the normal command path. A success renews every cached view, so the next
    /// decision sees its effect.</summary>
    public bool Issue(GameCommand command)
    {
        if (_observer) throw new InvalidOperationException("an observing AI context cannot issue commands");
        bool ok = Sim.TryApply(command, out _);
        if (ok)
        {
            IssuedCommands++;
            // Command application includes synchronous event callbacks (capture, culture, assignments, war).
            Refresh();
        }
        return ok;
    }

    internal IEnumerable<Unit> UnitsWithRole(UnitRole role) => Units.Where(u => Sim.State.FindUnit(u.Id) is not null && Role(u) == role);
    public bool Alive(Unit unit) => Sim.State.FindUnit(unit.Id) is not null;
    internal City? CityAtUnit(Unit unit) => Sim.State.CityAt(unit.Position) is { } c && c.Owner == PlayerId ? c : null;
    internal City? NearestCity(Hex from) => _segment.NearestCities.GetOrAdd(Tile(from),
        tile => Cities.MinBy(c => (Grid.Distance(Tile(c.Position), tile), c.Id)));

    /// <summary>Reuse read-only production eligibility until a successful command or explicit refresh.
    /// Issue still validates the command, independently of this scoring cache.</summary>
    internal bool CanProduce(int city, string kind, string id) => _segment.Production.GetOrAdd((city, kind, id),
        key => new Lazy<bool>(() => Service<ICityProductionService>().Validate(Sim, key.City, key.Kind, key.Id).Success)).Value;
    internal MilitaryUnitState MilitaryState(Unit unit) => unit.Extensions.Get<MilitaryUnitState>(Sim.Content) ?? new();

    /// <summary>Exact path query in the unchanged segment. Direct mutations require Refresh before the next query.</summary>
    internal UnitPath? FindPath(Unit unit, Hex target, bool attack = false) => Grid.TryTile(target, out var goal)
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
    internal int ThreatAround(Hex position, int radius)
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

    /// <summary>Visible land strength of civilizations at peace with this player within a radius: troops massing on a
    /// border, which could attack the turn war is declared.</summary>
    internal int PressureAround(Hex position, int radius)
    {
        if (radius < 0) return 0;
        var segment = _segment;
        return segment.Pressures.GetOrAdd((Tile(position), radius), query =>
        {
            var strength = segment.PressureStrength.Value;
            int total = 0;
            foreach (var tile in Grid.Range(query.Tile, query.Radius)) total = checked(total + strength[tile.Value]);
            return total;
        });
    }

    /// <summary>How this player's team's war against <paramref name="enemyTeam"/> is going: the production cost of enemy
    /// units destroyed plus cities taken, less the same suffered (see <see cref="AiTeamState"/>); zero at peace.</summary>
    public int WarScore(int enemyTeam) => WarRecord.Score(Sim, Team.Id, enemyTeam);

    /// <summary>Whether the computer player would end its team's war against <paramref name="enemyTeam"/> now: it is
    /// outgunned, beaten on score, or a long war has stopped paying. False at peace.</summary>
    public bool WantsPeace(int enemyTeam)
    {
        if (!Team.Wars.Contains(enemyTeam)) return false;
        long mine = Team.Members.Where(m => PlayerRules.IsNation(Sim, m)).Sum(m => Power(Sim, m));
        long theirs = Sim.State.TeamById(enemyTeam).Members.Where(m => PlayerRules.IsNation(Sim, m)).Sum(m => Power(Sim, m));
        int length = Turn - (Sim.Rules().Relations.Relation(Sim, Team.Id, enemyTeam)?.WarStarted ?? 0);
        return WarPlanner.WantsPeace(mine, theirs, WarScore(enemyTeam), length);
    }

    /// <summary>A player's military strength as the computer player weighs it: unit strength weighted by health, plus a
    /// little per city. Compare two players' values; the scale has no meaning of its own.</summary>
    public long Strength(int player) => Power(Sim, player);

    /// <summary>What a civic is worth to this leader and empire, on the scale the civic planner compares civics with.</summary>
    public int CivicValue(CivicDef civic) => new CivicPlanner(this).Score(civic);

    /// <summary>Sum of own military unit strength weighted by health, a simple integer power estimate.</summary>
    internal static long Power(Simulation sim, int player)
    {
        long power = 0;
        foreach (var u in sim.State.Units)
            if (u.Owner == player) power += sim.Content.Get<UnitDef>(u.Type).Strength * u.Health / 10;
        return power + 20L * sim.State.Cities.Count(c => c.Owner == player);
    }
}
