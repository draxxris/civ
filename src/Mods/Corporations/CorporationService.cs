using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

internal sealed class CorporationService : ICorporationService, IUnitTrainingRule
{
    public CorporationService() => Natural = new NaturalCorporations(this);
    /// <summary>The Realistic Guilds/Corporations rules: founding, spread and decay on their own.</summary>
    internal NaturalCorporations Natural { get; }
    public int? NaturalFoundingCity(Simulation sim, int player, string corporation) =>
        sim.Content.TryGet<CorporationDef>(corporation, out var def) ? Natural.FoundingSite(sim, player, def)?.Id : null;
    public CorporationSpreadOdds NaturalSpreadOdds(Simulation sim, int city, string corporation) =>
        sim.State.FindCity(city) is { } target && sim.Content.TryGet<CorporationDef>(corporation, out var def)
            ? Natural.SpreadOdds(sim, target, def) : default;
    private static CorporationGameState Read(Simulation sim) => sim.State.Extensions.Get<CorporationGameState>(sim.Content) ?? new();
    /// <summary>The recorded state, created and attached on first use. Callers must mutate the returned instance:
    /// a fresh state is only persisted once it is set, so a caller that reads without editing loses nothing.</summary>
    private static CorporationGameState Edit(Simulation sim)
    {
        var state = sim.State.Extensions.Get<CorporationGameState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }
    /// <summary>The player's percent change to corporation maintenance, from civics and any other modifier source.</summary>
    internal const string MaintenanceKey = "corporations:maintenance_percent";
    /// <summary>What the player's civics mean to corporations; a civic without an attachment means nothing.</summary>
    private static IEnumerable<CivicCorporationsDef> Civics(Simulation sim, int player)
    {
        foreach (var civic in sim.State.PlayerById(player).Civics.Values.Order(StringComparer.Ordinal))
            if (sim.Content.Attachment<CivicCorporationsDef>(civic) is { } rules) yield return rules;
    }
    /// <summary>Corporations with a branch in the city; empty for a stale id, like the sibling queries.</summary>
    public IReadOnlyList<string> Present(Simulation sim, int city) => sim.State.FindCity(city)?.Extensions.Get<CorporationCityState>(sim.Content)?.Corporations.ToArray() ?? [];
    /// <summary>The founding record is the headquarters: it names the city that completed the founding building, and
    /// a captured headquarters keeps it, as the headquarters income does.</summary>
    public bool IsHeadquartersOf(Simulation sim, int city, string corporation) =>
        sim.Content.TryGet<CorporationDef>(corporation, out _)
        && Read(sim).Founded.TryGetValue(corporation, out var record) && record.Headquarters == city;
    /// <summary>The seat as running text: "the headquarters of Cereal Mills", "the Masons Master Guild". A founding
    /// building that already carries the seat's words in its name (the guilds' do) is named as it is, so the phrase does
    /// not read "the Master Guild of Masons Guild"; otherwise the seat is "of" the corporation.</summary>
    public string? SeatPhrase(ContentDb content, string corporation)
    {
        if (!content.TryGet<CorporationDef>(corporation, out var def)) return null;
        string? building = CorporationFounder.Headquarters(content, def.Id)?.Name;
        return building is not null && building.Contains(def.Seat, StringComparison.OrdinalIgnoreCase)
            ? "the " + building : $"the {def.Seat} of {def.Name}";
    }
    /// <summary>One founding answer for the rules, the effects and the UI: a live headquarters city.</summary>
    internal static CorporationFounding? Founding(Simulation sim, string corporation) =>
        Read(sim).Founded.TryGetValue(corporation, out var record) && sim.State.FindCity(record.Headquarters) is not null ? record : null;
    public int? Headquarters(Simulation sim, string corporation) => Founding(sim, corporation)?.Headquarters;
    public bool IsFounded(Simulation sim, string corporation) => Founding(sim, corporation) is not null;
    /// <summary>The team that owns the headquarters now. A captured headquarters moves the corporation with it; a razed one falls back to the founder.</summary>
    internal static int HeadquartersTeam(Simulation sim, CorporationFounding founded) =>
        sim.State.FindCity(founded.Headquarters) is { } hq ? sim.TeamOf(hq.Owner).Id : founded.FounderTeam;
    internal static bool Blocked(Simulation sim, City city, int headquartersTeam) => Civics(sim, city.Owner).Any(c => c.NoCorporations || (c.NoForeignCorporations && sim.TeamOf(city.Owner).Id != headquartersTeam));
    /// <summary>Whether the player's civics ban every corporation.</summary>
    internal static bool BansCorporations(Simulation sim, int player) => Civics(sim, player).Any(c => c.NoCorporations);
    /// <summary>A branch works, as the source's active corporation does: the corporation is founded, its branch is in
    /// the city, the owner's civics allow it, it is not obsolete for the owner, and the city can use a copy of a resource
    /// it consumes (a corporation that consumes none needs none). A branch without its resource stays, but gives,
    /// produces and costs nothing and counts for no headquarters income.</summary>
    public bool IsActive(Simulation sim, int city, string corporation) => IsOperating(sim, city, corporation)
        && (sim.Content.Get<CorporationDef>(corporation).PrereqBonuses.Length == 0 || ConsumedBonusCount(sim, city, corporation) > 0);
    /// <summary><see cref="IsActive"/> without the resource: what the branch's produced resource asks
    /// (<see cref="CorporationResourceSupply"/>), which counts its copies from the city's network itself and is asked
    /// from inside the resource count, so it must not count resources again.</summary>
    internal bool IsOperating(Simulation sim, int city, string corporation) => sim.State.FindCity(city) is { } target
        && Founding(sim, corporation) is { } record && Present(sim, city).Contains(corporation)
        && !Blocked(sim, target, HeadquartersTeam(sim, record)) && !Obsolete(sim, target.Owner, sim.Content.Get<CorporationDef>(corporation));
    /// <summary>Whether the player's team knows the corporation's <see cref="CorporationDef.ObsoleteTech"/>.</summary>
    internal static bool Obsolete(Simulation sim, int player, CorporationDef def) => def.ObsoleteTech is { } tech && sim.KnowsTech(player, tech);
    private static string ObsoleteReason(Simulation sim, CorporationDef def, string whose) =>
        $"{def.Name} is obsolete for {whose}: {sim.Content.Find(def.ObsoleteTech!)?.Name ?? def.ObsoleteTech} is known";
    public long ConsumedBonusCount(Simulation sim, int city, string corporation)
    {
        if (sim.State.FindCity(city) is not { } cityObj) return 0;
        var def = sim.Content.Get<CorporationDef>(corporation);
        return Consumed(def, ConsumedCounts(sim, cityObj, [def]));
    }
    /// <summary>The copies the city can use of every resource the corporations consume, in one query: the city's
    /// connectivity and deals are gathered once, and each resource's count does not depend on the others asked.</summary>
    internal static IReadOnlyDictionary<string, long> ConsumedCounts(Simulation sim, City city, IReadOnlyCollection<CorporationDef> corporations)
    {
        var wanted = corporations.SelectMany(c => c.PrereqBonuses).Distinct(StringComparer.Ordinal).ToArray();
        return wanted.Length == 0 ? NoCounts : ResourceAccess.Counts(sim, city.Owner, city.Id, wanted);
    }
    private static readonly IReadOnlyDictionary<string, long> NoCounts = new Dictionary<string, long>();
    /// <summary>The copies of its consumed resources a branch uses, read from <see cref="ConsumedCounts"/>.</summary>
    internal static long Consumed(CorporationDef def, IReadOnlyDictionary<string, long> counts) => def.PrereqBonuses.Length == 0 ? 0
        : def.PrereqBonuses.Distinct(StringComparer.Ordinal).Sum(b => Math.Max(0, counts.GetValueOrDefault(b)));
    /// <summary>The corporations with an active branch in the city (<see cref="IsActive"/>), in id order, and the copies
    /// of their consumed resources the city can use, gathered in one resource query for all of them.</summary>
    internal (IReadOnlyList<CorporationDef> Active, IReadOnlyDictionary<string, long> Counts) ActiveIn(Simulation sim, City city)
    {
        var present = Present(sim, city.Id);
        if (present.Count == 0) return ([], NoCounts);
        var operating = present.Where(c => IsOperating(sim, city.Id, c)).Select(sim.Content.Get<CorporationDef>).ToArray();
        if (operating.Length == 0) return ([], NoCounts);
        var counts = ConsumedCounts(sim, city, operating);
        return (operating.Where(d => d.PrereqBonuses.Length == 0 || Consumed(d, counts) > 0).ToArray(), counts);
    }
    public long MaintenanceHundredths(Simulation sim, int city)
    {
        var cityObj = sim.State.CityById(city);
        var (active, counts) = ActiveIn(sim, cityObj);
        return MaintenanceHundredths(sim, cityObj, active, counts);
    }
    /// <summary>The city's maintenance for its active branches, given their consumed resource counts
    /// (<see cref="ConsumedCounts"/>), so the city's effects and its maintenance can share one resource query.</summary>
    internal static long MaintenanceHundredths(Simulation sim, City cityObj, IReadOnlyList<CorporationDef> active, IReadOnlyDictionary<string, long> counts)
    {
        // Look the ids up as before, so an unknown world size or handicap still fails loudly.
        var world = sim.Content.Attachment<WorldSizeCorporationsDef>(sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize!).Id)
            ?? WorldSizeCorporationsDef.None;
        var handicap = sim.Content.Attachment<HandicapCorporationsDef>(sim.Content.Get<HandicapDef>(sim.Settings.Handicap).Id)
            ?? HandicapCorporationsDef.None;
        // Every city calculation asks this, and the player's maintenance modifier is a whole player calculation, so it
        // is gathered only for a city with an active branch to pay for.
        if (active.Count == 0) return 0;
        long modifier = ModifierSink.Collect(new(sim, ModifierScope.Player, cityObj.Owner)).Ledger.Total(MaintenanceKey);
        int inflation = InflationPercent(sim, cityObj.Owner) + 100;
        bool realistic = CorporationMode.Realistic(sim);
        long total = 0;
        foreach (var def in active)
        {
            var hq = def.HeadquartersCommerce;
            long maintenance = 100L * (hq.Gold + hq.Research + hq.Culture + hq.Espionage);
            long bonusMaintenance = (long)def.MaintenanceHundredths * Consumed(def, counts);
            maintenance += bonusMaintenance * world.CorporationMaintenancePercent / 100;
            maintenance = maintenance * (cityObj.Population + 17) / 18;
            maintenance = maintenance * handicap.CorporationMaintenancePercent / 100;
            maintenance = maintenance * Math.Max(0, 100 + modifier) / 100;
            if (inflation > 0) maintenance = maintenance * 100 / inflation;
            if (realistic && handicap.CorporationMaintenancePercent > 80)
                maintenance = maintenance * handicap.CorporationMaintenancePercent / 80;
            total += maintenance;
        }
        return total;
    }
    /// <summary>Inflation percent the player pays on expenses, 0 when disabled. Maintenance is divided by
    /// (100 + this) so the economy can re-apply it to the aggregate (see RecurringCosts).</summary>
    internal static int InflationPercent(Simulation sim, int player)
    {
        var raw = sim.Content.Get<RuleConstantsDef>().All.Single().Raw;
        if (int.TryParse(raw.GetValueOrDefault("DISABLE_INFLATION"), out int disabled) && disabled != 0) return 0;
        long ratio = sim.State.PlayerById(player).Extensions.TryGet<EconomyPlayerState>(sim.Content)?.AccruedCostRatioTimes10000 ?? 10000;
        return (int)((ratio - 10000) / 100);
    }
    /// <summary>The yield and commerce one active branch adds to its city with this many consumed resource copies, in
    /// whole points: the per-copy output, summed in hundredths and truncated, plus the branch's flat changes.</summary>
    internal static (YieldVector Yield, CommerceVector Commerce) BranchOutput(CorporationDef def, long copies)
    {
        var y = def.YieldProducedHundredths * copies;
        var c = def.CommerceProducedHundredths * copies;
        return (new YieldVector(y.Food / 100, y.Production / 100, y.Commerce / 100) + def.YieldChanges,
            c / 100 + def.CommerceChanges);
    }
    /// <summary>Whether a unit is an executive of a corporation.</summary>
    internal static bool Spreads(Simulation sim, Unit unit, CorporationDef def) => Spreads(sim, unit.Type, def);
    /// <summary>Whether units of the type are executives of a corporation.</summary>
    internal static bool Spreads(Simulation sim, string unitType, CorporationDef def) =>
        (sim.Content.Attachment<UnitCorporationsDef>(unitType) ?? UnitCorporationsDef.None).CorporationSpreads.GetValueOrDefault(def.Id) > 0;
    /// <summary>Whether units of the type are executives of any corporation.</summary>
    internal static bool IsExecutive(Simulation sim, string unitType) =>
        (sim.Content.Attachment<UnitCorporationsDef>(unitType) ?? UnitCorporationsDef.None).CorporationSpreads.Any(p => p.Value > 0);

    /// <summary>Why the city may not found the corporation by completing its founding building now, or null. The
    /// building's own requirements (its technology, and for a guild the buildings of its trade) are checked by
    /// Cities, which also refuses barbarians first.</summary>
    internal string? RefuseFounding(Simulation sim, City city, CorporationDef def)
    {
        if (CorporationMode.Realistic(sim)) return RealisticFoundingRefusal(sim.Content, def);
        if (IsFounded(sim, def.Id)) return "Corporation is already founded";
        if (Obsolete(sim, city.Owner, def)) return ObsoleteReason(sim, def, "your team");
        if (Blocked(sim, city, sim.TeamOf(city.Owner).Id)) return "Civics prohibit corporations";
        return RefuseSite(sim, city, def);
    }
    /// <summary>What the founding building says with Realistic Guilds/Corporations, where nobody builds it.</summary>
    internal static string RealisticFoundingRefusal(ContentDb content, CorporationDef def) =>
        $"Founded automatically with {CorporationMode.OptionName(content)}: {FoundingCondition(content, def)}";
    /// <summary>What a nation needs for the corporation to be founded on its own: "a nation with Guilds and stone", or
    /// for a guild that consumes nothing, "a nation with Engineering and 4 Siege Workshops".</summary>
    internal static string FoundingCondition(ContentDb content, CorporationDef def)
    {
        string Name(string id) => content.Find(id)?.Name is { Length: > 0 } name ? name : id;
        string techs = string.Join(" and ", CorporationIndex.Of(content).FoundingTechs.GetValueOrDefault(def.Id, []).Select(Name));
        string need = def.PrereqBonuses.Length > 0
            ? string.Join(" or ", def.PrereqBonuses.Select(Name)) + " connected"
            : string.Join(" and ", def.FoundingBuildingClasses.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Value} {Name(p.Key)}"));
        return techs.Length == 0 ? $"a nation with {need}" : $"a nation with {techs} and {need}";
    }
    /// <summary>Found the corporation with its headquarters and first branch in the city. One already founded, by a city
    /// that completed its headquarters first or by a scenario, stays as it is.</summary>
    internal void Found(Simulation sim, City city, CorporationDef def)
    {
        if (IsFounded(sim, def.Id)) return;
        var state = Edit(sim);
        // A stale record whose headquarters city no longer exists is replaced rather than rejected by IsFounded.
        state.Founded[def.Id] = new(city.Id, sim.TeamOf(city.Owner).Id, sim.State.Turn);
        Add(sim, city, def);
        sim.Events.Publish(new CorporationFounded(def.Id, city.Id, city.Owner));
    }
    /// <summary>Found the corporation in the city without anyone building it (Realistic Guilds/Corporations): its founding
    /// building is placed there as the source moves it with its headquarters, so the city is the seat as if it had built
    /// it. Nothing is completed, so no <see cref="BuildingCompleted"/> is published.</summary>
    internal void FoundWithoutBuilding(Simulation sim, City city, CorporationDef def)
    {
        if (IsFounded(sim, def.Id)) return;
        if (CorporationFounder.Headquarters(sim.Content, def.Id) is { } building && !city.Buildings.Contains(building.Id)) city.Buildings.Add(building.Id);
        Found(sim, city, def);
    }
    /// <summary>A razed headquarters dissolves its corporations.</summary>
    internal void DissolveHeadquartersOf(Simulation sim, int city, int player)
    {
        if (Read(sim).Founded.Count == 0) return;
        foreach (var corporation in Read(sim).Founded.Where(pair => pair.Value.Headquarters == city).Select(pair => pair.Key).ToArray())
            Dissolve(sim, corporation, city, player);
    }
    /// <summary>Dissolve the corporation: the headquarters city loses its founding building, its branches leave every
    /// city and it may be founded again.</summary>
    internal static void Dissolve(Simulation sim, string corporation, int city, int player)
    {
        if (sim.State.FindCity(city) is { } seat && CorporationFounder.Headquarters(sim.Content, corporation) is { } building)
            seat.Buildings.Remove(building.Id);
        foreach (var other in sim.State.Cities)
            other.Extensions.Get<CorporationCityState>(sim.Content)?.Corporations.Remove(corporation);
        Edit(sim).Founded.Remove(corporation);
        sim.Events.Publish(new CorporationDissolved(corporation, city, player));
    }
    /// <summary>Close one branch outside the headquarters, which lost its resource (Realistic Guilds/Corporations).</summary>
    internal static void Withdraw(Simulation sim, City city, string corporation)
    {
        if (city.Extensions.Get<CorporationCityState>(sim.Content)?.Corporations.Remove(corporation) == true)
            sim.Events.Publish(new CorporationWithdrawn(corporation, city.Id));
    }
    /// <summary>A team learned a technology that makes corporations obsolete (<see cref="CorporationDef.ObsoleteTech"/>):
    /// their branches close in the team's cities, and a headquarters there loses its founding building and dissolves the
    /// corporation, as in the source. A team without the technology may found it again.</summary>
    internal void Obsolete(Simulation sim, int team, string tech)
    {
        foreach (var def in sim.Content.Get<CorporationDef>().All.Where(d => d.ObsoleteTech == tech).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            foreach (var city in sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == team).OrderBy(c => c.Id).ToArray())
            {
                if (!Present(sim, city.Id).Contains(def.Id)) continue;
                if (Founding(sim, def.Id)?.Headquarters == city.Id)
                {
                    Dissolve(sim, def.Id, city.Id, city.Owner);
                    continue;
                }
                city.Extensions.Get<CorporationCityState>(sim.Content)!.Corporations.Remove(def.Id);
                sim.Events.Publish(new CorporationObsolete(def.Id, city.Id));
            }
        }
    }
    public string? ValidateSpread(Simulation sim, int player, int cityId, string corporation) =>
        ValidateSpread(sim, player, cityId, corporation, null);
    /// <summary>One check for both ways to open a branch: paid for in an own city, or by an executive standing in
    /// the city, which may be foreign.</summary>
    internal string? ValidateSpread(Simulation sim, int player, int cityId, string corporation, Unit? executive)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        if (!sim.Content.TryGet<CorporationDef>(corporation, out var def)) return "Unknown corporation";
        if (CorporationMode.Realistic(sim)) return RealisticSpreadRefusal(sim.Content);
        if (Founding(sim, corporation) is not { } record) return "Corporation is not founded";
        var city = sim.State.FindCity(cityId);
        if (city is null) return "Unknown city";
        // Cheap checks first: the branch scan and the connectivity queries are wasted on a player who cannot pay.
        if (sim.State.PlayerById(player).Treasury < SpreadCost(sim, player, city, def)) return "Insufficient gold";
        if (ValidateBranch(sim, player, city, corporation, record) is { } branchError) return branchError;
        if (executive is null && city.Owner != player) return "Foreign spread requires an executive";
        if (executive is not null && !Spreads(sim, executive, def)) return "Unit cannot spread this corporation";
        return RefuseSite(sim, city, def);
    }
    /// <summary>Why the player could not open a branch of the corporation in the city even with the gold and, for a
    /// foreign city, an executive standing there; null when it could. The computer player asks this before it trains
    /// an executive or sends one, so it never aims at a city the spread itself would refuse.</summary>
    internal string? RefuseBranch(Simulation sim, int player, City city, CorporationDef def)
    {
        if (CorporationMode.Realistic(sim)) return RealisticSpreadRefusal(sim.Content);
        if (Founding(sim, def.Id) is not { } record) return "Corporation is not founded";
        return ValidateBranch(sim, player, city, def.Id, record) ?? RefuseSite(sim, city, def);
    }
    /// <summary>Why nobody opens a branch by hand with Realistic Guilds/Corporations.</summary>
    internal static string RealisticSpreadRefusal(ContentDb content) =>
        $"Guilds and corporations spread on their own with {CorporationMode.OptionName(content)}";
    /// <summary>Executives are not trained with Realistic Guilds/Corporations, where nothing can use them, nor by a
    /// civilization whose <see cref="CivilizationCorporationsDef"/> overrides their class; every other unit is left to
    /// its own requirements.</summary>
    public string? RefuseUnit(Simulation sim, City city, UnitDef unit)
    {
        if (CorporationMode.Realistic(sim) && IsExecutive(sim, unit.Id))
            return $"Executives are not used with {CorporationMode.OptionName(sim.Content)}: guilds and corporations spread on their own";
        // The civilization's overrides of this mod's own executives, which its base overrides cannot name: the same
        // class substitution the city rules apply to every other unit.
        var overrides = (sim.Content.Attachment<CivilizationCorporationsDef>(CityTraining.Civilization(sim, city)) ?? CivilizationCorporationsDef.None).UnitOverrides;
        return overrides.TryGetValue(unit.DefaultUnit, out var replacement) && replacement != unit.Id ? "Civilization class replacement required" : null;
    }
    /// <summary>The checks on the city's resources and headquarters, last because the connectivity query is the dearest.
    /// A corporation that consumes no resource needs none.</summary>
    internal string? RefuseSite(Simulation sim, City city, CorporationDef def)
    {
        if (def.PrereqBonuses.Length > 0 && ConsumedBonusCount(sim, city.Id, def.Id) <= 0) return "Requires a connected corporation resource";
        return HeadquartersConflict(sim, city.Id, def);
    }
    private string? ValidateBranch(Simulation sim, int player, City city, string corporation, CorporationFounding record)
    {
        if (Present(sim, city.Id).Contains(corporation)) return "Corporation is already present";
        var def = sim.Content.Get<CorporationDef>(corporation);
        if (Obsolete(sim, player, def)) return ObsoleteReason(sim, def, "your team");
        if (Obsolete(sim, city.Owner, def)) return ObsoleteReason(sim, def, "this city's owner");
        if (Blocked(sim, city, HeadquartersTeam(sim, record))) return "Civics prohibit this corporation";
        return sim.State.Cities.Any(c => c.Owner == player && IsActive(sim, c.Id, corporation)) ? null : "Requires an active owned branch";
    }
    public void Spread(Simulation sim, int player, int city, string corporation)
    {
        if (ValidateSpread(sim, player, city, corporation) is { } error) throw new InvalidOperationException(error);
        OpenBranch(sim, player, sim.State.CityById(city), corporation);
        sim.Rules().Visibility?.Recompute(sim);
    }
    /// <summary>Charge the spread cost and open the branch; the caller has validated it.</summary>
    internal void OpenBranch(Simulation sim, int player, City city, string corporation) => SpendOnBranch(sim, player, city, corporation, true);
    /// <summary>Charge the spread cost, then open the branch when the attempt succeeded: as in the source an executive's
    /// gold is spent whether or not its spread takes. The caller has validated it.</summary>
    internal void SpendOnBranch(Simulation sim, int player, City city, string corporation, bool success)
    {
        var def = sim.Content.Get<CorporationDef>(corporation);
        // Priced before the branch opens: a competitor it evicts still counted.
        sim.State.PlayerById(player).Treasury -= SpreadCost(sim, player, city, def);
        if (!success) return;
        Add(sim, city, def);
        sim.Events.Publish(new CorporationSpread(corporation, city.Id, player));
    }
    public long SpreadCost(Simulation sim, int player, int city, string corporation) =>
        sim.State.FindCity(city) is { } target && sim.Content.TryGet<CorporationDef>(corporation, out var def) ? SpreadCost(sim, player, target, def) : 0;
    internal long SpreadCost(Simulation sim, int player, City city, CorporationDef def)
    {
        long cost = Math.Max(0L, (long)def.SpreadCost * (100 + InflationPercent(sim, player))) / 100;
        if (sim.TeamOf(player).Id != sim.TeamOf(city.Owner).Id) cost = cost * RuleConstant(sim, "CORPORATION_FOREIGN_SPREAD_COST_PERCENT", 100) / 100;
        foreach (var other in Present(sim, city.Id))
        {
            var rival = sim.Content.Get<CorporationDef>(other);
            if (Competes(def, rival) && IsActive(sim, city.Id, other)) cost = cost * (100 + rival.SpreadFactorPercent) / 100;
        }
        return cost;
    }
    private static int RuleConstant(Simulation sim, string key, int fallback) =>
        int.TryParse(sim.Content.Get<RuleConstantsDef>().All.Single().Raw.GetValueOrDefault(key), out int value) ? value : fallback;
    /// <summary>Percent chance an executive's spread takes, as the source rolls it: the unit's spread value for the
    /// corporation, halved in another team's city, then raised toward 100 by the share of corporations the city does not
    /// yet have. Read-only and draws nothing; the mission service rolls it.</summary>
    internal int SpreadChance(Simulation sim, Unit executive, City city, CorporationDef def)
    {
        int chance = (sim.Content.Attachment<UnitCorporationsDef>(executive.Type) ?? UnitCorporationsDef.None).CorporationSpreads.GetValueOrDefault(def.Id);
        if (sim.TeamOf(executive.Owner).Id != sim.TeamOf(city.Owner).Id) chance /= 2;
        int corporations = Math.Max(1, sim.Content.Get<CorporationDef>().Count);
        chance += (corporations - Present(sim, city.Id).Count) * (100 - chance) / corporations;
        return Math.Clamp(chance, 0, 100);
    }
    private static bool Competes(Simulation sim, string corporation, CorporationDef incoming) => Competes(sim.Content.Get<CorporationDef>(corporation), incoming);
    /// <summary>Whether two corporations compete, as the source defines it: either names the other in its
    /// <see cref="CorporationDef.CompetingCorporations"/>, or they consume a common resource.</summary>
    internal static bool Competes(CorporationDef a, CorporationDef b) => a.Id != b.Id
        && (a.CompetingCorporations.Contains(b.Id, StringComparer.Ordinal) || b.CompetingCorporations.Contains(a.Id, StringComparer.Ordinal)
            || a.PrereqBonuses.Intersect(b.PrereqBonuses, StringComparer.Ordinal).Any());
    internal string? HeadquartersConflict(Simulation sim, int city, CorporationDef incoming) => Present(sim, city).Any(c => Competes(sim, c, incoming) && Headquarters(sim, c) == city) ? "Cannot displace competing headquarters" : null;
    internal static void Add(Simulation sim, City city, CorporationDef def)
    {
        var state = city.Extensions.Get<CorporationCityState>(sim.Content) ?? new();
        city.Extensions.Set(sim.Content, state);
        var displaced = state.Corporations.Where(c => Competes(sim, c, def)).Order(StringComparer.Ordinal).ToArray();
        state.Corporations.RemoveWhere(c => Competes(sim, c, def));
        state.Corporations.Add(def.Id);
        // Displacement is silent in the rules, as in the reference shared-resource competition, so the event carries it.
        foreach (var corporation in displaced) sim.Events.Publish(new CorporationDisplaced(corporation, def.Id, city.Id));
    }
}
