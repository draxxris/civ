using Ariel.Content;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Corporations;

/// <summary>Every corporation with its founding state, and what each of the player's cities hosts. It says which way
/// guilds and corporations work in this game (<see cref="CorporationMode"/>), and for an unfounded one what founds it.</summary>
internal sealed class CorporationsScreen(NaturalCorporations natural) : IGameScreen
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
        bool realistic = CorporationMode.Realistic(sim);
        string option = CorporationMode.OptionName(sim.Content);
        var list = new List<ViewElement>();
        var pass = realistic ? natural.BeginPass(sim) : null;
        foreach (var def in sim.Content.Get<CorporationDef>().All.OrderBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            int? hq = service.Headquarters(sim, def.Id);
            int branches = sim.State.Cities.Count(c => service.Present(sim, c.Id).Contains(def.Id));
            string seat = def.Seat;
            string state = hq is int city ? $"{seat} {sim.State.FindCity(city)?.Name ?? "?"}, {branches} cities" + (realistic ? ", spreads on its own" : "")
                : realistic ? "not founded: founded automatically when " + CorporationService.FoundingCondition(sim.Content, def)
                    + (natural.FoundingSite(sim, context.Player, def, pass: pass) is { } site ? $"; your {site.Name} may found it" : "")
                : "not founded" + (CorporationFounder.Headquarters(sim.Content, def.Id) is { } building ? $" (build its {seat}, {building.Name})" : "");
            if (CorporationService.Obsolete(sim, context.Player, def)) state += "; obsolete for you";
            list.Add(new ListItem(def.Id, def.Name, state, def.Id));
        }
        string mode = realistic
            ? $"{option}: guilds and corporations are founded and spread on their own. A nation that knows one's technology and has its resource may found it; branches open in nearby cities that have the resource and close when it is lost. Executives are not used."
            : $"{option} is off: found a guild or corporation by building its Master Guild or headquarters, and spread it with executives and gold.";
        var items = new List<ViewElement> { new Paragraph("mode", mode, Tone.Dim), new Section("corporations", null, list) };
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
            string text = Labels.Name<CorporationDef>(sim.Content, id) + (hq ? " ★ " + sim.Content.Get<CorporationDef>(id).Seat : "") + (service.IsActive(sim, city.Id, id) ? "" : " (inactive)")
                + Closing(sim, city, id);
            return (ViewElement)new Paragraph(id, text, hq ? Tone.Accent : Tone.Dim, TextSize.Body, id);
        }).ToList())];
    }

    /// <summary>With Realistic Guilds/Corporations, how long a branch without its resource (or a headquarters whose
    /// nation has none) has before it closes.</summary>
    private static string Closing(Simulation sim, City city, string corporation)
    {
        var timers = sim.Rules().Timers;
        var holder = TimerHolder.OfCity(city.Id);
        var timer = timers.Find(sim, NaturalCorporations.BranchDecayKind, holder, corporation)
            ?? timers.Find(sim, NaturalCorporations.HeadquartersDecayKind, holder, corporation);
        return timer?.TurnsRemaining(sim.State.Turn) is int turns ? $" (closes in {turns} turns without its resource)" : "";
    }
}

/// <summary>Founding, spread, dissolution, displacement, obsolescence and withdrawal of corporations that touch the player.</summary>
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
                new("corporation", $"{Name(d.Corporation)} was dissolved with its {context.Sim.Content.Get<CorporationDef>(d.Corporation).Seat}"),
            CorporationDisplaced d when context.OwnsCity(d.City) =>
                new("corporation", $"{Name(d.Corporation)} was displaced by {Name(d.By)}", d.City),
            CorporationObsolete o when context.OwnsCity(o.City) =>
                new("corporation", $"{Name(o.Corporation)} closed in {context.CityName(o.City)}: it is obsolete", o.City),
            CorporationWithdrawn w when context.OwnsCity(w.City) =>
                new("corporation", $"{Name(w.Corporation)} closed in {context.CityName(w.City)}: its resource is no longer connected", w.City),
            _ => null,
        };
    }
}

/// <summary>Corporations and guilds in the encyclopedia, each with the headquarters or Master Guild that founds it, under
/// the technology that makes that building available.</summary>
internal sealed class CorporationPedia : IPediaSource
{
    public string Id => "corporations:pedia";
    public string Category => "Corporations";

    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<CorporationDef>().All.Select(d =>
    {
        var building = CorporationFounder.Headquarters(content, d.Id);
        var notes = Notes(content, d, building).ToList();
        return new PediaItem(d, Notes: notes.Count == 0 ? null : notes, UnlockedBy: building is null ? null : FoundingTech(building));
    });

    /// <summary>The rules of a corporation the player reads in its entry, beyond its output.</summary>
    internal static IEnumerable<string> Notes(ContentDb content, CorporationDef d, Ariel.Content.Schema.BuildingDef? building)
    {
        string option = CorporationMode.OptionName(content);
        yield return $"With {option}: founded automatically when {CorporationService.FoundingCondition(content, d)}; "
            + $"spreads on its own to nearby cities with its resource (spread {d.NaturalSpreadPercent}%)";
        if (building is not null) yield return $"Without {option}: {d.SeatTitle()} founded by building {building.Name}; spread by executives and gold";
        var rivals = content.Get<CorporationDef>().All.Where(o => CorporationService.Competes(d, o))
            .Select(o => o.Name).Order(StringComparer.Ordinal).ToArray();
        if (rivals.Length > 0) yield return "Competes with " + string.Join(", ", rivals) + ": a branch of one evicts the other";
    }

    /// <summary>The first technology the founding building requires, which is where the encyclopedia files it.</summary>
    private static string? FoundingTech(Ariel.Content.Schema.BuildingDef building) =>
        ContentReferences.Leaves(building.Requirements ?? new Requirement.All([])).FirstOrDefault(l => l.Kind == "tech")?.Id;
}
