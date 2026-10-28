using Ariel.Content;

namespace Ariel.Game;

/// <summary>Deterministic game setup, included in versioned saves.</summary>
public sealed record SimulationSettings
{
    /// <summary>Initial RNG seed.</summary>
    public ulong Seed { get; init; } = 1;
    /// <summary>Map dimensions and wrapping.</summary>
    public MapSpec Map { get; init; } = new(21, 21);
    /// <summary>Registered generator id. Empty selects the loaded ruleset default.</summary>
    public string MapGenerator { get; init; } = "";
    /// <summary>Player count.</summary>
    public int PlayerCount { get; init; } = 2;
    /// <summary>Civilization per player. Missing entries cycle through content in ordinal id order.</summary>
    public string[] Civilizations { get; init; } = [];
    /// <summary>Feature ruleset world size. Null retains legacy setup.</summary>
    public string? WorldSize { get; init; }
    /// <summary>Speed definition for feature rules.</summary>
    public string GameSpeed { get; init; } = "";
    /// <summary>Difficulty definition for feature rules.</summary>
    public string Handicap { get; init; } = "";
    /// <summary>Initial era, used for starting grants and build-time scaling.</summary>
    public string StartingEra { get; init; } = "";
    /// <summary>One leader per player in feature rulesets.</summary>
    public string[] Leaders { get; init; } = [];
    /// <summary>AI per player; missing or null means human.</summary>
    public string?[] PlayerAis { get; init; } = [];
    /// <summary>Team id per player; missing entries default to player id.</summary>
    public int[] PlayerTeams { get; init; } = [];
    /// <summary>Per-session game option overrides. Missing options use their ruleset defaults.</summary>
    public SortedDictionary<string, bool> GameOptions { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Resolve omitted identities once, before generation.</summary>
    public SimulationSettings Resolve(ContentDb content) => this with
    {
        MapGenerator = Choose(content, SetupField.MapGenerator, MapGenerator, content.MapGenerators.All.FirstOrDefault()?.Id ?? ""),
        GameSpeed = Choose(content, SetupField.GameSpeed, GameSpeed),
        Handicap = Choose(content, SetupField.Handicap, Handicap),
        StartingEra = Choose(content, SetupField.StartingEra, StartingEra),
    };

    /// <summary>Map dimensions and wrapping. A ruleset world size resolves through its own setup; without one the
    /// literal <see cref="Map"/> is used.</summary>
    public MapSpec ResolveMap(ContentDb content) => WorldSize is null
        ? Map : content.Services.Get<INewGameSetup>().Single().ResolveMap(content, this);

    private static string Choose(ContentDb content, SetupField field, string value, string fallback = "") =>
        value.Length > 0 ? value : SetupPolicies.Default(content, field) ?? fallback;
}

/// <summary>Headless deterministic simulation. Rules, commands and cross-mod services come from frozen content.</summary>
public sealed partial class Simulation
{
    /// <summary>Frozen ruleset contract.</summary>
    public ContentDb Content { get; }
    /// <summary>Authoritative state.</summary>
    public GameState State { get; }
    /// <summary>Resumable deterministic RNG.</summary>
    public DeterministicRng Rng { get; }
    /// <summary>Game setup with omitted identities resolved from the loaded ruleset.</summary>
    public SimulationSettings Settings { get; }
    /// <summary>Deterministic rule notification bus and non-authoritative presentation events.</summary>
    public DomainEventBus Events { get; }
    private readonly List<ITurnSystem> _systems;

    /// <summary>Create a new game, generate plots, initialize teams, then let AI players act.</summary>
    public Simulation(ContentDb content, SimulationSettings settings)
        : this(content, settings.Resolve(content), new GameState(), new DeterministicRng(settings.Seed))
    {
        settings = Settings;
        State.Map = settings.ResolveMap(content);
        var setup = InitializePlayers();
        State.Plots = Enumerable.Range(0, State.Grid.Count).Select(_ => new Plot()).ToArray();
        content.MapGenerators.Get(settings.MapGenerator).Generate(State.Map, State.Plots, Rng, content, settings.PlayerCount);
        ValidateTerrains();
        State.RebuildIndexes();
        setup?.Complete(this);
        SanitizeMap();
        StartGame();
    }
    internal Simulation(ContentDb content, SimulationSettings settings, GameState state, DeterministicRng rng)
    {
        Content = content; Settings = settings; State = state; Rng = rng;
        _systems = content.TurnSystems.All.OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
        if (_systems.Any(s => s.Order >= TurnPhase.PlayerBegin && s.Order < TurnPhase.Map && s is not IPlayerTurnSystem))
            throw new InvalidDataException("Player phase systems must implement IPlayerTurnSystem");
        Events = new(this);
    }
    private INewGameSetup? InitializePlayers()
    {
        if (Settings.PlayerCount <= 0) throw new ArgumentOutOfRangeException(nameof(Settings.PlayerCount));
        var setup = Settings.WorldSize is null ? null : Content.Services.Get<INewGameSetup>().Single();
        if (setup is not null) setup.CreatePlayers(this);
        else
        {
            var civs = Content.Civilizations.All.ToList();
            for (int i = 0; i < Settings.PlayerCount; i++) AddPlayer(i, ChooseCivilization(i, civs));
        }
        State.Teams.Sort((a, b) => a.Id.CompareTo(b.Id));
        return setup;
    }
    private void ValidateTerrains()
    {
        foreach (var plot in State.Plots)
        {
            if (Settings.WorldSize is null) _ = Content.Terrains.Get(plot.Terrain);
            else _ = Content.Get<Ariel.Content.Schema.TerrainDef>(plot.Terrain);
        }
    }
    internal void SanitizeMap()
    {
        foreach (var sanitizer in Content.Services.Get<IMapSanitizer>()) sanitizer.Sanitize(this);
    }
    private void StartGame()
    {
        Events.Publish(new TurnStarted(State.Turn));
        RunAi();
    }
    private CivilizationDef? ChooseCivilization(int player, IReadOnlyList<CivilizationDef> civs)
    {
        if (player < Settings.Civilizations.Length) return Content.Civilizations.Get(Settings.Civilizations[player]);
        return civs.Count == 0 ? null : civs[player % civs.Count];
    }
    private void AddPlayer(int id, CivilizationDef? civ)
    {
        var ai = id < Settings.PlayerAis.Length ? Settings.PlayerAis[id] : null;
        if (ai != null) Content.PlayerAIs.Get(ai);
        int teamId = id < Settings.PlayerTeams.Length ? Settings.PlayerTeams[id] : id;
        if (teamId < 0) throw new ArgumentException("Team ids must be nonnegative");
        var team = GetOrCreateTeam(teamId);
        team.Members.Add(id);
        team.Techs.UnionWith(civ?.StartingTechs ?? []);
        var player = CreatePlayer(id, teamId, civ, ai);
        foreach (var civic in Content.Civics.All.Where(c => c.IsDefault)) player.Civics.TryAdd(civic.Category, civic.Id);
        State.Players.Add(player);
    }
    private static Player CreatePlayer(int id, int teamId, CivilizationDef? civ, string? ai) => new()
    {
        Id = id, TeamId = teamId, Name = civ?.Name ?? $"Player {id + 1}", Civilization = civ?.Id ?? "", Ai = ai,
    };
    private Team GetOrCreateTeam(int id)
    {
        var team = State.Teams.FirstOrDefault(t => t.Id == id);
        if (team != null) return team;
        team = new Team { Id = id };
        State.Teams.Add(team);
        return team;
    }
    /// <summary>Whether a game option is enabled in this session, using its content default when unset.</summary>
    public bool IsOptionEnabled(string id) => Settings.GameOptions.TryGetValue(id, out bool enabled)
        ? enabled
        : Content.TryGet<Ariel.Content.Schema.GameOptionDef>(id, out var definition) && definition.Default;
    /// <summary>Team state for a player, including research and known techs.</summary>
    public Team TeamOf(int playerId) => State.TeamOf(playerId);
    /// <summary>Whether a player's team knows a technology.</summary>
    public bool KnowsTech(int playerId, string tech) => TeamOf(playerId).Techs.Contains(tech);
    /// <summary>Civilization definition for a player.</summary>
    public CivilizationDef? CivilizationOf(int playerId)
    {
        var id = State.PlayerById(playerId).Civilization;
        return id.Length == 0 ? null : Content.Civilizations.Get(id);
    }
    /// <summary>Let each AI act and end its turn, without recursively advancing all-AI games. Players act in id order;
    /// independent players may plan concurrently, but the result is the same on every peer.</summary>
    public void RunAi()
    {
        // AI commands are the players' own top-level commands, even when an end-turn command advanced the turn.
        int depth = _commandDepth;
        _commandDepth = 0;
        try { AiTurnSpeculator.Run(this); }
        finally { _commandDepth = depth; }
    }
    /// <summary>Run one AI player's turn in place: its decisions, then any returned commands. Does not end its turn.</summary>
    internal void RunAiPlayer(int playerId)
    {
        var player = State.PlayerById(playerId);
        foreach (var cmd in Content.PlayerAIs.Get(player.Ai!).Decide(this, player.Id).ToList()) TryApply(cmd, out _);
    }
    /// <summary>Isolated copy of the state and RNG for speculative planning. It records its top-level commands.</summary>
    internal Simulation ForkSpeculative()
    {
        var (a, b, c, d) = Rng.State;
        return new Simulation(Content, Settings, State.Clone(), DeterministicRng.Restore(a, b, c, d))
        {
            _recordedCommands = new(),
        };
    }
    /// <summary>Top-level commands applied since recording started, with the RNG state before validation and after application.</summary>
    internal List<(GameCommand Command, (ulong, ulong, ulong, ulong) PreRng, (ulong, ulong, ulong, ulong) PostRng)>? _recordedCommands;
    /// <summary>Optional observer of top-level commands, used to measure what a speculative turn touched.</summary>
    internal ICommandObserver? CommandObserver { get; set; }
    private int _commandDepth;
    /// <summary>Whether this simulation is planning a player in a concurrent AI wave. Read-only planners should
    /// score serially in this scope to avoid nesting parallel work inside the wave. Not authoritative or saved.</summary>
    public bool IsParallelAiTurn { get; internal set; }
    /// <summary>Most AI players planned together in one wave; one runs every AI strictly in turn. Part of the
    /// deterministic rules, so it must never depend on the machine.</summary>
    internal int AiWaveLimit { get; set; } = int.MaxValue;
    /// <summary>Optional diagnostic sink for AI wave decisions.</summary>
    internal Action<SpeculationEvent>? SpeculationObserver { get; set; }
    /// <summary>Terrain definition on a valid plot, including normalized wrap coordinates.</summary>
    public TerrainDef TerrainAt(Hex hex) => Content.Terrains.Get(State.PlotAt(hex).Terrain);
    /// <summary>Validate and apply; rejected commands must not change state.</summary>
    public bool TryApply(GameCommand cmd, out string? error)
    {
        // Only top-level commands are recorded; commands applied from inside another command replay with it.
        bool top = _commandDepth == 0;
        var observer = top ? CommandObserver : null;
        var preRng = Rng.State;
        _commandDepth++;
        try
        {
            error = cmd.Validate(this);
            if (error != null) { observer?.Rejected(this, cmd); return false; }
            observer?.Before(this, cmd);
            cmd.Apply(this);
        }
        finally { _commandDepth--; }
        if (top) _recordedCommands?.Add((cmd, preRng, Rng.State));
        observer?.After(this, cmd);
        return true;
    }
    /// <summary>Run ordered rules, end the old turn, clear presentation events, start the new turn, and run AI.</summary>
    public void AdvanceTurn()
    {
        foreach (var system in _systems.Where(s => s.Order < TurnPhase.PlayerBegin)) system.Run(this);
        var playerSystems = _systems.Where(s => s.Order >= TurnPhase.PlayerBegin && s.Order < TurnPhase.Map).ToArray();
        foreach (var player in State.Players.OrderBy(p => p.Id))
            foreach (var system in playerSystems.Cast<IPlayerTurnSystem>()) system.Run(this, player.Id);
        foreach (var system in _systems.Where(s => s.Order >= TurnPhase.Map)) system.Run(this);
        Events.Publish(new TurnEnded(State.Turn));
        State.Turn++;
        foreach (var player in State.Players) player.EndedTurn = false;
        Events.BeginTurn();
        Events.Publish(new TurnStarted(State.Turn));
        RunAi();
    }
    /// <summary>Hash all authoritative fields and RNG state; presentation events and caches are excluded.</summary>
    public ulong ComputeHash() => StateHasher.Hash(State, Rng);
}
