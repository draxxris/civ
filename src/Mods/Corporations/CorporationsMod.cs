using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;
using Ariel.Modding;

namespace Ariel.Mods.Corporations;

public sealed class CorporationsMod : IPreparableMod
{
    /// <summary>The corporations and guilds, imported. Each is founded by the base building whose
    /// <c>FoundsCorporation</c> names its identity.</summary>
    private static readonly ModJsonFile<CorporationDef[]> Corporations = new("data/corporation.json");
    private static readonly ModJsonFile<CivicCorporationsDef[]> Civics = new("data/civic-corporations.json");
    private static readonly ModJsonFile<ModifierKeyDef[]> ModifierKeys = new("data/modifier-key.json");
    /// <summary>What units, handicaps and world sizes mean to corporations, attached to the base rows.</summary>
    private static readonly ModJsonFile<UnitCorporationsDef[]> Units = new("data/unit-corporations.json");
    private static readonly ModJsonFile<HandicapCorporationsDef[]> Handicaps = new("data/handicap-corporations.json");
    private static readonly ModJsonFile<WorldSizeCorporationsDef[]> WorldSizes = new("data/world-size-corporations.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Corporations, Civics, ModifierKeys, Units, Handicaps, WorldSizes];

    public void Register(IModContext context)
    {
        Ariel.Rules.ContentRegistration.Load(context, Civics);
        Ariel.Rules.ContentRegistration.Load(context, ModifierKeys);
        Ariel.Rules.ContentRegistration.Load(context, Units);
        Ariel.Rules.ContentRegistration.Load(context, Handicaps);
        Ariel.Rules.ContentRegistration.Load(context, WorldSizes);
        var definitions = Corporations.Read(context.Data);
        foreach (var definition in definitions) context.Content<CorporationDef>().Register(definition);
        // The schema covers reference resolution and sign-free fields; these invariants are rules-level. The founding
        // buildings and executives belong to other modules and are checked once content freezes.
        CorporationContentValidation.Validate(definitions);
        var service = new CorporationService();
        context.RegisterService<ICorporationService>("corporations", service);
        context.RegisterService<IGameScreen>("screen", new CorporationsScreen());
        context.RegisterService<ICityPanel>("city", new CorporationsCityPanel());
        context.RegisterService<INotificationSource>("notices", new CorporationNotices());
        context.RegisterService<IPediaSource>("pedia", new CorporationPedia());
        context.RegisterService<ICityCorporations>("city_corporations", service);
        context.RegisterService<IModifierSource>("effects", new CorporationEffects(service));
        context.RegisterService<ICityResourceSupply>("produced_resources", new CorporationResourceSupply(service));
        context.RegisterStateComponent<CorporationCityState>("city_state");
        context.RegisterStateComponent<CorporationGameState>("game_state");
        context.RegisterStateComponent<CorporationPlayerState>("player_state");
        context.RegisterService<IBuildingFounder>("headquarters", new CorporationFounder(service));
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.BuildingSubject>>("headquarters_valuer", new HeadquartersValuer());
        context.RegisterService<IMission>("spread", new SpreadCorporationMission(service));
        context.RegisterService<IContentCheck>("founding_buildings", new FoundingBuildingCheck());
        context.RegisterService<IContentCheck>("executives", new ExecutiveTrainingCheck());
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new CorporationAiPlanner(service));
        context.RegisterService<Ariel.Ai.IProductionWishes>("executives", new ExecutiveWishes(service));
        context.RegisterService<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>("civic_valuer", new CorporationCivicValuer(service));
        context.RegisterCommand<SpreadCorporationCommand>("spread_corporation");
        context.TurnSystems.Register(new CorporationMaintenance(service));
        context.RegisterService<ICapitalWeight>("capital_weight", new BranchCapitalWeight(service));
        context.Subscribe<CityRazed>("corporations:dissolve", (sim, razed) => service.DissolveHeadquartersOf(sim, razed.City, razed.Player));
        context.Subscribe<TechDiscovered>("corporations:obsolete", (sim, e) => service.Obsolete(sim, e.Team, e.Tech));
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
        IReadOnlyList<CorporationDef> active = service.Present(sim, city.Id).Count == 0 ? [] : service.ActiveIn(sim, city);
        var counts = CorporationService.ConsumedCounts(sim, city, active);
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

    /// <summary>The player's civics' change to corporation maintenance, with the realistic corporations option choosing
    /// which of the two figures counts.</summary>
    private static void CivicMaintenance(Simulation sim, int player, ModifierSink sink)
    {
        bool realistic = sim.IsOptionEnabled("foundation:gameoption_realistic_corporations");
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
