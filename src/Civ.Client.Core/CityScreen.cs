using Civ.Content;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

public sealed record LedgerRow(string Label, long Amount, bool Percent = false)
{
    public string Text => $"{Labels.Signed(Amount)}{(Percent ? "%" : "")}  {Label}";
}

public sealed record SpecialistRow(string Id, string Name, int Count, int Slots, string Effects);

public sealed record QueueRow(int Index, string Kind, string Id, string Name, long Progress, long Cost, int? Turns, bool IsContinuous);

/// <summary>Something the city could produce. <see cref="Blockers"/> are the requirement failures, empty when it can be queued.</summary>
public sealed record ProductionChoice(string Kind, string Id, string Name, long Cost, int? Turns, IReadOnlyList<string> Blockers, bool IsContinuous)
{
    public bool Available => Blockers.Count == 0;
    public string Tooltip => Available ? $"{Name}: {Cost} production" : string.Join("\n", Blockers);
}

public sealed record TileRow(Hex Hex, string Name, long Food, long Production, long Commerce);

public sealed record HurryOption(string Id, string Name, long Gold, int Population, string? Blocked);

public sealed record ConscriptOption(string UnitId, string Name, int PopulationCost, int MinPopulation, int Experience,
    int AngerTurns, int MaxPlayerDrafts, string? Blocked);

/// <summary>What the city screen shows, computed from the services so every renderer (and the tests) agree on the numbers.</summary>
public sealed record CityScreen(
    int Id, string Name, int Population, long FoodStored, long FoodThreshold, long FoodSurplus, int? TurnsToGrow,
    long Food, long Production, CommerceVector CommercePerTurn, long Maintenance,
    int Happy, int Unhappy, int Angry, int Healthy, int Unhealthy,
    long CultureHundredths, string? CultureLevel, long GreatPeopleProgress, long GreatPeopleThreshold,
    bool Manual, bool AvoidGrowth, bool Disorder,
    IReadOnlyList<LedgerRow> FoodLedger, IReadOnlyList<LedgerRow> ProductionLedger, IReadOnlyList<LedgerRow> CommerceLedger,
    IReadOnlyList<LedgerRow> HappinessLedger, IReadOnlyList<LedgerRow> HealthLedger,
    IReadOnlyList<TileRow> WorkedTiles, int IdleCitizens, IReadOnlyList<SpecialistRow> Specialists,
    IReadOnlyList<QueueRow> Queue, IReadOnlyList<string> Buildings, IReadOnlyList<HurryOption> Hurry,
    IReadOnlyList<CityReligion> Religions, long GreatPeopleRate, IReadOnlyDictionary<string, int> SettledSpecialists,
    int GoldenAgeTurns, IReadOnlyList<TradeRouteResult> TradeRoutes, int MaxTradeRoutes,
    IReadOnlyList<CityCorporation> Corporations, CityStabilityReport? Stability, IReadOnlyList<CityWonderScript> WonderScripts)
{
    public int Citizens => Population;
    public ConscriptOption? Conscript { get; init; }
}

public sealed partial class GameSession
{
    public CityScreen? DescribeCity(City? city)
    {
        if (city == null) return null;
        var yield = Service<ICityYieldService>().Calculate(Sim, city.Id);
        var wellbeing = Service<ICityWellbeingService>().Calculate(Sim, city.Id);
        var growth = Service<ICityGrowthService>().Calculate(Sim, city.Id);
        var commerce = Service<IPlayerEconomyService>().CalculateCity(Sim, city.Id);
        var rules = city.Extensions.Get<CityRuleState>(Sim.Content) ?? new();
        var greatPeopleService = OptionalService<IGreatPeopleService>();
        var greatPeople = greatPeopleService?.Calculate(Sim, city.Id);
        var cityGreatPeople = OptionalService<ICityGreatPeopleService>()?.Calculate(Sim, city.Id);
        var tradeService = OptionalService<ITradeRouteService>();
        var trade = tradeService?.Calculate(Sim, city.Id);
        long perTurn = yield.Yields.Production;
        int? grow = growth.Difference > 0 ? (int?)Math.Max(1, (growth.Threshold - city.FoodStored + growth.Difference - 1) / growth.Difference) : null;
        int idle = Math.Max(0, city.Population - wellbeing.Angry - city.Worked.Count - city.Specialists.Values.Sum());
        return new(city.Id, city.Name, city.Population, city.FoodStored, growth.Threshold, growth.Difference, grow,
            yield.Yields.Food, perTurn, PerTurn(commerce.Hundredths),
            Service<ICityMaintenanceService>().Calculate(Sim, city.Id).TotalHundredths / 100,
            wellbeing.Happy, wellbeing.Unhappy, wellbeing.Angry, wellbeing.Healthy, wellbeing.Unhealthy,
            CultureOf(city), CultureLevelName(city), greatPeople?.Progress ?? rules.GreatPeopleProgress,
            greatPeople?.Threshold ?? cityGreatPeople?.Threshold ?? 0,
            rules.ManualWork, rules.AvoidGrowth, GovernmentStatus.IsDisorder(Sim, city),
            Rows(yield.Ledger, ".food"), Rows(yield.Ledger, ".production"), CommerceRows(yield.Ledger),
            Wellbeing(wellbeing.Ledger, "happiness"), Wellbeing(wellbeing.Ledger, "health"),
            city.Worked.Select(h => TileRowOf(h)).ToList(), idle, SpecialistRows(city),
            QueueRows(city), city.Buildings.Select(b => Sim.Content.Get<Schema.BuildingDef>(b).Name).Order(StringComparer.Ordinal).ToList(),
            HurryOptions(city), ReligionsIn(city), greatPeople?.Rate ?? 0,
            greatPeopleService?.SettledSpecialists(Sim, city.Id) ?? new Dictionary<string, int>(),
            greatPeopleService?.GoldenAgeTurnsRemaining(Sim, city.Owner) ?? 0, trade?.Routes ?? [],
            tradeService?.MaxRoutes(Sim, city.Id) ?? 0, CorporationsIn(city), StabilityOf(city), WonderScriptsIn(city))
        {
            Conscript = ConscriptOptionOf(city),
        };
    }

    private static CommerceVector PerTurn(CommerceVector h) => new(h.Gold / 100, h.Research / 100, h.Culture / 100, h.Espionage / 100);

    private string? CultureLevelName(City city) => OptionalService<ICityCultureService>()?.Level(Sim, city.Id).Name;

    private long CultureOf(City city) => OptionalService<ICityCultureSource>()?.CultureHundredths(Sim, city.Id) ?? 0;

    private IReadOnlyList<LedgerRow> Rows(Ledger ledger, string axis) => ledger.Lines
        .Where(l => l.Key.EndsWith(axis, StringComparison.Ordinal) && l.Key.StartsWith("yield.", StringComparison.Ordinal) && l.Amount != 0)
        .Select(l => new LedgerRow(Labels.Source(Sim, l.Source), l.Amount, l.Key.Contains(".percent."))).ToList();

    private IReadOnlyList<LedgerRow> CommerceRows(Ledger ledger) => ledger.Lines
        .Where(l => (l.Key.StartsWith("commerce.", StringComparison.Ordinal) || l.Key == "yield.flat.commerce") && l.Amount != 0)
        .Select(l => new LedgerRow($"{Labels.Source(Sim, l.Source)} ({l.Key[(l.Key.LastIndexOf('.') + 1)..]})", l.Amount, l.Key.Contains(".percent."))).ToList();

    private IReadOnlyList<LedgerRow> Wellbeing(Ledger ledger, string kind) => ledger.Lines
        .Where(l => l.Key.Contains(kind) && l.Amount != 0 && (l.Key.EndsWith("_good", StringComparison.Ordinal) || l.Key.EndsWith("_bad", StringComparison.Ordinal)))
        .Select(l => new LedgerRow(Labels.Source(Sim, l.Source), l.Key.EndsWith("_bad", StringComparison.Ordinal) ? -l.Amount : l.Amount)).ToList();

    private TileRow TileRowOf(Hex hex)
    {
        var plot = Sim.State.PlotAt(hex);
        var y = Service<IPlotYieldService>().Calculate(Sim, Human, Sim.State.Grid.TileAt(hex)).Yields;
        var name = plot.Feature is { } f ? Sim.Content.Get<Schema.FeatureDef>(f).Name : Sim.Content.Get<Schema.TerrainDef>(plot.Terrain).Name;
        return new(hex, name, y.Food, y.Production, y.Commerce);
    }

    private IReadOnlyList<SpecialistRow> SpecialistRows(City city)
    {
        var rows = new List<SpecialistRow>();
        foreach (var def in Sim.Content.Get<Schema.SpecialistDef>().All.OrderBy(s => s.Id == CitizenSpecialist ? "" : s.Id, StringComparer.Ordinal))
        {
            int count = city.Specialists.GetValueOrDefault(def.Id);
            int slots = SlotsOf(city, def.Id);
            if (slots > 0 || count > 0) rows.Add(new(def.Id, def.Name, count, slots, SpecialistEffects(def)));
        }
        return rows;
    }

    /// <summary>Open slots for a specialist, probed through the work service's own validation.</summary>
    private int SlotsOf(City city, string id)
    {
        if (id == CitizenSpecialist) return city.Population;
        var work = Service<ICityWorkService>();
        int slots = 0;
        while (slots < city.Population && work.Validate(Sim, city.Id, [], new Dictionary<string, int> { [id] = slots + 1 }) == null) slots++;
        return slots;
    }

    public static string SpecialistEffects(Schema.SpecialistDef s) =>
        string.Join(", ", new[] { Labels.YieldText(s.Yields.Food, s.Yields.Production, s.Yields.Commerce, s.Commerces.Culture),
            s.Commerces.Gold != 0 ? $"{Labels.Signed(s.Commerces.Gold)} gold" : "", s.Commerces.Research != 0 ? $"{Labels.Signed(s.Commerces.Research)} research" : "" }
            .Where(t => t.Length > 0));

    public IReadOnlyList<IProductionKind> ProductionKinds => Sim.Content.Services.Get<IProductionKind>();

    private IProductionKind KindOf(string kind) => ProductionKinds.Single(k => k.Id == kind);

    private IReadOnlyList<QueueRow> QueueRows(City city)
    {
        var rows = new List<QueueRow>();
        for (int i = 0; i < city.ProductionQueue.Count; i++)
        {
            var order = city.ProductionQueue[i];
            var kind = KindOf(order.Kind);
            var calc = kind.Calculate(Sim, city.Id, order.ContentId);
            rows.Add(new(i, order.Kind, order.ContentId, kind.Definition(Sim.Content, order.ContentId).Name, order.Progress, calc.Cost,
                kind.IsContinuous ? null : TurnsToFinish(calc.Cost - order.Progress, calc.Production), kind.IsContinuous));
        }
        return rows;
    }



    private ConscriptOption? ConscriptOptionOf(City city)
    {
        var service = OptionalService<ICityConscriptService>();
        if (service?.Calculate(Sim, city.Id) is not { } result) return null;
        return new(result.UnitId, Sim.Content.Get<Schema.UnitDef>(result.UnitId).Name, result.PopulationCost,
            result.MinPopulation, result.Experience, result.AngerTurns, result.MaxPlayerDrafts,
            service.Validate(Sim, Human, city.Id));
    }

    private IReadOnlyList<HurryOption> HurryOptions(City city)
    {
        var service = OptionalService<ICityHurryService>();
        if (service == null || city.ProductionQueue.Count == 0) return [];
        return Sim.Content.Get<Schema.HurryDef>().All.OrderBy(h => h.Id, StringComparer.Ordinal).Select(h =>
        {
            var cost = service.Calculate(Sim, city.Id, h.Id);
            return new HurryOption(h.Id, h.Name, cost.Gold, cost.Population, service.Validate(Sim, Human, city.Id, h.Id));
        }).ToList();
    }



    private readonly Dictionary<string, IReadOnlyList<(Content.ContentDef Def, IReadOnlyList<string> Blockers)>> _eligibility = new();
    private (int Revision, int City) _eligibilityKey = (-1, -1);

    /// <summary>Every building, unit or process (by <paramref name="kind"/>) the city could ever queue, with the reasons it cannot be queued now;
    /// structural impossibilities are left out. Requirement checks are reused until the simulation changes; costs and turns are always current.</summary>
    public IReadOnlyList<ProductionChoice> ProductionChoices(City city, string kind)
    {
        var key = (WorldRevision, city.Id);
        if (key != _eligibilityKey) { _eligibility.Clear(); _eligibilityKey = key; }
        if (!_eligibility.TryGetValue(kind, out var eligible)) _eligibility[kind] = eligible = CheckEligibility(city, kind);
        var production = KindOf(kind);
        return eligible.Select(e => e.Blockers.Count > 0
            ? new ProductionChoice(kind, e.Def.Id, e.Def.Name, production.BaseCost(e.Def), null, e.Blockers, production.IsContinuous)
            : Priced(city, production, e.Def)).ToList();
    }

    /// <summary>All registered kinds together.</summary>
    public IReadOnlyList<ProductionChoice> ProductionChoices(City city) =>
        ProductionKinds.SelectMany(k => ProductionChoices(city, k.Id)).ToList();

    private IReadOnlyList<(Content.ContentDef, IReadOnlyList<string>)> CheckEligibility(City city, string kind)
    {
        var production = KindOf(kind);
        var defs = production.Candidates(Sim.Content);
        var result = new List<(Content.ContentDef, IReadOnlyList<string>)>();
        foreach (var def in defs.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var eligibility = production.Validate(Sim, city.Id, def.Id);
            var blockers = eligibility.Failures.Select(f => f.Explanation).Distinct().ToList();
            if (production.IsVisible(eligibility)) result.Add((def, blockers));
        }
        return result;
    }

    private ProductionChoice Priced(City city, IProductionKind production, Content.ContentDef def)
    {
        var calc = production.Calculate(Sim, city.Id, def.Id);
        int? turns = production.IsContinuous ? null : TurnsToFinish(calc.Cost - ProgressOf(city, production.Id, def.Id), calc.Production);
        return new(production.Id, def.Id, def.Name, calc.Cost, turns, [], production.IsContinuous);
    }

    private long ProgressOf(City city, string kind, string id)
    {
        var state = city.Extensions.Get<CityRuleState>(Sim.Content);
        return state?.PausedProduction.GetValueOrDefault(kind + "/" + id) ?? 0;
    }
}
