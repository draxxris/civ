using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;
using Ariel.Content.Schema;

namespace Ariel.Mods.Espionage;

public sealed class EspionageMod : IPreparableMod
{
    private static readonly ModJsonFile<EspionageRulesDef> Rules = new("data/espionage-rules.json");
    /// <summary>What a caught spy leaves in the memory of the city's owner.</summary>
    private static readonly ModJsonFile<MemoryKindDef[]> Memories = new("data/memory-kind.json");
    /// <summary>Which units are spies and which defend against them, attached to the base units.</summary>
    private static readonly ModJsonFile<UnitEspionageDef[]> Units = new("data/unit-espionage.json");
    /// <summary>The espionage commerce type, and the process that turns production into it.</summary>
    private static readonly ModJsonFile<CommerceDef[]> Commerce = new("data/commerce.json");
    private static readonly ModJsonFile<ProcessDef[]> Processes = new("data/process.json");
    /// <summary>The spy specialist, the spy units and the mod's game options.</summary>
    private static readonly ModJsonFile<SpecialistDef[]> OwnSpecialists = new("data/specialist.json");
    private static readonly ModJsonFile<UnitDef[]> OwnUnits = new("data/unit.json");
    private static readonly ModJsonFile<GameOptionDef[]> Options = new("data/game-option.json");
    /// <summary>Civilizations' versions of the spy units and leaders' attitudes to a caught spy, added to their rows.</summary>
    private static readonly ModJsonFile<CivilizationEspionageDef[]> Civilizations = new("data/civilization-espionage.json");
    private static readonly ModJsonFile<LeaderEspionageDef[]> Leaders = new("data/leader-espionage.json");
    /// <summary>The espionage the base game's buildings, civics, traits and specialists give, added to their rows.</summary>
    private static readonly ModJsonFile<BuildingEspionageDef[]> Buildings = new("data/building-espionage.json");
    private static readonly ModJsonFile<CivicEspionageDef[]> Civics = new("data/civic-espionage.json");
    private static readonly ModJsonFile<TraitEspionageDef[]> Traits = new("data/trait-espionage.json");
    private static readonly ModJsonFile<SpecialistEspionageDef[]> Specialists = new("data/specialist-espionage.json");
    /// <summary>The espionage optional mods' rows give, registered only when that mod is loaded.</summary>
    private static readonly ModJsonFile<BuildingEspionageDef[]> CorporationBuildings = new("data/corporations/building-espionage.json");
    private static readonly ModJsonFile<BuildingReligionEspionageDef[]> ReligionBuildings = new("data/religion/building-religion-espionage.json");
    private static readonly ModJsonFile<BuildingEspionageDef[]> ReligiousBuildings = new("data/religion/building-espionage.json");
    /// <summary>The great person the spy specialist's points produce, in the Great People mod's own row type.</summary>
    private static readonly ModJsonFile<Ariel.Mods.GreatPeople.SpecialistGreatPeopleDef[]> GreatPeople = new("data/great_people/specialist-great-people.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [Rules, Memories, Units, Commerce, Processes, OwnSpecialists, OwnUnits, Options, Buildings, Civics, Traits, Specialists,
         Civilizations, Leaders, CorporationBuildings, ReligionBuildings, ReligiousBuildings, GreatPeople];

    public void Register(IModContext context)
    {
        var rules = Rules.Read(context.Data);
        Validate(rules);
        context.Content<EspionageRulesDef>().Register(rules);
        MemoryKinds.Load(context, Memories);
        ContentRegistration.Load(context, Commerce);
        ContentRegistration.Load(context, Processes);
        ContentRegistration.Load(context, OwnSpecialists);
        ContentRegistration.Load(context, OwnUnits);
        ContentRegistration.Load(context, Units);
        ContentRegistration.Load(context, Options);
        ContentRegistration.Load(context, Civilizations);
        ContentRegistration.Load(context, Leaders);
        ContentRegistration.Load(context, Buildings);
        ContentRegistration.Load(context, Civics);
        ContentRegistration.Load(context, Traits);
        ContentRegistration.Load(context, Specialists);
        if (context.IsLoaded("corporations")) ContentRegistration.Load(context, CorporationBuildings);
        if (context.IsLoaded("religion"))
        {
            ContentRegistration.Load(context, ReligionBuildings);
            ContentRegistration.Load(context, ReligiousBuildings);
        }
        if (context.IsLoaded("great_people")) ContentRegistration.Load(context, GreatPeople);
        var service = new EspionageService();
        context.RegisterService<IEspionageService>("espionage", service);
        context.RegisterStateComponent<EspionageTeamState>("team_state");
        context.RegisterStateComponent<EspionagePlayerState>("player_state");
        // Espionage points and remainders are read by Espionage's own rules and planners, never by a modifier source.
        context.RegisterPrivateStateDomain("points", "Players/*/Extensions/espionage:player_state", "Teams/*/Extensions/espionage:team_state");
        context.RegisterCommand<SetEspionageWeightCommand>("set_weight");
        // Fomenting again restarts the unrest, and a new counter-espionage restarts its protection. Both, and a spy's
        // revolt, end if the city changes hands, as the Espionage state of a conquered city always did.
        context.RegisterService<ITimerKind>("foment_unhappiness", new TimerKind(SpyMissionRules.FomentUnhappinessKind));
        context.RegisterService<ITimerKind>("counter_espionage", new TimerKind(EspionageService.CounterEspionageKind));
        context.RegisterService<ITimerKind>("revolt", new SpyRevolt());
        // Registration order is the order a spy's panel lists them in.
        foreach (var kind in EspionageMissionOrder.Display)
            context.RegisterService<IMission>(EspionageMissions.Of(kind)["espionage:".Length..], new SpyMission(service, service.Missions, kind));
        context.Subscribe<EspionageMissionResult>("memory_spy", RememberCaughtSpy);
        context.TurnSystems.Register(new SpyAllocation(service));
        context.RegisterService<Ariel.Presentation.IGameScreen>("screen", new EspionageScreen());
        context.RegisterService<Ariel.Presentation.INotificationSource>("notices", new EspionageNotices());
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new EspionageAiPlanner());
        context.RegisterService<Ariel.Ai.IProductionWishes>("spies", new SpyWishes());
    }

    /// <summary>The owner of the target city remembers a spy it caught.</summary>
    private static void RememberCaughtSpy(Simulation sim, EspionageMissionResult result)
    {
        if (!result.Success && sim.State.FindCity(result.City) is { } city)
            sim.Rules().Relations.Remember(sim, city.Owner, result.Player, EspionageMemories.SpyCaught);
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

/// <summary>Memory kinds the Espionage mod records (<c>data/memory-kind.json</c>).</summary>
public static class EspionageMemories
{
    /// <summary>Remembered by the owner of a city where a spy was caught, toward the spy's owner.</summary>
    public const string SpyCaught = "espionage:spy_caught";
}

internal sealed class SpyAllocation(EspionageService service) : IPlayerTurnSystem
{
    public string Id => "espionage:allocation";
    /// <summary>After research settles, in the slot for spending a mod's own commerce type.</summary>
    public int Order => TurnPhase.CommerceSpending;
    public IReadOnlyList<string>? WriteDomains => Points;
    private static readonly IReadOnlyList<string> Points = ["espionage:points"];
    public void Run(Simulation sim, int player) => service.Allocate(sim, player);
}

/// <summary>A spy's revolt: the city is in disorder while the timer runs. It sets the city's disorder flag when it
/// starts and clears it when its time runs out, at the end of the last turn it applies, in the owner's turn, so a
/// city keeps exactly the configured number of turns regardless of when the turn interval runs. A conquest ends it
/// without clearing the flag: the capture already did.</summary>
internal sealed class SpyRevolt() : TimerKind(SpyMissionRules.RevoltKind)
{
    public override TimerExpiry Expiry => TimerExpiry.OwnerTurnEnd;
    public override void Started(Simulation sim, GameTimer timer) => SetDisorder(sim, timer, true);
    public override void Changed(Simulation sim, GameTimer before, GameTimer after) => SetDisorder(sim, after, true);
    public override void Ended(Simulation sim, GameTimer timer, TimerEnd reason)
    {
        if (reason == TimerEnd.Expired) SetDisorder(sim, timer, false);
    }

    private static void SetDisorder(Simulation sim, GameTimer timer, bool disorder)
    {
        if (timer.Holder.City is not { } id || sim.State.FindCity(id) is not { } city) return;
        var rules = city.Extensions.Get<CityRuleState>(sim.Content);
        if (rules is null && !disorder) return;
        if (rules is null) city.Extensions.Set(sim.Content, rules = new());
        rules.Disorder = disorder;
    }
}

