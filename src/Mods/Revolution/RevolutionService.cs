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
            Math.Max(0, state.UnrestUntilTurn - sim.State.Turn), ledger);
    }

    private static CityStabilityStatus Status(int index, RevolutionRulesDef rules)
    {
        if (index >= rules.RebellionThreshold) return CityStabilityStatus.Rebellion;
        if (index >= rules.MutinyThreshold) return CityStabilityStatus.Mutiny;
        return index >= rules.WarningThreshold ? CityStabilityStatus.Warning : CityStabilityStatus.Stable;
    }

    private static Ledger InstabilityLedger(Simulation sim, City city, RevolutionRulesDef rules)
    {
        var lines = new List<LedgerLine>();
        int speed = Math.Max(1, sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).ResearchPercent);
        void Add(string source, long amount) => lines.Add(new(source, "revolution:change", amount * 100 / speed));
        var wellbeing = sim.Content.Services.Get<ICityWellbeingService>().Single().Calculate(sim, city.Id);
        Add("revolution:wellbeing", ((long)wellbeing.Unhappy - wellbeing.Happy) * rules.WellbeingFactor);
        var owned = sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id).ToArray();
        var capital = owned.FirstOrDefault(c => c.Buildings.Any(b => sim.Content.Get<S.BuildingDef>(b).Capital)) ?? owned.First();
        Add("revolution:distance", (long)sim.State.Grid.Distance(capital.Position, city.Position) * rules.DistanceFactor);
        AddReligion(sim, city, rules, Add);
        foreach (var civic in Civics(sim, city.Owner)) Add(civic.Id, rules.CivicFactors.GetValueOrDefault(civic.Id));
        if (GovernmentStatus.IsAnarchy(sim, city.Owner)) Add("revolution:anarchy", rules.AnarchyFactor);
        int garrison = sim.State.UnitsAt(city.Position).Count(u => u.Owner == city.Owner && Military(sim, u));
        Add("revolution:garrison", garrison == 0 ? rules.UndefendedFactor : (long)Math.Min(garrison, 4) * rules.GarrisonFactor);
        if (sim.Content.Services.Get<IGreatPeopleService>().SingleOrDefault()?.GoldenAgeTurnsRemaining(sim, city.Owner) > 0)
            Add("revolution:golden_age", rules.GoldenAgeFactor);
        Add("revolution:recovery", rules.NaturalRecovery);
        return new(lines);
    }

    private static IEnumerable<S.CivicDef> Civics(Simulation sim, int player) => sim.State.PlayerById(player).Civics.Values
        .Order(StringComparer.Ordinal).Select(sim.Content.Get<S.CivicDef>);

    private static void AddReligion(Simulation sim, City city, RevolutionRulesDef rules, Action<string, long> add)
    {
        var religions = sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        if (religions?.StateReligion(sim, city.Owner) is not { } stateReligion) return;
        var present = religions.Present(sim, city.Id);
        int intolerance = Civics(sim, city.Owner).Count(c => c.NoNonStateReligionSpread || c.AllowInquisitions);
        long nonState = present.Count(r => r != stateReligion) * (long)rules.NonStateReligionFactor;
        add("revolution:non_state_religion", nonState * (100 + intolerance * rules.IntolerancePercent) / 100);
        if (present.Contains(stateReligion)) add("revolution:state_religion", rules.StateReligionFactor);
    }

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
        if (Read(sim, city).InstabilityIndex <= 0) return "City has no instability";
        if (garrison is { } id) return ValidateGarrison(sim, city, id);
        return sim.State.PlayerById(player).Treasury >= Rules(sim).SuppressionGold ? null : "Insufficient gold";
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
        long gold = garrison is null ? rules.SuppressionGold : 0;
        sim.State.PlayerById(player).Treasury -= gold;
        if (garrison is { } unit) sim.State.UnitById(unit).MovesLeft = 0;
        var state = Edit(sim, sim.State.CityById(cityId));
        int reduction = Math.Min(state.InstabilityIndex, rules.SuppressionReduction);
        state.InstabilityIndex -= reduction;
        sim.Events.Publish(new CityRebellionSuppressed(cityId, player, garrison, gold, reduction));
    }

    internal void Rebel(Simulation sim, City city)
    {
        if (sim.State.PlayerById(city.Owner).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true) return;
        var state = Read(sim, city);
        var rules = Rules(sim);
        if (state.InstabilityIndex < rules.RebellionThreshold || sim.State.Turn < state.RebellionCooldownUntilTurn) return;
        RebellionSpawns.Trigger(sim, city, rules);
    }

    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        if (Read(context.Sim, city).UnrestUntilTurn > context.Sim.State.Turn)
            sink.Add("revolution:unrest", "cities:unhappiness", Rules(context.Sim).UnrestUnhappiness);
    }
}
