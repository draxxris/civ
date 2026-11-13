using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Revolution;

/// <summary>A city's stability on its screen, with both suppression routes priced separately and never chosen for the
/// player: gold, or an owned military garrison's remaining movement.</summary>
internal sealed class StabilityCityPanel : ICityPanel
{
    public string Id => "revolution:stability";
    public CityPanelSlot Slot => CityPanelSlot.Status;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city)
    {
        var sim = context.Sim;
        if (sim.Service<IRevolutionService>() is not { } service) return [];
        var d = service.DescribeCityStability(sim, city.Id);
        bool mine = city.Owner == context.Player;
        bool gold = mine && service.ValidateSuppression(sim, context.Player, city.Id, null) is null;
        int? garrison = mine ? Garrison(sim, service, context.Player, city) : null;
        string extra = d.CooldownTurns > 0 ? $", rebellion cooldown {d.CooldownTurns} turns" : "";
        var items = new List<ViewElement>
        {
            new Heading("status", $"Stability: {d.Status} ({d.InstabilityIndex})"),
            new Paragraph("change", $"{Labels.Signed(d.ChangePerTurn)} per turn{extra}", ToneOf(d.Status)),
        };
        foreach (var (line, i) in d.Ledger.Lines.Where(l => l.Amount != 0).Select((l, i) => (l, i)))
            items.Add(new Paragraph($"ledger:{i}", $"{Labels.Signed(line.Amount)}  {Labels.Source(sim, line.Source)}", Tone.Dim, TextSize.Small));
        if (gold)
            items.Add(new ActionButton("suppress_gold", $"Suppress Rebellion ({d.SuppressionGold} gold)",
                new SubmitCommand(new SuppressRebellionCommand { Player = context.Player, CityId = city.Id }),
                Tooltip: "Pay to reduce instability by up to the full suppression amount"));
        if (garrison is int unit)
            items.Add(new ActionButton("suppress_garrison", "Suppress with garrison",
                new SubmitCommand(new SuppressRebellionCommand { Player = context.Player, CityId = city.Id, Garrison = unit }),
                Tooltip: "Spend a military garrison's remaining movement instead of gold"));
        string? blocked = !mine ? "Not your city" : gold || garrison is not null ? null : service.ValidateSuppression(sim, context.Player, city.Id, null);
        if (blocked is not null) items.Add(new Paragraph("blocked", blocked, Tone.Dim, TextSize.Small));
        return items;
    }

    private static Tone ToneOf(CityStabilityStatus status) => status switch
    {
        CityStabilityStatus.Rebellion => Tone.Bad,
        CityStabilityStatus.Mutiny => Tone.Warn,
        CityStabilityStatus.Warning => Tone.Accent,
        _ => Tone.Dim,
    };

    /// <summary>The garrison that suppresses instead of gold, when one qualifies: the lowest unit id that may.</summary>
    private static int? Garrison(Simulation sim, IRevolutionService service, int player, City city)
    {
        foreach (var unit in sim.State.UnitsAt(city.Position).Where(u => u.Owner == player).OrderBy(u => u.Id))
            if (service.ValidateSuppression(sim, player, city.Id, unit.Id) is null) return unit.Id;
        return null;
    }
}

/// <summary>The top bar's count of the player's cities at or above the warning tier.</summary>
internal sealed class UnrestSummary : IPlayerSummaryItem
{
    public string Id => "revolution:unrest";

    public ViewElement? Describe(ScreenContext context)
    {
        var risk = context.Sim.Service<IRevolutionService>()?.CitiesAtRisk(context.Sim, context.Player) ?? [];
        return risk.Count == 0 ? null
            : new Paragraph("unrest", $"Unrest · {risk.Count} {(risk.Count == 1 ? "city" : "cities")}", Tone.Warn);
    }
}

/// <summary>Rebellions in and suppressions of the player's cities.</summary>
internal sealed class RevolutionNotices : INotificationSource
{
    public string Id => "revolution:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e switch
    {
        CityRebellionTriggered r when r.Player == context.Player || context.OwnsCity(r.City) =>
            new("rebellion", $"{context.CityName(r.City)} rebelled: {r.Units.Count} rebel units appeared", r.City, Tone: Tone.Warn),
        CityRebellionSuppressed r when r.Player == context.Player =>
            new("rebellion", $"Rebellion in {context.CityName(r.City)} was suppressed", r.City),
        _ => null,
    };
}
