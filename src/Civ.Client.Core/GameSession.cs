using Civ.BaseGame;
using Civ.Game;
using Civ.Modding;

namespace Civ.Client;

/// <summary>
/// Everything a front end needs that is not drawing: the simulation, who the local player is, what is selected, and the
/// player's actions expressed as commands. It has no engine types, so it is shared by any renderer and unit tested.
/// For multiplayer, <see cref="Submit"/> is the one place to route commands into a LockstepSession instead.
/// </summary>
public sealed class GameSession
{
    public const int Human = 0;

    public Simulation Sim { get; }
    public string Message { get; set; } = "";
    public int? SelectedUnitId { get; set; }
    public int? SelectedCityId { get; set; }

    public GameSession(Simulation sim) { Sim = sim; }

    /// <summary>The standard hot-seat-vs-AI game: the base mod plus any extra mods, one human and two AI civilizations.</summary>
    public static GameSession Start(IEnumerable<ModSource> extraMods, int mapRadius = 12, ulong seed = 1)
    {
        var baseMod = ModLoader.FromAssembly(
            new ModManifest { Id = "base", Assembly = "Civ.BaseGame.dll" },
            typeof(BaseGameMod).Assembly, AppContext.BaseDirectory);
        var content = ModLoader.Build(extraMods.Prepend(baseMod));
        var sim = new Simulation(content, new SimulationSettings
        {
            Seed = seed, MapRadius = mapRadius, PlayerCount = 3,
            PlayerAis = [null, "base:basic", "base:basic"],
        });
        Sites.PlaceStartingUnits(sim, Human);
        var session = new GameSession(sim);
        session.SelectedUnitId = sim.State.Units.FirstOrDefault(u => u.Owner == Human)?.Id;
        return session;
    }

    public Player Me => Sim.State.PlayerById(Human);
    public City? City => SelectedCityId is int id ? Sim.State.Cities.FirstOrDefault(c => c.Id == id) : null;
    public Unit? Unit => SelectedUnitId is int id ? Sim.State.Units.FirstOrDefault(u => u.Id == id) : null;

    /// <summary>Changes whenever anything a UI shows could have changed, so front ends can rebuild only when needed.</summary>
    public ulong Version => Sim.ComputeHash() ^ ((ulong)(SelectedUnitId ?? -1) * 0x9E3779B97F4A7C15UL) ^ ((ulong)(SelectedCityId ?? -1) * 0xC2B2AE3D27D4EB4FUL)
        ^ (ulong)Message.GetHashCode();

    public void Submit(GameCommand command)
    {
        Message = Sim.TryApply(command, out var error) ? "" : $"Can't do that: {error}";
        if (City == null) SelectedCityId = null;
        if (Unit == null) SelectedUnitId = null;
    }

    public void EndTurn() => Submit(new EndTurnCommand { Player = Human });

    // --- selection --------------------------------------------------------------------------

    /// <summary>Select my unit or city on this tile; clicking again cycles through the stack (units, then the city). Empty tile clears.</summary>
    public void SelectAt(Hex hex)
    {
        var stack = new List<(bool IsCity, int Id)>();
        stack.AddRange(Sim.State.Units.Where(u => u.Owner == Human && u.Position == hex).OrderBy(u => u.Id).Select(u => (false, u.Id)));
        if (Sim.State.Cities.FirstOrDefault(c => c.Position == hex && c.Owner == Human) is { } here) stack.Add((true, here.Id));
        if (stack.Count == 0) { SelectedCityId = null; SelectedUnitId = null; return; }

        int current = stack.FindIndex(e => e.IsCity ? e.Id == SelectedCityId : e.Id == SelectedUnitId);
        var pick = stack[(current + 1) % stack.Count];
        SelectedCityId = pick.IsCity ? pick.Id : null;
        SelectedUnitId = pick.IsCity ? null : pick.Id;
    }

    public void ClearSelection() { SelectedCityId = null; SelectedUnitId = null; }

    // --- map clicks ----------------------------------------------------------------------

    /// <summary>Left click: with a city selected, a workable tile toggles its citizen; otherwise select.</summary>
    public void LeftClick(Hex hex)
    {
        var city = City;
        if (city != null && hex != city.Position && CityRules.WorkableTiles(Sim, city).Contains(hex))
            ToggleTile(city, hex);
        else
            SelectAt(hex);
    }

    /// <summary>Right click: attack an adjacent enemy, otherwise walk the selected unit toward the tile (greedy, no pathfinding yet).</summary>
    public void RightClick(Hex hex)
    {
        var unit = Unit;
        if (unit == null) return;
        if (unit.Position.DistanceTo(hex) == 1 && Sim.State.Units.Any(u => u.Owner != Human && u.Position == hex))
        {
            Submit(new AttackCommand { Player = Human, UnitId = unit.Id, Target = hex });
            return;
        }
        for (int step = 0; step < 12 && unit.Position != hex && unit.MovesLeft > 0; step++)
        {
            Hex? best = unit.Position.Neighbors()
                .Where(n => new MoveUnitCommand { Player = Human, UnitId = unit.Id, To = n }.Validate(Sim) == null)
                .OrderBy(n => n.DistanceTo(hex)).Cast<Hex?>().FirstOrDefault();
            if (best == null || best.Value.DistanceTo(hex) >= unit.Position.DistanceTo(hex)) break;
            Submit(new MoveUnitCommand { Player = Human, UnitId = unit.Id, To = best.Value });
        }
    }

    public TileMark MarkOf(Hex hex)
    {
        if (City is { } city)
        {
            if (city.Worked.Contains(hex)) return TileMark.Worked;
            if (CityRules.WorkableTiles(Sim, city).Contains(hex)) return TileMark.Workable;
            if (hex == city.Position) return TileMark.Selected;
        }
        if (Unit is { } unit && unit.Position == hex) return TileMark.Selected;
        return TileMark.None;
    }

    // --- city management -------------------------------------------------------------------

    /// <summary>Unassign a worked tile (the citizen becomes a plain specialist) or put a citizen on a free tile, taking one from the plain pool, then any specialist, then the least valuable worked tile.</summary>
    public void ToggleTile(City city, Hex hex)
    {
        var tiles = city.Worked.ToList();
        var spec = new Dictionary<string, int>(city.Specialists);
        var citizen = CityRules.DefaultSpecialist(Sim);
        if (tiles.Remove(hex))
        {
            if (citizen == null) { Message = "Can't do that: no default citizen specialist is registered"; return; }
            spec[citizen.Id] = spec.GetValueOrDefault(citizen.Id) + 1;
        }
        else
        {
            var from = citizen != null && spec.GetValueOrDefault(citizen.Id) > 0 ? citizen.Id
                : spec.Where(kv => kv.Value > 0).Select(kv => kv.Key).FirstOrDefault();
            if (from != null)
            {
                if (--spec[from] <= 0) spec.Remove(from);
            }
            else if (tiles.Count > 0)
            {
                tiles.Remove(tiles.OrderBy(t => CityRules.TileScore(Sim, t)).ThenBy(t => t.Q).ThenBy(t => t.R).First());
            }
            else { Message = "No citizen to assign"; return; }
            tiles.Add(hex);
        }
        Submit(new SetCityWorkCommand { Player = Human, CityId = city.Id, Tiles = tiles.ToArray(), Specialists = spec });
    }

    /// <summary>Move one citizen into (+1) or out of (-1) a specialist role; out goes back to the plain citizen pool.</summary>
    public void ChangeSpecialist(City city, string id, int delta)
    {
        var tiles = city.Worked.ToList();
        var spec = new Dictionary<string, int>(city.Specialists);
        var citizen = CityRules.DefaultSpecialist(Sim);
        if (citizen == null) return;

        if (delta > 0)
        {
            if (spec.GetValueOrDefault(citizen.Id) > 0 && id != citizen.Id) { if (--spec[citizen.Id] <= 0) spec.Remove(citizen.Id); }
            else if (tiles.Count > 0)
                tiles.Remove(tiles.OrderBy(t => CityRules.TileScore(Sim, t)).ThenBy(t => t.Q).ThenBy(t => t.R).First());
            else { Message = "No citizen available to reassign"; return; }
            spec[id] = spec.GetValueOrDefault(id) + 1;
        }
        else
        {
            if (spec.GetValueOrDefault(id) <= 0 || id == citizen.Id) return;
            if (--spec[id] <= 0) spec.Remove(id);
            spec[citizen.Id] = spec.GetValueOrDefault(citizen.Id) + 1;
        }
        Submit(new SetCityWorkCommand { Player = Human, CityId = city.Id, Tiles = tiles.ToArray(), Specialists = spec });
    }
}

public enum TileMark { None, Worked, Workable, Selected }
