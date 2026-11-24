using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.ScriptedWonders;

public sealed class ScriptedWondersMod : IPreparableMod
{
    private static readonly ModJsonFile<WonderScriptDef[]> Scripts = new("data/wonder-script.json");
    /// <summary>The scripts of religious wonders (the Crusade), registered only with Religion, whose buildings they are.</summary>
    private static readonly ModJsonFile<WonderScriptDef[]> ReligiousScripts = new("data/religion/wonder-script.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Scripts, ReligiousScripts];

    public void Register(IModContext context)
    {
        var rules = Scripts.Read(context.Data).AsEnumerable();
        if (context.IsLoaded("religion")) rules = rules.Concat(ReligiousScripts.Read(context.Data));
        foreach (var rule in rules)
        {
            if (rule.IntervalTurns < 0 || rule.CompletionUnits < 0 || rule.CompletionCulture < 0
                || ((rule.IntervalTurns > 0 || rule.CompletionUnits > 0) && rule.Unit is null))
                throw new InvalidDataException($"Invalid wonder script: {rule.Id}");
            context.Content<WonderScriptDef>().Register(rule);
        }
        var service = new ScriptedWonderService();
        context.RegisterService<IScriptedWonderService>("queries", service);
        context.RegisterService<Ariel.Presentation.ICityPanel>("city", new WonderCityPanel());
        context.RegisterService<Ariel.Presentation.INotificationSource>("notices", new WonderNotices());
        context.RegisterService<IModifierSource>("effects", service);
        context.RegisterStateComponent<ScriptedWonderCityState>("city_state");
        // Wonder scripts' progress is read only by their own steps; completions and periodic units widen the step.
        context.RegisterPrivateStateDomain("progress", "Cities/*/Extensions/scripted_wonders:city_state");
        context.Subscribe<BuildingCompleted>("completion", service.Completed);
        context.Subscribe<CombatResolved>("victory", service.Victory);
        context.TurnSystems.Register(new WonderTurns(service));
        context.RegisterService<IFreeBuildingGrants>("free_buildings", service);
        // Asked by the base game's movement rules for every unit; it bars only barbarians, so without the Barbarians
        // mod it bars nothing.
        context.RegisterService<IMovementBarrier>("border_wonders", service);
    }
}

internal sealed class WonderTurns(ScriptedWonderService service) : IPlayerTurnSystem
{
    public string Id => "scripted_wonders:city_turns";
    public int Order => TurnPhase.CityPostProduction + 10;
    public IReadOnlyList<string>? WriteDomains => Progress;
    private static readonly IReadOnlyList<string> Progress = ["scripted_wonders:progress"];
    public void Run(Simulation sim, int player)
    {
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).ToArray()) service.Tick(sim, city);
    }
}
