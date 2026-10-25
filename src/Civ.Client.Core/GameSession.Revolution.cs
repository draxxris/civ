using Civ.Content;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

public sealed record CityStabilityReport(int City, int Index, CityStabilityStatus Status, int ChangePerTurn, int CooldownTurns,
    int UnrestTurns, IReadOnlyList<LedgerRow> Ledger, bool CanSuppress, string? SuppressBlocked);

public sealed record CityWonderScript(string Building, string Name, bool Active, int ElapsedTurns, int SpawnInterval,
    string? SpawnUnit, int? TurnsToSpawn);

public sealed partial class GameSession
{
    private CityStabilityReport? StabilityOf(City city)
    {
        var service = OptionalService<IRevolutionService>();
        if (service == null) return null;
        var d = service.DescribeCityStability(Sim, city.Id);
        string? blocked = city.Owner == Human ? SuppressionBlocked(service, city) : "Not your city";
        var rows = d.Ledger.Lines.Where(l => l.Amount != 0).Select(l => new LedgerRow(Labels.Source(Sim, l.Source), l.Amount)).ToList();
        return new(city.Id, d.InstabilityIndex, d.Status, d.ChangePerTurn, d.CooldownTurns, d.UnrestTurns, rows, blocked == null, blocked);
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

    /// <summary>Pay gold to suppress a city's rebellion, or spend a garrison when gold cannot pay.</summary>
    public bool SuppressRebellion(int cityId)
    {
        var service = OptionalService<IRevolutionService>();
        var city = Sim.State.FindCity(cityId);
        int? garrison = service == null || city == null ? null : SuppressionGarrison(service, city);
        return Submit(new SuppressRebellionCommand { Player = Human, CityId = cityId, Garrison = garrison });
    }

    /// <summary>The garrison that suppresses instead of gold. Null means gold pays, or nothing can.</summary>
    private int? SuppressionGarrison(IRevolutionService service, City city)
    {
        if (city.Owner != Human || service.ValidateSuppression(Sim, Human, city.Id, null) is null) return null;
        return Sim.State.UnitsAt(city.Position).Where(u => u.Owner == Human).OrderBy(u => u.Id)
            .Where(u => service.ValidateSuppression(Sim, Human, city.Id, u.Id) is null).Select(u => (int?)u.Id).FirstOrDefault();
    }

    /// <summary>Why the player cannot suppress this city now, or null when gold or a garrison can.</summary>
    private string? SuppressionBlocked(IRevolutionService service, City city) =>
        SuppressionGarrison(service, city) is null ? service.ValidateSuppression(Sim, Human, city.Id, null) : null;
}
