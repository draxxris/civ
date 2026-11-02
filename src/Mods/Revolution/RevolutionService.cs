using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Revolution;

internal sealed class RevolutionService : IRevolutionService, IModifierSource
{
    internal static RevolutionRulesDef Rules(Simulation sim) => sim.Content.Get<RevolutionRulesDef>().All.Single();
    private static RevolutionCityState Read(Simulation sim, City city) => city.Extensions.TryGet<RevolutionCityState>(sim.Content) ?? new();
    internal static RevolutionCityState Edit(Simulation sim, City city)
    {
        var state = city.Extensions.Get<RevolutionCityState>(sim.Content);
        if (state is null) { state = new(); city.Extensions.Set(sim.Content, state); }
        return state;
    }

    public CityStabilityDescription DescribeCityStability(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var state = Read(sim, city);
        var rules = Rules(sim);
        var ledger = InstabilityLedger(sim, city, rules);
        return new(cityId, state.InstabilityIndex, Status(state.InstabilityIndex, rules),
            checked((int)ledger.Total("revolution:change")),
            Math.Max(0, state.RebellionCooldownUntilTurn - sim.State.Turn),
            Math.Max(0, state.UnrestUntilTurn - sim.State.Turn), SuppressionGold(sim, state.InstabilityIndex), ledger);
    }

    private static CityStabilityStatus Status(int index, RevolutionRulesDef rules)
    {
        if (index >= rules.RebellionThreshold) return CityStabilityStatus.Rebellion;
        if (index >= rules.MutinyThreshold) return CityStabilityStatus.Mutiny;
        return index >= rules.WarningThreshold ? CityStabilityStatus.Warning : CityStabilityStatus.Stable;
    }

    /// <summary>Each term is scaled to the game speed's pace and rounded once, to the nearest point away from zero,
    /// so the displayed lines add up to the displayed total instead of each losing up to a point to truncation.</summary>
    private static Ledger InstabilityLedger(Simulation sim, City city, RevolutionRulesDef rules)
    {
        var lines = new List<LedgerLine>();
        int pace = GamePace.Percent(sim);
        void Add(string source, long amount) => lines.Add(new(source, "revolution:change", Round(amount, pace)));
        var wellbeing = sim.Content.Services.Get<ICityWellbeingService>().Single().Calculate(sim, city.Id);
        Add("revolution:wellbeing", ((long)wellbeing.Unhappy - wellbeing.Happy) * rules.WellbeingFactor);
        Add("revolution:distance", (long)sim.State.Grid.Distance(CapitalOf(sim, city), city.Position) * rules.DistanceFactor);
        AddReligion(sim, city, rules, Add);
        foreach (var civic in Civics(sim, city.Owner)) Add(civic.Id, rules.CivicFactors.GetValueOrDefault(civic.Id));
        if (GovernmentStatus.IsAnarchy(sim, city.Owner)) Add("revolution:anarchy", rules.AnarchyFactor);
        int garrison = sim.State.UnitsAt(city.Position).Count(u => u.Owner == city.Owner && Military(sim, u));
        Add("revolution:garrison", garrison == 0 ? rules.UndefendedFactor : (long)Math.Min(garrison, rules.MaxGarrison) * rules.GarrisonFactor);
        if (sim.Content.Services.Get<IGreatPeopleService>().SingleOrDefault()?.GoldenAgeTurnsRemaining(sim, city.Owner) > 0)
            Add("revolution:golden_age", rules.GoldenAgeFactor);
        Add("revolution:recovery", rules.NaturalRecovery);
        return new(lines);
    }

    /// <summary>Percent-of with rounding to the nearest point away from zero. Zero stays zero.</summary>
    private static long Round(long amount, int percent) =>
        (long)Math.Round(amount * 100.0 / Math.Max(1, percent), MidpointRounding.AwayFromZero);

    private static IEnumerable<S.CivicDef> Civics(Simulation sim, int player) => sim.State.PlayerById(player).Civics.Values
        .Order(StringComparer.Ordinal).Select(sim.Content.Get<S.CivicDef>);

    private static Hex CapitalOf(Simulation sim, City city)
    {
        var owned = sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id).ToArray();
        var capital = Array.Find(owned, c => c.Buildings.Any(b => sim.Content.Get<S.BuildingDef>(b).Capital));
        return (capital ?? owned[0]).Position;
    }

    private static void AddReligion(Simulation sim, City city, RevolutionRulesDef rules, Action<string, long> add)
    {
        var religions = sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        if (religions?.StateReligion(sim, city.Owner) is not { } stateReligion) return;
        var present = religions.Present(sim, city.Id);
        // One civic pass serves both the intolerance multiplier and the rest of the ledger.
        var civics = Civics(sim, city.Owner).ToArray();
        int intolerance = civics.Count(c => c.NoNonStateReligionSpread || c.AllowInquisitions);
        long nonState = present.Count(r => r != stateReligion) * (long)rules.NonStateReligionFactor;
        add("revolution:non_state_religion", nonState * (100 + intolerance * rules.IntolerancePercent) / 100);
        if (present.Contains(stateReligion)) add("revolution:state_religion", rules.StateReligionFactor);
    }

    /// <summary>
    /// A garrison is a land military unit that can actually fight: positive strength, alive and not an animal.
    /// Air and naval units in the city add nothing, a wounded unit calms as much as a healthy one, and garrisons
    /// past <see cref="RevolutionRulesDef.MaxGarrison"/> add nothing. An undefended city is worse than a garrisoned
    /// one by more than one garrison's worth (<c>UndefendedFactor</c> versus <c>GarrisonFactor</c>), on purpose.
    /// </summary>
    internal static bool Military(Simulation sim, Unit unit)
    {
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        return unit.Health > 0 && def.Strength > 0 && !def.Animal && def.Domain == "military:domain_land";
    }

    internal void Update(Simulation sim, City city)
    {
        var state = Edit(sim, city);
        if (state.LastUpdatedTurn == sim.State.Turn) return;
        long change = DescribeCityStability(sim, city.Id).ChangePerTurn;
        state.InstabilityIndex = (int)Math.Clamp(state.InstabilityIndex + change, 0, int.MaxValue);
        state.LastUpdatedTurn = sim.State.Turn;
    }

    public string? ValidateSuppression(Simulation sim, int player, int cityId, int? garrison)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        if (sim.State.PlayerById(player).EndedTurn) return "Turn already ended";
        var state = Read(sim, city);
        if (state.InstabilityIndex <= 0) return "City has no instability";
        if (garrison is { } id) return ValidateGarrison(sim, city, id);
        return sim.State.PlayerById(player).Treasury >= SuppressionGold(sim, state.InstabilityIndex) ? null : "Insufficient gold";
    }

    /// <summary>Gold price of the reduction this city can absorb. Suppressing a nearly calm city is cheap, and
    /// suppressing all of a full reduction always costs the rule value.</summary>
    internal static long SuppressionGold(Simulation sim, int instability)
    {
        var rules = Rules(sim);
        int reduction = Math.Min(instability, rules.SuppressionReduction);
        return reduction <= 0 ? 0 : Math.Max(1, checked((long)rules.SuppressionGold * reduction) / rules.SuppressionReduction);
    }

    private static string? ValidateGarrison(Simulation sim, City city, int id)
    {
        var unit = sim.State.FindUnit(id);
        if (unit is null || unit.Owner != city.Owner || unit.Position != city.Position || !Military(sim, unit))
            return "Requires an owned military garrison in the city";
        return unit.MovesLeft > 0 ? null : "Garrison has no moves left";
    }

    public void Suppress(Simulation sim, int player, int cityId, int? garrison)
    {
        if (ValidateSuppression(sim, player, cityId, garrison) is { } error) throw new InvalidOperationException(error);
        var rules = Rules(sim);
        var state = Edit(sim, sim.State.CityById(cityId));
        long gold = garrison is null ? SuppressionGold(sim, state.InstabilityIndex) : 0;
        sim.State.PlayerById(player).Treasury -= gold;
        if (garrison is { } unit) sim.State.UnitById(unit).MovesLeft = 0;
        int reduction = Math.Min(state.InstabilityIndex, rules.SuppressionReduction);
        state.InstabilityIndex -= reduction;
        sim.Events.Publish(new CityRebellionSuppressed(cityId, player, garrison, gold, reduction));
    }

    /// <summary>Outside agents raise pressure without spawning anything themselves: a real rebellion follows
    /// through the normal turn system if the city crosses the threshold. A city already in disorder is immune.</summary>
    public bool Incite(Simulation sim, int cityId)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || GovernmentStatus.IsDisorder(sim, city)) return false;
        var rules = Rules(sim);
        var state = Edit(sim, city);
        state.InstabilityIndex = (int)Math.Clamp(state.InstabilityIndex + rules.InciteInstability, 0, int.MaxValue);
        state.UnrestUntilTurn = Math.Max(state.UnrestUntilTurn, checked(sim.State.Turn + 1 + GamePace.Scale(sim, rules.UnrestTurns)));
        return true;
    }

    public IReadOnlyList<int> CitiesAtRisk(Simulation sim, int player)
    {
        var rules = Rules(sim);
        return sim.State.Cities.Where(c => c.Owner == player)
            .Where(c => Read(sim, c).InstabilityIndex >= rules.WarningThreshold)
            .OrderBy(c => c.Id).Select(c => c.Id).ToArray();
    }

    internal void Rebel(Simulation sim, City city)
    {
        if (PlayerRules.IsBarbarian(sim, city.Owner)) return;
        var state = Read(sim, city);
        var rules = Rules(sim);
        if (state.InstabilityIndex < rules.RebellionThreshold || sim.State.Turn < state.RebellionCooldownUntilTurn) return;
        RebellionSpawns.Trigger(sim, city, rules);
    }

    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        var rules = Rules(context.Sim);
        var state = Read(context.Sim, city);
        if (state.UnrestUntilTurn > context.Sim.State.Turn)
            sink.Add("revolution:unrest", "cities:happiness", rules.UnrestUnhappiness);
        // Mutiny is the first tier with a gameplay effect: the city's people stop cooperating.
        if (Status(state.InstabilityIndex, rules) >= CityStabilityStatus.Mutiny)
            sink.Add("revolution:mutiny", "cities:happiness", rules.MutinyUnhappiness);
    }
}
