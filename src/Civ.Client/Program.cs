using Civ.BaseGame;
using Civ.Game;
using Civ.Modding;
using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Civ.Client;

internal static class Program
{
    private static void Main() => new CivGame().Run("Civ", 1280, 720);
}

/// <summary>
/// Single-player (hot-seat vs AI) front end over the headless simulation. It reads state to draw it and changes the game
/// only by submitting commands. For multiplayer, <see cref="Submit"/> would hand the command to a LockstepSession instead.
/// </summary>
public sealed class CivGame : Prowl.Runtime.Game
{
    public const int Human = 0;

    private Simulation _sim = null!;
    private WorldView _view = null!;
    private GameUi _ui = null!;
    private Camera _camera = null!;
    private GameObject _cameraObject = null!;

    private Float3 _focus = new(0, 0, 0);
    private float _distance = 16f;

    public Simulation Sim => _sim;
    public WorldView View => _view;
    public string Message { get; set; } = "";

    public int? SelectedUnit { get; set; }
    public int? SelectedCity { get; set; }

    public override void Initialize()
    {
        // The base game is loaded exactly like any other mod. Extra mods would be added here from a mods/ folder.
        var baseMod = ModLoader.FromAssembly(
            new ModManifest { Id = "base", Assembly = "Civ.BaseGame.dll" },
            typeof(BaseGameMod).Assembly, AppContext.BaseDirectory);
        var content = ModLoader.Build([baseMod]);
        _sim = new Simulation(content, new SimulationSettings
        {
            Seed = 1, MapRadius = 12, PlayerCount = 3,
            PlayerAis = [null, "base:basic", "base:basic"],
        });
        Sites.PlaceStartingUnits(_sim, Human);

        var scene = new Scene();

        var light = new GameObject("Sun");
        light.AddComponent<DirectionalLight>().Intensity = 0.9f;
        light.Transform.LocalEulerAngles = new Float3(-60, 20, 0);
        scene.Add(light);

        _cameraObject = new GameObject("Camera") { Tag = "Main Camera" };
        _camera = _cameraObject.AddComponent<Camera>();
        _camera.HDR = true;
        _camera.Effects = [new FXAAEffect(), new TonemapperEffect()];
        scene.Add(_cameraObject);

        _view = new WorldView(_sim, scene);
        _ui = new GameUi(this);

        var start = _sim.State.Units.FirstOrDefault(u => u.Owner == Human);
        if (start != null)
        {
            _focus = WorldView.WorldOf(start.Position);
            SelectedUnit = start.Id;
        }
        UpdateCamera();
        Scene.Load(scene);
    }

    public override void OnUpdate(Scene? scene)
    {
        HandleCamera();
        HandleKeys();
        HandleMouse();
        Prune();

        _view.Sync(MarkOf, SelectedUnit, SelectedCity);
    }

    public override void BeginGui(Paper paper) => _ui.Build(paper);

    public void Submit(GameCommand command)
    {
        // Stamped here so UI code can't act for another player.
        if (!_sim.TryApply(command, out var error)) Message = $"Can't do that: {error}";
        else Message = "";
    }

    // --- Selection and marks ---------------------------------------------------------------

    public City? City => SelectedCity is int id ? _sim.State.Cities.FirstOrDefault(c => c.Id == id) : null;
    public Unit? Unit => SelectedUnit is int id ? _sim.State.Units.FirstOrDefault(u => u.Id == id) : null;

    private void Prune()
    {
        if (City == null) SelectedCity = null;
        if (Unit == null) SelectedUnit = null;
    }

    private TileMark MarkOf(Hex hex)
    {
        var city = City;
        if (city != null)
        {
            if (city.Worked.Contains(hex)) return TileMark.Worked;
            if (CityRules.WorkableTiles(_sim, city).Contains(hex)) return TileMark.Workable;
            if (hex == city.Position) return TileMark.Selected;
        }
        var unit = Unit;
        if (unit != null && unit.Position == hex) return TileMark.Selected;
        return TileMark.None;
    }

    // --- Input ----------------------------------------------------------------------------

    private void HandleCamera()
    {
        float speed = _distance * 0.8f * Time.UnscaledDeltaTime;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Up)) _focus += new Float3(0, 0, speed);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Down)) _focus += new Float3(0, 0, -speed);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Left)) _focus += new Float3(-speed, 0, 0);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.Right)) _focus += new Float3(speed, 0, 0);
        _distance = Math.Clamp(_distance - Input.MouseWheelDelta * 1.5f, 6f, 40f);
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        const float pitch = 60f;
        float height = _distance * MathF.Sin(pitch * MathF.PI / 180f);
        float back = _distance * MathF.Cos(pitch * MathF.PI / 180f);
        _cameraObject.Transform.Position = _focus + new Float3(0, height, -back);
        _cameraObject.Transform.LocalEulerAngles = new Float3(pitch, 0, 0);
    }

    private void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Enter) || Input.GetKeyDown(KeyCode.Space)) EndTurn();
        if (Input.GetKeyDown(KeyCode.Escape)) { SelectedCity = null; SelectedUnit = null; }
    }

    public void EndTurn() => Submit(new EndTurnCommand { Player = Human });

    private Hex? HexUnderMouse()
    {
        var size = Window.InternalWindow.Size;
        var ray = _camera.ScreenPointToRay(new Float2(Input.MousePosition.X, Input.MousePosition.Y), new Float2(size.X, size.Y));
        return _view.Pick(ray.Origin, ray.Direction);
    }

    private void HandleMouse()
    {
        bool left = Input.GetMouseButtonDown(0), right = Input.GetMouseButtonDown(1);
        if (!left && !right) return;
        if (_ui.PointerOverUi(PaperInstance)) return;
        if (HexUnderMouse() is not Hex hex) return;

        if (left) LeftClick(hex);
        else RightClick(hex);
    }

    private void LeftClick(Hex hex)
    {
        var city = City;
        if (city != null && hex != city.Position && CityRules.WorkableTiles(_sim, city).Contains(hex))
        {
            ToggleTile(city, hex);
            return;
        }

        // Everything of mine on this tile (units, then the city); clicking again cycles to the next one.
        var stack = new List<(bool IsCity, int Id)>();
        stack.AddRange(_sim.State.Units.Where(u => u.Owner == Human && u.Position == hex).OrderBy(u => u.Id).Select(u => (false, u.Id)));
        if (_sim.State.Cities.FirstOrDefault(c => c.Position == hex && c.Owner == Human) is { } here) stack.Add((true, here.Id));
        if (stack.Count == 0) { SelectedCity = null; SelectedUnit = null; return; }

        int current = stack.FindIndex(e => e.IsCity ? e.Id == SelectedCity : e.Id == SelectedUnit);
        var pick = stack[(current + 1) % stack.Count];
        SelectedCity = pick.IsCity ? pick.Id : null;
        SelectedUnit = pick.IsCity ? null : pick.Id;
    }

    private void RightClick(Hex hex)
    {
        var unit = Unit;
        if (unit == null) return;
        if (_sim.State.Units.Any(u => u.Owner != Human && u.Position == hex) && unit.Position.DistanceTo(hex) == 1)
        {
            Submit(new AttackCommand { Player = Human, UnitId = unit.Id, Target = hex });
            return;
        }

        // Greedy walk: no pathfinding yet, so it stops when blocked.
        for (int step = 0; step < 12 && unit.Position != hex && unit.MovesLeft > 0; step++)
        {
            var best = unit.Position.Neighbors()
                .Where(n => new MoveUnitCommand { Player = Human, UnitId = unit.Id, To = n }.Validate(_sim) == null)
                .OrderBy(n => n.DistanceTo(hex)).Cast<Hex?>().FirstOrDefault();
            if (best == null || best.Value.DistanceTo(hex) >= unit.Position.DistanceTo(hex)) break;
            Submit(new MoveUnitCommand { Player = Human, UnitId = unit.Id, To = best.Value });
        }
    }

    // --- City management ------------------------------------------------------------------

    private void ToggleTile(City city, Hex hex)
    {
        var tiles = city.Worked.ToList();
        var spec = new Dictionary<string, int>(city.Specialists);
        var citizen = CityRules.DefaultSpecialist(_sim);
        if (tiles.Remove(hex))
        {
            if (citizen == null) { Message = "Can't do that: no default citizen specialist is registered"; return; }
            spec[citizen.Id] = spec.GetValueOrDefault(citizen.Id) + 1;
        }
        else
        {
            var from = citizen != null && spec.GetValueOrDefault(citizen.Id) > 0 ? citizen.Id : spec.Where(kv => kv.Value > 0).Select(kv => kv.Key).FirstOrDefault();
            if (from != null)
            {
                if (--spec[from] <= 0) spec.Remove(from);
            }
            else if (tiles.Count > 0)
            {
                // Everyone is busy: move the citizen off the least valuable worked tile.
                tiles.Remove(tiles.OrderBy(t => CityRules.TileScore(_sim, t)).ThenBy(t => t.Q).ThenBy(t => t.R).First());
            }
            else { Message = "No citizen to assign"; return; }
            tiles.Add(hex);
        }
        Submit(new SetCityWorkCommand { Player = Human, CityId = city.Id, Tiles = tiles.ToArray(), Specialists = spec });
    }

    public void ChangeSpecialist(City city, string id, int delta)
    {
        var tiles = city.Worked.ToList();
        var spec = new Dictionary<string, int>(city.Specialists);
        var citizen = CityRules.DefaultSpecialist(_sim);
        if (citizen == null) return;

        if (delta > 0)
        {
            if (spec.GetValueOrDefault(citizen.Id) > 0 && id != citizen.Id) { if (--spec[citizen.Id] <= 0) spec.Remove(citizen.Id); }
            else if (tiles.Count > 0) tiles.RemoveAt(tiles.Count - 1);
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
