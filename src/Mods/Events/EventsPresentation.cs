using Ariel.Content;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Events;

/// <summary>Events waiting for the local player's choice, each option a button, and above them what the player's choices
/// this turn did (each option's <see cref="EventOptionDef.Outcome"/>), so the screen turns into a result page once an
/// option is pressed. The top-bar button only shows while an event is waiting.</summary>
internal sealed class EventsScreen : IGameScreen
{
    public string Id => "events:screen";
    public int Order => 30;

    public ScreenButton? Button(ScreenContext context) =>
        context.Sim.Service<IEventService>() is { } service && service.Pending(context.Sim, context.Player).Count > 0
            ? new("Events!", "An event needs your choice") : null;

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        var pending = sim.Service<IEventService>()?.Pending(sim, context.Player) ?? [];
        var items = new List<ViewElement>(Outcomes(context));
        if (pending.Count == 0 && items.Count == 0) items.Add(new Paragraph("none", "No events are waiting.", Tone.Dim));
        var service = sim.Service<IEventService>();
        foreach (var e in pending)
        {
            sim.Content.TryGet<EventTriggerDef>(e.Trigger, out var trigger);
            var city = e.City >= 0 ? sim.State.FindCity(e.City) : null;
            var entry = new List<ViewElement>
            {
                new Paragraph("name", (trigger?.Name ?? Labels.Humanize(e.Trigger)) + (city is null ? "" : $" ({city.Name})"), Tone.Accent, TextSize.Title),
            };
            if (trigger is { Help.Length: > 0 }) entry.Add(new Paragraph("help", Labels.Plain(trigger.Help), Tone.Dim, TextSize.Small));
            foreach (var option in service!.Options(sim, e.Id))
            {
                sim.Content.TryGet<EventOptionDef>(option, out var def);
                string? blocked = service.ValidateChoice(sim, context.Player, e.Id, option);
                entry.Add(new ActionButton(option, def?.Name ?? Labels.Humanize(option),
                    new SubmitCommand(new ChooseEventOptionCommand { Player = context.Player, EventId = e.Id, Option = option }),
                    blocked, Labels.Plain(def?.Help ?? "")));
            }
            items.Add(new Section($"event:{e.Id}", null, entry));
        }
        return new("Events", items);
    }

    /// <summary>The result page: what each of the player's events resolved this turn did, for the options that say.
    /// The resolutions are read from this turn's published events, so another player's choice, and a choice an AI
    /// made with no screen to show it on, never appears here, and the page clears when the turn ends.</summary>
    private static IEnumerable<ViewElement> Outcomes(ScreenContext context)
    {
        var content = context.Sim.Content;
        foreach (var resolved in context.Sim.Events.Published.OfType<EventResolved>())
        {
            if (resolved.Player != context.Player || OutcomeOf(content, resolved.Option) is not { } option) continue;
            yield return new Section($"outcome:{resolved.EventId}", null,
            [
                new Paragraph("name", $"{Labels.Name<EventTriggerDef>(content, resolved.Trigger)}: {option.Name}"
                    + (resolved.Automatic ? " (chosen automatically)" : ""), Tone.Accent, TextSize.Title),
                new Paragraph("outcome", Labels.Plain(option.Outcome)),
            ]);
        }
    }

    /// <summary>The chosen option when it has an outcome to tell, else null (an expired event names no option).</summary>
    internal static EventOptionDef? OutcomeOf(ContentDb content, string option) =>
        content.TryGet<EventOptionDef>(option, out var def) && def.Outcome.Length > 0 ? def : null;
}

/// <summary>Events that start or end for the local player.</summary>
internal sealed class EventNotices : INotificationSource
{
    public string Id => "events:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e switch
    {
        EventTriggered t when t.Player == context.Player =>
            new("event", $"Event: {Labels.Name<EventTriggerDef>(context.Sim.Content, t.Trigger)}", t.City >= 0 ? t.City : null),
        EventResolved r when r.Player == context.Player =>
            new("event", $"{Labels.Name<EventTriggerDef>(context.Sim.Content, r.Trigger)} resolved" + (r.Automatic ? " automatically" : "")
                + (EventsScreen.OutcomeOf(context.Sim.Content, r.Option) is { } option ? ": " + Labels.Plain(option.Outcome) : "")),
        _ => null,
    };
}

/// <summary>Event triggers in the encyclopedia. They are not unlocks: a technology only lets them happen.</summary>
internal sealed class EventPedia : IPediaSource
{
    public string Id => "events:pedia";
    public string Category => "Events";
    public bool ListsUnlocks => false;

    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<EventTriggerDef>().All.Select(d => new PediaItem(d,
        Notes: d.RequiredTech is { } tech ? [$"Requires {Labels.Name<Content.Schema.TechDef>(content, tech)}"] : null));
}
