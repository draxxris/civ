using Ariel.Content;
using Ariel.Modding;

namespace Ariel.Rules;

/// <summary>The one place a host composes a ruleset: the base modules plus the mods it chose.</summary>
public static class GameContent
{
    /// <summary>Folder beside the host that holds the base modules' manifests, data and art.</summary>
    public const string RulesFolder = "rules";
    /// <summary>Folder beside the host whose mods load by default.</summary>
    public const string ModsFolder = "mods";
    /// <summary>Folder beside the host whose mods load only when asked for.</summary>
    public const string OptionalModsFolder = "optional-mods";

    static GameContent() => ModLoader.Share(typeof(GameContent).Assembly);

    /// <summary>Base modules staged under <paramref name="rulesRoot"/>, followed by <paramref name="mods"/>.</summary>
    public static IReadOnlyList<ModSource> Sources(string rulesRoot, IEnumerable<ModSource> mods) =>
        [.. BaseRules.Sources(rulesRoot), .. mods];

    /// <summary>The layout the build stages beside a tool or a test host: base modules in <c>rules/</c>, every mod in
    /// <c>mods/</c>, and the named folders of <c>optional-mods/</c>.</summary>
    public static IReadOnlyList<ModSource> Staged(string hostRoot, params string[] optionalMods)
    {
        string mods = Path.Combine(hostRoot, ModsFolder);
        IEnumerable<string> folders = Directory.Exists(mods) ? Directory.GetDirectories(mods).Order(StringComparer.Ordinal) : [];
        var optional = optionalMods.Select(name => Path.Combine(hostRoot, OptionalModsFolder, name));
        return Sources(Path.Combine(hostRoot, RulesFolder), ModLoader.FromDirectories(folders.Concat(optional)));
    }

    /// <summary>Register and freeze base modules and mods.</summary>
    public static ContentDb Build(string rulesRoot, IEnumerable<ModSource> mods) => ModLoader.Build(Sources(rulesRoot, mods));
}
