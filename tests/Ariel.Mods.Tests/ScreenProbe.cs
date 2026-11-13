using Ariel.Client;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>
/// Drives a mod's screen through the client's headless host the way a renderer does: describe, read, press a control by
/// its id. <see cref="Body"/> always describes afresh, because tests change the world directly, past the session.
/// </summary>
internal static class ScreenProbe
{
    public static bool Registered(GameSession session, string id) => session.Screens.Screens.Any(s => s.Id == id);

    /// <summary>The screen's button for the local player now, or null when hidden or absent.</summary>
    public static ScreenButton? Button(GameSession session, string id) =>
        session.Screens.Entries().FirstOrDefault(e => e.Id == id)?.Button;

    /// <summary>Every top-level element of the screen in document order: pinned header, body and pinned footer.</summary>
    public static IReadOnlyList<ViewElement> Body(GameSession session, string id) =>
        View(session, id).Elements.ToList();

    public static ScreenView View(GameSession session, string id) =>
        session.Screens.Screens.Single(s => s.Id == id).Describe(session.Screens.ContextFor(id));

    public static string Text(GameSession session, string id) => ViewWalk.Text(Body(session, id));

    /// <summary>Opens the screen and presses the control with this id; false when it has no action or did nothing.</summary>
    public static bool Press(GameSession session, string screen, string control)
    {
        Assert.True(session.Screens.Open(screen), screen);
        return session.Screens.Press(ViewWalk.ActionOf(Body(session, screen), control));
    }

    /// <summary>The section with this id under the roots; fails when there is none.</summary>
    public static IReadOnlyList<ViewElement> Section(IEnumerable<ViewElement> roots, string id) =>
        (ViewWalk.Find<Ariel.Presentation.Section>(roots, id) ?? throw new Xunit.Sdk.XunitException($"no section {id}")).Items;

    public static string? Paragraph(IEnumerable<ViewElement> roots, string id) => ViewWalk.Find<Paragraph>(roots, id)?.Text;

    public static IEnumerable<string> ButtonIds(IEnumerable<ViewElement> roots, string prefix) =>
        ViewWalk.All(roots).OfType<ActionButton>().Select(b => b.Id).Where(id => id.StartsWith(prefix, StringComparison.Ordinal));
}
