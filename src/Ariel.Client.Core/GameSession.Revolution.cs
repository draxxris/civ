using Ariel.Content;
using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>Instability of one city, with both suppression routes priced separately and never chosen for the player.</summary>
public sealed record CityStabilityReport(int City, int Index, CityStabilityStatus Status, int ChangePerTurn, int CooldownTurns,
    int UnrestTurns, IReadOnlyList<LedgerRow> Ledger, long SuppressGold, bool CanSuppressGold, bool CanSuppressGarrison,
    string? SuppressBlocked);

public sealed record CityWonderScript(string Building, string Name, bool Active, int ElapsedTurns, int SpawnInterval,
    string? SpawnUnit, int? TurnsToSpawn);

public sealed partial class GameSession
{
    /// <summary>Own cities at or above the warning tier, lowest id first. Saved instability only, no ledger per city.</summary>
    public IReadOnlyList<int> CitiesAtRisk() => OptionalService<IRevolutionService>()?.CitiesAtRisk(Sim, Human) ?? [];

    private CityStabilityReport? StabilityOf(City city)
    {
        var service = OptionalService<IRevolutionService>();
        if (service == null) return null;
        var d = service.DescribeCityStability(Sim, city.Id);
        var rows = d.Ledger.Lines.Where(l => l.Amount != 0).Select(l => new LedgerRow(Labels.Source(Sim, l.Source), l.Amount)).ToList();
        bool gold = city.Owner == Human && service.ValidateSuppression(Sim, Human, city.Id, null) is null;
        bool garrison = city.Owner == Human && GarrisonFor(service, city) is not null;
        string? blocked = city.Owner != Human ? "Not your city"
            : gold || garrison ? null : service.ValidateSuppression(Sim, Human, city.Id, null);
        return new(city.Id, d.InstabilityIndex, d.Status, d.ChangePerTurn, d.CooldownTurns, d.UnrestTurns, rows,
            d.SuppressionGold, gold, garrison, blocked);
    }

    private IReadOnlyList<CityWonderScript> WonderScriptsIn(City city)
    {
        var service = OptionalService<IScriptedWonderService>();
        if (service == null) return [];
        return service.DescribeCity(Sim, city.Id).Select(w => new CityWonderScript(w.Building,
            Sim.Content.TryGet<Schema.BuildingDef>(w.Building, out var def) ? def.Name : Labels.Humanize(w.Building),
            w.Active, w.ElapsedTurns, w.SpawnInterval, w.SpawnUnit,
            w.Active && w.SpawnUnit != null && w.SpawnInterval > 0 ? Math.Max(0, w.SpawnInterval - w.ElapsedTurns) : null)).ToList();
    }

    /// <summary>Pay gold to suppress a city's rebellion. The price falls with the instability actually removed.</summary>
    public bool SuppressRebellion(int cityId) => OptionalService<IRevolutionService>() is not null
        && Submit(new SuppressRebellionCommand { Player = Human, CityId = cityId });

    /// <summary>Spend an owned military garrison's remaining movement instead of gold.</summary>
    public bool SuppressRebellionWithGarrison(int cityId)
    {
        var service = OptionalService<IRevolutionService>();
        var city = Sim.State.FindCity(cityId);
        if (service is null || city is null) { SetMessage("No revolution rules loaded"); return false; }
        if (GarrisonFor(service, city) is not { } garrison) { SetMessage("No garrison can suppress this city"); return false; }
        return Submit(new SuppressRebellionCommand { Player = Human, CityId = cityId, Garrison = garrison });
    }

    /// <summary>The garrison that suppresses instead of gold, when one qualifies.</summary>
    private int? GarrisonFor(IRevolutionService service, City city)
    {
        if (city.Owner != Human) return null;
        foreach (var unit in Sim.State.UnitsAt(city.Position).Where(u => u.Owner == Human).OrderBy(u => u.Id))
            if (service.ValidateSuppression(Sim, Human, city.Id, unit.Id) is null) return unit.Id;
        return null;
    }
}
