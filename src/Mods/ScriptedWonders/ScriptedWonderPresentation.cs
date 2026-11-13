using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.ScriptedWonders;

/// <summary>Each scripted wonder of a city on its screen: whether it works, and when it next sends a unit.</summary>
internal sealed class WonderCityPanel : ICityPanel
{
    public string Id => "scripted_wonders:city";
    public CityPanelSlot Slot => CityPanelSlot.Status;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city)
    {
        var sim = context.Sim;
        if (sim.Service<IScriptedWonderService>() is not { } service) return [];
        return service.DescribeCity(sim, city.Id).Select(w =>
        {
            string line = !w.Active ? "inactive"
                : w.SpawnUnit is { } unit && w.SpawnInterval > 0
                    ? $"next {Labels.Name<UnitDef>(sim.Content, unit)} in {Math.Max(0, w.SpawnInterval - w.ElapsedTurns)} turns"
                    : "active";
            return (ViewElement)new Paragraph(w.Building, $"{Labels.Name<BuildingDef>(sim.Content, w.Building)}: {line}", Tone.Dim);
        }).ToList();
    }
}

/// <summary>What a scripted wonder of the player did this turn.</summary>
internal sealed class WonderNotices : INotificationSource
{
    public string Id => "scripted_wonders:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e is ScriptedWonderEffectApplied w && context.OwnsCity(w.City)
        ? new("wonder", $"{Labels.Name<BuildingDef>(context.Sim.Content, w.Building)} in {context.CityName(w.City)}: {Labels.Humanize(w.Effect)}", w.City)
        : null;
}
