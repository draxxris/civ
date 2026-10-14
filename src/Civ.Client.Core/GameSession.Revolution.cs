using Civ.Content;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

public sealed record CityStabilityReport(int City, int Index, CityStabilityStatus Status, int ChangePerTurn, int CooldownTurns,
    int UnrestTurns, IReadOnlyList<LedgerRow> Ledger, bool CanSuppress, string? SuppressBlocked);

public sealed record CityStabilitySummary(int City, string Name, int Index, CityStabilityStatus Status, int ChangePerTurn);

public sealed record CityWonderScript(string Building, string Name, bool Active, int ElapsedTurns, int SpawnInterval,
    string? SpawnUnit, int? TurnsToSpawn);

public sealed partial class GameSession
{
    private CityStabilityReport? StabilityOf(City city)
    {
        var service = OptionalService<IRevolutionService>();
        if (service == null) return null;
        var d = service.DescribeCityStability(Sim, city.Id);
        string? blocked = city.Owner == Human ? service.ValidateSuppression(Sim, Human, city.Id, null) : "Not your city";
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

    /// <summary>Gold-based suppression of a city's rebellion.</summary>
    public bool SuppressRebellion(int cityId) => Submit(new SuppressRebellionCommand { Player = Human, CityId = cityId });

    /// <summary>Stability of every city the local player owns; empty when no revolution rules are loaded.</summary>
    public IReadOnlyList<CityStabilitySummary> DescribeStability()
    {
        if (OptionalService<IRevolutionService>() == null) return [];
        return Sim.State.Cities.Where(c => c.Owner == Human).OrderBy(c => c.Id)
            .Select(c => StabilityOf(c)!).Select(s => new CityStabilitySummary(s.City, CityName(s.City), s.Index, s.Status, s.ChangePerTurn)).ToList();
    }
}
