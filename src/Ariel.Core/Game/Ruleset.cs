using Ariel.Content;

namespace Ariel.Game;

/// <summary>Setup fields whose defaults and display order belong to the loaded ruleset.</summary>
public enum SetupField { WorldSize, GameSpeed, Handicap, StartingEra, MapGenerator }

/// <summary>A per-session switch a ruleset offers at setup.</summary>
public sealed record SetupGameOption(string Id, string Name, bool Default, string Help = "");

/// <summary>Optional ruleset policy. At most one provider may supply a default for each field.</summary>
public interface ISetupPolicy
{
    string? Default(SetupField field);
    int? SortOrder(SetupField field, string id) => null;
    IEnumerable<SetupGameOption> GameOptions(ContentDb content) => [];
}

public static class SetupPolicies
{
    public static string? Default(ContentDb content, SetupField field) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.Default(field)).Where(id => id != null).SingleOrDefault();

    public static int SortOrder(ContentDb content, SetupField field, string id) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.SortOrder(field, id)).Where(n => n != null).SingleOrDefault() ?? 0;

    public static IReadOnlyList<SetupGameOption> GameOptions(ContentDb content) =>
        content.Services.Get<ISetupPolicy>().SelectMany(p => p.GameOptions(content))
            .OrderBy(option => option.Name, StringComparer.Ordinal).ThenBy(option => option.Id, StringComparer.Ordinal).ToList();
}

/// <summary>What the kernel asks of the loaded game. The kernel knows players, teams, plots, units and cities as plain
/// data; everything that gives them meaning is content, and exactly one registered ruleset answers for it.</summary>
public interface IRuleset
{
    /// <summary>Called once when content freezes, after every mod registered: resolve whatever this ruleset needs
    /// from the frozen registries, so rules read fields instead of searching the registries during play.</summary>
    void Bind(ContentDb content) { }
    /// <summary>Validate setup content ids and resolve the effective map dimensions.</summary>
    MapSpec ResolveMap(ContentDb content, SimulationSettings settings);
    /// <summary>Create player and team data before map generation.</summary>
    void CreatePlayers(Simulation sim);
    /// <summary>Normalize starts, grant units and initialize sight after map generation and indexing.</summary>
    void Complete(Simulation sim);
    /// <summary>Finish an authored scenario after its configure step: derived state such as sight, but no generation,
    /// start normalization or free units.</summary>
    void CompleteScenario(Simulation sim);
    /// <summary>Throw unless every content id held by plots and units resolves in the loaded content.</summary>
    void Validate(Simulation sim);
    /// <summary>Whether a game option is on when the session does not set it.</summary>
    bool OptionDefault(ContentDb content, string option);
    /// <summary>Whether a unit's arrival changes what its owner's planning can depend on well beyond its own plot, as
    /// an explorer or a settler does. Concurrent AI planning treats the surrounding plots as touched.</summary>
    bool IsScout(Simulation sim, Unit unit);
}
