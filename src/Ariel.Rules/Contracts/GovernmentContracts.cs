using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>Saved civic-change timers. Each boundary consumes one anarchy turn at PlayerEnd.</summary>
public sealed class GovernmentPlayerState
{
    private int _anarchyTurns;
    private int _revolutionTimer;

    public int AnarchyTurns
    {
        get => _anarchyTurns;
        set
        {
            _anarchyTurns = Math.Max(0, value);
            if (_revolutionTimer < _anarchyTurns)
                _revolutionTimer = _anarchyTurns;
        }
    }

    /// <summary>Cooldown before the next civic change. Unrelated to city unrest (<see cref="ICityUnrest"/>).</summary>
    public int RevolutionTimer
    {
        get => _revolutionTimer;
        set => _revolutionTimer = Math.Max(_anarchyTurns, Math.Max(0, value));
    }

    [JsonIgnore]
    public bool IsAnarchy => _anarchyTurns > 0;

    public void StartRevolution(int anarchyTurns, int cooldownTurns)
    {
        _anarchyTurns = Math.Max(0, anarchyTurns);
        _revolutionTimer = Math.Max(_anarchyTurns, Math.Max(0, cooldownTurns));
    }

    public void AdvanceTurn()
    {
        _anarchyTurns = Math.Max(0, _anarchyTurns - 1);
        _revolutionTimer = Math.Max(0, _revolutionTimer - 1);
    }
}

public sealed record CivicSwitchResult(int AnarchyTurns, int CooldownTurns, Ledger Ledger);
public interface IGovernmentService
{
    /// <summary>Why the player may not change to these civics now, or null. Asks every <see cref="ICivicRestrictions"/>
    /// about each civic of the resulting cabinet.</summary>
    string? Validate(Simulation sim, int player, IReadOnlyList<string> civics);
    CivicSwitchResult Calculate(Simulation sim, int player, IReadOnlyList<string> civics);
    void Adopt(Simulation sim, int player, IReadOnlyList<string> civics);
    /// <summary>Put the player on these civics at once, without anarchy, a cooldown or any requirement: what a rule
    /// that forces a civic does (a council's resolution). The player's timers are left as they are.</summary>
    void Impose(Simulation sim, int player, IReadOnlyList<string> civics);
}

/// <summary>Limits a mod sets on which civics a player may run, beyond the civics' own requirements: a world council's
/// forced civics. Every registered instance is asked (through <see cref="IGovernmentService.Validate"/>); without one,
/// only requirements, anarchy and the cooldown limit a change. Answers are RNG-free and never change state.</summary>
public interface ICivicRestrictions
{
    /// <summary>Why the player may not run this civic, or null.</summary>
    string? Refuse(Simulation sim, int player, CivicDef civic);
}

/// <summary>Read-only identity and the complete imported AI personality. Queries never consume RNG.</summary>
public interface IPeoplesService
{
    LeaderDef? Leader(Simulation sim, int player);
    Ariel.Content.Schema.CivilizationDef? Civilization(Simulation sim, int player);
    IReadOnlyList<TraitDef> Traits(Simulation sim, int player);
    /// <summary>The player a faction plays as, created on first use through <see cref="AddPlayer"/>: on its own team,
    /// named after its civilization. Every caller gets the same player for the rest of the game. Changes state, so
    /// call it from rules, never from a query.</summary>
    int EnsureFaction(Simulation sim, FactionDef faction);
    /// <summary>Add a player to a running game and return its id: the next free player id, on a new team unless
    /// <see cref="NewPlayer.Team"/> names one. A nation starts on the civics a new game would give it and knows its
    /// civilization's starting technologies besides <see cref="NewPlayer.Techs"/>; a faction's player has neither and
    /// never takes a turn. War follows the always-at-war restriction in both directions: a faction that has it is at war
    /// with every other team at once, and every such faction already in the game is at war with the newcomer's team.
    /// Publishes <see cref="PlayerJoined"/>. Call it from a global turn step or an event handler, never from inside a
    /// player's own turn steps or while computer players are deciding, and never from a query.</summary>
    int AddPlayer(Simulation sim, NewPlayer player);
    /// <summary>A civilization and leader for a nation that forms during play, or null when every playable civilization
    /// is already in the game. The first of <paramref name="preferred"/> that is free wins; otherwise one free
    /// civilization is drawn at random, then one of its leaders no player uses. Draws random numbers, so call it from
    /// rules, never from a query.</summary>
    (string Civilization, string Leader)? DrawNation(Simulation sim, IEnumerable<string?> preferred);
}

/// <summary>A player to add to a running game (<see cref="IPeoplesService.AddPlayer"/>).</summary>
/// <param name="Civilization">The civilization it plays as; a civilization with a faction row makes it that faction's player.</param>
/// <param name="Leader">Its leader.</param>
/// <param name="Ai">The computer player that runs it, or null for none (a faction, or a seat a human takes).</param>
/// <param name="Name">Its display name, or null for the civilization's short description.</param>
/// <param name="Team">An existing team to join, or null for a team of its own.</param>
/// <param name="Techs">Technologies its team learns on joining.</param>
/// <param name="Treasury">Its starting gold.</param>
public sealed record NewPlayer(string Civilization, string Leader, string? Ai = null, string? Name = null, int? Team = null,
    IReadOnlyCollection<string>? Techs = null, long Treasury = 0);

/// <summary>A player joined a running game.</summary>
public sealed record PlayerJoined(int Player, int Team) : DomainEvent;

public static class GovernmentStatus
{
    public static bool IsAnarchy(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.TryGet<GovernmentPlayerState>(sim.Content)?.IsAnarchy == true;
    public static bool IsDisorder(Simulation sim, City city) =>
        IsAnarchy(sim, city.Owner)
        || (city.Extensions.TryGet<CityRuleState>(sim.Content) is { } state && (state.Disorder || state.OccupationTurns > 0));
}

/// <summary>One atomic revolution. Omitted civic options retain their current choice.</summary>
public sealed class SetCivicsCommand : GameCommand
{
    public List<string> Civics { get; init; } = [];
    public override string? Validate(Simulation sim) => sim.Rules().Government.Validate(sim, Player, Civics);
    public override void Apply(Simulation sim) => sim.Rules().Government.Adopt(sim, Player, Civics);
}
