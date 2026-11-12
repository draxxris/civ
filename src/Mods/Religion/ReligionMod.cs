using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Religion;

public sealed class ReligionMod : IPreparableMod
{
    private static readonly ModJsonFile<ReligionDef[]> Religions = new("data/religion.json");
    /// <summary>One world wonder per religion; completing it founds the religion in that city.</summary>
    private static readonly ModJsonFile<Ariel.Content.Schema.BuildingDef[]> FoundingWonders = new("data/founding-wonders.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Religions, FoundingWonders];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Religions);
        ContentRegistration.Load(context, FoundingWonders);
        var service = new ReligionService();
        context.RegisterService<IReligionService>("religion", service);
        context.RegisterService<IModifierSource>("effects", new ReligionEffects());
        context.RegisterService<IRequirementEvaluator>("requirements", new ReligionRequirements(service));
        context.RegisterStateComponent<ReligionCityState>("city_state");
        context.RegisterStateComponent<ReligionPlayerState>("player_state");
        context.RegisterStateComponent<ReligionGameState>("game_state");
        context.RegisterCommand<SetStateReligionCommand>("set_state_religion");
        context.RegisterCommand<SpreadReligionCommand>("spread_religion");
        context.RegisterCommand<BuildShrineCommand>("build_shrine");
        context.RegisterCommand<PerformInquisitionCommand>("perform_inquisition");
        context.Subscribe<BuildingCompleted>("founding_wonder", ReligionFoundingRules.Completed);
        context.TurnSystems.Register(new ReligionTurns(service));
    }
}
internal sealed class ReligionTurns(ReligionService service) : IPlayerTurnSystem
{
    public string Id => "religion:city_turns";
    public int Order => TurnPhase.CityPostProduction - 1;
    public void Run(Simulation sim, int player)
    {
        var spread = service.BeginSpreadTurn(sim);
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id)) service.NaturalSpread(sim, city, spread);
        var state = sim.State.PlayerById(player).Extensions.Get<ReligionPlayerState>(sim.Content);
        if (state is not null) state.ConversionTimer = Math.Max(0, state.ConversionTimer - 1);
    }
}
