using Ariel.Game;

namespace Ariel.Client;

/// <summary>What one slot of the city screen holds from the mods: one section per contributing panel, in registration
/// order, each under the panel's id.</summary>
public sealed record CityPanels(IReadOnlyList<ViewElement> Stats, IReadOnlyList<ViewElement> Status, IReadOnlyList<ViewElement> Details)
{
    public static readonly CityPanels None = new([], [], []);
}

public sealed partial class GameSession
{
    private ScreenHost? _screens;

    /// <summary>The screens mods register, and the one the player has open.</summary>
    public ScreenHost Screens => _screens ??= new(this);

    /// <summary>The mods' rows for a city's screen, grouped by slot.</summary>
    public CityPanels CityPanelsOf(City city)
    {
        var panels = Sim.Content.Services.Get<ICityPanel>();
        if (panels.Count == 0) return CityPanels.None;
        var context = Screens.ContextFor(null) with { City = city.Id };
        List<ViewElement> stats = [], status = [], details = [];
        foreach (var panel in panels)
        {
            var items = panel.Describe(context, city);
            if (items.Count == 0) continue;
            var into = panel.Slot switch { CityPanelSlot.Stats => stats, CityPanelSlot.Status => status, _ => details };
            into.Add(new Section(panel.Id, null, items));
        }
        return new(stats, status, details);
    }

    /// <summary>The mods' items for the top bar's player summary, in registration order.</summary>
    public IReadOnlyList<ViewElement> SummaryItems()
    {
        var items = Sim.Content.Services.Get<IPlayerSummaryItem>();
        if (items.Count == 0) return [];
        var context = Screens.ContextFor(null);
        var result = new List<ViewElement>(items.Count);
        foreach (var item in items) if (item.Describe(context) is { } element) result.Add(element);
        return result;
    }
}
