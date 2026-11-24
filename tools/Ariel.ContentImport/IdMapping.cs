using System.Text.RegularExpressions;

namespace Ariel.ContentImport;

public sealed record IdEntry(string Class, string SourceType, string Mod, string Id, string? Era);
/// <summary>A feature mod that owns the records of a base class that <see cref="Claims"/> selects (<see cref="IdMapping.RecordOwners"/>).
/// The rule sees the record and every source record, by class and type.</summary>
public sealed record RecordOwner(string Class, string Mod, Func<EffectiveRecord, IdMapping.SourceRecords, bool> Claims);

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
        return mod + ":" + Name(family, type);
    }
    /// <summary>The id's name part, without its mod: the source symbol, less its class prefix, normalized.</summary>
    private static string Name(string family, string type)
    {
        if (StrippedPrefixes.TryGetValue(family, out var prefix) && type.StartsWith(prefix, StringComparison.Ordinal)) type = type[prefix.Length..];
        return Normalize(type);
    }

    /// <summary>Every source record by canonical class and type, for <see cref="RecordOwner.Claims"/>.</summary>
    public sealed class SourceRecords(IEnumerable<EffectiveRecord> records)
    {
        private readonly ILookup<string, EffectiveRecord> byClass = records.ToLookup(r => CanonicalClass(r.Class), StringComparer.Ordinal);
        /// <summary>The records of a class.</summary>
        public IEnumerable<EffectiveRecord> Of(string family) => byClass[family];
    }

    /// <summary>Records a feature mod owns outright although their class belongs to a base module: the buildings that
    /// found a corporation or guild, the stores only a corporation's branch lets a city build, the special building types
    /// only those buildings have, and the executives that spread a corporation are Corporations' own content
    /// (<c>corporations:*</c>), so the base game carries no trace of corporations. Rules apply in order; the first match
    /// owns the record.</summary>
    public static readonly IReadOnlyList<RecordOwner> RecordOwners =
    [
        new("BuildingInfo", "corporations", (r, _) => CorporateBuilding(r)),
        new("SpecialBuildingInfo", "corporations", (r, all) => all.Of("BuildingInfo").Where(b => b.Node.Value("SpecialBuildingType") == r.Type).ToArray()
            is { Length: > 0 } users && users.All(CorporateBuilding)),
        new("UnitInfo", "corporations",
            (r, _) => r.Node.Child("CorporationSpreads")?.Children.Any(n => n.Children.Length > 1 && ContentMapper.Integer(n.Children[1].Text) > 0) == true),
        // Espionage's commerce type, process, spies, spy specialist and options are its own (espionage:*). The espionage
        // amounts in base rows are not split out: the mod keeps them in hand-written amending rows (docs/ESPIONAGE.md).
        new("CommerceInfo", "espionage", (r, _) => r.Type == "COMMERCE_ESPIONAGE"),
        new("ProcessInfo", "espionage", (r, _) => r.Type == "PROCESS_ESPIONAGE"),
        new("SpecialistInfo", "espionage", (r, _) => r.Type == "SPECIALIST_SPY"),
        new("UnitInfo", "espionage", (r, _) => ContentMapper.Boolean(r.Node.Value("bSpy"))),
        new("GameOptionInfo", "espionage", (r, _) => r.Type.Contains("ESPIONAGE", StringComparison.Ordinal)),
    ];
    /// <summary>A building that founds a corporation, or that a corporation's branch in the city lets it build.</summary>
    private static bool CorporateBuilding(EffectiveRecord building) =>
        ContentMapper.Present(building.Node.Value("FoundsCorporation")) || ContentMapper.Present(building.Node.Value("PrereqCorporation"));

    /// <summary>The mod that owns the record: its class's module, unless a <see cref="RecordOwners"/> rule claims it.</summary>
    public static string Owner(EffectiveRecord record, SourceRecords all)
    {
        var family = CanonicalClass(record.Class);
        if (!ClassMods.TryGetValue(family, out var mod)) throw new InvalidDataException($"No mod assignment for {family}.");
        return Claimant(record, all) ?? mod;
    }
    /// <summary>The feature mod a <see cref="RecordOwners"/> rule gives the record to, or null when its class's module owns it.</summary>
    public static string? Claimant(EffectiveRecord record, SourceRecords all) =>
        RecordOwners.FirstOrDefault(o => o.Class == CanonicalClass(record.Class) && o.Claims(record, all))?.Mod;

    public static IReadOnlyList<IdEntry> Create(IEnumerable<EffectiveRecord> records)
    {
        var source = records as IReadOnlyCollection<EffectiveRecord> ?? records.ToArray();
        var all = new SourceRecords(source);
        var entries = source.Select(x => (Record: x, Owner: Owner(x, all))).Select(x => new IdEntry(CanonicalClass(x.Record.Class), x.Record.Type,
            x.Owner, x.Owner + ":" + Name(CanonicalClass(x.Record.Class), x.Record.Type), x.Record.Era)).ToArray();
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
        Assign("foundation", "YieldInfo CommerceInfo EraInfo GameSpeedInfo HandicapInfo WorldInfo CultureLevelInfo SpecialistInfo CalendarInfo TurnTimerInfo GameOptionInfo PlayerOptionInfo MPOptionInfo ForceControlInfo GraphicOptionInfo ControlInfo InterfaceModeInfo CityTabInfo AdvisorInfo EmphasizeInfo MonthInfo SeasonInfo CameraInfo LandscapeInfo PlayerColorInfo ConceptInfo NewConceptInfo SourceConceptInfo InterfaceArtInfo MiscArtInfo CursorInfo AttachableInfo EffectInfo EntityEventInfo MovieArtInfo TutorialInfo QuestInfo ReligionInfo VictoryInfo");
        Assign("world", "TerrainInfo FeatureInfo BonusInfo BonusClassInfo ImprovementInfo RouteInfo BuildInfo RiverInfo MapInfo MapSwitchInfo ClimateInfo SeaLevelInfo GoodyInfo TerrainArtInfo FeatureArtInfo BonusArtInfo ImprovementArtInfo");
        Assign("research", "TechInfo");
        Assign("economy", "ProcessInfo");
        Assign("cities", "BuildingInfo BuildingClassInfo SpecialBuildingInfo BuildingArtInfo HurryInfo");
        Assign("military", "UnitInfo UnitClassInfo UnitCombatInfo UnitAIInfo SpecialUnitInfo PromotionInfo DomainInfo InvisibleInfo CommandInfo MissionInfo OutcomeInfo UnitArtInfo");
        Assign("government", "CivicInfo CivicOptionInfo UpkeepInfo");
        Assign("peoples", "CivilizationInfo LeaderHeadInfo TraitInfo CivilizationArtInfo LeaderheadArtInfo UnitArtStyleTypeInfo");
        // Foundation reserves identities for mechanics that still have no owning mod.
        Assign("diplomacy", "DiplomacyInfo AttitudeInfo MemoryInfo DenialInfo");
        Assign("votes", "VoteInfo VoteSourceInfo");
        Assign("espionage", "EspionageMissionInfo");
        Assign("events", "EventInfo EventTriggerInfo");
        Assign("projects-victory", "ProjectInfo");
        // A corporation is the Corporations mod's alone: every row that names one is its content.
        Assign("corporations", "CorporationInfo");
        return result;
    }
}
