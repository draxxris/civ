using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;
using Ariel.Modding;

namespace Ariel.Mods.Corporations;

public sealed class CorporationsMod : IPreparableMod
{
    /// <summary>The corporations and guilds, imported. Each is founded by this mod's building whose <c>Founds</c> names
    /// it.</summary>
    private static readonly ModJsonFile<CorporationDef[]> Corporations = new("data/corporation.json");
    /// <summary>The 21 headquarters and 15 Master Guilds that found them, the 20 stores a branch lets a city build, and
    /// the executives that spread them. They are this mod's own buildings and units, so without it the base game has
    /// none.</summary>
    private static readonly ModJsonFile<BuildingDef[]> Buildings = new("data/building.json");
    /// <summary>The kinds of building that are this mod's alone: corporation headquarters, Master Guilds and stores.</summary>
    private static readonly ModJsonFile<BuildingCategoryDef[]> BuildingCategories = new("data/building-category.json");
    private static readonly ModJsonFile<UnitDef[]> Executives = new("data/unit.json");
    /// <summary>The great person each founding building's great people points count towards, in the Great People mod's
    /// vocabulary. Read only when that mod is loaded.</summary>
    private static readonly ModJsonFile<Ariel.Mods.GreatPeople.BuildingGreatPeopleDef[]> GreatPeople = new("data/building-great-people.json");
    /// <summary>The civilizations that may not train some executives (the barbarians).</summary>
    private static readonly ModJsonFile<CivilizationCorporationsDef[]> Civilizations = new("data/civilization-corporations.json");
    private static readonly ModJsonFile<CivicCorporationsDef[]> Civics = new("data/civic-corporations.json");
    private static readonly ModJsonFile<ModifierKeyDef[]> ModifierKeys = new("data/modifier-key.json");
    /// <summary>What units, handicaps and world sizes mean to corporations, attached to the base rows.</summary>
    private static readonly ModJsonFile<UnitCorporationsDef[]> Units = new("data/unit-corporations.json");
    private static readonly ModJsonFile<HandicapCorporationsDef[]> Handicaps = new("data/handicap-corporations.json");
    private static readonly ModJsonFile<WorldSizeCorporationsDef[]> WorldSizes = new("data/world-size-corporations.json");
    /// <summary>The numbers of the Realistic Guilds/Corporations mode the source does not define.</summary>
    private static readonly ModJsonFile<CorporationRulesDef> Rules = new("data/corporation-rules.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [Corporations, Buildings, BuildingCategories, Executives, GreatPeople, Civilizations, Civics, ModifierKeys, Units, Handicaps, WorldSizes, Rules];

    public void Register(IModContext context)
    {
        var rules = Rules.Read(context.Data);
        if (rules.Problems().ToArray() is { Length: > 0 } problems)
            throw new InvalidDataException("Corporation rules validation failed:\n" + string.Join("\n", problems));
        context.Content<CorporationRulesDef>().Register(rules);
        Ariel.Rules.ContentRegistration.Load(context, Buildings);
        Ariel.Rules.ContentRegistration.Load(context, BuildingCategories);
        Ariel.Rules.ContentRegistration.Load(context, Executives);
        Ariel.Rules.ContentRegistration.Load(context, GreatPeople);
        Ariel.Rules.ContentRegistration.Load(context, Civilizations);
        Ariel.Rules.ContentRegistration.Load(context, Civics);
        Ariel.Rules.ContentRegistration.Load(context, ModifierKeys);
        Ariel.Rules.ContentRegistration.Load(context, Units);
        Ariel.Rules.ContentRegistration.Load(context, Handicaps);
        Ariel.Rules.ContentRegistration.Load(context, WorldSizes);
        var definitions = Corporations.Read(context.Data);
        foreach (var definition in definitions) context.Content<CorporationDef>().Register(definition);
        // The schema covers reference resolution and sign-free fields; these invariants are rules-level. The founding
        // buildings and executives are checked once content freezes, with every building founder registered.
        CorporationContentValidation.Validate(definitions);
        var service = new CorporationService();
        var natural = service.Natural;
        context.RegisterService<ICorporationService>("corporations", service);
        context.RegisterService<IGameScreen>("screen", new CorporationsScreen(natural));
        context.RegisterService<ICityPanel>("city", new CorporationsCityPanel());
        context.RegisterService<INotificationSource>("notices", new CorporationNotices());
        context.RegisterService<IPediaSource>("pedia", new CorporationPedia());
        context.RegisterService<IUnitTrainingRule>("executives", service);
        context.RegisterService<ISetupPolicy>("setup", new CorporationSetupPolicy());
        RegisterLeaf(context, service);
        context.RegisterService<IModifierSource>("effects", new CorporationEffects(service));
        context.RegisterService<ICityResourceSupply>("produced_resources", new CorporationResourceSupply(service));
        context.RegisterStateComponent<CorporationCityState>("city_state");
        context.RegisterStateComponent<CorporationGameState>("game_state");
        context.RegisterStateComponent<CorporationPlayerState>("player_state");
        // Only the fallback maintenance step reads a player's maintenance remainder.
        context.RegisterPrivateStateDomain("maintenance", "Players/*/Extensions/corporations:player_state");
        context.RegisterService<IBuildingFounder>("headquarters", new CorporationFounder(service));
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.BuildingSubject>>("headquarters_valuer", new HeadquartersValuer());
        context.RegisterService<IMission>("spread", new SpreadCorporationMission(service));
        context.RegisterService<IContentCheck>("founding_buildings", new FoundingBuildingCheck());
        context.RegisterService<IContentCheck>("executives", new ExecutiveTrainingCheck());
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new CorporationAiPlanner(service));
        context.RegisterService<Ariel.Ai.IProductionWishes>("executives", new ExecutiveWishes(service));
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>("civic_valuer", new CorporationCivicValuer(service, natural));
        context.RegisterCommand<SpreadCorporationCommand>("spread_corporation");
        context.TurnSystems.Register(new CorporationMaintenance(service));
        context.TurnSystems.Register(new NaturalCorporationTurn(natural));
        context.RegisterService<ITimerKind>("branch_decay", new NaturalCorporations.BranchDecay(service));
        context.RegisterService<ITimerKind>("headquarters_decay", new NaturalCorporations.HeadquartersDecay(service));
        context.RegisterService<ITimerKind>("founding_cooldown", new TimerKind(NaturalCorporations.CooldownKind));
        context.RegisterService<IContentCheck>("source_constants", new SourceConstantCheck());
        context.RegisterService<ICapitalWeight>("capital_weight", new BranchCapitalWeight(service));
        context.Subscribe<CityRazed>("corporations:dissolve", (sim, razed) => service.DissolveHeadquartersOf(sim, razed.City, razed.Player));
        context.Subscribe<TechDiscovered>("corporations:obsolete", (sim, e) => service.Obsolete(sim, e.Team, e.Tech));
    }

    /// <summary>A corporation requirement is a branch in the city (no parameter, CvCity.cpp:4039-4046) or, with parameter
    /// 2 as for a religion's holy city, the city being the corporation's headquarters, which is what an executive is
    /// trained in. Only this mod's rows name the kind, so without the mod nothing asks it. The parameter also chooses what
    /// the leaf is said to ask for, by the corporation's name.</summary>
    private static void RegisterLeaf(IModContext context, CorporationService service) =>
        Ariel.Rules.ContentRegistration.Evaluator<CorporationDef>(context, CorporationFounder.Kind, (leaf, subject) =>
            subject.City is { } city && leaf.Param switch
            {
                null or 0 => service.Present(subject.Sim, city.Id).Contains(leaf.Id, StringComparer.Ordinal),
                2 => service.IsHeadquartersOf(subject.Sim, city.Id, leaf.Id),
                _ => false,
            },
            (leaf, content) => leaf.Param switch
            {
                null or 0 => "a branch of " + (content.TryGet<CorporationDef>(leaf.Id, out var def) && def.Name.Length > 0 ? def.Name : leaf.Id),
                2 => service.SeatPhrase(content, leaf.Id) ?? "the headquarters of " + leaf.Id,
                _ => null,
            });
}

/// <summary>The Realistic Guilds/Corporations switch (<see cref="CorporationMode"/>). The option is base content
/// (<c>foundation:*</c>, on by default), so the setup screen offers it only while this mod is loaded.</summary>
internal sealed class CorporationSetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => null;

    public IEnumerable<SetupGameOption> GameOptions(ContentDb content)
    {
        if (content.TryGet<GameOptionDef>(CorporationMode.Option, out var option)) yield return new(option.Id, option.Name, option.Default, option.Help);
    }
}

internal sealed class CorporationEffects(CorporationService service) : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope == ModifierScope.Player) { CivicMaintenance(context.Sim, context.Player, sink); return; }
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        var sim = context.Sim;
        // The branches' outputs and their maintenance read the same resource counts, gathered once for the city.
        var (active, counts) = service.ActiveIn(sim, city);
        foreach (var def in active)
        {
            string corporation = def.Id;
            var (yields, commerce) = CorporationService.BranchOutput(def, CorporationService.Consumed(def, counts));
            sink.AddYield(def.Id, yields);
            sink.AddCommerce(def.Id, commerce);
            // The source's branch happiness and health, and its military production modifier. The keys are Cities'
            // (a branch is neither a building nor a resource, so its health is the general kind).
            sink.Add(def.Id, "cities:happiness", def.Happiness);
            sink.Add(def.Id, "cities:health", def.Health);
            sink.Add(def.Id, "cities:military_production_percent", def.MilitaryProductionPercent);
            if (service.Headquarters(sim, corporation) == city.Id)
            {
                int branches = sim.State.Cities.Count(c => service.IsActive(sim, c.Id, corporation));
                sink.AddCommerce(def.Id + ":headquarters", def.HeadquartersCommerce * branches);
            }
        }
        // One key: the city maintenance service folds flat costs into its own total. A second corporate key
        // would be skipped there as the same source and would suggest a settlement that never happens.
        sink.Add("corporations:maintenance", "cities:maintenance_flat_hundredths", CorporationService.MaintenanceHundredths(sim, city, active, counts));
    }

    /// <summary>The player's civics' change to corporation maintenance, with the Realistic Guilds/Corporations option choosing
    /// which of the two figures counts.</summary>
    private static void CivicMaintenance(Simulation sim, int player, ModifierSink sink)
    {
        bool realistic = CorporationMode.Realistic(sim);
        foreach (var civic in sim.State.PlayerById(player).Civics.Values.Order(StringComparer.Ordinal))
            if (sim.Content.Attachment<CivicCorporationsDef>(civic) is { } rules)
                sink.Add(civic, CorporationService.MaintenanceKey, realistic ? rules.RealisticMaintenancePercent : rules.MaintenancePercent);
    }
}
/// <summary>Fallback settlement when Economy is absent. Economy otherwise owns all city maintenance.</summary>
internal sealed class CorporationMaintenance(ICorporationService service) : IPlayerTurnSystem
{
    public string Id => "corporations:maintenance";
    public int Order => TurnPhase.Economy + 1;
    public IReadOnlyList<string>? WriteDomains => Writes;
    private static readonly IReadOnlyList<string> Writes = ["corporations:maintenance", EconomyStateDomains.Treasury];
    public void Run(Simulation sim, int player)
    {
        var owner = sim.State.PlayerById(player);
        var state = owner.Extensions.Get<CorporationPlayerState>(sim.Content);
        if (sim.Content.Services.Get<IPlayerEconomyService>().Any())
        {
            // Economy settles city maintenance without a remainder; one carried over from a save made without it
            // would otherwise resurface if Economy were removed again.
            if (state is not null) state.MaintenanceRemainder = 0;
            return;
        }
        long cost = sim.State.Cities.Where(c => c.Owner == player).Sum(c => service.MaintenanceHundredths(sim, c.Id));
        // MaintenanceHundredths is stripped of inflation for the economy's aggregate pass; nobody re-inflates it
        // here, so the fallback must, or a corporation would cost less without Economy loaded.
        cost = cost * (CorporationService.InflationPercent(sim, player) + 100) / 100;
        if (cost == 0 && state is null) return;
        state ??= new(); owner.Extensions.Set(sim.Content, state);
        long total = state.MaintenanceRemainder + cost;
        long gold = total / 100; state.MaintenanceRemainder = total % 100;

        owner.Treasury = Math.Max(0, owner.Treasury - gold);
    }
}

/// <summary>A new capital leans toward a city with corporate branches: one point for each corporation present.</summary>
internal sealed class BranchCapitalWeight(ICorporationService corporations) : ICapitalWeight
{
    public int Weight(Simulation sim, int city) => corporations.Present(sim, city).Count;
}
