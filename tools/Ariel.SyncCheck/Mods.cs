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

    /// <summary>The content for <paramref name="args"/>: <c>--optional A,B</c> picks the optional mods, and
    /// <c>--without A,B</c> leaves out mods by id (<c>religion</c>), main ones included, to play a game without a
    /// feature. A mod that hard-references a left-out one then fails to load, which is the point.</summary>
    public static ContentDb Load(string root, string[] args)
    {
        string optional = Option(args, "--optional") ?? DefaultOptional;
        var without = List(Option(args, "--without") ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = Ariel.Composition.GameContent.Staged(root, List(optional));
        if (without.Except(all.Select(s => s.Manifest.Id), StringComparer.OrdinalIgnoreCase).FirstOrDefault() is { } unknown)
            throw new ArgumentException($"--without names no loaded mod: {unknown}");
        return ModLoader.Build(all.Where(source => !without.Contains(source.Manifest.Id)).ToArray());
    }

    private static string? Option(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string[] List(string value) =>
        value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
