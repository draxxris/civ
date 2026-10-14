using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.Cities;

/// <summary>City rules communicate only through Core services, modifiers, queues and events.</summary>
public sealed class CitiesMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<BuildingCategoryDef>(context, "building-category.json");
        ContentRegistration.Load<Civ.Content.Schema.BuildingDef>(context, "building.json");
        ContentRegistration.Load<HurryDef>(context, "hurry.json");
        context.RegisterService<IRequirementEvaluator>("building_requirements", new BuildingRequirement("building"));
        context.RegisterService<IRequirementEvaluator>("class_requirements", new BuildingRequirement("building-class"));
        context.RegisterStateComponent<CityRuleState>("city_state");
        context.RegisterStateComponent<CityPlayerState>("player_state");
        context.RegisterService<IModifierSource>("buildings", new BuildingEffects());
        context.RegisterService<IModifierSource>("military_building_grants", new BuildingMilitaryEffects());
        context.RegisterService<ICityCaptureService>("capture", new CityCapture());
        var yields = new CityYields();
        var wellbeing = new CityWellbeingService();
        var work = new CityWork(wellbeing);
        var growth = new CityGrowth(yields, wellbeing, work);
        var culture = new CityCulture(yields, work);
        var production = new CityProduction(yields, growth);
        context.RegisterService<ICityCultureService>("culture_grants", culture);
        context.RegisterService<IProductionKind>("building_kind", new ProductionKind<Civ.Content.Schema.BuildingDef>("cities:building", "Buildings", b => b.Cost, production));
        context.RegisterService<IProductionKind>("unit_kind", new ProductionKind<Civ.Content.Schema.UnitDef>("cities:unit", "Units", u => u.Cost, production));
        context.RegisterService<ILedgerSourceLabel>("plot_labels", new PlotSourceLabel());
        context.RegisterService<ICityYieldService>("yields", yields);
        context.RegisterService<ICityWellbeingService>("wellbeing", wellbeing);
        context.RegisterService<ICityWorkService>("work", work);
        context.RegisterService<ICityGrowthService>("growth", growth);
        context.RegisterService<ICityProductionService>("production", production);
        context.RegisterService<ICityMaintenanceService>("maintenance", new CityMaintenance());

        context.RegisterService<ICityRequirementService>("requirements", new CityRequirements(work));
        context.RegisterService<ICityCommandService>("commands", new CityCommands(production, work));
        context.RegisterCommand<FoundCityWithSettlerCommand>("found_city");
        context.RegisterCommand<SetCityAssignmentsCommand>("set_city_work");
        context.RegisterCommand<SetCityProductionCommand>("set_city_production");
        context.TurnSystems.Register(new AssignWorkPhase(work));
        context.TurnSystems.Register(new CityPhase("growth", TurnPhase.CityGrowth, growth.Run));
        context.TurnSystems.Register(new CityPhase("culture", TurnPhase.CityCulture, culture.Accumulate));
        context.TurnSystems.Register(new CityPhase("borders", TurnPhase.CityPlotCulture, culture.Spread));
        context.TurnSystems.Register(new CityPhase("production", TurnPhase.CityProduction, production.Work));
        context.TurnSystems.Register(new CityPhase("completion", TurnPhase.CityCompletion, production.Complete));

        context.Subscribe<TechDiscovered>("era_progression", EraProgression.Discovered);
        context.Subscribe<FeatureProduction>("feature_production", (sim, grant) =>
        {
            if (grant.City is { } id && sim.State.FindCity(id) is { } city && city.Owner == grant.Player)
                CityData.Edit(sim, city).FeatureProduction += grant.Production;
        });
    }
}
