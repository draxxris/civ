using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.GreatPeople;

/// <summary>A city's great people figure on its screen, and the great people settled in it.</summary>
internal sealed class GreatPeopleCityStats : ICityPanel
{
    public string Id => "great_people:city";
    public CityPanelSlot Slot => CityPanelSlot.Stats;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city)
    {
        if (context.Sim.Service<IGreatPeopleService>() is not { } service) return [];
        var people = service.Calculate(context.Sim, city.Id);
        return [new StatLine("great_people", "Great people",
            people.Threshold > 0 ? $"{people.Progress}/{people.Threshold}  ·  +{people.Rate}/turn" : "—")];
    }
}

internal sealed class SettledGreatPeoplePanel : ICityPanel
{
    public string Id => "great_people:settled";
    public CityPanelSlot Slot => CityPanelSlot.Details;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city)
    {
        var settled = context.Sim.Service<IGreatPeopleService>()?.SettledSpecialists(context.Sim, city.Id);
        if (settled is null || settled.Count == 0) return [];
        var items = new List<ViewElement> { new Heading("heading", "Settled great people") };
        foreach (var (id, count) in settled.OrderBy(p => p.Key, StringComparer.Ordinal))
            items.Add(new Paragraph(id, $"{Labels.Name<SpecialistDef>(context.Sim.Content, id)} ×{count}", Tone.Dim));
        return items;
    }
}

/// <summary>The top bar's golden age countdown.</summary>
internal sealed class GoldenAgeSummary : IPlayerSummaryItem
{
    public string Id => "great_people:golden_age";

    public ViewElement? Describe(ScreenContext context)
    {
        int turns = context.Sim.Service<IGreatPeopleService>()?.GoldenAgeTurnsRemaining(context.Sim, context.Player) ?? 0;
        return turns > 0 ? new Paragraph("golden_age", $"Golden Age · {turns} turns", Tone.Accent) : null;
    }
}
