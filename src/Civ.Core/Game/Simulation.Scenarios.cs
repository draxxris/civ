using Civ.Content;
using S = Civ.Content.Schema;

namespace Civ.Game;

public sealed partial class Simulation
{
    /// <summary>Create a new game on authored plots, without generation, start normalization or free units.</summary>
    /// <remarks>
    /// The simulation takes ownership of the plots. Incoming sight and exploration memory are cleared.
    /// Map dimensions override settings.Map; WorldSize still selects ruleset setup and scaling.
    /// Player identities, teams, treasury, civics and technology grants use normal setup defaults.
    /// Configure runs with player and plot indexes ready. It can author entities, starting plots,
    /// knowledge and mod state, or explicitly reveal plots through the visibility service.
    /// After configure, entity positions are normalized, indexes and next ids are rebuilt, and sight is computed.
    /// The seed starts a fresh RNG; setup consumes no random values before configure.
    /// TurnStarted then runs against the authored state, followed by AI, just as in a generated new game.
    /// Initial entities do not emit creation events. Use SaveGame.Load to resume without startup effects.
    /// </remarks>
    public static Simulation CreateScenario(ContentDb content, SimulationSettings settings, MapSpec map,
        Plot[] plots, Action<Simulation>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(plots);
        var grid = new HexGrid(map);
        if (plots.Length != grid.Count) throw new ArgumentException("Plot count does not match map dimensions", nameof(plots));
        if (plots.Any(p => p is null)) throw new ArgumentException("Scenario plots cannot be null", nameof(plots));
        var sim = new Simulation(content, (settings with { Map = map }).Resolve(content),
            new GameState { Map = map, Plots = plots }, new DeterministicRng(settings.Seed));
        // Validate ruleset setup ids, but never use world-size dimensions for authored maps.
        _ = ResolveMap(content, sim.Settings);
        sim.InitializePlayers();
        sim.ValidateTerrains();
        foreach (var plot in plots)
        {
            plot.Visible.Clear();
            plot.Revealed.Clear();
            plot.RevealedSnapshots.Clear();
        }
        sim.State.RebuildIndexes();
        configure?.Invoke(sim);
        sim.PrepareScenario();
        sim.SanitizeMap();
        content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
        sim.StartGame();
        return sim;
    }

    private void PrepareScenario()
    {
        if (State.Map != Settings.Map) throw new InvalidDataException("Configure cannot change the scenario map dimensions");
        if (State.Plots.Length != State.Grid.Count) throw new InvalidDataException("Plot count does not match map dimensions");
        ValidateTerrains();
        foreach (var plot in State.Plots) ValidateScenarioPlot(plot);
        foreach (var player in State.Players)
            if (player.StartingPlot is { } tile) _ = State.Grid.HexOf(tile);
        foreach (var city in State.Cities)
        {
            ValidateScenarioEntity(city.Id, city.Owner);
            city.SetPosition(State.Grid.Normalize(city.Position));
        }
        foreach (var unit in State.Units)
        {
            ValidateScenarioEntity(unit.Id, unit.Owner);
            if (Settings.WorldSize is null) _ = Content.Units.Get(unit.Type);
            else _ = Content.Get<S.UnitDef>(unit.Type);
            unit.SetPosition(State.Grid.Normalize(unit.Position));
        }
        State.NextCityId = Math.Max(State.NextCityId, checked(State.Cities.Select(c => c.Id).DefaultIfEmpty(0).Max() + 1));
        State.NextUnitId = Math.Max(State.NextUnitId, checked(State.Units.Select(u => u.Id).DefaultIfEmpty(0).Max() + 1));
        State.RebuildIndexes();
    }

    private void ValidateScenarioPlot(Plot plot)
    {
        if (plot.Owner is { } owner) _ = State.PlayerById(owner);
        if (Settings.WorldSize is null) return;
        if (plot.Feature is { } feature) _ = Content.Get<S.FeatureDef>(feature);
        if (plot.Bonus is { } bonus) _ = Content.Get<S.BonusDef>(bonus);
        if (plot.Improvement is { } improvement) _ = Content.Get<S.ImprovementDef>(improvement);
        if (plot.Route is { } route) _ = Content.Get<S.RouteDef>(route);
    }

    private void ValidateScenarioEntity(int id, int owner)
    {
        if (id <= 0) throw new InvalidDataException("Scenario entity ids must be positive");
        _ = State.PlayerById(owner);
    }
}
