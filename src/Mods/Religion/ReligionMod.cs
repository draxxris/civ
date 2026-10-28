using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Religion;

public sealed class ReligionMod : IPreparableMod
{
    private static readonly ModJsonFile<ReligionDef[]> Religions = new("data/religion.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Religions];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Religions);
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
        context.Subscribe<TechDiscovered>("founding_discovery", ReligionFoundingRules.Discovered);
        context.Subscribe<CityFounded>("founding_city", ReligionFoundingRules.CityFounded);
        context.TurnSystems.Register(new ReligionTurns(service));
    }
}
internal sealed class ReligionTurns(ReligionService service) : IPlayerTurnSystem
{
    public string Id => "religion:city_turns";
    public int Order => TurnPhase.CityPostProduction - 1;
    public void Run(Simulation sim, int player)
    {
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id)) service.NaturalSpread(sim, city);
        var state = sim.State.PlayerById(player).Extensions.Get<ReligionPlayerState>(sim.Content);
        if (state is not null) state.ConversionTimer = Math.Max(0, state.ConversionTimer - 1);
    }
    public void Run(Simulation sim)
    {
        foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id);
    }
}
