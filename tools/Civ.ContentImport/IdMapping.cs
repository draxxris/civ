using System.Text.RegularExpressions;

namespace Civ.ContentImport;

public sealed record IdEntry(string Class, string SourceType, string Mod, string Id, string? Era);

public static class IdMapping
{
    public static readonly IReadOnlyDictionary<string, string> ClassMods = BuildClassMods();
    private static readonly IReadOnlyDictionary<string, string> StrippedPrefixes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["TechInfo"] = "TECH_", ["BuildingInfo"] = "BUILDING_", ["UnitInfo"] = "UNIT_",
        ["CivilizationInfo"] = "CIVILIZATION_", ["CivicInfo"] = "CIVIC_",
        ["TerrainInfo"] = "TERRAIN_", ["FeatureInfo"] = "FEATURE_", ["BonusInfo"] = "BONUS_",
        ["ImprovementInfo"] = "IMPROVEMENT_", ["RouteInfo"] = "ROUTE_", ["BuildInfo"] = "BUILD_",
        ["ReligionInfo"] = "RELIGION_", ["CorporationInfo"] = "CORPORATION_",
        ["ProjectInfo"] = "PROJECT_", ["EventInfo"] = "EVENT_", ["PromotionInfo"] = "PROMOTION_"
    };

    public static string CanonicalClass(string value) => Branding.Identifier(value);
    public static string Id(string family, string type)
    {
        family = CanonicalClass(family);
        if (!ClassMods.TryGetValue(family, out var mod)) throw new InvalidDataException($"No mod assignment for {family}.");
        var name = type;
        if (StrippedPrefixes.TryGetValue(family, out var prefix) && name.StartsWith(prefix, StringComparison.Ordinal)) name = name[prefix.Length..];
        return mod + ":" + Normalize(name);
    }

    public static IReadOnlyList<IdEntry> Create(IEnumerable<EffectiveRecord> records)
    {
        var entries = records.Select(x => new IdEntry(CanonicalClass(x.Class), x.Type,
            ClassMods[CanonicalClass(x.Class)], Id(x.Class, x.Type), x.Era)).ToArray();
        var collisions = entries.GroupBy(x => x.Id, StringComparer.Ordinal).Where(x => x.Count() > 1)
            .SelectMany(x => x.Select(y => (y.Class, y.SourceType))).ToHashSet();
        entries = entries.Select(x => collisions.Contains((x.Class, x.SourceType))
            ? x with { Id = x.Mod + ":" + Normalize(x.Class[..^4]) + "_" + Normalize(x.SourceType) } : x).ToArray();
        if (entries.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
            throw new InvalidDataException("Game IDs are not unique after class disambiguation.");
        return entries.OrderBy(x => x.Class, StringComparer.Ordinal).ThenBy(x => x.SourceType, StringComparer.Ordinal).ToArray();
    }

    private static string Normalize(string value) => Regex.Replace(Branding.Identifier(value).ToLowerInvariant(), @"[^a-z0-9_]+", "_").Trim('_');

    private static IReadOnlyDictionary<string, string> BuildClassMods()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Assign(string mod, string classes)
        {
            foreach (var family in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) result.Add(family, mod);
        }
        Assign("foundation", "YieldInfo CommerceInfo EraInfo GameSpeedInfo HandicapInfo WorldInfo CultureLevelInfo SpecialistInfo CalendarInfo TurnTimerInfo GameOptionInfo PlayerOptionInfo MPOptionInfo ForceControlInfo GraphicOptionInfo ControlInfo InterfaceModeInfo CityTabInfo AdvisorInfo EmphasizeInfo MonthInfo SeasonInfo CameraInfo LandscapeInfo PlayerColorInfo ConceptInfo NewConceptInfo SourceConceptInfo InterfaceArtInfo MiscArtInfo CursorInfo AttachableInfo EffectInfo EntityEventInfo MovieArtInfo TutorialInfo QuestInfo");
        Assign("world", "TerrainInfo FeatureInfo BonusInfo BonusClassInfo ImprovementInfo RouteInfo BuildInfo RiverInfo MapInfo MapSwitchInfo ClimateInfo SeaLevelInfo GoodyInfo TerrainArtInfo FeatureArtInfo BonusArtInfo ImprovementArtInfo");
        Assign("research", "TechInfo");
        Assign("economy", "ProcessInfo");
        Assign("cities", "BuildingInfo BuildingClassInfo SpecialBuildingInfo BuildingArtInfo HurryInfo");
        Assign("military", "UnitInfo UnitClassInfo UnitCombatInfo UnitAIInfo SpecialUnitInfo PromotionInfo DomainInfo InvisibleInfo CommandInfo MissionInfo OutcomeInfo UnitArtInfo");
        Assign("government", "CivicInfo CivicOptionInfo UpkeepInfo");
        Assign("peoples", "CivilizationInfo LeaderHeadInfo TraitInfo CivilizationArtInfo LeaderheadArtInfo UnitArtStyleTypeInfo");
        Assign("religion", "ReligionInfo");
        Assign("corporations", "CorporationInfo");
        Assign("diplomacy", "DiplomacyInfo AttitudeInfo MemoryInfo DenialInfo VoteInfo VoteSourceInfo");
        Assign("espionage", "EspionageMissionInfo");
        Assign("events", "EventInfo EventTriggerInfo");
        Assign("projects-victory", "ProjectInfo VictoryInfo");
        return result;
    }
}
