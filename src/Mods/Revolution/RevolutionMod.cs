using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.Revolution;

public sealed class RevolutionMod : IMod
{
    public void Register(IModContext context)
    {
        var rules = context.Data.ReadJson<RevolutionRulesDef>("data/revolution-rules.json");
        ValidateThresholds(rules);
        ValidateResponses(rules);
        context.Content<RevolutionRulesDef>().Register(rules);
        var service = new RevolutionService();
        context.RegisterService<IRevolutionService>("queries", service);
        context.RegisterService<IModifierSource>("unrest", service);
        context.RegisterStateComponent<RevolutionCityState>("city_state");
        context.RegisterCommand<SuppressRebellionCommand>("suppress_rebellion");
        context.TurnSystems.Register(new InstabilityTurns(service));
        context.TurnSystems.Register(new RebellionTurns(service));
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
}

internal sealed class InstabilityTurns(RevolutionService service) : IPlayerTurnSystem
{
    public string Id => "revolution:instability";
    public int Order => TurnPhase.PlayerEnd - 1;
    public void Run(Simulation sim, int player)
    {
        if (sim.State.PlayerById(player).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true) return;
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
