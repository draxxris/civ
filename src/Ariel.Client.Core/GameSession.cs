using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>
/// Everything a front end needs that is not drawing: the simulation, who the local player is, what is selected, and the
/// player's actions expressed as commands. It has no engine types, so it is shared by any renderer and unit tested.
/// Every change to the simulation goes through its <see cref="ICommandChannel"/>, so the same session drives a local or
/// a networked game; whatever the channel applies, from any player, reaches <see cref="SimulationChanged"/>.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The seat a single-player game gives its human.</summary>
    public const int DefaultLocalPlayer = 0;

    private readonly ICommandChannel _channel;
    private readonly Dictionary<int, Hex> _gotos = new();
    private int _ordersTurn;   // the turn whose standing orders already continued
    private Simulation? _espionageBudgetSim;
    private int _espionageBudgetRevision = -1;
    private long _espionageBudget;

    public Simulation Sim { get; private set; }
    /// <summary>The player this front end controls.</summary>
    public int LocalPlayer { get; }
    public string Message { get; private set; } = "";
    public int? SelectedUnitId { get; private set; }
    public int? SelectedCityId { get; private set; }
    /// <summary>Counts every command and selection change so front ends rebuild only when something changed.</summary>
    public int Revision { get; private set; }
    /// <summary>Counts only changes to the simulation, for the map to rebuild its meshes.</summary>
    public int WorldRevision { get; private set; }

    /// <param name="backgroundTurns">See <see cref="BackgroundTurns"/>.</param>
    public GameSession(Simulation sim, int localPlayer = DefaultLocalPlayer, ICommandChannel? channel = null, bool backgroundTurns = false)
    {
        if (sim.State.Players.All(p => p.Id != localPlayer)) throw new ArgumentException($"No player {localPlayer} to control", nameof(localPlayer));
        Sim = sim;
        LocalPlayer = localPlayer;
        _channel = channel ?? LocalCommandChannel.Instance;
        BackgroundTurns = backgroundTurns;
        sim.DeferTurnWork = backgroundTurns;
        _ordersTurn = sim.State.Turn;
        ResetNotifications();
        SelectNextUnit();
        StartTurnWork();
    }

    /// <summary>Create a new game from the setup screen's choices.</summary>
    public static GameSession Start(GameCatalog catalog, NewGameOptions options, bool backgroundTurns = false) =>
        new(new Simulation(catalog.Content, options.ToSettings(catalog)), backgroundTurns: backgroundTurns);

    public Player Me => Sim.State.PlayerById(LocalPlayer);
    /// <summary>What has gone wrong with a networked game's connection, or null.</summary>
    public string? ConnectionProblem => _channel.Problem;
    /// <summary>Whether the local player's segment of the turn is moving, so its commands are accepted.</summary>
    public bool IsMyMove => Sim.MayAct(LocalPlayer);
    public Team MyTeam => Sim.TeamOf(LocalPlayer);
    public City? City => SelectedCityId is int id ? Sim.State.FindCity(id) : null;
    public Unit? Unit => SelectedUnitId is int id ? Sim.State.FindUnit(id) : null;

    internal T Service<T>() where T : class => Sim.Content.Services.Get<T>().Single();
    internal T? OptionalService<T>() where T : class => Sim.Content.Services.Get<T>().SingleOrDefault();
    internal int MoveDenominator => Math.Max(1, Sim.Content.Get<Schema.RuleConstantsDef>().All.Single().Values.MoveDenominator);

    /// <summary>Publish a message; an unchanged message is not a change, so it does not bump the revision.</summary>
    public void SetMessage(string message)
    {
        if (Message == message) return;
        Message = message;
        Revision++;
    }

    /// <summary>Issue a command for the local player; a rejection becomes <see cref="Message"/>. True means the
    /// channel accepted it, which in a networked game is before it applies.</summary>
    public bool Submit(GameCommand command)
    {
        if (TurnInProgress)
        {
            SetMessage("Wait for the other players to finish moving.");
            return false;
        }
        var result = _channel.Submit(Sim, command);
        Message = result.Error is null ? "" : $"Can't do that: {result.Error}";
        Revision++;
        if (result.Applied) SimulationChanged();
        else CollectNotifications();
        return result.Error is null;
    }

    /// <summary>Apply whatever the channel received since the last call. Front ends call this every frame; with nothing
    /// new it returns at once.</summary>
    public void Pump()
    {
        bool changed = FinishTurnWork();
        if (_channel.Pump(Sim)) changed = true;
        if (!changed) return;
        Revision++;
        SimulationChanged();
    }

    /// <summary>React to applied commands, whoever issued them: read new events, drop what no longer exists, and pick
    /// up standing orders once the local player's segment of a new turn is moving.</summary>
    private void SimulationChanged()
    {
        WorldRevision++;
        CollectNotifications();
        if (City == null) SelectedCityId = null;
        if (Unit == null) SelectedUnitId = null;
        foreach (var (id, destination) in _gotos)
            if (Sim.State.FindUnit(id) is not { } unit || unit.Position == destination) _gotos.Remove(id);
        if (StartTurnWork()) return;
        if (Sim.State.Turn == _ordersTurn || !IsMyMove) return;
        _ordersTurn = Sim.State.Turn;
        ContinueOrders();
        SelectNextUnit();
    }

    // --- turns ------------------------------------------------------------------------------

    /// <summary>End the turn. In a local game, other humans have nobody at the controls, so they end theirs first and
    /// the local player ends last. The turn advances once every player has ended, which may be another peer's command.</summary>
    public void EndTurn()
    {
        if (TurnInProgress)
        {
            SetMessage("Wait for the other players to finish moving.");
            return;
        }
        int turn = Sim.State.Turn;
        if (_channel.PlaysAbsentHumans && EndAbsentHumanTurns()
            && (Sim.HasPendingTurnWork || Sim.State.Turn != turn || !Sim.MayAct(LocalPlayer)))
        {
            // The local player's segment was already over: the absent humans' end turns finished the part of the turn
            // it was waiting on. Start what follows (other players' moves, the rollover) instead of ending the local
            // player's turn, which would be refused, or would end the next turn it has not played yet.
            Revision++;
            SimulationChanged();
            return;
        }
        Submit(new EndTurnCommand { Player = LocalPlayer });
    }

    /// <summary>End the turn of every other human seat whose segment is moving. Returns whether any applied.</summary>
    private bool EndAbsentHumanTurns()
    {
        bool applied = false;
        foreach (var player in Sim.State.Players.Where(p => p.Id != LocalPlayer && p.Ai == null && !p.EndedTurn).ToList())
            if (_channel.Submit(Sim, new EndTurnCommand { Player = player.Id }).Applied) applied = true;
        return applied;
    }

    /// <summary>The human players the game is waiting on, by name, once the local player cannot move: its turn is
    /// over or another segment is moving. Null while the local player still has its move. Empty when only AI or the
    /// rules remain, as while <see cref="TurnInProgress"/>.</summary>
    public string? WaitingFor()
    {
        if (TurnInProgress) return "";
        if (IsMyMove && !Me.EndedTurn) return null;
        return string.Join(", ", Sim.State.Players
            .Where(p => p.Id != LocalPlayer && p.Ai == null && !p.EndedTurn && Sim.MayAct(p.Id))
            .OrderBy(p => p.Id).Select(p => p.Name));
    }

    /// <summary>Re-issue standing go-to orders at the start of a turn.</summary>
    private void ContinueOrders()
    {
        foreach (var (id, destination) in _gotos.OrderBy(g => g.Key).ToList())
        {
            var unit = Sim.State.FindUnit(id);
            if (unit == null || unit.Position == destination) { _gotos.Remove(id); continue; }
            if (!TryWalk(unit, destination)) _gotos.Remove(id);
        }
        CollectNotifications(); // Walks bypass Submit, so collect the fights and events they published.
        Revision++;
        WorldRevision++;
    }

    // --- selection --------------------------------------------------------------------------

    /// <summary>Select my unit or city on this tile; clicking again cycles through the stack (units, then the city). Empty tile clears.</summary>
    public void SelectAt(Hex hex)
    {
        var stack = new List<(bool IsCity, int Id)>();
        stack.AddRange(Sim.State.UnitsAt(hex).Where(u => u.Owner == LocalPlayer).OrderBy(u => u.Id).Select(u => (false, u.Id)));
        if (Sim.State.CityAt(hex) is { } here && here.Owner == LocalPlayer) stack.Add((true, here.Id));
        if (stack.Count == 0) { ClearSelection(); return; }

        int current = stack.FindIndex(e => e.IsCity ? e.Id == SelectedCityId : e.Id == SelectedUnitId);
        var pick = stack[(current + 1) % stack.Count];
        int? unitId = pick.IsCity ? null : pick.Id;
        int? cityId = pick.IsCity ? pick.Id : null;
        if (unitId == SelectedUnitId && cityId == SelectedCityId) return;
        SelectedCityId = cityId;
        SelectedUnitId = unitId;
        Revision++;
    }

    public void SelectUnit(int id)
    {
        if (Sim.State.FindUnit(id)?.Owner != LocalPlayer) return;
        if (id == SelectedUnitId && SelectedCityId == null) return;
        SelectedUnitId = id; SelectedCityId = null; Revision++;
    }

    public void SelectCity(int id)
    {
        if (Sim.State.FindCity(id)?.Owner != LocalPlayer) return;
        if (id == SelectedCityId && SelectedUnitId == null) return;
        SelectedCityId = id; SelectedUnitId = null; Revision++;
    }

    public void ClearSelection()
    {
        if (SelectedCityId == null && SelectedUnitId == null) return;
        SelectedCityId = null;
        SelectedUnitId = null;
        Revision++;
    }

    /// <summary>Select the next of my units that still has moves and is awake; clears the selection when none is left.</summary>
    public void SelectNextUnit()
    {
        var waiting = Sim.State.Units.Where(u => u.Owner == LocalPlayer && NeedsOrders(u)).OrderBy(u => u.Id).ToList();
        var next = waiting.FirstOrDefault(u => u.Id > (SelectedUnitId ?? 0)) ?? waiting.FirstOrDefault();
        if (next?.Id == SelectedUnitId && SelectedCityId == null) return;
        SelectedCityId = null;
        SelectedUnitId = next?.Id;
        Revision++;
    }

    private bool NeedsOrders(Unit unit) =>
        unit.MovesLeft > 0 && !_gotos.ContainsKey(unit.Id) && ActivityOf(unit) == UnitActivity.Awake;

    public UnitActivity ActivityOf(Unit unit) => unit.Extensions.Get<MilitaryUnitState>(Sim.Content)?.Activity ?? UnitActivity.Awake;

    /// <summary>Units whose turn is not finished; the end-turn button warns when this is not zero.</summary>
    public int UnitsNeedingOrders => Sim.State.Units.Count(u => u.Owner == LocalPlayer && NeedsOrders(u));

    // --- map clicks ---------------------------------------------------------------------

    /// <summary>Left click: with a city selected, a workable tile toggles its citizen; otherwise select.</summary>
    public void LeftClick(Hex hex)
    {
        if (!Sim.State.Grid.TryTile(hex, out _)) return;
        hex = Sim.State.Grid.Normalize(hex);
        var city = City;
        if (city != null && hex != city.Position && WorkableTiles(city).Contains(Sim.State.Grid.Normalize(hex)))
            ToggleTile(city, hex);
        else
            SelectAt(hex);
    }

    /// <summary>Right click: attack an enemy, otherwise walk the selected unit there and keep going on later turns.</summary>
    public void RightClick(Hex hex)
    {
        var unit = Unit;
        if (unit == null || !Sim.State.Grid.TryTile(hex, out _)) return;
        hex = Sim.State.Grid.Normalize(hex);
        if (hex == unit.Position) return;
        var preview = PreviewAt(hex);
        if (preview == null) { SetMessage("No path there"); return; }
        if (preview.Attack != null) _gotos.Remove(unit.Id);
        else _gotos[unit.Id] = hex;
        // Arrival clears the order once the walk applies; see SimulationChanged.
        if (!Submit(new MoveUnitPathCommand { Player = LocalPlayer, Unit = unit.Id, Path = preview.Path.ToArray() })) _gotos.Remove(unit.Id);
    }

    private bool TryWalk(Unit unit, Hex destination)
    {
        var path = Service<IPathfindingService>().FindPath(Sim, unit.Id, destination);
        if (path == null || path.Steps.Count == 0) return false;
        return _channel.Submit(Sim, new MoveUnitPathCommand { Player = LocalPlayer, Unit = unit.Id, Path = path.Steps.ToArray() }).Error is null;
    }

    /// <summary>Whether a unit has a standing go-to order.</summary>
    public bool HasGoto(int unitId) => _gotos.ContainsKey(unitId);
}
