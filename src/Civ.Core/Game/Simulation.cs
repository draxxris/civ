using Civ.Content;

namespace Civ.Game;

/// <summary>Deterministic game setup, included in versioned saves.</summary>
public sealed class SimulationSettings
{
    /// <summary>Initial RNG seed.</summary>
    public ulong Seed { get; init; } = 1;
    /// <summary>Map dimensions and wrapping.</summary>
    public MapSpec Map { get; init; } = new(21, 21);
    /// <summary>Registered generator id.</summary>
    public string MapGenerator { get; init; } = "base:continents";
    /// <summary>Player count.</summary>
    public int PlayerCount { get; init; } = 2;
    /// <summary>Civilization per player. Missing entries cycle through content in ordinal id order.</summary>
    public string[] Civilizations { get; init; } = [];
    /// <summary>AI per player; missing or null means human.</summary>
    public string?[] PlayerAis { get; init; } = [];
    /// <summary>Team id per player; missing entries default to player id.</summary>
    public int[] PlayerTeams { get; init; } = [];
}

/// <summary>Headless deterministic simulation. Rules, commands and cross-mod services come from frozen content.</summary>
public sealed class Simulation
{
    /// <summary>Frozen ruleset contract.</summary>
    public ContentDb Content { get; }
    /// <summary>Authoritative state.</summary>
    public GameState State { get; }
    /// <summary>Resumable deterministic RNG.</summary>
    public DeterministicRng Rng { get; }
    /// <summary>Original game setup.</summary>
    public SimulationSettings Settings { get; }
    /// <summary>Deterministic rule notification bus and non-authoritative presentation events.</summary>
    public DomainEventBus Events { get; }
    private readonly List<ITurnSystem> _systems;

    /// <summary>Create a new game, generate plots, initialize teams, then let AI players act.</summary>
    public Simulation(ContentDb content, SimulationSettings settings)
        : this(content, settings, new GameState { Map = settings.Map }, new DeterministicRng(settings.Seed))
    {
        if (settings.PlayerCount <= 0) throw new ArgumentOutOfRangeException(nameof(settings.PlayerCount));
        var civs = content.Civilizations.All.ToList();
        for (int i = 0; i < settings.PlayerCount; i++) AddPlayer(i, ChooseCivilization(i, civs));
        State.Teams.Sort((a, b) => a.Id.CompareTo(b.Id));
        State.Plots = Enumerable.Range(0, State.Grid.Count).Select(_ => new Plot()).ToArray();
        content.MapGenerators.Get(settings.MapGenerator).Generate(settings.Map, State.Plots, Rng, content);
        foreach (var plot in State.Plots) _ = content.Terrains.Get(plot.Terrain);
        State.RebuildIndexes();
        Events.Publish(new TurnStarted(State.Turn));
        RunAi();
    }
    internal Simulation(ContentDb content, SimulationSettings settings, GameState state, DeterministicRng rng)
    {
        Content = content; Settings = settings; State = state; Rng = rng;
        _systems = content.TurnSystems.All.OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
        Events = new(this);
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
    /// <summary>Let each AI act and end its turn, without recursively advancing all-AI games.</summary>
    public void RunAi()
    {
        foreach (var player in State.Players.OrderBy(p => p.Id))
        {
            if (player.Ai == null || player.EndedTurn) continue;
            foreach (var cmd in Content.PlayerAIs.Get(player.Ai).Decide(this, player.Id).ToList()) TryApply(cmd, out _);
            player.EndedTurn = true;
        }
    }
    /// <summary>Terrain definition on a valid plot, including normalized wrap coordinates.</summary>
    public TerrainDef TerrainAt(Hex hex) => Content.Terrains.Get(State.PlotAt(hex).Terrain);
    /// <summary>Validate and apply; rejected commands must not change state.</summary>
    public bool TryApply(GameCommand cmd, out string? error)
    {
        error = cmd.Validate(this);
        if (error != null) return false;
        cmd.Apply(this);
        return true;
    }
    /// <summary>Run ordered rules, end the old turn, clear presentation events, start the new turn, and run AI.</summary>
    public void AdvanceTurn()
    {
        foreach (var system in _systems) system.Run(this);
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
