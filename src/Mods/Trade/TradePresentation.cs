using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Trade;

/// <summary>A city's trade routes on its screen: the partner, the gold it brings and what kind of route it is.</summary>
internal sealed class TradeCityPanel : ICityPanel
{
    public string Id => "trade:routes";
    public CityPanelSlot Slot => CityPanelSlot.Details;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city)
    {
        var sim = context.Sim;
        if (sim.Service<ITradeRouteService>() is not { } service) return [];
        var routes = service.Calculate(sim, city.Id).Routes;
        var items = new List<ViewElement> { new Heading("heading", $"Trade routes ({routes.Count}/{service.MaxRoutes(sim, city.Id)})") };
        foreach (var route in routes)
        {
            string flags = string.Join(", ", new[] { route.Foreign ? "foreign" : "domestic", route.Overseas ? "overseas" : "" }.Where(x => x.Length > 0));
            string partner = sim.State.FindCity(route.PartnerCity)?.Name ?? "City " + route.PartnerCity;
            items.Add(new Paragraph($"route:{route.PartnerCity}", $"{partner}  ·  {route.ProfitHundredths / 100} gold  ({flags})", Tone.Dim));
        }
        if (routes.Count == 0) items.Add(new Paragraph("none", "No active routes.", Tone.Dim));
        return items;
    }
}
