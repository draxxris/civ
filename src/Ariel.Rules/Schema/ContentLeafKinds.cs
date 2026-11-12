namespace Ariel.Content.Schema;

/// <summary>Shared leaf identity types. Feature mods own evaluation, not this table.</summary>
public static class ContentLeafKinds
{
    public static IReadOnlyDictionary<string, Type> Types { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["tech"] = typeof(TechDef), ["bonus"] = typeof(BonusDef), ["feature"] = typeof(FeatureDef),
        ["terrain"] = typeof(TerrainDef), ["improvement"] = typeof(ImprovementDef), ["route"] = typeof(RouteDef),
        ["build"] = typeof(BuildDef), ["building"] = typeof(BuildingDef), ["building-class"] = typeof(BuildingDef),
        ["unit"] = typeof(UnitDef), ["unit-class"] = typeof(UnitDef), ["unit-combat"] = typeof(UnitCombatDef),
        ["promotion"] = typeof(PromotionDef), ["civic"] = typeof(CivicDef), ["era"] = typeof(EraDef),
        ["culture-level"] = typeof(CultureLevelDef), ["condition"] = typeof(ConditionDef),
        ["game-option"] = typeof(GameOptionDef), ["religion"] = typeof(ReligionDef),
        ["corporation"] = typeof(CorporationIdentityDef), ["victory"] = typeof(VictoryIdentityDef)
    };
}
