using Ariel.Content;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Corporations;

/// <summary>Every corporation with its founding state, and what each of the player's cities hosts.</summary>
internal sealed class CorporationsScreen : IGameScreen
{
    public string Id => "corporations:screen";
    public int Order => 10;

    public ScreenButton? Button(ScreenContext context) => context.Sim.Service<ICorporationService>() is null ? null
        : new("Corporations", "Corporations and their branches");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        var service = sim.Service<ICorporationService>();
        if (service is null) return new("Corporations", [new Paragraph("off", "Corporations are not part of this ruleset.", Tone.Dim)]);
        var list = new List<ViewElement>();
        foreach (var def in sim.Content.Get<CorporationDef>().All.OrderBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            int? hq = service.Headquarters(sim, def.Id);
            int branches = sim.State.Cities.Count(c => service.Present(sim, c.Id).Contains(def.Id));
            string state = hq is int city ? $"headquarters {sim.State.FindCity(city)?.Name ?? "?"}, {branches} cities" : "not founded";
            list.Add(new ListItem(def.Id, def.Name, state, def.Id));
        }
        var items = new List<ViewElement> { new Section("corporations", null, list) };
        foreach (var city in sim.State.Cities.Where(c => c.Owner == context.Player).OrderBy(c => c.Id))
        {
            var present = service.Present(sim, city.Id);
            if (present.Count == 0) continue;
            items.Add(new Paragraph($"city:{city.Id}", $"{city.Name}: " + string.Join(", ", present.Select(id =>
                Labels.Name<CorporationDef>(sim.Content, id) + (service.Headquarters(sim, id) == city.Id ? "*" : ""))), Tone.Accent));
        }
        return new("Corporations", items);
    }
}

/// <summary>The corporations present in a city on its screen, headquarters marked.</summary>
internal sealed class CorporationsCityPanel : ICityPanel
{
    public string Id => "corporations:city";
    public CityPanelSlot Slot => CityPanelSlot.Status;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city)
    {
        var sim = context.Sim;
        if (sim.Service<ICorporationService>() is not { } service) return [];
        var present = service.Present(sim, city.Id);
        if (present.Count == 0) return [];
        return [new Row("present", present.Select(id =>
        {
            bool hq = service.Headquarters(sim, id) == city.Id;
            string text = Labels.Name<CorporationDef>(sim.Content, id) + (hq ? " ★ headquarters" : "") + (service.IsActive(sim, city.Id, id) ? "" : " (inactive)");
            return (ViewElement)new Paragraph(id, text, hq ? Tone.Accent : Tone.Dim, TextSize.Body, id);
        }).ToList())];
    }
}

/// <summary>Founding, spread, dissolution and displacement of corporations that touch the player.</summary>
internal sealed class CorporationNotices : INotificationSource
{
    public string Id => "corporations:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e)
    {
        string Name(string id) => Labels.Name<CorporationDef>(context.Sim.Content, id);
        return e switch
        {
            CorporationFounded f when context.OwnsCity(f.City) || f.Player == context.Player =>
                new("corporation", $"{Name(f.Corporation)} was founded in {context.CityName(f.City)}", f.City),
            CorporationSpread s when context.OwnsCity(s.City) =>
                new("corporation", $"{Name(s.Corporation)} spread to {context.CityName(s.City)}", s.City),
            CorporationDissolved d when d.Player == context.Player =>
                new("corporation", $"{Name(d.Corporation)} was dissolved with its headquarters"),
            CorporationDisplaced d when context.OwnsCity(d.City) =>
                new("corporation", $"{Name(d.Corporation)} was displaced by {Name(d.By)}", d.City),
            _ => null,
        };
    }
}

/// <summary>Corporations in the encyclopedia, each under the technology that lets a great person found it.</summary>
internal sealed class CorporationPedia : IPediaSource
{
    public string Id => "corporations:pedia";
    public string Category => "Corporations";

    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<CorporationDef>().All.Select(d => new PediaItem(d,
        Notes: d.FoundingTech is { } tech ? [$"Founded with {Labels.Name<Content.Schema.TechDef>(content, tech)}"] : null, UnlockedBy: d.FoundingTech));
}
