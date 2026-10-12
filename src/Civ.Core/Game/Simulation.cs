using Civ.Content;

namespace Civ.Game;

public sealed class SimulationSettings
{
    public ulong Seed { get; init; } = 1;
    public int MapRadius { get; init; } = 10;
    public string MapGenerator { get; init; } = "base:continents";
    public int PlayerCount { get; init; } = 2;
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

        for (int i = 0; i < settings.PlayerCount; i++)
            State.Players.Add(new Player { Id = i, Name = $"Player {i + 1}" });

        var gen = content.MapGenerators.Get(settings.MapGenerator);
        foreach (var (hex, terrain) in gen.Generate(settings.MapRadius, Rng, content))
            State.Terrain[hex] = terrain;
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
    }

    public ulong ComputeHash() => StateHasher.Hash(State, Rng);
}
