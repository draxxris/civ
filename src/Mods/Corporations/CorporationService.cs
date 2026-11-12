using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using CivicDef = Ariel.Content.Schema.CivicDef;

namespace Ariel.Mods.Corporations;

internal sealed class CorporationService : ICorporationService
{
    private static CorporationGameState Read(Simulation sim) => sim.State.Extensions.Get<CorporationGameState>(sim.Content) ?? new();
    /// <summary>The recorded state, created and attached on first use. Callers must mutate the returned instance:
    /// a fresh state is only persisted once it is set, so a caller that reads without editing loses nothing.</summary>
    private static CorporationGameState Edit(Simulation sim)
    {
        var state = sim.State.Extensions.Get<CorporationGameState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }
    private static IEnumerable<CivicDef> Civics(Simulation sim, int player) => sim.State.PlayerById(player).Civics.Values
        .Order(StringComparer.Ordinal).Select(sim.Content.Get<CivicDef>);
    /// <summary>Corporations with a branch in the city; empty for a stale id, like the sibling queries.</summary>
    public IReadOnlyList<string> Present(Simulation sim, int city) => sim.State.FindCity(city)?.Extensions.Get<CorporationCityState>(sim.Content)?.Corporations.ToArray() ?? [];
    public bool HasIdentity(Simulation sim, int city, string identity) => Present(sim, city)
        .Any(corporation => sim.Content.TryGet<CorporationDef>(corporation, out var def) && def.Identity == identity);
    /// <summary>One founding answer for the rules, the effects and the UI: a live headquarters city.</summary>
    private static CorporationFounding? Founding(Simulation sim, string corporation) =>
        Read(sim).Founded.TryGetValue(corporation, out var record) && sim.State.FindCity(record.Headquarters) is not null ? record : null;
    public int? Headquarters(Simulation sim, string corporation) => Founding(sim, corporation)?.Headquarters;
    public bool IsFounded(Simulation sim, string corporation) => Founding(sim, corporation) is not null;
    /// <summary>The team that owns the headquarters now. A captured headquarters moves the corporation with it; a razed one falls back to the founder.</summary>
    private static int HeadquartersTeam(Simulation sim, CorporationFounding founded) =>
        sim.State.FindCity(founded.Headquarters) is { } hq ? sim.TeamOf(hq.Owner).Id : founded.FounderTeam;
    private static bool Blocked(Simulation sim, City city, int headquartersTeam) => Civics(sim, city.Owner).Any(c => c.NoCorporations || (c.NoForeignCorporations && sim.TeamOf(city.Owner).Id != headquartersTeam));
    public bool IsActive(Simulation sim, int city, string corporation) => sim.State.FindCity(city) is { } target
        && Founding(sim, corporation) is { } record && Present(sim, city).Contains(corporation)
        && !Blocked(sim, target, HeadquartersTeam(sim, record));
    public long ConsumedBonusCount(Simulation sim, int city, string corporation)
    {
        if (sim.State.FindCity(city) is not { } cityObj) return 0;
        var owner = cityObj.Owner;
        var prerequisites = sim.Content.Get<CorporationDef>(corporation).PrereqBonuses;
        if (prerequisites.Length == 0) return 0;
        // One connectivity query for the city, then a lookup per prerequisite.
        var available = ResourceAccess.Counts(sim, owner, city, prerequisites);
        return prerequisites.Distinct(StringComparer.Ordinal).Sum(b => Math.Max(0, available.GetValueOrDefault(b)));
    }
    public long MaintenanceHundredths(Simulation sim, int city)
    {
        var cityObj = sim.State.CityById(city);
        var world = sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize!);
        var handicap = sim.Content.Get<HandicapDef>(sim.Settings.Handicap);
        long modifier = ModifierSink.Collect(new(sim, ModifierScope.Player, cityObj.Owner)).Ledger.Total("cities:corporation_maintenance_percent");
        int inflation = InflationPercent(sim, cityObj.Owner) + 100;
        bool realistic = sim.IsOptionEnabled("foundation:gameoption_realistic_corporations");
        long total = 0;
        foreach (var corporation in Present(sim, city).Where(c => IsActive(sim, city, c)))
        {
            var def = sim.Content.Get<CorporationDef>(corporation);
            var hq = def.HeadquartersCommerce;
            long maintenance = 100L * (hq.Gold + hq.Research + hq.Culture + hq.Espionage);
            long bonusMaintenance = (long)def.MaintenanceHundredths * ConsumedBonusCount(sim, city, corporation);
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
    public string? ValidateFound(Simulation sim, int player, int unitId, string corporation)
    {
        if (!sim.Content.TryGet<CorporationDef>(corporation, out var def)) return "Unknown corporation";
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your Great Person";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (sim.Content.Get<Ariel.Content.Schema.UnitDef>(unit.Type).DefaultUnit != def.RequiredGreatPersonClass) return "Requires the correct Great Person class";
        var city = sim.State.CityAt(unit.Position);
        if (city is null || city.Owner != player) return "Great Person must be in your city";
        if (IsFounded(sim, corporation)) return "Corporation is already founded";
        if (def.FoundingTech is { } tech && !sim.KnowsTech(player, tech)) return "Requires technology: " + tech;
        if (Blocked(sim, city, sim.TeamOf(player).Id)) return "Civics prohibit corporations";
        if (ConsumedBonusCount(sim, city.Id, corporation) <= 0) return "Requires a connected corporation resource";
        return HeadquartersConflict(sim, city.Id, def);
    }
    public void Found(Simulation sim, int player, int unit, string corporation)
    {
        if (ValidateFound(sim, player, unit, corporation) is { } error) throw new InvalidOperationException(error);
        var city = sim.State.CityAt(sim.State.UnitById(unit).Position)!;
        var state = Edit(sim);
        // A stale record whose headquarters city no longer exists is replaced rather than rejected by IsFounded.
        state.Founded[corporation] = new(city.Id, sim.TeamOf(player).Id, sim.State.Turn);
        Add(sim, city, sim.Content.Get<CorporationDef>(corporation));
        sim.State.RemoveUnit(unit);
        sim.Events.Publish(new CorporationFounded(corporation, city.Id, player));
        sim.Rules().Visibility?.Recompute(sim);
    }
    public string? ValidateSpread(Simulation sim, int player, int cityId, string corporation, int? unitId = null)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        if (!sim.Content.TryGet<CorporationDef>(corporation, out var def)) return "Unknown corporation";
        if (Founding(sim, corporation) is not { } record) return "Corporation is not founded";
        var city = sim.State.FindCity(cityId);
        if (city is null) return "Unknown city";
        // Cheap checks first: the branch scan and the connectivity queries are wasted on a player who cannot pay.
        if (sim.State.PlayerById(player).Treasury < def.SpreadCost) return "Insufficient gold";
        if (ValidateBranch(sim, player, city, corporation, record) is { } branchError) return branchError;
        if (ValidateSpreadAgent(sim, player, city, def, unitId) is { } agentError) return agentError;
        if (ConsumedBonusCount(sim, cityId, corporation) <= 0) return "Requires a connected corporation resource";
        return HeadquartersConflict(sim, cityId, def);
    }
    private string? ValidateBranch(Simulation sim, int player, City city, string corporation, CorporationFounding record)
    {
        if (Present(sim, city.Id).Contains(corporation)) return "Corporation is already present";
        if (Blocked(sim, city, HeadquartersTeam(sim, record))) return "Civics prohibit this corporation";
        return sim.State.Cities.Any(c => c.Owner == player && IsActive(sim, c.Id, corporation)) ? null : "Requires an active owned branch";
    }
    private static string? ValidateSpreadAgent(Simulation sim, int player, City city, CorporationDef def, int? unitId)
    {
        if (unitId is not { } id) return city.Owner == player ? null : "Foreign spread requires an executive";
        var unit = sim.State.FindUnit(id);
        if (unit is null || unit.Owner != player || unit.Position != city.Position) return "Executive must be in the target city";
        if (unit.MovesLeft <= 0) return "No moves left";
        return sim.Content.Get<Ariel.Content.Schema.UnitDef>(unit.Type).CorporationSpreads.GetValueOrDefault(def.Identity) > 0 ? null : "Unit cannot spread this corporation";
    }
    public void Spread(Simulation sim, int player, int city, string corporation, int? unit = null)
    {
        if (ValidateSpread(sim, player, city, corporation, unit) is { } error) throw new InvalidOperationException(error);
        var def = sim.Content.Get<CorporationDef>(corporation);
        sim.State.PlayerById(player).Treasury -= def.SpreadCost;
        Add(sim, sim.State.CityById(city), def);
        if (unit is { } id) sim.State.RemoveUnit(id);
        sim.Events.Publish(new CorporationSpread(corporation, city, player));
        sim.Rules().Visibility?.Recompute(sim);
    }
    private static bool Competes(Simulation sim, string corporation, CorporationDef incoming) => corporation != incoming.Id && sim.Content.Get<CorporationDef>(corporation).PrereqBonuses.Intersect(incoming.PrereqBonuses, StringComparer.Ordinal).Any();
    private string? HeadquartersConflict(Simulation sim, int city, CorporationDef incoming) => Present(sim, city).Any(c => Competes(sim, c, incoming) && Headquarters(sim, c) == city) ? "Cannot displace competing headquarters" : null;
    private static void Add(Simulation sim, City city, CorporationDef def)
    {
        var state = city.Extensions.Get<CorporationCityState>(sim.Content) ?? new();
        city.Extensions.Set(sim.Content, state);
        var displaced = state.Corporations.Where(c => Competes(sim, c, def)).Order(StringComparer.Ordinal).ToArray();
        state.Corporations.RemoveWhere(c => Competes(sim, c, def));
        state.Corporations.Add(def.Id);
        // Displacement is silent in the rules, as in AND's shared-resource competition, so the event carries it.
        foreach (var corporation in displaced) sim.Events.Publish(new CorporationDisplaced(corporation, def.Id, city.Id));
    }
}
