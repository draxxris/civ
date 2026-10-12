using Civ.Content;

namespace Civ.Game;

public sealed class SimulationSettings
{
    public ulong Seed { get; init; } = 1;
    public int MapRadius { get; init; } = 10;
    public string MapGenerator { get; init; } = "base:continents";
    public int PlayerCount { get; init; } = 2;
    /// <summary>Civilization id per player index. Missing entries cycle through the registered civilizations in id order.</summary>
    public string[] Civilizations { get; init; } = [];
    /// <summary>AI id per player index; null or missing means human.</summary>
    public string?[] PlayerAis { get; init; } = [];
}

/// <summary>
/// Headless, deterministic game. No engine, wall clock, floats or unordered iteration in anything that
/// touches <see cref="State"/>. Rules live in mods: commands and turn systems come from <see cref="Content"/>.
/// </summary>
public sealed class Simulation
{
    public ContentDb Content { get; }
    public GameState State { get; } = new();
    public DeterministicRng Rng { get; }

    private readonly List<ITurnSystem> _systems;

    public Simulation(ContentDb content, SimulationSettings settings)
    {
        Content = content;
        Rng = new DeterministicRng(settings.Seed);
        _systems = content.TurnSystems.All.OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();

        var civs = content.Civilizations.All.ToList();
        for (int i = 0; i < settings.PlayerCount; i++)
        {
            var civ = i < settings.Civilizations.Length
                ? content.Civilizations.Get(settings.Civilizations[i])
                : civs.Count > 0 ? civs[i % civs.Count] : null;
            var ai = i < settings.PlayerAis.Length ? settings.PlayerAis[i] : null;
            if (ai != null) content.PlayerAIs.Get(ai); // fail early on unknown AI
            var player = new Player { Id = i, Name = civ?.Name ?? $"Player {i + 1}", Civilization = civ?.Id ?? "", Ai = ai };
            foreach (var tech in civ?.StartingTechs ?? []) player.Techs.Add(tech);
            State.Players.Add(player);
        }

        var gen = content.MapGenerators.Get(settings.MapGenerator);
        foreach (var (hex, terrain) in gen.Generate(settings.MapRadius, Rng, content))
            State.Terrain[hex] = terrain;

        RunAi();
    }

    public CivilizationDef? CivilizationOf(int playerId)
    {
        var id = State.PlayerById(playerId).Civilization;
        return id.Length == 0 ? null : Content.Civilizations.Get(id);
    }

    /// <summary>
    /// Lets every AI player act, then marks it as having ended its turn. Runs inside the simulation on every peer
    /// (start of game and after each turn advance), so AI never touches the network. If all players are AI the turn is
    /// not advanced automatically: call <see cref="AdvanceTurn"/>.
    /// </summary>
    public void RunAi()
    {
        foreach (var player in State.Players.OrderBy(p => p.Id))
        {
            if (player.Ai == null || player.EndedTurn) continue;
            var ai = Content.PlayerAIs.Get(player.Ai);
            foreach (var cmd in ai.Decide(this, player.Id).ToList())
                TryApply(cmd, out _);
            player.EndedTurn = true;
        }
    }

    public TerrainDef TerrainAt(Hex hex) => Content.Terrains.Get(State.Terrain[hex]);

    /// <summary>Validate and apply. A rejected command changes nothing, and is rejected identically on every peer.</summary>
    public bool TryApply(GameCommand cmd, out string? error)
    {
        error = cmd.Validate(this);
        if (error != null) return false;
        cmd.Apply(this);
        return true;
    }

    public void AdvanceTurn()
    {
        foreach (var system in _systems) system.Run(this);
        State.Turn++;
        foreach (var p in State.Players) p.EndedTurn = false;
        RunAi();
    }

    public ulong ComputeHash() => StateHasher.Hash(State, Rng);
}
