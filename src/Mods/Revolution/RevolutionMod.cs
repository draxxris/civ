using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Revolution;

public sealed class RevolutionMod : IPreparableMod
{
    private static readonly ModJsonFile<RevolutionRulesDef> Rules = new("data/revolution-rules.json");
    private static readonly ModJsonFile<CivicRevolutionDef[]> Civics = new("data/civic-revolution.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Rules, Civics];

    public void Register(IModContext context)
    {
        Ariel.Rules.ContentRegistration.Load(context, Civics);
        var rules = Rules.Read(context.Data);
        ValidateThresholds(rules);
        ValidateResponses(rules);
        ValidateFactors(rules);
        context.Content<RevolutionRulesDef>().Register(rules);
        var service = new RevolutionService();
        context.RegisterService<IRevolutionService>("queries", service);
        context.RegisterService<IModifierSource>("unrest", service);
        context.RegisterStateComponent<RevolutionCityState>("city_state");
        context.RegisterCommand<SuppressRebellionCommand>("suppress_rebellion");
        // A conquest must not hand the loser's instability, cooldown or unrest to the winner.
        context.Subscribe<CityCaptured>("reset_capture", (sim, captured) =>
        {
            if (sim.State.FindCity(captured.City) is { } city) city.Extensions.Set(sim.Content, new RevolutionCityState());
        });
        context.TurnSystems.Register(new InstabilityTurns(service));
        context.TurnSystems.Register(new RebellionTurns(service));
        context.RegisterService<ICityUnrest>("unrest_level", service);
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new StabilityAiPlanner());
    }

    private static void ValidateThresholds(RevolutionRulesDef rules)
    {
        if (rules.WarningThreshold < 0 || rules.MutinyThreshold <= rules.WarningThreshold
            || rules.RebellionThreshold <= rules.MutinyThreshold)
            throw new InvalidDataException("Invalid revolution thresholds");
    }

    private static void ValidateResponses(RevolutionRulesDef rules)
    {
        if (rules.RebellionRelease <= 0 || rules.SuppressionGold <= 0 || rules.SuppressionReduction <= 0
            || rules.CooldownTurns < 1 || rules.UnrestTurns < 1 || rules.RebelUnits < 1)
            throw new InvalidDataException("Invalid revolution responses");
    }

    private static void ValidateFactors(RevolutionRulesDef rules)
    {
        if (rules.WellbeingFactor <= 0 || rules.DistanceFactor <= 0 || rules.GarrisonFactor > 0
            || rules.UndefendedFactor < 0 || rules.GoldenAgeFactor > 0 || rules.NaturalRecovery > 0
            || rules.MaxGarrison < 0 || rules.AnarchyFactor < 0 || rules.IntolerancePercent < 0
            || rules.NonStateReligionFactor < 0 || rules.MutinyUnhappiness < 0 || rules.UnrestUnhappiness < 0
            || rules.InciteInstability < 0)
            throw new InvalidDataException("Invalid revolution instability factors");
    }
}

internal sealed class InstabilityTurns(RevolutionService service) : IPlayerTurnSystem
{
    public string Id => "revolution:instability";
    public int Order => TurnPhase.PlayerEnd + 1;
    public void Run(Simulation sim, int player)
    {
        if (PlayerRules.IsBarbarian(sim, player)) return;
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id)) service.Update(sim, city);
    }
}

internal sealed class RebellionTurns(RevolutionService service) : ITurnSystem
{
    public string Id => "revolution:rebellions";
    public int Order => TurnPhase.GameEnd;
    public void Run(Simulation sim)
    {
        foreach (var city in sim.State.Cities.OrderBy(c => c.Id).ToArray()) service.Rebel(sim, city);
    }
}
