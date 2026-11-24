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
        // Espionage's commerce type, process, spy specialist, spy units and options are its own (espionage:*). The great
        // spy, its specialist and the spy unit combat and roles stay in the base: base rows name them as single references.
        new("CommerceInfo", "espionage", (r, _) => r.Type == "COMMERCE_ESPIONAGE"),
        new("ProcessInfo", "espionage", (r, _) => r.Type == "PROCESS_ESPIONAGE"),
        new("SpecialistInfo", "espionage", (r, _) => r.Type == "SPECIALIST_SPY"),
        new("UnitInfo", "espionage", (r, _) => ContentMapper.Boolean(r.Node.Value("bSpy"))),
        new("GameOptionInfo", "espionage", (r, _) => r.Type.Contains("ESPIONAGE", StringComparison.Ordinal)),
        // A building, unit or promotion of a faith, or one that needs a religion or a state religion, is Religion's own
        // (religion:*): the temples, monasteries, shrines and religious wonders, the missionaries and the inquisitor, and
        // each faith's promotion.
        new("BuildingInfo", "religion", (r, _) => Religious(r, "ReligionType", "PrereqReligion", "StateReligion", "HolyCity", "bStateReligion", "bPrereqReligion")),
        new("UnitInfo", "religion", (r, _) => Religious(r, "PrereqReligion", "StateReligion", "HolyCity", "bInquisitor")
            || r.Node.Child("ReligionSpreads")?.Children.Any(n => n.Children.Length > 1 && ContentMapper.Integer(n.Children[1].Text) > 0) == true),
        new("PromotionInfo", "religion", (r, _) => Religious(r, "PrereqReligion", "StateReligionPrereq")),
    ];
    /// <summary>A record that names a religion, or sets a religion flag, in one of these fields.</summary>
    private static bool Religious(EffectiveRecord record, params string[] fields) => fields.Any(field =>
        field.StartsWith('b') ? ContentMapper.Boolean(record.Node.Value(field)) : ContentMapper.Present(record.Node.Value(field)));

    /// <summary>The mods a feature mod registers content for only when they are loaded (its <c>optionalReferences</c>, which
    /// a test keeps equal to the committed manifests). A row of that mod about a record such a provider owns, or in the
    /// provider's own row type, stays in the feature mod and lives under <c>data/&lt;provider&gt;/</c>.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> OptionalProviders = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        ["espionage"] = new HashSet<string>(["corporations", "great_people", "religion"], StringComparer.Ordinal),
        ["votes"] = new HashSet<string>(["religion"], StringComparer.Ordinal),
    };
    /// <summary>Whether rows of <paramref name="mod"/> may name records of <paramref name="provider"/>: its own, or an
    /// optional provider's.</summary>
    public static bool MayName(string mod, string provider) =>
        mod == provider || OptionalProviders.TryGetValue(mod, out var optional) && optional.Contains(provider);
    /// <summary>The mod that writes a row of <paramref name="mappingMod"/>'s vocabulary about a record that <paramref name="owner"/>
    /// owns outright: the owner (which then emits every row about its record), unless the row's mod registers content for
    /// the owner only when it is loaded, which then keeps its own rows.</summary>
    public static string Emitter(string? owner, string mappingMod) => owner is null || MayName(mappingMod, owner) ? mappingMod : owner;
    /// <summary>The folder, under the emitting mod's <c>data</c>, of a row about <paramref name="provider"/>'s content: that
    /// provider's name when the emitter registers it only when loaded, else none (the data folder itself).</summary>
    public static string? Subfolder(string emitter, string provider) =>
        emitter != provider && OptionalProviders.TryGetValue(emitter, out var optional) && optional.Contains(provider) ? provider : null;

    /// <summary>Defines a feature mod owns, read by no rule: the importer keeps them out of Foundation's raw constants. Espionage's
    /// mission costs, intercept chances and multipliers, none of which the mod reads yet.</summary>
    private static readonly Regex EspionageConstant = new(@"(^|_)(ESPIONAGE|SPY)_", RegexOptions.CultureInvariant);
    /// <summary>The mod that owns a source define, or null for the base game's.</summary>
    public static string? ConstantOwner(string define) => EspionageConstant.IsMatch(define) ? "espionage" : null;
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
        Assign("foundation", "YieldInfo CommerceInfo EraInfo GameSpeedInfo HandicapInfo WorldInfo CultureLevelInfo SpecialistInfo CalendarInfo TurnTimerInfo GameOptionInfo PlayerOptionInfo MPOptionInfo ForceControlInfo GraphicOptionInfo ControlInfo InterfaceModeInfo CityTabInfo AdvisorInfo EmphasizeInfo MonthInfo SeasonInfo CameraInfo LandscapeInfo PlayerColorInfo ConceptInfo NewConceptInfo SourceConceptInfo InterfaceArtInfo MiscArtInfo CursorInfo AttachableInfo EffectInfo EntityEventInfo MovieArtInfo TutorialInfo QuestInfo VictoryInfo");
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
        // So is a religion the Religion mod's.
        Assign("religion", "ReligionInfo");
        return result;
    }
}
