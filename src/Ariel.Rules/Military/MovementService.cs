using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Military;

internal sealed class MovementService : IMovementService
{
    public string? CanEnter(Simulation sim, int unitId, TileId from, TileId to, bool attack = false) => Query(sim, unitId).CanEnter(from, to, attack);
    internal MovementQuery Query(Simulation sim, int unit) => new(sim, sim.State.UnitById(unit));
    /// <summary>A query that keeps its plot facts in <paramref name="plots"/>, begun for it and for nothing else while it
    /// is used: a search asks a hundred plots or more, which a dense table answers without hashing or growing.</summary>
    internal MovementQuery Query(Simulation sim, int unit, PlotFactTable plots) => new(sim, sim.State.UnitById(unit), plots);

    private static string? CanEnter(MovementQuery q, TileId from, TileId to, bool attack)
    {
        var edgeError = EdgeEntry(q.Sim, q.Def, from, to);
        if (edgeError is not null) return edgeError;
        return Entry(q, from, to, attack);
    }
    /// <summary>Everything <see cref="CanEnter(MovementQuery, TileId, TileId, bool)"/> checks after the edge itself, in
    /// the same order: the plot's own entry (<see cref="PlotEntry"/>), then the zone of control.</summary>
    private static string? Entry(MovementQuery q, TileId from, TileId to, bool attack)
    {
        var plotError = attack ? PlotEntry(q, to, attack: true) : q.PlotEntry(to);
        if (plotError is not null) return plotError;
        return ControlledTransition(q, from, to, attack) ? "Enemy zone of control" : null;
    }
    /// <summary>The checks that read only the plot entered, never the plot left: terrain (or a transport to board),
    /// borders, movement barriers and whoever stands there.</summary>
    private static string? PlotEntry(MovementQuery q, TileId to, bool attack)
    {
        var (sim, unit, def) = (q.Sim, q.Unit, q.Def);
        var plot = sim.State.Plots[to.Value];
        var error = TerrainEntry(sim, unit, def, to, q.Effects);
        if (error is not null && AutoLoadTransport(sim, unit, def, to) is null) return error;
        if (!TerritoryEntry(sim, unit, def, plot.Owner)) return "Closed borders";
        var target = sim.State.Grid.HexOf(to);
        if (!attack && Barred(sim, unit.Id, target)) return "Movement barred";
        return TargetEntry(sim, unit, def, q.Effects, target, attack);
    }
    /// <summary>Whether any registered <see cref="IMovementBarrier"/> keeps this unit off the plot.</summary>
    private static bool Barred(Simulation sim, int unit, Hex target)
    {
        var barriers = sim.Rules().MovementBarriers;
        for (int i = 0; i < barriers.Count; i++)
            if (barriers[i].Blocks(sim, unit, target)) return true;
        return false;
    }
    private static string? EdgeEntry(Simulation sim, S.UnitDef def, TileId from, TileId to)
    {
        if (!ValidTile(sim, from) || !ValidTile(sim, to)) return "Off map";
        if (!sim.State.Grid.Neighbors(from).Contains(to)) return "Not adjacent";
        if (def.CanMoveAllTerrain || def.Domain is not ("military:domain_land" or "military:domain_sea")) return null;
        return MilitaryData.CrossesCliff(sim, from, to) ? "Cliff" : null;
    }
    private static string? TargetEntry(Simulation sim, Unit unit, S.UnitDef def, Ariel.Content.ModifierSink effects, Hex target, bool attack)
    {
        if (!HostilePlot(sim, unit, target)) return null;
        if (!attack) return "Enemy occupies plot";
        if (!CanAttack(sim, unit, def, effects)) return "Unit cannot attack";
        if (def.NoCapture && sim.State.CityAt(target) is not null) return "Unit cannot capture cities";
        var defenders = sim.State.UnitsAt(target).Where(u => MilitaryData.VisibleEnemy(sim, unit.Owner, u) && MilitaryData.Definition(sim, u).Strength > 0);
        if (defenders.Any() && defenders.All(u => MilitaryData.Constants(sim).MaxHitPoints - u.Health >= def.CombatLimit)) return "Defenders are at combat limit";
        return null;
    }
    private static bool ValidTile(Simulation sim, TileId tile) => tile.Value >= 0 && tile.Value < sim.State.Grid.Count;
    internal static string? TerrainEntry(Simulation sim, Unit unit, S.UnitDef def, TileId tile, Ariel.Content.ModifierSink effects)
    {
        var plot = sim.State.Plots[tile.Value];
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        if (!DomainEntry(sim, unit, def, terrain.IsWater, tile)) return "Wrong domain";
        if (def.CanMoveImpassable) return null;
        if (terrain.Impassable) return "Impassable terrain";
        if (plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).Impassable) return "Impassable feature";
        if (plot.Kind == PlotKind.Peak && !PeakEntry(sim, unit, effects)) return "Cannot enter peaks";
        return UnitTerrainEntry(sim, unit, def, plot);
    }
    private static string? UnitTerrainEntry(Simulation sim, Unit unit, S.UnitDef def, Plot plot)
    {
        if (def.TerrainImpassables.GetValueOrDefault(plot.Terrain) && !TerrainTechnology(sim, unit, def, plot.Terrain)) return "Terrain requires technology";
        if (plot.Feature is { } f && def.FeatureImpassables.GetValueOrDefault(f)) return "Unit cannot enter feature";
        return null;
    }
    private static bool TerrainTechnology(Simulation sim, Unit unit, S.UnitDef def, string terrain) =>
        def.TerrainPassableTechs.TryGetValue(terrain, out var tech) && tech is not null && sim.KnowsTech(unit.Owner, tech);
    private static bool PeakEntry(Simulation sim, Unit unit, Ariel.Content.ModifierSink effects)
    {
        if (MilitaryData.Effect(effects, "move_peaks") > 0) return true;
        foreach (var tech in sim.TeamOf(unit.Owner).Techs)
            if (sim.Content.Get<S.TechDef>(tech).CanPassPeaks) return true;
        return false;
    }
    private static bool DomainEntry(Simulation sim, Unit unit, S.UnitDef def, bool water, TileId tile)
    {
        if (def.CanMoveAllTerrain) return true;
        return def.Domain switch {
            "military:domain_land" => !water,
            "military:domain_sea" => water || FriendlyCoastalCity(sim, unit, tile),
            "military:domain_air" => false, // Rebase/air missions need a separate, non-adjacent command.
            _ => false,
        };
    }
    private static int? AutoLoadTransport(Simulation sim, Unit unit, S.UnitDef def, TileId to)
    {
        if (def.Domain != "military:domain_land" || def.CanMoveAllTerrain || MilitaryData.Read(sim, unit).TransportId is not null) return null;
        if (!sim.Content.Get<S.TerrainDef>(sim.State.Plots[to.Value].Terrain).IsWater || !sim.State.HasUnitsAt(to)) return null;
        var target = sim.State.Grid.HexOf(to);
        return sim.State.UnitsAt(target).OrderByDescending(u => u.Owner == unit.Owner).ThenBy(u => u.Id)
            .Where(u => UnitService.LoadError(sim, unit, u, target) is null).Select(u => (int?)u.Id).FirstOrDefault();
    }
    private static bool FriendlyCoastalCity(Simulation sim, Unit unit, TileId tile)
    {
        var city = sim.State.CityAtTile(tile);
        return city is not null && MilitaryData.Friendly(sim, unit.Owner, city.Owner)
            && sim.State.Grid.Neighbors(tile).Any(n => sim.Content.Get<S.TerrainDef>(sim.State.Plots[n.Value].Terrain).IsWater
                && !MilitaryData.CrossesCliff(sim, tile, n));
    }
    private static bool TerritoryEntry(Simulation sim, Unit unit, S.UnitDef def, int? owner) => owner is null
        || MilitaryData.Friendly(sim, unit.Owner, owner) || MilitaryData.Enemy(sim, unit.Owner, owner.Value) || def.RivalTerritory || def.BaseTrade > 0 || def.TradeMultiplier > 0
        || sim.Rules().Relations.MayEnter(sim, sim.TeamOf(unit.Owner).Id, sim.TeamOf(owner.Value).Id);
    internal static bool HostilePlot(Simulation sim, Unit unit, Hex target) =>
        sim.State.Grid.TryTile(target, out var tile) && sim.State.HasUnitsAt(tile)
            && sim.State.UnitsAt(target).Any(other => MilitaryData.VisibleEnemy(sim, unit.Owner, other))
        || sim.State.CityAt(target) is { } city && MilitaryData.Enemy(sim, unit.Owner, city.Owner);
    private static bool CanAttack(Simulation sim, Unit unit, S.UnitDef def, Ariel.Content.ModifierSink effects) =>
        def.Strength > 0 && !def.OnlyDefensive && (!MilitaryData.Read(sim, unit).Attacked || MilitaryData.Effect(effects, "blitz") > 0);
    private static bool ControlledTransition(MovementQuery q, TileId from, TileId to, bool attack)
    {
        if (q.Def.HiddenNationality || q.Def.AlwaysHostile || !q.Controlled(to)) return false;
        if (attack && q.Sim.State.UnitsAt(q.Sim.State.Grid.HexOf(to)).Any(q.Controls)) return false;
        return q.InControl(from);
    }
    /// <summary>Whether entering <paramref name="to"/> can count as a move within an enemy zone of control: no friendly
    /// city stands there, and a controlling unit stands next to it.</summary>
    private static bool Controlled(MovementQuery q, TileId to)
    {
        var city = q.Sim.State.CityAt(q.Sim.State.Grid.HexOf(to));
        if (city is not null && MilitaryData.Friendly(q.Sim, q.Unit.Owner, city.Owner)) return false;
        return q.InControl(to);
    }
    /// <summary>Whether a unit that exerts a zone of control over the query's unit stands next to the tile.</summary>
    private static bool InControl(MovementQuery q, TileId tile)
    {
        var grid = q.Sim.State.Grid;
        // Most plots are empty: asking first spares enumerating an empty one.
        foreach (var n in grid.Neighbors(tile))
            if (q.Sim.State.HasUnitsAt(n))
                foreach (var other in q.Sim.State.UnitsAt(grid.HexOf(n)))
                    if (q.Controls(other)) return true;
        return false;
    }
    private static bool HasControl(Simulation sim, Unit unit, Unit other) => MilitaryData.VisibleEnemy(sim, unit.Owner, other)
        && MilitaryData.Effect(MilitaryData.Effects(sim, other), "zone_control") > 0;
    public int StepCost(Simulation sim, int unitId, TileId from, TileId to) => Query(sim, unitId).StepCost(from, to);
    private static int StepCost(MovementQuery q, TileId from, TileId to)
    {
        if (q.Def.FlatMovementCost || q.Def.Domain == "military:domain_air") return q.Denominator;
        return Math.Max(1, Math.Min(q.RegularCost(to), RouteCost(q, from, to)));
    }
    private static int RegularCost(MovementQuery q, Plot plot)
    {
        var sim = q.Sim;
        if (q.Def.IgnoreTerrainCost) return q.Denominator;
        int cost = plot.Feature is { } f ? sim.Content.Get<S.FeatureDef>(f).MovementCost : sim.Content.Get<S.TerrainDef>(plot.Terrain).MovementCost;
        if (plot.Kind == PlotKind.Hills) cost += q.HillsExtra;
        if (plot.Kind == PlotKind.Peak) cost += q.PeakExtra;
        cost = Math.Min(q.BaseMoves, Math.Max(1, cost - q.MoveDiscount)) * q.Denominator;
        if (DoubleMove(q.Promotions, plot)) cost /= 2;
        return cost;
    }
    internal static int BaseMoves(Simulation sim, Unit unit, S.UnitDef def, Ariel.Content.ModifierSink effects) => Math.Max(1,
        def.Moves + MilitaryData.Effect(effects, "moves") + MilitaryData.Techs(sim, unit.Owner).Sum(t => t.DomainExtraMoves.GetValueOrDefault(def.Domain ?? "")));
    private static bool DoubleMove(S.PromotionDef[] promotions, Plot plot)
    {
        foreach (var p in promotions)
            if (plot.Kind == PlotKind.Hills && p.HillsDoubleMove
                || (plot.Feature is { } f ? p.FeatureDoubleMoves.GetValueOrDefault(f) : p.TerrainDoubleMoves.GetValueOrDefault(plot.Terrain)))
                return true;
        return false;
    }
    private static int RouteCost(MovementQuery q, TileId from, TileId to)
    {
        var sim = q.Sim;
        var a = sim.State.Plots[from.Value]; var b = sim.State.Plots[to.Value];
        if (!ValidRoute(q, a) || !ValidRoute(q, b)) return int.MaxValue;
        if (MilitaryData.CrossesRiver(sim, from, to) && !q.BridgeBuilding) return int.MaxValue;
        var x = sim.Content.Get<S.RouteDef>(a.Route!); var y = sim.Content.Get<S.RouteDef>(b.Route!);
        int cost = Math.Max(x.MovementCost, y.MovementCost);
        int flat = Math.Max(x.FlatMovement, y.FlatMovement) * q.BaseMoves;
        return Math.Min(cost, flat);
    }
    private static bool ValidRoute(MovementQuery q, Plot plot) => plot.Route is not null
        && (plot.Owner is null || !MilitaryData.Enemy(q.Sim, q.Unit.Owner, plot.Owner.Value) || q.EnemyRoute);
    public string? ValidatePath(Simulation sim, int player, int unitId, IReadOnlyList<Hex> path)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your unit";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (path.Count == 0) return "Empty path";
        var from = sim.State.Grid.TileAt(unit.Position);
        // Validation writes nothing, so one query (one ledger of the unit) answers every step.
        var query = Query(sim, unitId);
        for (int i = 0; i < path.Count; i++)
        {
            if (!sim.State.Grid.TryTile(path[i], out var to)) return "Off map";
            var error = query.CanEnter(from, to, i == path.Count - 1);
            if (error is not null) return error;
            from = to;
        }
        return null;
    }
    public void Move(Simulation sim, int unitId, IReadOnlyList<Hex> path)
    {
        var unit = sim.State.UnitById(unitId);
        // A step that walks onto its plot ends by renewing sight and contacts. Nothing runs between that and the end
        // of the path, so renewing them again then would find exactly what that step found.
        bool sighted = false;
        foreach (var step in path)
        {
            if (unit.MovesLeft <= 0) break;
            if (MoveStep(sim, unit, sim.State.Grid.TileAt(step), out sighted)) break;
        }
        if (!sighted) MilitaryData.Sight(sim);
    }
    /// <summary>Take one step; true when the path stops here. <paramref name="sighted"/> is whether the step ended by
    /// renewing sight and contacts, so that nothing has changed since.</summary>
    private bool MoveStep(Simulation sim, Unit unit, TileId to, out bool sighted)
    {
        sighted = false;
        var from = sim.State.Grid.TileAt(unit.Position);
        var target = sim.State.Grid.HexOf(to);
        int cost = StepCost(sim, unit.Id, from, to);
        int? transport = AutoLoadTransport(sim, unit, MilitaryData.Definition(sim, unit), to);
        var state = MilitaryData.Edit(sim, unit);
        // A move command declares only units, sight and worker orders (UnitStateDomains.MoveWrites). Readers see units
        // only through blockades, so moving a blockading unit, which ends its blockade, widens first, and so does
        // every step below that writes more: a fight, a capture, a goody hut.
        if (state.Activity == UnitActivity.Blockade) sim.WidenWriteWindow();
        state.Moved = true; state.BreakActivity();
        sim.Rules().WorkerBuilds?.Cancel(sim, unit.Owner, unit.Id);
        bool hostile = HostilePlot(sim, unit, target);
        var combatService = sim.Rules().Combat;
        var defender = hostile ? combatService.BestDefender(sim, unit.Id, target) : null;
        // A hostile plot with a visible strength but no eligible defender is a wall, not a target.
        if (hostile && defender is null && HostileDefenders(sim, unit, target)) return true;
        if (defender is { } defenderUnit)
        {
            // The route was planned against the units visible when it was built, so walking a step can reveal a
            // defender the planner could not see. Re-ask the movement rules before fighting and stop the path when
            // the attack is illegal, instead of throwing from Resolve.
            if (CanEnter(sim, unit.Id, from, to, attack: true) is not null) return true;
            sim.WidenWriteWindow();
            var report = combatService.Resolve(sim, unit.Id, defenderUnit);
            if (sim.State.FindUnit(unit.Id) is null) return true;
            if (report.Outcome is not (CombatOutcome.AttackerWon or CombatOutcome.DefenderWithdrawn)) return true;
            if (HostileDefenders(sim, unit, target)) return true;
        }
        else unit.MovesLeft = Math.Max(0, unit.MovesLeft - cost);
        CaptureCivilians(sim, unit, target);
        var city = sim.State.CityAt(target);
        if (city is not null && MilitaryData.Enemy(sim, unit.Owner, city.Owner) && !MilitaryData.Definition(sim, unit).NoCapture)
        {
            sim.WidenWriteWindow();
            sim.Rules().CityCapture.Capture(sim, city.Id, unit.Owner);
        }
        MilitaryData.MoveUnit(sim, unit.Id, target);
        CompleteBoarding(sim, unit, from, to, transport);
        GoodyService.Receive(sim, unit);
        MilitaryData.Sight(sim);
        sighted = true;
        return hostile;
    }
    private static void CompleteBoarding(Simulation sim, Unit unit, TileId from, TileId to, int? transport)
    {
        if (transport is { } id)
        {
            sim.Rules().Units.Load(sim, unit.Id, id);
            unit.MovesLeft = 0;
            return;
        }
        var state = MilitaryData.Edit(sim, unit);
        if (state.TransportId is null || sim.Content.Get<S.TerrainDef>(sim.State.Plots[to.Value].Terrain).IsWater) return;
        state.TransportId = null;
        if (sim.Content.Get<S.TerrainDef>(sim.State.Plots[from.Value].Terrain).IsWater
            && MilitaryData.Effect(MilitaryData.Effects(sim, unit), "amphibious") == 0) unit.MovesLeft = 0;
    }
    /// <summary>Read-only context scoped to one path query. The unit's ledger, the per-unit step-cost inputs derived
    /// from it, its technologies and promotions, control checks, each plot's own entry (for a move that does not
    /// attack) and whether a plot lies in an enemy zone of control are reused across edges, never cached across
    /// commands or games: the state cannot change while one query runs.</summary>
    internal sealed class MovementQuery
    {
        internal readonly Simulation Sim;
        internal readonly Unit Unit;
        internal readonly S.UnitDef Def;
        internal readonly Ariel.Content.ModifierSink Effects;
        /// <summary>Whether another unit exerts a zone of control over this one, asked once per unit.</summary>
        internal readonly Func<Unit, bool> Controls;
        private readonly Dictionary<int, bool> control = new();
        private Dictionary<int, PlotFacts>? _plots;
        private readonly PlotFactTable? _table;
        private StepInputs? _step;
        internal MovementQuery(Simulation sim, Unit unit, PlotFactTable? table = null)
        {
            Sim = sim; Unit = unit; _table = table;
            Def = MilitaryData.Definition(sim, unit); Effects = MilitaryData.Effects(sim, unit);
            Controls = HasControlOver;
        }
        internal string? CanEnter(TileId from, TileId to, bool attack) => MovementService.CanEnter(this, from, to, attack);
        /// <summary>Whether <see cref="CanEnter"/> is null for <paramref name="neighbor"/>, which must be one of
        /// <paramref name="from"/>'s <see cref="HexGrid.Neighbors(TileId)"/>: the step's tiles are then on the map and
        /// adjacent, so only the cliff remains of the edge checks.</summary>
        internal bool CanStep(TileId from, TileId neighbor, bool attack)
        {
            if (!Def.CanMoveAllTerrain && Def.Domain is "military:domain_land" or "military:domain_sea"
                && MilitaryData.CrossesCliff(Sim, from, neighbor)) return false;
            return Entry(this, from, neighbor, attack) is null;
        }
        internal int StepCost(TileId from, TileId to) => MovementService.StepCost(this, from, to);
        /// <summary>What one plot answers for this unit, each part worked out on first use.</summary>
        internal struct PlotFacts
        {
            public Known Known;
            public string? Entry;
            public bool InControl, Controlled;
            public int RegularCost;
        }
        [Flags]
        internal enum Known : byte { Entry = 1, InControl = 2, Controlled = 4, RegularCost = 8 }
        private PlotFacts Facts(TileId tile) => _table is not null ? _table.Get(tile.Value)
            : _plots is not null && _plots.TryGetValue(tile.Value, out var facts) ? facts : default;
        private void Store(TileId tile, PlotFacts facts)
        {
            if (_table is not null) _table.Set(tile.Value, facts);
            else (_plots ??= new())[tile.Value] = facts;
        }
        /// <summary>The plot's own entry for a move that does not attack (<see cref="MovementService.PlotEntry"/>).</summary>
        internal string? PlotEntry(TileId to)
        {
            var facts = Facts(to);
            if ((facts.Known & Known.Entry) != 0) return facts.Entry;
            facts.Entry = MovementService.PlotEntry(this, to, attack: false);
            facts.Known |= Known.Entry;
            Store(to, facts);
            return facts.Entry;
        }
        /// <summary>Whether a unit that controls this one stands next to the tile.</summary>
        internal bool InControl(TileId tile)
        {
            var facts = Facts(tile);
            if ((facts.Known & Known.InControl) != 0) return facts.InControl;
            bool value = MovementService.InControl(this, tile);
            facts = Facts(tile);
            facts.InControl = value;
            facts.Known |= Known.InControl;
            Store(tile, facts);
            return value;
        }
        /// <summary><see cref="MovementService.Controlled"/>, once per plot.</summary>
        internal bool Controlled(TileId to)
        {
            var facts = Facts(to);
            if ((facts.Known & Known.Controlled) != 0) return facts.Controlled;
            bool value = MovementService.Controlled(this, to);
            facts = Facts(to);
            facts.Controlled = value;
            facts.Known |= Known.Controlled;
            Store(to, facts);
            return value;
        }
        /// <summary>The cost of entering the plot off any route (<see cref="MovementService.RegularCost"/>).</summary>
        internal int RegularCost(TileId to)
        {
            var facts = Facts(to);
            if ((facts.Known & Known.RegularCost) != 0) return facts.RegularCost;
            facts.RegularCost = MovementService.RegularCost(this, Sim.State.Plots[to.Value]);
            facts.Known |= Known.RegularCost;
            Store(to, facts);
            return facts.RegularCost;
        }
        private StepInputs Step => _step ??= new(Sim, Unit, Def, Effects);
        internal int Denominator => Step.Denominator;
        internal int BaseMoves => Step.BaseMoves;
        internal int MoveDiscount => Step.MoveDiscount;
        internal int HillsExtra => Step.HillsExtra;
        internal int PeakExtra => Step.PeakExtra;
        internal bool EnemyRoute => Step.EnemyRoute;
        internal bool BridgeBuilding => Step.BridgeBuilding;
        internal S.PromotionDef[] Promotions => Step.Promotions;
        private bool HasControlOver(Unit other)
        {
            if (!control.TryGetValue(other.Id, out var value)) control[other.Id] = value = HasControl(Sim, Unit, other);
            return value;
        }
        /// <summary>What every edge's cost reads that depends only on the unit, its owner and the rules.</summary>
        private sealed class StepInputs(Simulation sim, Unit unit, S.UnitDef def, Ariel.Content.ModifierSink effects)
        {
            public readonly int Denominator = MilitaryData.Constants(sim).MoveDenominator;
            public readonly int BaseMoves = MovementService.BaseMoves(sim, unit, def, effects);
            public readonly int MoveDiscount = MilitaryData.Effect(effects, "move_discount");
            public readonly int HillsExtra = MilitaryData.Raw(sim, "HILLS_EXTRA_MOVEMENT", 1);
            public readonly int PeakExtra = MilitaryData.Raw(sim, "PEAK_EXTRA_MOVEMENT", 1);
            public readonly bool EnemyRoute = MilitaryData.Effect(effects, "enemy_route") > 0;
            public readonly bool BridgeBuilding = MilitaryData.Techs(sim, unit.Owner).Any(t => t.BridgeBuilding);
            public readonly S.PromotionDef[] Promotions = MilitaryData.Promotions(sim, unit).ToArray();
        }
    }

    private static bool HostileDefenders(Simulation sim, Unit unit, Hex target) => sim.State.UnitsAt(target)
        .Any(u => MilitaryData.VisibleEnemy(sim, unit.Owner, u) && MilitaryData.Definition(sim, u).Strength > 0);
    private static void CaptureCivilians(Simulation sim, Unit unit, Hex target)
    {
        var captured = sim.State.UnitsAt(target).Where(u => MilitaryData.VisibleEnemy(sim, unit.Owner, u)).ToArray();
        if (captured.Length > 0) sim.WidenWriteWindow();
        foreach (var other in captured)
        {
            var def = MilitaryData.Definition(sim, other);
            MilitaryData.RemoveUnit(sim, other.Id);
            // A captured unit changes hands spent.
            if (def.Capture is { } type && !MilitaryData.Definition(sim, unit).NoCapture)
                sim.Rules().Units.Create(sim, unit.Owner, type, target, captured => captured.MovesLeft = 0);
        }
    }
}

/// <summary>Per-plot movement facts for one query at a time, by tile index: an entry counts only when its stamp is the
/// current use's, so <see cref="Begin"/> clears nothing. Map-sized and reused (a <see cref="PathSearch"/> keeps one per
/// thread), never shared by two queries at once.</summary>
internal sealed class PlotFactTable
{
    private MovementService.MovementQuery.PlotFacts[] _facts = [];
    private int[] _stamp = [];
    private int _generation;

    /// <summary>Start a new use over a map of <paramref name="tiles"/> plots, forgetting every earlier entry.</summary>
    public void Begin(int tiles)
    {
        if (_stamp.Length != tiles) { _facts = new MovementService.MovementQuery.PlotFacts[tiles]; _stamp = new int[tiles]; _generation = 0; }
        if (_generation == int.MaxValue) { Array.Clear(_stamp); _generation = 0; }
        _generation++;
    }

    public MovementService.MovementQuery.PlotFacts Get(int tile) => _stamp[tile] == _generation ? _facts[tile] : default;

    public void Set(int tile, MovementService.MovementQuery.PlotFacts facts)
    {
        _stamp[tile] = _generation;
        _facts[tile] = facts;
    }
}
