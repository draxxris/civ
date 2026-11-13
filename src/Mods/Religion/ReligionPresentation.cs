using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Religion;

/// <summary>Every loaded religion, the player's state religion, and what changing it costs, with a control to adopt
/// each faith or to drop to none.</summary>
internal sealed class ReligionScreen : IGameScreen
{
    public string Id => "religion:screen";
    /// <summary>Before every other mod screen, where the client's own religion button used to sit.</summary>
    public int Order => 0;

    public ScreenButton? Button(ScreenContext context) => context.Sim.Service<IReligionService>() is null ? null
        : new("Religion", "State religion and the religions of the world");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        if (sim.Service<IReligionService>() is not { } service)
            return new("Religion", [new Paragraph("off", "Religion is not part of this ruleset.", Tone.Dim)]);
        int player = context.Player;
        var state = service.StateReligion(sim, player);
        var preview = service.PreviewConversion(sim, player);
        int remaining = context.Me.Extensions.Get<ReligionPlayerState>(sim.Content)?.ConversionTimer ?? 0;
        string stateName = state is null ? "none" : Labels.Name<ReligionDef>(sim.Content, state);
        var header = new List<ViewElement>
        {
            new Paragraph("summary", $"State religion - {stateName}   ·   Conversion: {preview.AnarchyTurns} anarchy turns, "
                + $"{preview.CooldownTurns} cooldown   ·   Cooldown left: {remaining}", Tone.Accent, TextSize.Title),
        };
        var founded = sim.State.Extensions.Get<ReligionGameState>(sim.Content)?.Founded;
        var rows = new List<ViewElement>();
        foreach (var def in sim.Content.Get<ReligionDef>().All.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            int? holy = service.HolyCity(sim, def.Id);
            string detail = holy is int city ? $"holy city {sim.State.FindCity(city)?.Name ?? "?"}"
                : founded?.ContainsKey(def.Id) == true ? "founded"
                : "not founded" + (FoundingWonder(sim.Content, def.Id) is { } wonder ? $" (build {wonder.Name})" : "");
            string? blocked = def.Id == state ? "Already the state religion" : service.ValidateConversion(sim, player, def.Id);
            rows.Add(new Row("row:" + def.Id,
            [
                new ListItem(def.Id, def.Name + (def.Id == state ? "  (state religion)" : ""), detail, def.Id),
                new ActionButton("adopt:" + def.Id, "Adopt", new SubmitCommand(new SetStateReligionCommand { Player = player, Religion = def.Id }),
                    blocked, Labels.Plain(def.Help)),
            ]));
        }
        string? none = state is null ? "No state religion to drop" : service.ValidateConversion(sim, player, null);
        // The summary and the way out of a state religion stay in view while the list of faiths scrolls.
        return new("Religion", [new Section("religions", null, rows)])
        {
            Header = header,
            Footer = [new ActionButton("none", "No state religion", new SubmitCommand(new SetStateReligionCommand { Player = player, Religion = null }), none)],
        };
    }

    /// <summary>The wonder that founds a religion. Buildings name the faith by its identity.</summary>
    internal static BuildingDef? FoundingWonder(ContentDb content, string religion)
    {
        string identity = ReligionIdentities.Identity(content, religion);
        return content.Get<BuildingDef>().All.FirstOrDefault(b => b.FoundsReligion == identity);
    }
}

/// <summary>The religions present in a city on its screen, its holy-city faiths and the owner's state religion marked.</summary>
internal sealed class ReligionCityPanel : ICityPanel
{
    public string Id => "religion:city";
    public CityPanelSlot Slot => CityPanelSlot.Status;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city)
    {
        var sim = context.Sim;
        if (sim.Service<IReligionService>() is not { } service) return [];
        var present = service.Present(sim, city.Id);
        if (present.Count == 0) return [];
        var state = service.StateReligion(sim, city.Owner);
        return [new Row("present", present.Select(id =>
        {
            bool holy = service.HolyCity(sim, id) == city.Id;
            string text = Labels.Name<ReligionDef>(sim.Content, id) + (holy ? " ★ holy city" : "") + (id == state ? " (state)" : "");
            return (ViewElement)new Paragraph(id, text, holy ? Tone.Accent : Tone.Dim, TextSize.Body, id);
        }).ToList())];
    }
}

/// <summary>The faiths of a city in the city list, such as "Buddhism*, Taoism", where a star marks a holy city.</summary>
internal sealed class ReligionCityTag : ICityTag
{
    public string Id => "religion:city_tag";

    public string? Describe(ScreenContext context, City city)
    {
        var sim = context.Sim;
        if (sim.Service<IReligionService>() is not { } service) return null;
        var present = service.Present(sim, city.Id);
        if (present.Count == 0) return null;
        return string.Join(", ", present.Select(id => Labels.Name<ReligionDef>(sim.Content, id) + (service.HolyCity(sim, id) == city.Id ? "*" : "")));
    }
}

/// <summary>Foundings, spread into the player's cities, changes of state religion, and the player's missionaries.</summary>
internal sealed class ReligionNotices : INotificationSource
{
    public string Id => "religion:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e)
    {
        string Name(string? id) => id is not null && context.Sim.Content.TryGet<ReligionDef>(id, out var r) ? r.Name : Labels.Humanize(id ?? "");
        return e switch
        {
            ReligionFounded f => new("religion", $"{Name(f.Religion)} was founded in {context.CityName(f.City)}", f.City),
            ReligionSpread s when context.OwnsCity(s.City) => new("religion", $"{Name(s.Religion)} spread to {context.CityName(s.City)}", s.City),
            StateReligionChanged s => new("religion", s.Religion is null ? $"{context.PlayerName(s.Player)} no longer has a state religion"
                : $"{context.PlayerName(s.Player)} adopted {Name(s.Religion)}"),
            MissionResolved { Mission: ReligionMissions.Spread } m when context.WasMine(m.Unit) || context.OwnsCity(m.City) =>
                new("religion", $"Missionary {(m.Success ? "spread" : "failed to spread")} {Name(m.Subject)} in {context.CityName(m.City)}", m.City),
            _ => null,
        };
    }
}

/// <summary>Religions in the encyclopedia, each with the wonder that founds it, under the technology that unlocks it.</summary>
internal sealed class ReligionPedia : IPediaSource
{
    public string Id => "religion:pedia";
    public string Category => "Religions";

    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<ReligionDef>().All.Select(d => new PediaItem(d,
        Notes: ReligionScreen.FoundingWonder(content, d.Id) is { } wonder ? [$"Founded by building {wonder.Name}"] : null,
        UnlockedBy: d.FoundingTech));
}
