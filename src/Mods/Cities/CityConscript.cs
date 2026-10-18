using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityConscript(ICityWorkService work, ICityCultureService culture, ICityGrowthService growth) : ICityConscriptService, IModifierSource
{
    public ConscriptResult? Calculate(Simulation sim, int cityId)
    {
        if (sim.State.FindCity(cityId) is not { } city || ConscriptUnit(sim, city) is not { } unit) return null;
        int perCost = CityData.Raw(sim, "CONSCRIPT_POPULATION_PER_COST", 60);
        int population = perCost <= 0 ? 0 : Math.Max(1, unit.Cost / perCost);
        int minimum = CityData.Constants(sim).MinimumConscriptPopulation + population;
        int experience = CityProduction.CityExperience(sim, city, unit) / 2;
        int anger = AngerLength(sim);
        var limits = ModifierSink.Collect(new(sim, ModifierScope.Player, city.Owner)).Ledger;
        int maximum = (int)Math.Clamp(limits.Total("cities:max_conscript"), 0, int.MaxValue);
        var lines = limits.Lines.Where(l => l.Key == "cities:max_conscript").ToList();
        lines.AddRange([
            new(unit.Id, "cities:conscript_population", population), new(unit.Id, "cities:conscript_min_population", minimum),
            new(unit.Id, "cities:conscript_experience", experience), new("cities:conscript", "cities:conscript_anger_turns", anger)]);
        return new(unit.Id, population, minimum, experience, anger, maximum, new(lines));
    }
    private static S.UnitDef? ConscriptUnit(Simulation sim, City city) => sim.Content.Get<S.UnitDef>().All
        .Where(u => u.Conscription > 0)
        .OrderByDescending(u => u.Conscription).ThenBy(u => u.Id, StringComparer.Ordinal)
        // Eligibility resolves civilization replacements, including the city's original civilization under assimilation.
        .FirstOrDefault(u => !MissingRequiredTechnology(sim, city, u.Requirements)
            && ProductionEligibility.Validate(sim, city, "cities:unit", u.Id).Success);

    // Reject unmet mandatory tech leaves before eligibility checks build resource networks.
    // Alternatives and negations still go through full validation; their tech leaves are not mandatory.
    private static bool MissingRequiredTechnology(Simulation sim, City city, Requirement? requirement) => requirement switch
    {
        Requirement.Leaf { Kind: "tech" } leaf => !sim.Content.Requirements.Evaluate(leaf, new(sim, city.Owner, city, CityData.Tile(sim, city))).Success,
        Requirement.All all => all.Items.Any(r => MissingRequiredTechnology(sim, city, r)),
        _ => false,
    };

    public string? Validate(Simulation sim, int player, int cityId)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        if (GovernmentStatus.IsDisorder(sim, city)) return "City is in disorder";
        if (CityData.Read(sim, city).DraftedThisTurn) return "City has already drafted this turn";
        if (Calculate(sim, cityId) is not { } result) return "No eligible unit to draft";
        return ValidateDraft(sim, city, result);
    }
    private string? ValidateDraft(Simulation sim, City city, ConscriptResult result)
    {
        if (result.MaxPlayerDrafts <= 0) return "No active civic permits drafting";
        if (CityData.PlayerState(sim, city.Owner).ConscriptCountThisTurn >= result.MaxPlayerDrafts) return "Player draft limit reached";
        if (city.Population <= result.PopulationCost || city.Population < result.MinPopulation) return "Not enough population to draft";
        if (culture.TeamCulturePercent(sim, city.Id) < CityData.Raw(sim, "CONSCRIPT_MIN_CULTURE_PERCENT", 10)) return "Not enough team culture to draft";
        return null;
    }
    public void Conscript(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        if (Validate(sim, city.Owner, cityId) is not null) return;
        var result = Calculate(sim, cityId)!;
        city.Population -= result.PopulationCost;
        var state = CityData.Edit(sim, city);
        state.ConscriptAngerTurns += result.AngerTurns;
        state.DraftedThisTurn = true;
        var playerState = CityData.PlayerState(sim, city.Owner);
        playerState.ConscriptCountThisTurn++;
        sim.State.PlayerById(city.Owner).Extensions.Set(sim.Content, playerState);
        work.Assign(sim, cityId);
        city.FoodStored = Math.Min(city.FoodStored, growth.Calculate(sim, cityId).Threshold);
        CityProduction.Spawn(sim, city, result.UnitId, conscript: true);
    }
    private static int AngerLength(Simulation sim) => Math.Max(1,
        CityData.Constants(sim).ConscriptAngerDivisor * CityData.Speed(sim).HurryConscriptAngerPercent / 100);

    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        int turns = CityData.Read(context.Sim, city).ConscriptAngerTurns;
        if (turns <= 0) return;
        int stacks = (turns - 1) / AngerLength(context.Sim) + 1;
        sink.Add("cities:conscript", "cities:unhappiness", stacks * CityData.Constants(context.Sim).ConscriptPopulationAnger);
    }
    internal static void ResetDrafts(Simulation sim, int player)
    {
        var state = CityData.PlayerState(sim, player);
        state.ConscriptCountThisTurn = 0;
        sim.State.PlayerById(player).Extensions.Set(sim.Content, state);
        foreach (var city in CityData.Owned(sim, player)) CityData.Edit(sim, city).DraftedThisTurn = false;
    }
    internal static void DecayAnger(Simulation sim, int player)
    {
        foreach (var city in CityData.Owned(sim, player))
        {
            var state = CityData.Edit(sim, city);
            state.ConscriptAngerTurns = Math.Max(0, state.ConscriptAngerTurns - 1);
        }
    }
}

internal sealed class ConscriptPlayerPhase(string name, int order, Action<Simulation, int> run) : IPlayerTurnSystem
{
    public string Id => "cities:" + name;
    public int Order => order;
    public void Run(Simulation sim, int player) => run(sim, player);
}
