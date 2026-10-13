using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

public sealed record SetupChoice(string Id, string Name);

/// <summary>A selectable leader with the civilization they lead and the traits they bring.</summary>
public sealed record LeaderChoice(string Id, string Name, string CivilizationId, string CivilizationName, IReadOnlyList<string> Traits)
{
    public string Label => $"{Name} — {CivilizationName}" + (Traits.Count == 0 ? "" : $" ({string.Join(", ", Traits)})");
}

/// <summary>
/// The loaded ruleset: mods and frozen content, built once at startup, plus the choices a new game offers and where
/// mod art lives. Engine free; the renderer turns <see cref="IconPath"/> into textures.
/// </summary>
public sealed class GameCatalog
{
    private static readonly string[] HandicapOrder = ["settler", "chieftain", "warlord", "noble", "prince", "monarch", "emperor", "immortal", "deity"];
    private readonly Dictionary<string, string> _modRoots;
    private readonly Dictionary<string, string?> _icons = new();

    public ContentDb Content { get; }
    public IReadOnlyList<ModSource> Mods { get; }
    public IReadOnlyList<SetupChoice> WorldSizes { get; }
    public IReadOnlyList<SetupChoice> GameSpeeds { get; }
    public IReadOnlyList<SetupChoice> Handicaps { get; }
    public IReadOnlyList<LeaderChoice> Leaders { get; }
    /// <summary>The AI service the opponents use, or null until an AI mod exists; then opponents only end their turn.</summary>
    public string? OpponentAi { get; }

    public GameCatalog(IReadOnlyList<ModSource> mods)
    {
        Mods = mods;
        Content = ModLoader.Build(mods);
        _modRoots = mods.ToDictionary(m => m.Manifest.Id, m => m.Root, StringComparer.Ordinal);
        WorldSizes = Choices(Content.Get<Schema.WorldSizeDef>().All.OrderBy(w => w.GridWidth * w.GridHeight));
        GameSpeeds = Choices(Content.Get<Schema.GameSpeedDef>().All.OrderBy(s => s.GrowthPercent));
        Handicaps = Choices(Content.Get<Schema.HandicapDef>().All.OrderBy(h => Array.IndexOf(HandicapOrder, h.Id.Split('_').Last())).ThenBy(h => h.Id, StringComparer.Ordinal));
        Leaders = BuildLeaders(Content);
        OpponentAi = Content.PlayerAIs.All.OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;
    }

    public static GameCatalog Load(string modsRoot) => new(ModDiscovery.LoadMods(modsRoot));

    private static IReadOnlyList<SetupChoice> Choices<T>(IEnumerable<T> defs) where T : ContentDef =>
        defs.Select(d => new SetupChoice(d.Id, d.Name)).ToList();

    private static IReadOnlyList<LeaderChoice> BuildLeaders(ContentDb content)
    {
        var result = new List<LeaderChoice>();
        foreach (var civ in content.Get<Schema.CivilizationDef>().All.Where(c => c.Playable).OrderBy(c => c.Name, StringComparer.Ordinal))
            foreach (var id in civ.Leaders.Where(l => l.Value).Select(l => l.Key).Order(StringComparer.Ordinal))
                if (content.TryGet<Schema.LeaderDef>(id, out var leader))
                    result.Add(new(leader.Id, leader.Name, civ.Id, civ.Name, TraitNames(content, leader)));
        return result;
    }

    private static IReadOnlyList<string> TraitNames(ContentDb content, Schema.LeaderDef leader) =>
        leader.Traits.Where(t => t.Value).Select(t => content.TryGet<Schema.TraitDef>(t.Key, out var def) ? def.Name : t.Key).Order(StringComparer.Ordinal).ToList();

    public LeaderChoice LeaderById(string id) => Leaders.First(l => l.Id == id);

    /// <summary>The human leader, then <paramref name="opponents"/> others from distinct civilizations, chosen deterministically from the seed.</summary>
    public IReadOnlyList<LeaderChoice> PickLeaders(string human, int opponents, ulong seed)
    {
        var chosen = new List<LeaderChoice> { LeaderById(human) };
        var pool = Leaders.Where(l => l.CivilizationId != chosen[0].CivilizationId).ToList();
        ulong state = seed * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL;
        while (chosen.Count <= opponents && pool.Count > 0)
        {
            state = (state ^ (state >> 29)) * 0xBF58476D1CE4E5B9UL + 0x94D049BB133111EBUL;
            var pick = pool[(int)((state >> 11) % (ulong)pool.Count)];
            chosen.Add(pick);
            pool.RemoveAll(l => l.CivilizationId == pick.CivilizationId);
        }
        return chosen;
    }

    public NewGameOptions DefaultOptions() => new(
        Pick(WorldSizes, "standard"), Pick(GameSpeeds, "normal"), Pick(Handicaps, "noble"), Leaders[0].Id, 3, 1);

    private static string Pick(IReadOnlyList<SetupChoice> choices, string word) =>
        (choices.FirstOrDefault(c => c.Id.EndsWith("_" + word, StringComparison.Ordinal)) ?? choices[0]).Id;

    /// <summary>PNG for a content id from its owning mod's <c>art/icons</c> folder, or null when the mod ships none.</summary>
    public string? IconPath(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_icons.TryGetValue(id, out var cached)) return cached;
        int colon = id.IndexOf(':');
        string? path = null;
        if (colon > 0 && _modRoots.TryGetValue(id[..colon], out var root))
        {
            var candidate = Path.Combine(root, "art", "icons", id[(colon + 1)..] + ".png");
            if (File.Exists(candidate)) path = candidate;
        }
        return _icons[id] = path;
    }

    public SimulationSettings SettingsFor(NewGameOptions options) => options.ToSettings(this);
}
