using Ariel.Ai;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Composition;

/// <summary>The base game: the rules modules (<see cref="BaseRules"/>) and the computer player (<see cref="AiModule"/>),
/// all linked into the host. None of them can be disabled or replaced, and a mod may depend on any of them.</summary>
public static class BaseGame
{
    /// <summary>Base module ids in ordinal order.</summary>
    public static IReadOnlyList<string> Ids { get; } = BaseRules.Ids.Append(AiModule.Id).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Whether an id names a base module.</summary>
    public static bool IsBase(string id) => id == AiModule.Id || BaseRules.IsBase(id);

    /// <summary>Ids of former mods whose feature a base module now provides under its own id: <c>tech_tree</c> (the
    /// Research module's tree). A saved mod selection may still name one; it is satisfied by the base game and no
    /// installed copy of the old mod is loaded.</summary>
    public static IReadOnlyList<string> FoldedMods { get; } = ["tech_tree"];

    /// <summary>Whether an id names a former mod the base game now provides (<see cref="FoldedMods"/>).</summary>
    public static bool IsFoldedMod(string id) => FoldedMods.Contains(id, StringComparer.Ordinal);

    /// <summary>Hand mods the host's copies of the base game's assemblies, so a mod and the host share one set of types.
    /// Call before loading a mod; composing through <see cref="GameContent"/> does it too.</summary>
    public static void ShareWithMods()
    {
        ModLoader.Share(typeof(BaseRules).Assembly);
        ModLoader.Share(typeof(AiModule).Assembly);
    }

    /// <summary>Fresh module instances with the manifests and sidecars staged under <paramref name="rulesRoot"/>.</summary>
    public static IReadOnlyList<ModSource> Sources(string rulesRoot) =>
        [.. BaseRules.Sources(rulesRoot), BaseRules.Linked(rulesRoot, AiModule.Id, AiModule.Folder, new AiModule())];
}
