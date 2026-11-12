using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Espionage;

public sealed class EspionageMod : IPreparableMod
{
    private static readonly ModJsonFile<EspionageRulesDef> Rules = new("data/espionage-rules.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Rules];

    public void Register(IModContext context)
    {
        var rules = Rules.Read(context.Data);
        Validate(rules);
        context.Content<EspionageRulesDef>().Register(rules);
        var service = new EspionageService();
        context.RegisterService<IEspionageService>("espionage", service);
        context.RegisterService<IModifierSource>("unhappiness", new SpyUnhappiness());
        context.RegisterStateComponent<EspionageTeamState>("team_state");
        context.RegisterStateComponent<EspionagePlayerState>("player_state");
        context.RegisterStateComponent<EspionageCityState>("city_state");
        context.RegisterCommand<SetEspionageWeightCommand>("set_weight");
        // Registration order is the order a spy's panel lists them in.
        foreach (var kind in EspionageMissionOrder.Display)
            context.RegisterService<IMission>(EspionageMissions.Of(kind)["espionage:".Length..], new SpyMission(service, service.Missions, kind));
        // A conquest must not hand the previous owner's spy effects to the new owner.
        context.Subscribe<CityCaptured>("reset_capture", (sim, captured) =>
        {
            if (sim.State.FindCity(captured.City) is { } city) city.Extensions.Set(sim.Content, new EspionageCityState());
        });
        context.TurnSystems.Register(new SpyAllocation(service));
        context.TurnSystems.Register(new SpyTimers());
        context.TurnSystems.Register(new SpyExpiry());
    }

    private static void Validate(EspionageRulesDef rules)
    {
        if (rules.DemographicsThreshold < 0 || rules.CityVisibilityThreshold < rules.DemographicsThreshold
            || rules.InspectResearchThreshold < rules.CityVisibilityThreshold
            || rules.InvestigateCityThreshold < rules.InspectResearchThreshold)
            throw new InvalidDataException("Espionage passive thresholds must be non-negative and ascending");
        if (rules.StealTreasuryCost <= 0 || rules.SabotageProductionCost <= 0 || rules.InciteRevoltCost <= 0
            || rules.FomentUnhappinessCost <= 0 || rules.CounterEspionageCost <= 0)
            throw new InvalidDataException("Espionage mission costs must be positive");
        if (rules.StealTechCostPercent <= 0 || rules.StealTreasuryPercent is < 0 or > 100
            || rules.SabotagePercent is < 0 or > 100)
            throw new InvalidDataException("Invalid espionage percentage");
        if (rules.FomentUnhappinessTurns < 1 || rules.InciteRevoltTurns < 1 || rules.CounterEspionageTurns < 1)
            throw new InvalidDataException("Espionage effect durations must be at least one turn");
        if (rules.CounterEspionagePercent < 100 || rules.CounterEspionagePerInterception < 0
            || rules.FomentUnhappiness < 0)
            throw new InvalidDataException("Invalid counter-espionage protection");
        if (rules.BaseSuccessPercent is < 0 or > 100 || rules.DefenderSuccessPenalty < 0 || rules.MaxDefenders < 0
            || rules.ProtectionSuccessPenalty < 0 || rules.RatioSuccessDivisor < 1
            || rules.MinSuccessPercent < 0 || rules.MaxSuccessPercent > 100 || rules.MaxSuccessPercent < rules.MinSuccessPercent)
            throw new InvalidDataException("Invalid espionage success chance");
        if (rules.MaxWeight < 1) throw new InvalidDataException("Espionage weights need a positive range");
    }
}

internal sealed class SpyAllocation(EspionageService service) : IPlayerTurnSystem
{
    public string Id => "espionage:allocation";
    /// <summary>One slot after the economy's espionage fallback, which runs at <see cref="TurnPhase.Espionage"/>.</summary>
    public int Order => TurnPhase.Espionage + 1;
    public void Run(Simulation sim, int player) => service.Allocate(sim, player);
}

/// <summary>Clears the disorder a spy imposed before its deadline. The deadline is absolute, so a city keeps
/// exactly the configured number of turns regardless of when the turn interval runs.</summary>
internal sealed class SpyTimers : IPlayerTurnSystem
{
    public string Id => "espionage:timers";
    /// <summary>After government timers (PlayerEnd + 1), so an anarchy flag set this interval is already visible.</summary>
    public int Order => TurnPhase.PlayerEnd + 2;
    public void Run(Simulation sim, int player)
    {
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id))
        {
            var state = city.Extensions.Get<EspionageCityState>(sim.Content);
            if (state is null || state.RevoltUntilTurn == 0 || sim.State.Turn + 1 < state.RevoltUntilTurn) continue;
            var rules = city.Extensions.Get<CityRuleState>(sim.Content);
            if (rules is not null) rules.Disorder = false;
            state.RevoltUntilTurn = 0;
        }
    }
}

/// <summary>Drops expired counter-espionage records at the first turn boundary after their expiry so dead entries cannot accumulate in authoritative state.</summary>
internal sealed class SpyExpiry : ITurnSystem
{
    public string Id => "espionage:expiry";
    public int Order => TurnPhase.GameBegin + 1;
    public void Run(Simulation sim)
    {
        foreach (var team in sim.State.Teams.OrderBy(t => t.Id))
        {
            var state = team.Extensions.Get<EspionageTeamState>(sim.Content);
            if (state is null) continue;
            foreach (int rival in state.CounterEspionage.Where(p => p.Value.ExpiresTurn <= sim.State.Turn).Select(p => p.Key).ToArray())
                state.CounterEspionage.Remove(rival);
        }
    }
}

internal sealed class SpyUnhappiness : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        if (city.Extensions.Get<EspionageCityState>(context.Sim.Content)?.UnhappyUntilTurn > context.Sim.State.Turn)
            sink.Add("espionage:foment_unhappiness", "cities:unhappiness", EspionageService.Rules(context.Sim).FomentUnhappiness);
    }
}
