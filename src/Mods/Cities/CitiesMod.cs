using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.Cities;

/// <summary>City rules communicate only through Core services, modifiers, queues and events.</summary>
public sealed class CitiesMod : IPreparableMod
{
    private static readonly ModJsonFile<BuildingCategoryDef[]> BuildingCategories = new("data/building-category.json");
    private static readonly ModJsonFile<Civ.Content.Schema.BuildingDef[]> Buildings = new("data/building.json");
    private static readonly ModJsonFile<HurryDef[]> Hurry = new("data/hurry.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [BuildingCategories, Buildings, Hurry];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, BuildingCategories);
        ContentRegistration.Load(context, Buildings);
        ContentRegistration.Load(context, Hurry);
        context.RegisterService<IRequirementEvaluator>("building_requirements", new BuildingRequirement("building"));
        context.RegisterService<IRequirementEvaluator>("class_requirements", new BuildingRequirement("building-class"));
        context.RegisterStateComponent<CityRuleState>("city_state");
        context.RegisterStateComponent<CityPlayerState>("player_state");
        context.RegisterService<ICityAppearanceService>("city_appearance", new CityAppearanceService());
        context.RegisterService<ICityBuildingService>("building_state", new CityBuildingService());
        context.RegisterService<IModifierSource>("buildings", new BuildingEffects());
        context.RegisterService<IModifierSource>("military_building_grants", new BuildingMilitaryEffects());
        context.RegisterService<ICityCaptureService>("capture", new CityCapture());
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
        context.RegisterService<IProductionKind>("building_kind", new ProductionKind<Civ.Content.Schema.BuildingDef>("cities:building", "Buildings", b => b.Cost, production,
            BuildingScope.Of, BuildingProductionCategories.All));
        context.RegisterService<IProductionKind>("unit_kind", new ProductionKind<Civ.Content.Schema.UnitDef>("cities:unit", "Units", u => u.Cost, production,
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
        context.TurnSystems.Register(new CityPhase("puppet_work", TurnPhase.PlayerBegin, PreparePuppet));
        context.TurnSystems.Register(new CityPhase("conquest_timers", TurnPhase.PlayerEnd, SettleConquest));
        context.TurnSystems.Register(new ConscriptPlayerPhase("reset_drafts", TurnPhase.PlayerBegin, CityConscript.ResetDrafts));
        context.TurnSystems.Register(new CityPhase("defense_recovery", TurnPhase.PlayerBegin + 1, RecoverDefense));
        context.TurnSystems.Register(new ConscriptPlayerPhase("conscript_anger", TurnPhase.PlayerEnd, CityConscript.DecayAnger));
        context.TurnSystems.Register(new AssignWorkPhase(work));
        context.TurnSystems.Register(new CityPhase("growth", TurnPhase.CityGrowth, growth.Run));
        context.TurnSystems.Register(new CityPhase("culture", TurnPhase.CityCulture, culture.Accumulate));
        context.TurnSystems.Register(new CityPhase("borders", TurnPhase.CityPlotCulture, culture.Spread));
        context.TurnSystems.Register(new CityPhase("production", TurnPhase.CityProduction, production.Work));
        context.TurnSystems.Register(new CityPhase("completion", TurnPhase.CityCompletion, production.Complete));
        context.TurnSystems.Register(new CityCultureDecayPhase());
        context.Subscribe<TechDiscovered>("era_progression", EraProgression.Discovered);
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
        state.OccupationTurns = Math.Max(0, state.OccupationTurns - 1);
        if (!state.PendingConquestChoice) return;
        sim.Content.Services.Get<ICityCommandService>().Single().ResolveConquestChoice(sim, city.Owner, city.Id, CityConquestOption.Puppet);
    }
    private static void RecoverDefense(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        if (!state.BombardedThisTurn && state.DefenseDamage > 0)
            state.DefenseDamage = Math.Max(0, state.DefenseDamage - CityData.Constants(sim).CityDefenseDamageHealRate);
        state.BombardedThisTurn = false;
    }
}
