using Ariel.Content;
using Ariel.Game;

namespace Ariel.Client;

/// <summary>A registered screen and its top-bar button for the local player; a null button hides it.</summary>
public sealed record ScreenEntry(string Id, ScreenButton? Button);

/// <summary>
/// Hosts the screens mods register (<see cref="IGameScreen"/>) for any renderer: which one is open, the local state of
/// each, its described view, and what pressing a control does. The view is described again only when its inputs move:
/// the simulation, <see cref="GameSession.WorldRevision"/>, the selected unit or city, or the screen's local state.
/// <see cref="IsStale"/> compares stored integers, so a frame loop may ask it every frame without allocating.
/// </summary>
public sealed class ScreenHost
{
    private readonly GameSession _session;
    private readonly Dictionary<string, ScreenState> _states = new(StringComparer.Ordinal);
    private ContentDb? _screensContent;
    private IReadOnlyList<IGameScreen> _screens = [];
    private Simulation? _statesSim;
    private int _stateVersion;
    private (Simulation? Sim, int World, int? Unit, int? City, int State, string? Open) _described;
    private ScreenView? _view;

    internal ScreenHost(GameSession session) => _session = session;

    /// <summary>The open screen's id, or null.</summary>
    public string? OpenId { get; private set; }

    /// <summary>Moves every time <see cref="View"/> is described again, so a renderer redraws only then.</summary>
    public int Version { get; private set; }

    /// <summary>The registered screens in top-bar order: <see cref="IGameScreen.Order"/>, then registration order.</summary>
    public IReadOnlyList<IGameScreen> Screens
    {
        get
        {
            var content = _session.Sim.Content;
            if (!ReferenceEquals(content, _screensContent))
            {
                _screens = content.Services.Get<IGameScreen>().Select((s, i) => (s, i)).OrderBy(p => p.s.Order).ThenBy(p => p.i)
                    .Select(p => p.s).ToArray();
                _screensContent = content;
            }
            return _screens;
        }
    }

    /// <summary>Every screen with its button for the local player now. Each screen's <see cref="IGameScreen.Button"/>
    /// is called once.</summary>
    public IReadOnlyList<ScreenEntry> Entries()
    {
        var context = ContextFor(null);
        return Screens.Select(s => new ScreenEntry(s.Id, s.Button(context))).ToArray();
    }

    /// <summary>Whether the open screen's view no longer matches its inputs. Allocation-free.</summary>
    public bool IsStale => OpenId is not null && _described != Key();

    /// <summary>The open screen's view, described again first when it is stale; null when none is open.</summary>
    public ScreenView? View
    {
        get
        {
            if (OpenId is null || Find(OpenId) is not { } screen) { _view = null; return null; }
            var key = Key();
            if (_view is null || _described != key)
            {
                _view = screen.Describe(ContextFor(OpenId));
                _described = key;
                Version++;
            }
            return _view;
        }
    }

    /// <summary>The local state of a screen, empty until the player changes it. A loaded game starts every screen afresh.</summary>
    public ScreenState StateOf(string id)
    {
        ForgetOtherGame();
        return _states.GetValueOrDefault(id) ?? ScreenState.Empty;
    }

    /// <summary>Opens a registered screen; false for an unknown id.</summary>
    public bool Open(string id)
    {
        if (Find(id) is null) return false;
        OpenId = id;
        _view = null;
        return true;
    }

    /// <summary>Opens the screen, or closes it when it is already open.</summary>
    public void Toggle(string id)
    {
        if (OpenId == id) Close();
        else Open(id);
    }

    public void Close()
    {
        OpenId = null;
        _view = null;
        Version++;
    }

    /// <summary>
    /// Does what a pressed control asks. A command goes through <see cref="GameSession.Submit"/>, so it is validated
    /// and sent like any other order; one built for another player is refused. A state change replaces the open
    /// screen's local state. Returns whether anything happened.
    /// </summary>
    public bool Press(ScreenAction? action)
    {
        switch (action)
        {
            case SubmitCommand { Command: var command }:
                if (command.Player != _session.LocalPlayer) { _session.SetMessage("That order is not yours to give"); return false; }
                return _session.Submit(command);
            case ChangeState { Next: var next } when OpenId is { } id:
                ForgetOtherGame();
                if (StateOf(id).Equals(next)) return false;
                _states[id] = next;
                _stateVersion++;
                return true;
            default:
                return false;
        }
    }

    /// <summary>The context a screen or panel is described with: the local player and selection, and the screen's state.</summary>
    public ScreenContext ContextFor(string? screen) =>
        new(_session.Sim, _session.LocalPlayer, _session.SelectedUnitId, _session.SelectedCityId, screen is null ? null : StateOf(screen));

    private IGameScreen? Find(string id)
    {
        foreach (var screen in Screens) if (screen.Id == id) return screen;
        return null;
    }

    private (Simulation?, int, int?, int?, int, string?) Key() =>
        (_session.Sim, _session.WorldRevision, _session.SelectedUnitId, _session.SelectedCityId, _stateVersion, OpenId);

    private void ForgetOtherGame()
    {
        if (ReferenceEquals(_statesSim, _session.Sim)) return;
        _states.Clear();
        _statesSim = _session.Sim;
        _stateVersion++;
    }
}
