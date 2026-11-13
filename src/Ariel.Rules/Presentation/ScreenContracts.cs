using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Presentation;

/// <summary>What a screen or panel may know about the client asking: the local player, what it has selected, and the
/// screen's own local state. Describing must not change the simulation or draw from its random numbers.</summary>
public sealed record ScreenContext(Simulation Sim, int Player, int? Unit = null, int? City = null, ScreenState? State = null)
{
    public ScreenState Local => State ?? ScreenState.Empty;
    public Player Me => Sim.State.PlayerById(Player);
    public Team MyTeam => Sim.TeamOf(Player);
}

/// <summary>The button that opens a screen from the top bar. <see cref="Tone"/> lets a screen ask for attention, as a
/// pending choice does.</summary>
public sealed record ScreenButton(string Label, string? Tooltip = null, Tone Tone = Tone.Normal);

/// <summary>A screen a mod adds to the client: an advisor or overview the player opens from the top bar. The client
/// renders the described view generically and hands every pressed control back, so the mod owns the whole screen.
/// Register one with <c>ctx.RegisterService&lt;IGameScreen&gt;(name, screen)</c>; <see cref="IHasId.Id"/> is its
/// stable id (<c>modid:name</c>). Implementations hold no per-game state: everything the screen remembers between
/// describes is in <see cref="ScreenContext.Local"/>.</summary>
public interface IGameScreen : IHasId
{
    /// <summary>Sorts the top bar's screen buttons; ties keep registration order.</summary>
    int Order => 0;

    /// <summary>The top-bar button for the local player, or null to hide it (the mod's rules are off, or there is
    /// nothing to show). Called on every world change, so it must be cheap.</summary>
    ScreenButton? Button(ScreenContext context);

    /// <summary>The whole screen. The host calls it again when the world, the selection or the local state changes.</summary>
    ScreenView Describe(ScreenContext context);
}

/// <summary>Where a contribution sits in the city screen: the figures grid, the status block over the ledgers (alerts
/// and the city's own controls), or the detail column beside production.</summary>
public enum CityPanelSlot { Stats, Status, Details }

/// <summary>Rows a mod adds to the city screen. Describe returns nothing for a city it has nothing to say about.
/// Contributions in one slot keep registration order.</summary>
public interface ICityPanel : IHasId
{
    CityPanelSlot Slot { get; }
    IReadOnlyList<ViewElement> Describe(ScreenContext context, City city);
}

/// <summary>A mod's item in the top bar's player summary, such as a countdown or an alert. Null shows nothing.</summary>
public interface IPlayerSummaryItem : IHasId
{
    ViewElement? Describe(ScreenContext context);
}

/// <summary>One encyclopedia article a mod's content adds: the definition (its name, help, strategy, pedia text,
/// icon, requirements and <see cref="IDescribedContent"/> lines are read from it), its cost if it has one,
/// <see cref="Notes"/> listed with its prerequisites, and the technology that makes it available beyond its
/// requirements.</summary>
public sealed record PediaItem(GameContentDef Definition, int Cost = 0, IReadOnlyList<string>? Notes = null, string? UnlockedBy = null);

/// <summary>A category of encyclopedia articles a mod adds. When <see cref="ListsUnlocks"/> holds, the research screen
/// also lists each article under the technologies that unlock it.</summary>
public interface IPediaSource : IHasId
{
    string Category { get; }
    bool ListsUnlocks => true;
    IEnumerable<PediaItem> Items(ContentDb content);
}

/// <summary>A message for the local player about something that happened, with an optional place to go to.</summary>
public sealed record Notice(string Kind, string Text, int? City = null, Hex? Where = null, Tone Tone = Tone.Normal);

/// <summary>What a notification source may ask about the local player while turning an event into a notice.
/// <see cref="WasMine"/> answers for units the player owned when the events were last read, so a unit that died in the
/// event still counts as the player's.</summary>
public sealed class NoticeContext(Simulation sim, int player, IReadOnlySet<int> myUnits)
{
    public Simulation Sim { get; } = sim;
    public int Player { get; } = player;
    public int Team => Sim.TeamOf(Player).Id;
    public bool WasMine(int unit) => myUnits.Contains(unit);
    public bool OwnsCity(int city) => Sim.State.FindCity(city)?.Owner == Player;
    public string CityName(int city) => Sim.State.FindCity(city)?.Name ?? "a city";
    public string PlayerName(int player) => player == Player ? "You"
        : Sim.State.Players.FirstOrDefault(p => p.Id == player)?.Name ?? $"Player {player}";
}

/// <summary>Turns a mod's domain events into notices for the local player. Return null for an event the player need
/// not hear about, or one that is not this source's.</summary>
public interface INotificationSource : IHasId
{
    Notice? Describe(NoticeContext context, DomainEvent e);
}
