using Ariel.Content;
using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

public sealed record LedgerRow(string Label, long Amount, bool Percent = false)
{
    public string Text => $"{Labels.Signed(Amount)}{(Percent ? "%" : "")}  {Label}";
}

/// <summary>What a city's map nameplate shows: growth and production fractions (0..1) and the item being built.</summary>
public sealed record CityPlate(string Name, int Population, float Growth, float? Production, string? ProductionId, int? TurnsToGrow, int? TurnsToFinish);

public sealed record SpecialistRow(string Id, string Name, int Count, int Slots, string Effects);

public sealed record QueueRow(int Index, string Kind, string Id, string Name, long Progress, long Cost, int? Turns, bool IsContinuous);

/// <summary>Something the city could produce. <see cref="Blockers"/> are the requirement failures, empty when it can be queued.</summary>
public sealed record ProductionChoice(string Kind, string Id, string Name, long Cost, int? Turns, IReadOnlyList<string> Blockers, bool IsContinuous)
{
    public bool Available => Blockers.Count == 0;
    public string Tooltip => Available ? $"{Name}: {Cost} production" : string.Join("\n", Blockers);
    /// <summary>Whether the choice belongs in a list filtered by <paramref name="query"/>. A blank query matches
    /// everything, and case is ignored so a player need not match the content's capitals.</summary>
    public bool Matches(string query) => Name.Contains(query, StringComparison.OrdinalIgnoreCase);
}

/// <summary>One tab of the city screen's production list. A null <see cref="Kind"/> is the tab that gathers every kind;
/// a null <see cref="Category"/> is a kind's own tab, and otherwise the tab holds one of the kind's categories. The record
/// itself is the tab's identity for the renderer, so nothing encodes it into a string.</summary>
public sealed record ProductionTab(string? Kind, string? Category, string Label)
{
    public static readonly ProductionTab All = new(null, null, "All");
}

/// <summary>The rows a tab draws and how many candidates the name filter matched. <see cref="Matches"/> counts the hidden
/// ones too, so the screen can tell "nothing matches" apart from "nothing available matches".</summary>
public sealed record ProductionList(IReadOnlyList<ProductionChoice> Rows, int Matches);
public sealed record TileRow(Hex Hex, string Name, long Food, long Production, long Commerce);

public sealed record HurryOption(string Id, string Name, long Gold, int Population, string? Blocked);

public sealed record ConscriptOption(string UnitId, string Name, int PopulationCost, int MinPopulation, int Experience,
    int AngerTurns, int MaxPlayerDrafts, string? Blocked);

public sealed record PendingCityConquest(int CityId, string Name, int Population, int OccupationTurns, bool CanRaze);
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
    public bool IsPuppet { get; init; }
    public int OccupationTurns { get; init; }
    public bool PendingConquestChoice { get; init; }
    public bool CanAnnex => IsPuppet || PendingConquestChoice;
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
        var rules = RulesOf(city);
        var greatPeople = GreatPeopleOf(city);
        var cityGreatPeople = CityGreatPeopleOf(city);
        var trade = TradeOf(city);
        long perTurn = yield.Yields.Production;
        int? grow = TurnsToGrow(city, growth);
        int idle = Math.Max(0, city.Population - wellbeing.Angry - city.Worked.Count - city.Specialists.Values.Sum());
        return new(city.Id, city.Name, city.Population, city.FoodStored, growth.Threshold, growth.Difference, grow,
            yield.Yields.Food, perTurn, PerTurn(commerce.Hundredths),
            Service<ICityMaintenanceService>().Calculate(Sim, city.Id).TotalHundredths / 100,
            wellbeing.Happy, wellbeing.Unhappy, wellbeing.Angry, wellbeing.Healthy, wellbeing.Unhealthy,
            CultureOf(city), CultureLevelName(city), GreatPeopleProgress(greatPeople, rules),
            GreatPeopleThreshold(greatPeople, cityGreatPeople),
            rules.ManualWork, rules.AvoidGrowth, GovernmentStatus.IsDisorder(Sim, city),
            Rows(yield.Ledger, ".food"), Rows(yield.Ledger, ".production"), CommerceRows(yield.Ledger),
            Wellbeing(wellbeing.Ledger, "happiness"), Wellbeing(wellbeing.Ledger, "health"),
            city.Worked.Select(h => TileRowOf(h)).ToList(), idle, SpecialistRows(city),
            QueueRows(city), BuildingNames(city), HurryOptions(city), ReligionsIn(city),
            GreatPeopleRate(greatPeople), SettledSpecialists(city), GoldenAgeTurns(city), TradeRoutes(trade),
            MaxTradeRoutes(city), CorporationsIn(city), StabilityOf(city), WonderScriptsIn(city))
        {
            Conscript = ConscriptOptionOf(city),
            IsPuppet = rules.IsPuppet,
            OccupationTurns = rules.OccupationTurns,
            PendingConquestChoice = rules.PendingConquestChoice,
        };
    }

    /// <summary>
    /// <paramref name="known"/> may be a map-only observation, which carries no food or queue. Own cities show their live
    /// growth and production; other civilizations' cities show only the name and size.
    /// </summary>
    public CityPlate PlateOf(City known)
    {
        var city = Sim.State.Cities.FirstOrDefault(c => c.Id == known.Id);
        if (city == null || city.Owner != Human) return new(known.Name, known.Population, 0f, null, null, null, null);
        var growth = Service<ICityGrowthService>().Calculate(Sim, city.Id);
        float food = growth.Threshold > 0 ? Math.Clamp((float)city.FoodStored / growth.Threshold, 0f, 1f) : 0f;
        var row = QueueRows(city).FirstOrDefault();
        float? production = row == null || row.IsContinuous || row.Cost <= 0 ? null : Math.Clamp((float)row.Progress / row.Cost, 0f, 1f);
        return new(city.Name, city.Population, food, production, row?.Id, TurnsToGrow(city, growth), row?.Turns);
    }

    private CityRuleState RulesOf(City city) => city.Extensions.Get<CityRuleState>(Sim.Content) ?? new();

    private GreatPeopleCityResult? GreatPeopleOf(City city) => OptionalService<IGreatPeopleService>()?.Calculate(Sim, city.Id);

    private CityGreatPeopleResult? CityGreatPeopleOf(City city) => OptionalService<ICityGreatPeopleService>()?.Calculate(Sim, city.Id);

    private CityTradeResult? TradeOf(City city) => OptionalService<ITradeRouteService>()?.Calculate(Sim, city.Id);

    private static int? TurnsToGrow(City city, CityGrowthResult growth) => growth.Difference > 0
        ? (int?)Math.Max(1, (growth.Threshold - city.FoodStored + growth.Difference - 1) / growth.Difference) : null;

    private static long GreatPeopleProgress(GreatPeopleCityResult? people, CityRuleState rules) => people?.Progress ?? rules.GreatPeopleProgress;

    private static long GreatPeopleThreshold(GreatPeopleCityResult? people, CityGreatPeopleResult? cityPeople) =>
        people?.Threshold ?? cityPeople?.Threshold ?? 0;

    private static long GreatPeopleRate(GreatPeopleCityResult? people) => people?.Rate ?? 0;

    private IReadOnlyDictionary<string, int> SettledSpecialists(City city) =>
        OptionalService<IGreatPeopleService>()?.SettledSpecialists(Sim, city.Id) ?? new Dictionary<string, int>();

    private int GoldenAgeTurns(City city) => OptionalService<IGreatPeopleService>()?.GoldenAgeTurnsRemaining(Sim, city.Owner) ?? 0;

    private static IReadOnlyList<TradeRouteResult> TradeRoutes(CityTradeResult? trade) => trade?.Routes ?? [];

    private int MaxTradeRoutes(City city) => OptionalService<ITradeRouteService>()?.MaxRoutes(Sim, city.Id) ?? 0;

    private IReadOnlyList<string> BuildingNames(City city) =>
        (OptionalService<ICityBuildingService>()?.EffectiveBuildings(Sim, city.Id) ?? city.Buildings)
            .Select(id => Sim.Content.Get<Schema.BuildingDef>(id).Name).Order(StringComparer.Ordinal).ToList();

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



    /// <summary>One candidate's requirement check, its price, and the content definition both were worked out from. A
    /// requirement failure leaves the price unset, because a blocked candidate cannot be queued and its cost is not
    /// drawn anywhere but the tooltip.</summary>
    private readonly record struct Candidate(Content.ContentDef Def, ProductionChoice Choice);

    /// <summary>Requirement checks and prices, keyed by the kind they belong to. Both are functions of the simulation
    /// alone, so the cache lives exactly as long as <see cref="WorldRevision"/>: a queue edit does not touch it, and
    /// the queued-copy notes are overlaid on every call because a queue can change without a price changing.</summary>
    private readonly Dictionary<string, IReadOnlyList<Candidate>> _choices = new();
    private (int Revision, int City) _choicesKey = (-1, -1);

    /// <summary>Every building, unit or process (by <paramref name="kind"/>) the city could ever queue, with the reasons it cannot be queued now;
    /// structural impossibilities are left out. A candidate a queued copy already covers reports
    /// <see cref="QueueBlocker.InCity"/>, or <see cref="QueueBlocker.InOtherCity"/> when the copy waits in another city of
    /// the same player.</summary>
    public IReadOnlyList<ProductionChoice> ProductionChoices(City city, string kind)
    {
        var key = (WorldRevision, city.Id);
        if (key != _choicesKey) { _choices.Clear(); _choicesKey = key; }
        if (!_choices.TryGetValue(kind, out var candidates)) _choices[kind] = candidates = CheckChoices(city, kind);
        var production = KindOf(kind);
        // One pass over the queues per kind; the notes themselves are then a dictionary read per candidate.
        var queues = QueueBlocker.Count(Sim, production, city);
        var result = new List<ProductionChoice>(candidates.Count);
        foreach (var candidate in candidates)
        {
            // Requirements take priority: they explain why it is still unbuildable even with a copy queued.
            if (candidate.Choice.Available && QueueBlocker.Of(Sim, production, city, candidate.Def, queues) is { } queued)
                result.Add(candidate.Choice with { Blockers = [queued] });
            else result.Add(candidate.Choice);
        }
        return result;
    }

    /// <summary>All registered kinds together.</summary>
    public IReadOnlyList<ProductionChoice> ProductionChoices(City city) =>
        ProductionKinds.SelectMany(k => ProductionChoices(city, k.Id)).ToList();

    /// <summary>The city screen's tabs, in display order: the "all" tab first, then each kind with the categories it
    /// declares after it, so a kind's own tab and its categories sit together. Built from the registered kinds, which are
    /// fixed for the content, so a renderer never names a kind or a category itself.</summary>
    public IReadOnlyList<ProductionTab> ProductionTabs
    {
        get
        {
            var tabs = new List<ProductionTab> { ProductionTab.All };
            foreach (var kind in ProductionKinds)
            {
                tabs.Add(new(kind.Id, null, kind.Label));
                foreach (var category in kind.Categories) tabs.Add(new(kind.Id, category.Id, category.Label));
            }
            return tabs;
        }
    }

    /// <summary>What one tab offers. The "all" tab gathers every kind; a kind's own tab leaves out the candidates its
    /// categories claim, so each candidate appears in one tab rather than two; a category tab holds only its own. The
    /// queued-copy blockers are worked out per kind, so a candidate reads the same under every tab.</summary>
    public IReadOnlyList<ProductionChoice> ProductionChoices(City city, ProductionTab tab)
    {
        if (tab.Kind is not { } kindId) return ProductionChoices(city);
        var choices = ProductionChoices(city, kindId);
        var kind = KindOf(kindId);
        var categories = kind.Categories;
        if (categories.Count == 0) return choices;
        // The definitions are already in hand and sit in the same order as the choices, so the tab filter never resolves
        // a content id a second time.
        if (!_choices.TryGetValue(kindId, out var candidates) || candidates.Count != choices.Count) return choices;
        if (tab.Category is { } categoryId)
        {
            if (categories.FirstOrDefault(c => c.Id == categoryId) is not { } category) return [];
            return Picked(choices, candidates, category.Contains);
        }
        return Picked(choices, candidates, def => !categories.Any(category => category.Contains(def)));
    }

    /// <summary>The choices whose definition passes <paramref name="keep"/>, in list order.</summary>
    private static List<ProductionChoice> Picked(IReadOnlyList<ProductionChoice> choices, IReadOnlyList<Candidate> candidates,
        Func<Content.ContentDef, bool> keep)
    {
        var picked = new List<ProductionChoice>(choices.Count);
        for (int i = 0; i < choices.Count; i++) if (keep(candidates[i].Def)) picked.Add(choices[i]);
        return picked;
    }

    /// <summary>The rows a tab draws under a name filter: every available match, then every blocked match when the
    /// screen offers them. The filter is applied first, so a blocked row a player searched for is never crowded out by
    /// unrelated blocked rows.</summary>
    public ProductionList ProductionRows(City city, ProductionTab tab, string filter, bool showBlocked)
    {
        var matching = ProductionChoices(city, tab).Where(c => c.Matches(filter)).ToList();
        var rows = new List<ProductionChoice>(matching.Count);
        rows.AddRange(matching.Where(c => c.Available));
        if (showBlocked) rows.AddRange(matching.Where(c => !c.Available));
        return new(rows, matching.Count);
    }

    private IReadOnlyList<Candidate> CheckChoices(City city, string kind)
    {
        var production = KindOf(kind);
        var defs = production.Candidates(Sim.Content).OrderBy(d => d.Name, StringComparer.Ordinal).ToArray();
        // One batch rather than one call per candidate: the mods share the whole-empire building, power and resource
        // work that every candidate repeats (605 candidates cost 246 ms per id, 97 ms as a batch on a small world).
        var eligibility = production.ValidateBatch(Sim, city.Id, defs.Select(d => d.Id).ToArray());
        var result = new List<Candidate>(defs.Length);
        for (int i = 0; i < defs.Length; i++)
        {
            if (!production.IsVisible(eligibility[i])) continue;
            var blockers = eligibility[i].Failures.Select(f => f.Explanation).Distinct().ToList();
            var def = defs[i];
            result.Add(new(def, blockers.Count > 0
                ? new ProductionChoice(kind, def.Id, def.Name, production.BaseCost(def), null, blockers, production.IsContinuous)
                : Priced(city, production, def)));
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
