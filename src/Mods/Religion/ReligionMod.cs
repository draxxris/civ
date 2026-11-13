using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Mods.Religion;

/// <summary>Religions, their founding, spread, decay, state religion and inquisitions, with the computer player's use
/// of them and their screens. Without this mod the base game has no religions: the religion requirement leaves and the
/// building and unit fields that name a faith by its identity answer as if no faith were present.</summary>
public sealed class ReligionMod : IPreparableMod
{
    private static readonly ModJsonFile<ReligionDef[]> Religions = new("data/religion.json");
    /// <summary>One world wonder per religion; completing it founds the religion in that city.</summary>
    private static readonly ModJsonFile<Ariel.Content.Schema.BuildingDef[]> FoundingWonders = new("data/founding-wonders.json");
    /// <summary>What an inquisition, or a razed holy city, leaves in the memory of the faithful.</summary>
    private static readonly ModJsonFile<MemoryKindDef[]> Memories = new("data/memory-kind.json");
    /// <summary>What each civic means to religion, attached to the base civics.</summary>
    private static readonly ModJsonFile<CivicReligionDef[]> Civics = new("data/civic-religion.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Religions, FoundingWonders, Memories, Civics];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Religions);
        ContentRegistration.Load(context, FoundingWonders);
        MemoryKinds.Load(context, Memories);
        ContentRegistration.Load(context, Civics);
        var service = new ReligionService();
        context.RegisterService<IReligionService>("religion", service);
        context.RegisterService<ICityReligions>("city_religions", new CityReligions(service));
        context.RegisterService<IModifierSource>("effects", new ReligionEffects());
        context.RegisterStateComponent<ReligionCityState>("city_state");
        context.RegisterStateComponent<ReligionPlayerState>("player_state");
        context.RegisterStateComponent<ReligionGameState>("game_state");
        context.RegisterCommand<SetStateReligionCommand>("set_state_religion");
        context.RegisterService<IMission>("inquisition", new InquisitionMission(service));
        context.RegisterService<IMission>("spread", new SpreadReligionMission());
        context.RegisterService<IMission>("shrine", new ShrineMission(service));
        context.Subscribe<BuildingCompleted>("founding_wonder", ReligionFoundingRules.Completed);
        context.Subscribe<CityRazed>("razed_holy_city", HolyCityRazing.Razed);
        context.TurnSystems.Register(new ReligionTurns(service));
        context.RegisterService<ICapitalWeight>("capital_weight", new FaithCapitalWeight(service));
        context.RegisterService<ISetupPolicy>("setup", new ReligionSetupPolicy());
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new ReligionAiPlanner());
        context.RegisterService<Ariel.Ai.IProductionWishes>("inquisitors", new InquisitorWishes());
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.BuildingSubject>>("founding_valuer", new FoundingWonderValuer());
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>("civic_valuer", new StateReligionCivicValuer());
        context.RegisterService<Ariel.Presentation.IGameScreen>("screen", new ReligionScreen());
        context.RegisterService<Ariel.Presentation.ICityPanel>("city", new ReligionCityPanel());
        context.RegisterService<Ariel.Presentation.ICityTag>("city_tag", new ReligionCityTag());
        context.RegisterService<Ariel.Presentation.INotificationSource>("notices", new ReligionNotices());
        context.RegisterService<Ariel.Presentation.IPediaSource>("pedia", new ReligionPedia());
    }
}

/// <summary>The setup switches religion honours. The options themselves are base content (<c>foundation:*</c>), so
/// the setup screen offers them only while this mod is loaded.</summary>
internal sealed class ReligionSetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => null;

    public IEnumerable<SetupGameOption> GameOptions(ContentDb content)
    {
        foreach (string id in (string[])["foundation:gameoption_religion_decay", "foundation:gameoption_no_inquisitions"])
            if (content.TryGet<GameOptionDef>(id, out var option)) yield return new(option.Id, option.Name, option.Default, option.Help);
        yield return new("foundation:gameoption_multiple_religion_spread", "Multiple Religion Spread", false,
            "Cities with an existing religion can still receive additional religions via natural spread.");
    }
}
/// <summary>A new capital leans toward a city with more faiths: one point for each religion present.</summary>
internal sealed class FaithCapitalWeight(IReligionService religion) : ICapitalWeight
{
    public int Weight(Simulation sim, int city) => religion.Present(sim, city).Count;
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
