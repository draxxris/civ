using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Rules.Cities;

/// <summary>City rules communicate only through Core services, modifiers, queues and events.</summary>
public sealed class CitiesModule : IPreparableMod
{
    private static readonly ModJsonFile<BuildingCategoryDef[]> BuildingCategories = new("data/building-category.json");
    private static readonly ModJsonFile<Ariel.Content.Schema.BuildingDef[]> Buildings = new("data/building.json");
    private static readonly ModJsonFile<HurryDef[]> Hurry = new("data/hurry.json");
    private static readonly ModJsonFile<ModifierKeyDef[]> ModifierKeys = new("data/modifier-key.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [BuildingCategories, Buildings, Hurry, ModifierKeys];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, BuildingCategories);
        ContentRegistration.Load(context, Buildings);
        ContentRegistration.Load(context, Hurry);
        ContentRegistration.Load(context, ModifierKeys);
        context.RegisterService<IRequirementEvaluator>("building_requirements", new BuildingRequirement("building"));
        context.RegisterService<IRequirementEvaluator>("class_requirements", new BuildingRequirement("building-class"));
        context.RegisterStateComponent<CityRuleState>("city_state");
        context.RegisterStateComponent<CityPlayerState>("player_state");
        context.RegisterStateDomain("work", "Cities/*/Worked", "Cities/*/Specialists",
            "Cities/*/Extensions/cities:city_state/ManualWork", "Cities/*/Extensions/cities:city_state/AvoidGrowth");
        context.RegisterStateDomain("queue", "Cities/*/ProductionQueue", "Cities/*/NextOrderId",
            "Cities/*/Extensions/cities:city_state/PausedProduction");
        context.RegisterStateDomain("growth", "Cities/*/FoodStored", "Cities/*/Population",
            "Cities/*/Extensions/cities:city_state/FoodKept", "Cities/*/Extensions/cities:city_state/HighestPopulation");
        context.RegisterStateDomain("culture", "Cities/*/Culture", "Cities/*/Extensions/cities:city_state/CultureRemainder",
            "Cities/*/Extensions/cities:city_state/LastCultureRateHundredths", "Cities/*/Extensions/cities:city_state/CultureHundredths");
        context.RegisterStateDomain("plot_culture", "Plots/*/Culture");
        context.RegisterStateDomain("conscript_anger", "Cities/*/Extensions/cities:city_state/ConscriptAngerTurns");
        context.RegisterPrivateStateDomain("occupation", "Cities/*/Extensions/cities:city_state/OccupationTurns");
        context.RegisterPrivateStateDomain("drafts", "Cities/*/Extensions/cities:city_state/DraftedThisTurn",
            "Players/*/Extensions/cities:player_state/ConscriptCountThisTurn");
        context.RegisterPrivateStateDomain("defense", "Cities/*/Extensions/cities:city_state/DefenseDamage",
            "Cities/*/Extensions/cities:city_state/BombardedThisTurn");
        context.RegisterStateDomain("production", "Cities/*/Extensions/cities:city_state/PendingCommit",
            "Cities/*/Extensions/cities:city_state/Overflow", "Cities/*/Extensions/cities:city_state/FeatureProduction");
        context.RegisterService<ICityAppearanceService>("city_appearance", new CityAppearanceService());
        context.RegisterService<ICityBuildingService>("building_state", new CityBuildingService());
        context.RegisterService<IModifierSource>("buildings", new BuildingEffects());
        context.RegisterService<IModifierSource>("military_building_grants", new BuildingMilitaryEffects());
        context.RegisterService<IModifierSource>("timer_modifiers", new TimerModifiers());
        context.RegisterService<ICityCaptureService>("capture", new CityCapture());
        context.TurnSystems.Register(new FactionBreakaway());
        var yields = new CityYields();
        var wellbeing = new CityWellbeingService();
        var work = new CityWork(wellbeing);
        var growth = new CityGrowth(yields, wellbeing, work);
        var culture = new CityCulture(yields, work);
        var production = new CityProduction(yields, growth);
        var conscript = new CityConscript(work, culture, growth);
        context.RegisterService<ICityConscriptService>("conscript", conscript);
        context.RegisterService<IModifierSource>("conscript_anger", conscript);
        context.RegisterService<ICityCultureService>("culture_grants", culture);
        // A building is unique within its city; one whose limit allows a single copy is also one per player, so a second
        // city of the same player cannot queue it. Different players still race to finish the same wonder.
        context.RegisterService<IProductionKind>("building_kind", new ProductionKind<Ariel.Content.Schema.BuildingDef>("cities:building", "Buildings", b => b.Cost, production,
            BuildingScope.Of, BuildingProductionCategories.All));
        context.RegisterService<IProductionKind>("unit_kind", new ProductionKind<Ariel.Content.Schema.UnitDef>("cities:unit", "Units", u => u.Cost, production,
            _ => QueueScope.None));
        context.RegisterService<ILedgerSourceLabel>("plot_labels", new PlotSourceLabel());
        context.RegisterService<ICityYieldService>("yields", yields);
        context.RegisterService<ICityWellbeingService>("wellbeing", wellbeing);
        context.RegisterService<ICityWorkService>("work", work);
        context.RegisterService<ICityGrowthService>("growth", growth);
        context.RegisterService<ICityProductionService>("production", production);
        context.RegisterService<ICityMaintenanceService>("maintenance", new CityMaintenance());

        context.RegisterService<ICityRequirementService>("requirements", new CityRequirements());
        context.RegisterService<ICityCommandService>("commands", new CityCommands(production, work));
        context.RegisterCommand<FoundCityWithSettlerCommand>("found_city");
        context.RegisterCommand<SetCityAssignmentsCommand>("set_city_work");
        context.RegisterCommand<SetCityProductionCommand>("set_city_production");
        context.RegisterCommand<MoveCityQueueCommand>("move_city_queue");
        context.RegisterCommand<ConscriptCityCommand>("conscript");
        context.RegisterCommand<ResolveCityConquestCommand>("resolve_conquest");
        context.TurnSystems.Register(new CityPhase("puppet_work", TurnPhase.PlayerBegin, PreparePuppet, CityStateDomains.WorkOnly));
        context.TurnSystems.Register(new CityPhase("conquest_timers", TurnPhase.PlayerEnd, SettleConquest, CityStateDomains.OccupationOnly));
        context.TurnSystems.Register(new ConscriptPlayerPhase("reset_drafts", TurnPhase.PlayerBegin, CityConscript.ResetDrafts, CityStateDomains.DraftsOnly));
        context.TurnSystems.Register(new CityPhase("defense_recovery", TurnPhase.PlayerBegin + 1, RecoverDefense, CityStateDomains.DefenseOnly));
        context.TurnSystems.Register(new ConscriptPlayerPhase("conscript_anger", TurnPhase.PlayerEnd, CityConscript.DecayAnger, CityStateDomains.ConscriptAngerOnly));
        context.TurnSystems.Register(new AssignWorkPhase(work));
        context.TurnSystems.Register(new CityPhase("growth", TurnPhase.CityGrowth, growth.Run, CityStateDomains.GrowthAndWork));
        context.TurnSystems.Register(new CityPhase("culture", TurnPhase.CityCulture, culture.Accumulate, CityStateDomains.CultureOnly));
        context.TurnSystems.Register(new CityPhase("borders", TurnPhase.CityPlotCulture, culture.Spread, CityStateDomains.BordersWrites));
        context.TurnSystems.Register(new CityPhase("production", TurnPhase.CityProduction, production.Work, CityStateDomains.ProductionWrites));
        context.TurnSystems.Register(new CityPhase("completion", TurnPhase.CityCompletion, production.Complete, CityStateDomains.ProductionWrites));
        context.TurnSystems.Register(new CityCultureDecayPhase());
        context.Subscribe<TechDiscovered>("era_progression", EraProgression.Discovered);
        context.Subscribe<BuildingCompleted>("founding", BuildingFounding.Completed);
        context.Subscribe<FeatureProduction>("feature_production", (sim, grant) =>
        {
            if (grant.City is { } id && sim.State.FindCity(id) is { } city && city.Owner == grant.Player)
                CityData.Edit(sim, city).FeatureProduction += grant.Production;
        });
    }

    private static void PreparePuppet(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        if (state.IsPuppet) state.ManualWork = false;
    }
    private static void SettleConquest(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        // The step declares only the private occupation turns: the end of an occupation (the city leaves disorder)
        // and a settled conquest are writes beyond them.
        if (state.OccupationTurns == 1 || state.Governance == CityGovernance.PendingConquest) sim.WidenWriteWindow();
        state.OccupationTurns = Math.Max(0, state.OccupationTurns - 1);
        if (state.Governance != CityGovernance.PendingConquest) return;
        sim.Rules().CityCommands.ResolveConquestChoice(sim, city.Owner, city.Id, CityConquestOption.Puppet);
    }
    private static void RecoverDefense(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        if (!state.BombardedThisTurn && state.DefenseDamage > 0)
            state.DefenseDamage = Math.Max(0, state.DefenseDamage - CityData.Constants(sim).CityDefenseDamageHealRate);
        state.BombardedThisTurn = false;
    }
}
