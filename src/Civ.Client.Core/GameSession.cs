using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>
/// Everything a front end needs that is not drawing: the simulation, who the local player is, what is selected, and the
/// player's actions expressed as commands. It has no engine types, so it is shared by any renderer and unit tested.
/// For multiplayer, <see cref="Submit"/> is the one place to route commands into a LockstepSession instead.
/// </summary>
public sealed partial class GameSession
{
    public const int Human = 0;

    private readonly Dictionary<int, Hex> _gotos = new();

    public Simulation Sim { get; }
    public string Message { get; private set; } = "";
    public int? SelectedUnitId { get; private set; }
    public int? SelectedCityId { get; private set; }
    /// <summary>Counts every command and selection change so front ends rebuild only when something changed.</summary>
    public int Revision { get; private set; }
    /// <summary>Counts only changes to the simulation, for the map to rebuild its meshes.</summary>
    public int WorldRevision { get; private set; }

    public GameSession(Simulation sim)
    {
        Sim = sim;
        SelectNextUnit();
    }

    /// <summary>Create a new game from the setup screen's choices.</summary>
    public static GameSession Start(GameCatalog catalog, NewGameOptions options) =>
        new(new Simulation(catalog.Content, options.ToSettings(catalog)));

    public Player Me => Sim.State.PlayerById(Human);
    public Team MyTeam => Sim.TeamOf(Human);
    public City? City => SelectedCityId is int id ? Sim.State.FindCity(id) : null;
    public Unit? Unit => SelectedUnitId is int id ? Sim.State.FindUnit(id) : null;

    internal T Service<T>() where T : class => Sim.Content.Services.Get<T>().Single();
    internal T? OptionalService<T>() where T : class => Sim.Content.Services.Get<T>().SingleOrDefault();
    internal int MoveDenominator => Math.Max(1, Sim.Content.Get<Schema.RuleConstantsDef>().All.Single().Values.MoveDenominator);

    public void SetMessage(string message) { Message = message; Revision++; }

    /// <summary>Apply a command for the local player; a rejection becomes <see cref="Message"/>.</summary>
    public bool Submit(GameCommand command)
    {
        bool ok = Sim.TryApply(command, out var error);
        Message = ok ? "" : $"Can't do that: {error}";
        Revision++;
        if (ok) WorldRevision++;
        if (City == null) SelectedCityId = null;
        if (Unit == null) SelectedUnitId = null;
        return ok;
    }

    // --- turns ------------------------------------------------------------------------------

    /// <summary>End the turn. Opponents without an AI service have nothing to do, so they end theirs first; the human ends last.</summary>
    public void EndTurn()
    {
        foreach (var player in Sim.State.Players.Where(p => p.Id != Human && p.Ai == null && !p.EndedTurn).ToList())
            Sim.TryApply(new EndTurnCommand { Player = player.Id }, out _);
        int turn = Sim.State.Turn;
        Submit(new EndTurnCommand { Player = Human });
        if (Sim.State.Turn == turn) return;
        ContinueOrders();
        SelectNextUnit();
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
        Revision++;
        WorldRevision++;
    }

    // --- selection --------------------------------------------------------------------------

    /// <summary>Select my unit or city on this tile; clicking again cycles through the stack (units, then the city). Empty tile clears.</summary>
    public void SelectAt(Hex hex)
    {
        var stack = new List<(bool IsCity, int Id)>();
        stack.AddRange(Sim.State.UnitsAt(hex).Where(u => u.Owner == Human).OrderBy(u => u.Id).Select(u => (false, u.Id)));
        if (Sim.State.CityAt(hex) is { Owner: Human } here) stack.Add((true, here.Id));
        if (stack.Count == 0) { ClearSelection(); return; }

        int current = stack.FindIndex(e => e.IsCity ? e.Id == SelectedCityId : e.Id == SelectedUnitId);
        var pick = stack[(current + 1) % stack.Count];
        SelectedCityId = pick.IsCity ? pick.Id : null;
        SelectedUnitId = pick.IsCity ? null : pick.Id;
        Revision++;
    }

    public void SelectUnit(int id)
    {
        if (Sim.State.FindUnit(id)?.Owner != Human) return;
        SelectedUnitId = id; SelectedCityId = null; Revision++;
    }

    public void SelectCity(int id)
    {
        if (Sim.State.FindCity(id)?.Owner != Human) return;
        SelectedCityId = id; SelectedUnitId = null; Revision++;
    }

    public void ClearSelection() { SelectedCityId = null; SelectedUnitId = null; Revision++; }

    /// <summary>Select the next of my units that still has moves and is awake; clears the selection when none is left.</summary>
    public void SelectNextUnit()
    {
        var waiting = Sim.State.Units.Where(u => u.Owner == Human && NeedsOrders(u)).OrderBy(u => u.Id).ToList();
        var next = waiting.FirstOrDefault(u => u.Id > (SelectedUnitId ?? 0)) ?? waiting.FirstOrDefault();
        SelectedCityId = null;
        SelectedUnitId = next?.Id;
        Revision++;
    }

    private bool NeedsOrders(Unit unit) =>
        unit.MovesLeft > 0 && !_gotos.ContainsKey(unit.Id) && ActivityOf(unit) == UnitActivity.Awake;

    public UnitActivity ActivityOf(Unit unit) => unit.Extensions.Get<MilitaryUnitState>(Sim.Content)?.Activity ?? UnitActivity.Awake;

    /// <summary>Units whose turn is not finished; the end-turn button warns when this is not zero.</summary>
    public int UnitsNeedingOrders => Sim.State.Units.Count(u => u.Owner == Human && NeedsOrders(u));

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
        if (!Submit(new MoveUnitPathCommand { Player = Human, Unit = unit.Id, Path = preview.Path.ToArray() }) || unit.Position == hex) _gotos.Remove(unit.Id);
    }

    private bool TryWalk(Unit unit, Hex destination)
    {
        var path = Service<IPathfindingService>().FindPath(Sim, unit.Id, destination);
        if (path == null || path.Steps.Count == 0) return false;
        return Sim.TryApply(new MoveUnitPathCommand { Player = Human, Unit = unit.Id, Path = path.Steps.ToArray() }, out _);
    }

    /// <summary>Whether a unit has a standing go-to order.</summary>
    public bool HasGoto(int unitId) => _gotos.ContainsKey(unitId);
}
