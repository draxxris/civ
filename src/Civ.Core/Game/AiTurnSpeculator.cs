using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using SchemaUnitDef = Civ.Content.Schema.UnitDef;

namespace Civ.Game;

/// <summary>Sees each top-level command a simulation applies.</summary>
internal interface ICommandObserver
{
    void Before(Simulation sim, GameCommand command);
    void After(Simulation sim, GameCommand command);
    void Rejected(Simulation sim, GameCommand command);
}

/// <summary>How one AI player's turn was resolved inside <see cref="Simulation.RunAi"/>.</summary>
internal enum SpeculationOutcome { Direct, Committed, Conflict }

/// <summary>Diagnostic record of one AI turn decision; never saved or hashed.</summary>
internal sealed record SpeculationEvent(int Turn, int Wave, int Player, SpeculationOutcome Outcome, string? Reason = null);

/// <summary>Runs AI turns in speculative waves. The first remaining player acts on the authoritative simulation while
/// later independent players plan concurrently on isolated forks of the wave-start state. Plans are then committed in
/// ascending player id order by replaying their commands, but only when nothing an earlier player did this wave could
/// have changed them: no shared RNG draws or entity ids, no war, global, cross-player or wonder-limit changes, and no
/// earlier active change within <see cref="ActiveFootprintRadius"/> of the plan's own activity. The first plan that
/// fails starts a new wave from the current state. Every decision depends only on state, so peers always agree.</summary>
internal static class AiTurnSpeculator
{
    /// <summary>Largest hex distance at which an earlier player's active change invalidates a later plan.</summary>
    public const int ActiveFootprintRadius = 5;
    /// <summary>Extra reach around moving settlers and explorers, whose choices depend on the land around them.</summary>
    public const int ScoutRadius = 3;
    // Unit AI roles are content ids; the land explorer role keeps this suffix in every namespace.
    private const string ExploreAiSuffix = ":unitai_explore";

    public static void Run(Simulation sim)
    {
        var remaining = sim.State.Players.OrderBy(p => p.Id).Where(p => p.Ai != null && !p.EndedTurn).Select(p => p.Id).ToArray();
        int wave = 0;
        while (remaining.Length > 0)
        {
            wave++;
            int width = WaveWidth(sim, remaining);
            if (width <= 1)
            {
                RunDirect(sim, remaining[0]);
                Report(sim, wave, remaining[0], SpeculationOutcome.Direct);
                remaining = remaining[1..];
                continue;
            }
            int committed = RunWave(sim, remaining[..width], wave);
            remaining = remaining[committed..];
        }
    }

    /// <summary>Number of leading players that can share a wave: the first player plus every following player with a
    /// different team that is not at war with any earlier player of the wave.</summary>
    private static int WaveWidth(Simulation sim, int[] remaining)
    {
        int width = 1;
        while (width < remaining.Length && width < sim.AiWaveLimit && Independent(sim, remaining, width)) width++;
        return width;
    }

    private static bool Independent(Simulation sim, int[] remaining, int i)
    {
        var team = sim.State.TeamOf(remaining[i]);
        for (int j = 0; j < i; j++)
        {
            int other = sim.State.PlayerById(remaining[j]).TeamId;
            if (other == team.Id || team.Wars.Contains(other)) return false;
        }
        return true;
    }

    private static void RunDirect(Simulation sim, int player)
    {
        sim.RunAiPlayer(player);
        sim.State.PlayerById(player).EndedTurn = true;
    }

    private static void Report(Simulation sim, int wave, int player, SpeculationOutcome outcome, string? reason = null) =>
        sim.SpeculationObserver?.Invoke(new SpeculationEvent(sim.State.Turn, wave, player, outcome, reason));

    /// <summary>Run one wave and return how many leading players ended their turns.</summary>
    private static int RunWave(Simulation sim, int[] players, int wave)
    {
        var baseline = new WaveBaseline(sim);
        // Shared immutable geometry: materialize its lazy cache before threads read it.
        _ = sim.State.Grid.MaximumDistance;
        var plans = PreparePlans(sim, players, baseline);
        var first = ExecutePlayers(sim, players, plans, baseline);
        sim.State.PlayerById(players[0]).EndedTurn = true;
        Report(sim, wave, players[0], SpeculationOutcome.Direct);
        return CommitPlans(sim, players, plans, baseline, first, wave);
    }

    private static SpeculativePlan?[] PreparePlans(Simulation sim, int[] players, WaveBaseline baseline)
    {
        var plans = new SpeculativePlan?[players.Length];
        // Forks only read the authoritative state, so they can be copied concurrently before the first player acts.
        Parallel.For(1, players.Length, i =>
        {
            if (HasPresence(sim, players[i])) plans[i] = new SpeculativePlan(players[i], sim.ForkSpeculative(), baseline);
        });
        return plans;
    }

    // A fork failure ends the wave; the player then runs in place, where real errors surface as in sequential play.
    private static PlayerTrace ExecutePlayers(Simulation sim, int[] players, SpeculativePlan?[] plans, WaveBaseline baseline)
    {
        PlayerTrace? first = null;
        ExceptionDispatchInfo? failure = null;
        Parallel.For(0, players.Length, i =>
        {
            if (i > 0) { plans[i]?.Execute(); return; }
            try { first = PlayerTrace.Run(sim, players[0], baseline); }
            catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
        });
        failure?.Throw();
        return first!;
    }

    private static int CommitPlans(Simulation sim, int[] players, SpeculativePlan?[] plans, WaveBaseline baseline,
        PlayerTrace first, int wave)
    {
        var mutations = new MutationFootprint(sim.State.Grid);
        mutations.Absorb(first);
        var acted = new HashSet<int> { players[0] };
        var commits = new CommitLog(sim);
        for (int i = 1; i < players.Length; i++)
        {
            string? conflict = null;
            if (plans[i] is { } plan) conflict = plan.Conflict(sim, mutations, acted) ?? commits.Commit(plan);
            else mutations.Absorb(commits.RunInline(players[i], baseline));
            if (conflict is not null)
            {
                Report(sim, wave, players[i], SpeculationOutcome.Conflict, conflict);
                return i;
            }
            sim.State.PlayerById(players[i]).EndedTurn = true;
            acted.Add(players[i]);
            Report(sim, wave, players[i], plans[i] is null ? SpeculationOutcome.Direct : SpeculationOutcome.Committed);
            if (plans[i] is { } done) mutations.Absorb(done.Trace!);
        }
        return players.Length;
    }

    private static bool HasPresence(Simulation sim, int player) =>
        sim.State.Cities.Any(c => c.Owner == player) || sim.State.Units.Any(u => u.Owner == player);

    /// <summary>Shared state at the start of a wave, used to tell what each player changed.</summary>
    internal sealed class WaveBaseline
    {
        public readonly (ulong, ulong, ulong, ulong) Rng;
        public readonly int NextCityId, NextUnitId;
        public readonly PlotRecord[] Plots;
        public readonly Dictionary<int, CityRecord> Cities;
        private readonly Dictionary<int, UnitRecord> _units;
        public readonly Dictionary<int, string> Players, Teams;
        public readonly Dictionary<int, int[]> Wars;
        public readonly string Global;

        public WaveBaseline(Simulation sim)
        {
            Rng = sim.Rng.State; NextCityId = sim.State.NextCityId; NextUnitId = sim.State.NextUnitId;
            Plots = Array.ConvertAll(sim.State.Plots, PlotRecord.Of);
            Cities = sim.State.Cities.ToDictionary(c => c.Id, c => CityRecord.Of(sim.State, c));
            _units = sim.State.Units.ToDictionary(u => u.Id, u => UnitRecord.Of(sim.State, u));
            Wars = sim.State.Teams.ToDictionary(t => t.Id, t => t.Wars.ToArray());
            (Players, Teams, Global) = Digests(sim.State);
        }

        public static (Dictionary<int, string> Players, Dictionary<int, string> Teams, string Global) Digests(GameState state) => (
            state.Players.ToDictionary(p => p.Id, p => CanonicalJson.Digest(new { p.Culture, p.Treasury, p.Civics, p.Extensions, p.StartingPlot })),
            state.Teams.ToDictionary(t => t.Id, CanonicalJson.Digest),
            CanonicalJson.Digest(state.Extensions));

        /// <summary>Players and teams whose shared records differ from the wave start, plus whether wars or global state changed.</summary>
        public (HashSet<int> Players, HashSet<int> Teams, bool Wars, bool Global) Changes(GameState state)
        {
            var (players, teams, global) = Digests(state);
            var changedPlayers = players.Where(p => Players.GetValueOrDefault(p.Key) != p.Value).Select(p => p.Key).ToHashSet();
            var changedTeams = teams.Where(t => Teams.GetValueOrDefault(t.Key) != t.Value).Select(t => t.Key).ToHashSet();
            bool wars = state.Teams.Any(t => !t.Wars.SetEquals(Wars.GetValueOrDefault(t.Id) ?? []));
            return (changedPlayers, changedTeams, wars, global != Global);
        }

        /// <summary>Unit creation, removal and all state changes have spatial effects, even without movement or a
        /// command target. Compare once per completed plan; command tracing still preserves intermediate movement.</summary>
        public IEnumerable<int> UnitChanges(GameState state)
        {
            foreach (var unit in state.Units)
            {
                var now = UnitRecord.Of(state, unit);
                if (_units.TryGetValue(unit.Id, out var before))
                {
                    if (before == now) continue;
                    yield return before.Tile;
                }
                yield return now.Tile;
            }
            foreach (var (id, before) in _units)
                if (state.FindUnit(id) is null) yield return before.Tile;
        }

        /// <summary>Tiles whose shared gameplay fields differ from the wave start, and whether any city gained or lost a building.</summary>
        public (List<int> Tiles, bool Buildings) Diff(GameState state)
        {
            var tiles = new List<int>();
            for (int i = 0; i < Plots.Length; i++)
                if (!Plots[i].Matches(state.Plots[i])) tiles.Add(i);
            bool buildings = false;
            foreach (var city in state.Cities)
            {
                var now = CityRecord.Of(state, city);
                if (!Cities.TryGetValue(city.Id, out var before)) { tiles.Add(now.Tile); buildings |= city.Buildings.Count > 0; continue; }
                if (before.Owner != now.Owner || before.Tile != now.Tile) { tiles.Add(before.Tile); tiles.Add(now.Tile); }
                if (before.Buildings != now.Buildings) { tiles.Add(now.Tile); buildings = true; }
            }
            foreach (var (id, before) in Cities)
                if (state.FindCity(id) is null) tiles.Add(before.Tile);
            return (tiles, buildings);
        }
    }

    internal readonly record struct PlotRecord(int? Owner, string? Improvement, string? Route, string? Feature, string Terrain,
        PlotKind Kind, string? Bonus, bool DepletedMine, string BuildProgress)
    {
        public static PlotRecord Of(Plot p) => new(p.Owner, p.Improvement, p.Route, p.Feature, p.Terrain, p.Kind, p.Bonus, p.DepletedMine,
            p.BuildProgress.Count == 0 ? "" : string.Join(';', p.BuildProgress.Select(e => e.Key + "=" + e.Value)));
        public bool Matches(Plot p) => Owner == p.Owner && Improvement == p.Improvement && Route == p.Route && Feature == p.Feature
            && Terrain == p.Terrain && Kind == p.Kind && Bonus == p.Bonus && DepletedMine == p.DepletedMine
            && BuildProgress == (p.BuildProgress.Count == 0 ? "" : string.Join(';', p.BuildProgress.Select(e => e.Key + "=" + e.Value)));
    }

    // Include the full serialized unit contract, including mod components, rather than maintaining a partial field list.
    private sealed record UnitRecord(int Tile, string State)
    {
        public static UnitRecord Of(GameState state, Unit unit) => new(state.Grid.TileAt(unit.Position).Value, CanonicalJson.Serialize(unit));
    }

    // Citizen assignments are not shared: a city can only work plots its owner controls.
    internal sealed record CityRecord(int Owner, int Tile, string Buildings)
    {
        public static CityRecord Of(GameState state, City c) => new(c.Owner, state.Grid.TileAt(c.Position).Value, string.Join(';', c.Buildings));
    }

    /// <summary>What one player's turn touched: active tiles, RNG draws, id allocations and shared records.</summary>
    internal sealed class PlayerTrace : ICommandObserver
    {
        private readonly WaveBaseline _baseline;
        private readonly HashSet<int> _tiles = new();
        private Dictionary<int, TileId>? _before;
        public IReadOnlyCollection<int> Tiles => _tiles;
        public bool UsedRng { get; private set; }
        public bool AllocatedCity { get; private set; }
        public bool AllocatedUnit { get; private set; }
        public HashSet<int> ChangedPlayers { get; private set; } = new();
        public HashSet<int> ChangedTeams { get; private set; } = new();

        private PlayerTrace(WaveBaseline baseline) => _baseline = baseline;

        /// <summary>Run the player's decisions in place on a simulation that starts at the wave baseline state.</summary>
        public static PlayerTrace Run(Simulation sim, int player, WaveBaseline baseline)
        {
            var trace = new PlayerTrace(baseline);
            var previous = sim.CommandObserver;
            bool parallel = sim.IsParallelAiTurn;
            sim.CommandObserver = trace;
            sim.IsParallelAiTurn = true;
            try { sim.RunAiPlayer(player); }
            finally { sim.CommandObserver = previous; sim.IsParallelAiTurn = parallel; }
            trace.Finish(sim);
            return trace;
        }

        public void Before(Simulation sim, GameCommand command)
        {
            _before = new Dictionary<int, TileId>(sim.State.Units.Count);
            foreach (var u in sim.State.Units) _before[u.Id] = sim.State.Grid.TileAt(u.Position);
        }

        public void After(Simulation sim, GameCommand command)
        {
            var grid = sim.State.Grid;
            var before = _before ?? new();
            _before = null;
            foreach (var u in sim.State.Units)
            {
                var now = grid.TileAt(u.Position);
                bool existed = before.Remove(u.Id, out var then);
                if (existed && then == now) continue;
                _tiles.Add(now.Value);
                if (existed) _tiles.Add(then.Value);
                if (Scout(sim, u)) foreach (var t in grid.Range(now, ScoutRadius)) _tiles.Add(t.Value);
            }
            foreach (var removed in before.Values) _tiles.Add(removed.Value);
            AddCommandTiles(sim, command);
        }

        // A rejected command changed nothing, but its targets show where the plan looked.
        public void Rejected(Simulation sim, GameCommand command) => AddCommandTiles(sim, command);

        private void AddCommandTiles(Simulation sim, GameCommand command)
        {
            foreach (var hex in CommandHexes(command))
                if (sim.State.Grid.TryTile(hex, out var tile)) _tiles.Add(tile.Value);
        }

        private static bool Scout(Simulation sim, Unit unit) => sim.Content.TryGet<SchemaUnitDef>(unit.Type, out var def)
            ? def.CanFoundCity || def.UnitAiTypes.Any(p => p.Value && p.Key.EndsWith(ExploreAiSuffix, StringComparison.Ordinal))
            : sim.Content.TryGet<Civ.Content.UnitDef>(unit.Type, out var legacy) && legacy.CanFoundCity;

        private void Finish(Simulation sim)
        {
            UsedRng = sim.Rng.State != _baseline.Rng;
            AllocatedCity = sim.State.NextCityId != _baseline.NextCityId;
            AllocatedUnit = sim.State.NextUnitId != _baseline.NextUnitId;
            _tiles.UnionWith(_baseline.Diff(sim.State).Tiles);
            _tiles.UnionWith(_baseline.UnitChanges(sim.State));
            (ChangedPlayers, ChangedTeams, _, _) = _baseline.Changes(sim.State);
        }
    }

    /// <summary>Cumulative active changes of the players that already ended their turns in this wave, dilated by the radius.</summary>
    private sealed class MutationFootprint(HexGrid grid)
    {
        private readonly bool[] _near = new bool[grid.Count];
        private readonly HashSet<int> _tiles = new();

        public void Absorb(PlayerTrace trace)
        {
            foreach (int tile in trace.Tiles)
                if (_tiles.Add(tile))
                    foreach (var t in grid.Range(new TileId(tile), ActiveFootprintRadius)) _near[t.Value] = true;
        }

        public bool Touches(IEnumerable<int> tiles) => tiles.Any(t => _near[t]);
    }

    /// <summary>Applies accepted plans to the authoritative simulation in player order. The state is copied once,
    /// before the first change, so a replay that does not reproduce its plan can be undone along with nothing else.</summary>
    private sealed class CommitLog(Simulation sim)
    {
        private readonly List<(SpeculativePlan? Plan, int Player)> _done = new();
        private GameState? _state;
        private (ulong, ulong, ulong, ulong) _rng;
        private int _events;

        /// <summary>A player with no cities or units is not worth a fork. It acts in place, at its turn position, which
        /// is exactly sequential; its trace still joins the wave footprint.</summary>
        public PlayerTrace RunInline(int player, WaveBaseline baseline)
        {
            Save();
            _done.Add((null, player));
            return PlayerTrace.Run(sim, player, baseline);
        }

        /// <summary>Replay a plan, or restore the state and return why the replay did not match the plan.</summary>
        public string? Commit(SpeculativePlan plan)
        {
            Save();
            var error = plan.Replay(sim);
            if (error is null && plan.Fingerprint(sim) != plan.Fingerprint(plan.Fork)) error = "replay diverged";
            if (error is null) { _done.Add((plan, plan.Player)); return null; }
            Restore();
            return error;
        }

        private void Save()
        {
            if (_state is not null) return;
            _state = sim.State.Clone(); _rng = sim.Rng.State; _events = sim.Events.PublishedCount;
        }

        // Rebuild the state before the failed plan: the copy, then the plans that already matched, replayed again.
        private void Restore()
        {
            sim.State.RestoreFrom(_state!);
            sim.Rng.Set(_rng);
            sim.Events.Truncate(_events);
            _state = null;
            foreach (var (plan, player) in _done)
            {
                if (plan is null) sim.RunAiPlayer(player);
                else if (plan.Replay(sim) is { } error) throw new InvalidOperationException($"Speculative replay is not repeatable: {error}");
                sim.State.PlayerById(player).EndedTurn = true;
            }
        }
    }

    /// <summary>One later player's turn planned on a fork of the wave-start state.</summary>
    private sealed class SpeculativePlan(int player, Simulation fork, WaveBaseline baseline)
    {
        public int Player => player;
        public Simulation Fork => fork;
        public PlayerTrace? Trace { get; private set; }
        private Exception? _error;

        public void Execute()
        {
            try
            {
                Trace = PlayerTrace.Run(fork, player, baseline);
            }
            catch (Exception e) { _error = e; }
        }

        /// <summary>Why this plan may differ from a sequential turn after the earlier players of the wave, or null.</summary>
        public string? Conflict(Simulation sim, MutationFootprint mutations, HashSet<int> acted)
        {
            if (_error is not null || Trace is null) return "plan failed";
            return AllocationConflict(sim) ?? SharedStateConflict(sim, acted)
                ?? (mutations.Touches(Trace.Tiles) ? "footprint" : null);
        }

        private string? AllocationConflict(Simulation sim)
        {
            if (Trace!.UsedRng && sim.Rng.State != baseline.Rng) return "rng";
            if (Trace.AllocatedCity && sim.State.NextCityId != baseline.NextCityId) return "city id";
            if (Trace.AllocatedUnit && sim.State.NextUnitId != baseline.NextUnitId) return "unit id";
            return null;
        }

        private string? SharedStateConflict(Simulation sim, HashSet<int> acted)
        {
            var earlier = baseline.Changes(sim.State);
            if (earlier.Wars) return "war";
            if (earlier.Global) return "global state";
            var (_, limits) = baseline.Diff(sim.State);
            if (limits) return "buildings";

            // Earlier players may change their own records; a change to anyone else's could be read by this plan.
            if (!earlier.Players.IsSubsetOf(acted)) return "cross-player change";
            var actedTeams = acted.Select(p => sim.State.PlayerById(p).TeamId).ToHashSet();
            if (!earlier.Teams.IsSubsetOf(actedTeams)) return "cross-team change";
            if (earlier.Players.Overlaps(Trace!.ChangedPlayers) || earlier.Teams.Overlaps(Trace.ChangedTeams)) return "shared record";
            return null;
        }

        /// <summary>Apply the recorded commands in order, checking that they behave as they did on the fork.</summary>
        public string? Replay(Simulation target)
        {
            // With the RNG at the wave start, restoring each recorded state also reproduces planner draws between commands.
            bool exact = Trace!.UsedRng;
            int cities = target.State.NextCityId, units = target.State.NextUnitId;
            foreach (var (command, pre, post) in fork._recordedCommands!)
            {
                if (exact) target.Rng.Set(pre);
                var before = target.Rng.State;
                if (!target.TryApply(command, out var error)) return "replay rejected: " + error;
                if (target.Rng.State != (exact ? post : before)) return "replay rng";
            }
            if (exact) target.Rng.Set(fork.Rng.State);
            if (target.State.NextCityId - cities != fork.State.NextCityId - baseline.NextCityId
                || target.State.NextUnitId - units != fork.State.NextUnitId - baseline.NextUnitId) return "replay ids";
            return null;
        }

        /// <summary>Digest of everything the player owns, which a faithful replay reproduces.</summary>
        public string Fingerprint(Simulation sim)
        {
            var state = sim.State;
            var self = state.PlayerById(player);
            return CanonicalJson.Digest(new
            {
                Player = new { self.Culture, self.Treasury, self.Civics, self.Extensions, self.StartingPlot },
                Team = state.TeamById(self.TeamId),
                Units = state.Units.Where(u => u.Owner == player).ToArray(),
                Cities = state.Cities.Where(c => c.Owner == player).ToArray(),
                Tiles = Trace!.Tiles.Order().Select(t => state.Plots[t]).Select(p => new
                {
                    Shared = PlotRecord.Of(p), Revealed = p.Revealed.Contains(self.TeamId), Visible = p.Visible.Contains(self.TeamId),
                    Memory = p.RevealedSnapshots.GetValueOrDefault(self.TeamId),
                }).ToArray(),
            });
        }
    }

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> HexProperties = new();

    /// <summary>Map positions named by a command (targets and path steps), found through its public data properties.</summary>
    private static IEnumerable<Hex> CommandHexes(GameCommand command)
    {
        var properties = HexProperties.GetOrAdd(command.GetType(), type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && (p.PropertyType == typeof(Hex) || p.PropertyType == typeof(Hex?)
                || typeof(IEnumerable<Hex>).IsAssignableFrom(p.PropertyType))).ToArray());
        foreach (var property in properties)
            switch (property.GetValue(command))
            {
                case Hex hex: yield return hex; break;
                case IEnumerable<Hex> hexes: foreach (var h in hexes) yield return h; break;
            }
    }
}
