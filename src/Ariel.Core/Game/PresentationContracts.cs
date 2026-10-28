using Ariel.Content;

namespace Ariel.Game;

/// <summary>Setup fields whose defaults and display order belong to the loaded ruleset.</summary>
public enum SetupField { WorldSize, GameSpeed, Handicap, StartingEra, MapGenerator }

/// <summary>Optional ruleset policy. At most one provider may supply a default for each field.</summary>
public sealed record SetupGameOption(string Id, string Name, bool Default, string Help = "");

public interface ISetupPolicy
{
    string? Default(SetupField field);
    int? SortOrder(SetupField field, string id) => null;
    IEnumerable<SetupGameOption> GameOptions(ContentDb content) => [];
}

public static class SetupPolicies
{
    public static string? Default(ContentDb content, SetupField field) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.Default(field)).Where(id => id != null).SingleOrDefault();

    public static int SortOrder(ContentDb content, SetupField field, string id) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.SortOrder(field, id)).Where(n => n != null).SingleOrDefault() ?? 0;

    public static IReadOnlyList<SetupGameOption> GameOptions(ContentDb content) =>
        content.Services.Get<ISetupPolicy>().SelectMany(p => p.GameOptions(content))
            .OrderBy(option => option.Name, StringComparer.Ordinal).ThenBy(option => option.Id, StringComparer.Ordinal).ToList();
}

/// <summary>How far a queued copy of a candidate reaches. Entries for kinds that can be built repeatedly never cover
/// each other and use <see cref="None"/>.</summary>
public enum QueueScope
{
    /// <summary>Every entry is a separate build, as for units and processes.</summary>
    None,
    /// <summary>The city's queue holds its copies; another city may queue its own.</summary>
    City,
    /// <summary>The player's cities share the copies: another of the player's cities blocks it, another player races for it.</summary>
    Player,
    /// <summary>The team's cities share the copies, because the cap they spend is the team's, as for projects. Another team
    /// still races for the same content.</summary>
    Team,
}

/// <summary>A second heading a kind's candidates fall under, such as the wonders among the buildings. The client draws
/// one tab per category, so a player reaches a large group without scrolling past the rest of the kind.</summary>
public sealed record ProductionCategory(string Id, string Label, Func<ContentDef, bool> Contains);

/// <summary>A mod-owned queue kind and its read-only catalog and query behavior.</summary>
public interface IProductionKind : IHasId
{
    string Label { get; }
    bool IsContinuous { get; }
    /// <summary>Extra headings this kind's candidates fall under. A category claims its candidates for its own tab and
    /// the kind's own tab leaves them out, so each candidate sits in one tab; the client's "all" tab still gathers
    /// everything. Empty by default, which keeps a kind to one tab.</summary>
    IReadOnlyList<ProductionCategory> Categories => [];
    /// <summary>How far a queued copy of this candidate reaches, so the choice list stops offering it once its queues
    /// hold as many copies as the scope allows.</summary>
    QueueScope QueuedScope(Simulation sim, int city, ContentDef definition) => QueueScope.None;
    /// <summary>How many queued copies the scope holds before the candidate is covered: the city's own queue for
    /// <see cref="QueueScope.City"/>, the player's queues together for <see cref="QueueScope.Player"/>. Ignored for
    /// <see cref="QueueScope.None"/>. The default is one copy, which is what a wonder or a unique building may hold.</summary>
    int QueuedCopies(Simulation sim, int city, ContentDef definition) => 1;
    IEnumerable<ContentDef> Candidates(ContentDb content);
    ContentDef Definition(ContentDb content, string id);
    long BaseCost(ContentDef definition);
    bool IsVisible(RequirementResult eligibility);
    RequirementResult Validate(Simulation sim, int city, string id);
    /// <summary>Validates a batch of ids in one read-only pass and returns the results in the order of <paramref name="ids"/>,
    /// each equal to <see cref="Validate"/> for that id. Mods that repeat whole-empire work for every id (building, power
    /// and resource state) override this to share it across the batch.</summary>
    IReadOnlyList<RequirementResult> ValidateBatch(Simulation sim, int city, IReadOnlyList<string> ids) =>
        ids.Select(id => Validate(sim, city, id)).ToArray();
    CityProductionResult Calculate(Simulation sim, int city, string id);
}

/// <summary>Why a queued copy stops a candidate from being offered again. The simulation's queue validation and the
/// choice list both read it, so a click and a command agree on what "already queued" means.</summary>
public static class QueueBlocker
{
    public const string InCity = "Already in queue";
    public const string InOtherCity = "Already queued in another city";
    public const string InEveryQueue = "All copies are already queued";

    /// <summary>The covering copy's note, or null when the queues still have room for one more. <paramref name="replace"/>
    /// says the command clears this city's queue before it adds, so the city's own entries do not count.</summary>
    public static string? Of(Simulation sim, IProductionKind kind, City city, ContentDef definition, bool replace = false) =>
        Of(sim, kind, city, definition, Count(sim, kind, city, replace));

    /// <summary>The same answer for a caller that already counted the queues, so a choice list makes one pass over the
    /// queues per kind instead of one pass per candidate.</summary>
    public static string? Of(Simulation sim, IProductionKind kind, City city, ContentDef definition, Queues queues)
    {
        QueueScope scope = kind.QueuedScope(sim, city.Id, definition);
        if (scope == QueueScope.None) return null;
        int room = Math.Max(1, kind.QueuedCopies(sim, city.Id, definition));
        int mine = queues.Here.GetValueOrDefault(definition.Id);
        if (mine >= room) return InCity;
        // A city-scoped copy reaches no further than this queue, so nothing else can fill the room.
        if (scope == QueueScope.City) return null;
        int elsewhere = queues.Own.GetValueOrDefault(definition.Id)
            + (scope == QueueScope.Team ? queues.Team.GetValueOrDefault(definition.Id) : 0);
        if (mine + elsewhere < room) return null;
        // The copies may be split: this city holds some and other cities hold the rest, so neither note alone is true.
        return mine > 0 ? InEveryQueue : InOtherCity;
    }

    /// <summary>Queued copies of each content id, in this city and in the cities the shared scopes reach: the same
    /// player's others, and the same team's others. One pass answers both, and a candidate only reads dictionaries.</summary>
    public readonly record struct Queues(
        Dictionary<string, int> Here, Dictionary<string, int> Own, Dictionary<string, int> Team);

    public static Queues Count(Simulation sim, IProductionKind kind, City city, bool replace = false)
    {
        var here = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!replace) Add(here, city, kind.Id);
        var own = new Dictionary<string, int>(StringComparer.Ordinal);
        var team = new Dictionary<string, int>(StringComparer.Ordinal);
        int teamId = sim.State.PlayerById(city.Owner).TeamId;
        foreach (var other in sim.State.Cities)
        {
            if (other.Id == city.Id) continue;
            if (other.Owner == city.Owner) Add(own, other, kind.Id);
            else if (sim.State.PlayerById(other.Owner).TeamId == teamId) Add(team, other, kind.Id);
        }
        return new(here, own, team);
    }

    private static void Add(Dictionary<string, int> counts, City city, string kind)
    {
        foreach (var order in city.ProductionQueue)
            if (order.Kind == kind) counts[order.ContentId] = counts.GetValueOrDefault(order.ContentId) + 1;
    }
}

/// <summary>Optional labels for mod-owned ledger source identities. Null leaves the source to other providers.</summary>
public interface ILedgerSourceLabel
{
    string? Label(Simulation sim, string source);
}

/// <summary>Integer RGB and percent tint metadata. No renderer or engine types cross the mod contract.
/// <paramref name="Spread"/> (terrains only, 0..100) is how far a terrain's ground reaches into its neighbours: lush
/// ground at 100 grows past the tile edge into dry ground at 0. <paramref name="Patch"/> (terrains only) is another terrain
/// whose ground shows through in scattered patches over <paramref name="PatchPercent"/> (0..100) of the ground, so a
/// large area of one terrain does not look uniform. <paramref name="TurnDegrees"/> (terrains and surfaces, 0..180) is
/// the largest random turn the renderer may give the ground texture to hide its repeat; ground with a direction, such
/// as wind-blown sand ripples, keeps a small value so its pattern stays roughly parallel.</summary>
public sealed record TileAppearance(int Rgb, int TintPercent = 0, int Marks = 0, string? Scatter = null, int ScatterDensity = 0,
    int Spread = 50, FeatureLook? Look = null, string? Patch = null, int PatchPercent = 0, int TurnDegrees = 180);

/// <summary>How a feature paints the ground (or water) under it. The client draws each pattern procedurally.</summary>
public enum FeatureGround
{
    /// <summary>Only the soft tint: shade under props, such as a forest floor.</summary>
    Shade,
    /// <summary>Dark, still pools between tussocks of the ground.</summary>
    Pools,
    /// <summary>Lush green growth and dark silt, richest along rivers.</summary>
    Fertile,
    /// <summary>A small pool of clear water at the tile centre, ringed by damp ground.</summary>
    Spring,
    /// <summary>Scorched, discoloured ground with a faint sickly glow.</summary>
    Blight,
    /// <summary>Broken floes of sea ice on the water, packed tight at the shore.</summary>
    Floes,
    /// <summary>Dark, wind-torn water with whitecaps under a cloud shadow.</summary>
    Squall,
    /// <summary>A summit crater with a glowing floor and dark ash slopes. The height field gains the crater.</summary>
    Crater,
}

/// <summary>A translucent layer of air over a feature. The terrain under it stays readable.</summary>
public enum FeatureHaze
{
    None,
    /// <summary>Low, pale wisps that hug the ground.</summary>
    Mist,
    /// <summary>Low, coloured, slowly rolling haze.</summary>
    Smog,
    /// <summary>Dark, torn storm clouds high over the tile.</summary>
    Storm,
    /// <summary>A plume of smoke rising from the tile's summit.</summary>
    Smoke,
}

/// <summary>Extra feature look. <paramref name="GroundPercent"/> is the strength of the ground pattern. The haze has a
/// colour and a density (0..100). <paramref name="ScatterInner"/> and <paramref name="ScatterOuter"/> limit props to a
/// ring around the tile centre, in percent of the hex radius. <paramref name="Clumping"/> (0..100) gathers props into
/// groves with glades between them.</summary>
public sealed record FeatureLook(FeatureGround Ground = FeatureGround.Shade, int GroundPercent = 100,
    FeatureHaze Haze = FeatureHaze.None, int HazeRgb = 0, int HazePercent = 0,
    int ScatterInner = 0, int ScatterOuter = 100, int Clumping = 0);

public enum TerrainSurface { Cliff, Seabed, DeepSeabed, Unexplored, Snow }

/// <summary>How a land route is drawn. The client draws an unknown route as a plain road.</summary>
public enum RouteLook
{
    /// <summary>Two narrow wheel ruts that wander over the grass.</summary>
    Track,
    /// <summary>A packed earth road with a worn middle and ruts.</summary>
    Road,
    /// <summary>Grey stone setts between soft verges.</summary>
    Paved,
    /// <summary>Dark asphalt with white edge lines and a dashed yellow centre line.</summary>
    Highway,
    /// <summary>Two steel rails on wooden sleepers in a ballast bed.</summary>
    Railroad,
    /// <summary>A railroad on concrete sleepers under portal masts and a contact wire.</summary>
    ElectricRailroad,
    /// <summary>A concrete guideway beam on piers.</summary>
    Maglev,
    /// <summary>A slim steel beam with glowing edges on lattice legs.</summary>
    Jumplane,
}

public interface ITileAppearanceService
{
    TileAppearance? Terrain(string id);
    TileAppearance? Feature(string id);
    string? Surface(TerrainSurface surface) => null;
    TileAppearance? Scatter(string feature, string terrain) => null;
    /// <summary>The look of a route id, or null to leave it to other providers.</summary>
    RouteLook? Route(string id) => null;
}

/// <summary>How a city's 3D buildings look.</summary>
public enum CityStyle { Huts, Victorian, Modern }

/// <summary>What lies under a city's 3D buildings.</summary>
public enum CityGround { Dirt, Cobblestone, Concrete }

/// <summary>The look of a city in an era. No renderer types cross the mod contract.</summary>
public sealed record CityAppearance(CityStyle Style, CityGround Ground);

/// <summary>Maps an era id to the look of its cities. Null leaves the era to other providers.</summary>
public interface ICityAppearanceService
{
    CityAppearance? Era(string era);
}
