using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

public sealed record SetupChoice(string Id, string Name);

/// <summary>A selectable leader with the civilization they lead and the traits they bring.</summary>
public sealed record LeaderChoice(string Id, string Name, string CivilizationId, string CivilizationName, IReadOnlyList<string> Traits)
{
    public string Label => $"{Name} — {CivilizationName}" + (Traits.Count == 0 ? "" : $" ({string.Join(", ", Traits)})");
}

/// <summary>
/// The loaded ruleset: base modules, mods and frozen content, built once at startup, plus the choices a new game offers and where
/// mod art lives. Engine free; the renderer turns <see cref="IconPath"/> into textures.
/// </summary>
public sealed class GameCatalog
{

    private readonly Dictionary<string, string> _modRoots;
    private readonly Dictionary<string, string?> _icons = new();
    private readonly Dictionary<string, string?> _models = new();
    private readonly Dictionary<string, string?> _textures = new();
    private readonly Dictionary<string, IReadOnlyList<string>> _scatterModels = new(StringComparer.Ordinal);

    public ContentDb Content { get; }
    public IReadOnlyList<ModSource> Mods { get; }
    public IReadOnlyList<SetupChoice> WorldSizes { get; }
    public IReadOnlyList<SetupChoice> MapGenerators { get; }
    public IReadOnlyList<SetupChoice> GameSpeeds { get; }
    public IReadOnlyList<SetupChoice> Handicaps { get; }
    public IReadOnlyList<SetupGameOption> GameOptions { get; }
    public IReadOnlyList<LeaderChoice> Leaders { get; }
    /// <summary>The AI service the opponents use: the first registered, by ordinal id. The base game always registers
    /// one; null only for a ruleset composed without it, and then opponents only end their turn.</summary>
    public string? OpponentAi { get; }

    /// <summary>Build from a complete source list: the base modules and the chosen mods, as
    /// <see cref="Ariel.Composition.GameContent.Sources"/> composes them.</summary>
    public GameCatalog(IReadOnlyList<ModSource> mods)
    {
        using var trace = StartupTrace.Phase("catalog build");
        Mods = mods;
        Content = ModLoader.Build(mods);
        using var choicesTrace = StartupTrace.Phase("catalog choices");
        _modRoots = mods.ToDictionary(m => m.Manifest.Id, m => m.Root, StringComparer.Ordinal);
        WorldSizes = Choices(Content.Get<Schema.WorldSizeDef>().All.OrderBy(w => w.GridWidth * w.GridHeight));
        MapGenerators = Content.MapGenerators.All.OrderBy(g => g.Id, StringComparer.Ordinal)
            .Select(g => new SetupChoice(g.Id, g.Name)).ToList();
        GameSpeeds = Choices(Content.Get<Schema.GameSpeedDef>().All.OrderBy(s => s.GrowthPercent));
        Handicaps = Choices(Content.Get<Schema.HandicapDef>().All.OrderBy(h => SetupPolicies.SortOrder(Content, SetupField.Handicap, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal));
        GameOptions = SetupPolicies.GameOptions(Content);
        Leaders = BuildLeaders(Content);
        OpponentAi = Content.PlayerAIs.All.OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;
    }

    /// <summary>Base modules from <paramref name="rulesRoot"/> with every mod installed in <paramref name="modsRoot"/>.</summary>
    public static GameCatalog Load(string rulesRoot, string modsRoot)
    {
        using var trace = StartupTrace.Phase("catalog load");
        var catalog = new GameCatalog(Ariel.Composition.GameContent.Sources(rulesRoot, ModDiscovery.LoadMods(modsRoot)));
        // Content is frozen; start the digest now so it overlaps the setup work instead of blocking the first reader.
        catalog.Content.PrefetchContentHash();
        return catalog;
    }

    /// <summary>Base modules from <paramref name="rulesRoot"/> with the enabled subset of the installed mods.</summary>
    public static GameCatalog Load(string rulesRoot, IReadOnlyList<InstalledMod> installed, IEnumerable<string> enabledIds)
    {
        using var trace = StartupTrace.Phase("catalog load");
        var catalog = new GameCatalog(Ariel.Composition.GameContent.Sources(rulesRoot, ModDiscovery.LoadMods(installed, enabledIds)));
        catalog.Content.PrefetchContentHash();
        return catalog;
    }

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

    /// <summary>One leader per seat. A seat that names a leader (and optionally its civilization) gets it; the rest
    /// get distinct leaders of civilizations nobody else plays, drawn from the seed, and repeat only once every
    /// civilization is taken.</summary>
    public IReadOnlyList<LeaderChoice> AssignLeaders(IReadOnlyList<(string Leader, string Civilization)> seats, ulong seed)
    {
        var chosen = new LeaderChoice?[seats.Count];
        for (int i = 0; i < seats.Count; i++)
        {
            var (leader, civ) = seats[i];
            if (leader.Length == 0) continue;
            chosen[i] = civ.Length == 0 ? LeaderById(leader)
                : Leaders.FirstOrDefault(c => c.Id == leader && c.CivilizationId == civ)
                  ?? throw new ArgumentException($"Leader '{leader}' does not lead civilization '{civ}'.");
        }
        var pool = Leaders.Where(l => chosen.All(c => c?.CivilizationId != l.CivilizationId)).ToList();
        ulong state = seed * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL;
        for (int i = 0; i < seats.Count; i++)
        {
            if (chosen[i] != null) continue;
            if (pool.Count == 0) pool = Leaders.ToList();
            state = (state ^ (state >> 29)) * 0xBF58476D1CE4E5B9UL + 0x94D049BB133111EBUL;
            var pick = pool[(int)((state >> 11) % (ulong)pool.Count)];
            chosen[i] = pick;
            pool.RemoveAll(l => l.CivilizationId == pick.CivilizationId);
        }
        return chosen!;
    }

    public NewGameOptions DefaultOptions() => new(
        Pick(WorldSizes, SetupField.WorldSize), Pick(GameSpeeds, SetupField.GameSpeed), Pick(Handicaps, SetupField.Handicap),
        Leaders[0].Id, 3, 1, MapGenerator: MapGenerators.Count == 0 ? "" : Pick(MapGenerators, SetupField.MapGenerator));

    private string Pick(IReadOnlyList<SetupChoice> choices, SetupField field) =>
        SetupPolicies.Default(Content, field) is { } id ? choices.FirstOrDefault(c => c.Id == id)?.Id ?? choices[0].Id : choices[0].Id;

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

    /// <summary>glTF model for a content id from its owning mod's <c>art/models</c> folder, or null when the mod ships none.</summary>
    public string? ModelPath(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_models.TryGetValue(id, out var cached)) return cached;
        int colon = id.IndexOf(':');
        string? path = null;
        if (colon > 0 && _modRoots.TryGetValue(id[..colon], out var root))
        {
            var candidate = Path.Combine(root, "art", "models", id[(colon + 1)..] + ".glb");
            if (File.Exists(candidate)) path = candidate;
        }
        return _models[id] = path;
    }

    /// <summary>PNG for a content id from its owning mod's <c>art/terrain</c> folder, or null when the mod ships none.</summary>
    public string? TexturePath(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_textures.TryGetValue(id, out var cached)) return cached;
        int colon = id.IndexOf(':');
        string? path = null;
        if (colon > 0 && _modRoots.TryGetValue(id[..colon], out var root))
        {
            var candidate = Path.Combine(root, "art", "terrain", id[(colon + 1)..] + ".png");
            if (File.Exists(candidate)) path = candidate;
        }
        return _textures[id] = path;
    }

    /// <summary>Sorted GLB files for a scatter set. A namespaced set selects its mod; a plain set uses the first matching mod.</summary>
    public IReadOnlyList<string> ScatterModels(string? set)
    {
        if (string.IsNullOrEmpty(set)) return Array.Empty<string>();
        if (_scatterModels.TryGetValue(set, out var cached)) return cached;

        int colon = set.IndexOf(':');
        string suffix = colon > 0 ? set[(colon + 1)..] : set;
        IEnumerable<string> roots = colon > 0 && _modRoots.TryGetValue(set[..colon], out var ownerRoot)
            ? [ownerRoot]
            : _modRoots.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => entry.Value);
        foreach (var root in roots)
        {
            var directory = Path.Combine(root, "art", "scatter", suffix);
            if (!Directory.Exists(directory)) continue;
            return _scatterModels[set] = Directory.GetFiles(directory, "*.glb", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();
        }
        return _scatterModels[set] = Array.Empty<string>();
    }

    public SimulationSettings SettingsFor(NewGameOptions options) => options.ToSettings(this);
}
