using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.ScriptedWonders;

public sealed class ScriptedWondersMod : IMod
{
    public void Register(IModContext context)
    {
        foreach (var rule in context.Data.ReadJson<WonderScriptDef[]>("data/wonder-script.json"))
        {
            if (rule.IntervalTurns < 0 || rule.CompletionUnits < 0 || rule.CompletionCulture < 0
                || ((rule.IntervalTurns > 0 || rule.CompletionUnits > 0) && rule.Unit is null))
                throw new InvalidDataException($"Invalid wonder script: {rule.Id}");
            context.Content<WonderScriptDef>().Register(rule);
        }
        var service = new ScriptedWonderService();
        context.RegisterService<IScriptedWonderService>("queries", service);
        context.RegisterService<IModifierSource>("effects", service);
        context.RegisterStateComponent<ScriptedWonderCityState>("city_state");
        context.Subscribe<BuildingCompleted>("completion", service.Completed);
        context.Subscribe<CombatResolved>("victory", service.Victory);
        context.TurnSystems.Register(new WonderTurns(service));
    }
}

internal sealed class WonderTurns(ScriptedWonderService service) : IPlayerTurnSystem
{
    public string Id => "scripted_wonders:city_turns";
    public int Order => TurnPhase.CityPostProduction + 10;
    public void Run(Simulation sim, int player)
    {
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).ToArray()) service.Tick(sim, city);
    }
}
