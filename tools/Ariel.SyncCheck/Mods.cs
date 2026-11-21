using System.Reflection;
using Ariel.Content;
using Ariel.Modding;

namespace Ariel.SyncCheck;

/// <summary>The content a multiplayer game runs on: the base modules and every main mod, plus the chosen optional ones.</summary>
internal static class Mods
{
    /// <summary>Optional mods loaded unless <c>--optional</c> names others: the optional feature mods of
    /// <c>build/Mods.props</c>, which the build writes into this assembly. The alternative rules mods (CardResearch
    /// replaces part of Research) are not among them, so they stay opt-in.</summary>
    private static readonly string DefaultOptional = typeof(Mods).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "ArielOptionalFeatureMods").Value ?? "";

    public static ContentDb Load(string root, string[] args)
    {
        int i = Array.IndexOf(args, "--optional");
        string optional = i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultOptional;
        return ModLoader.Build(Ariel.Composition.GameContent.Staged(root,
            optional.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    }
}
