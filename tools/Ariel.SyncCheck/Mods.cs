using Ariel.Content;
using Ariel.Modding;

namespace Ariel.SyncCheck;

/// <summary>The content a multiplayer game runs on: the base modules and every main mod, plus the chosen optional ones.</summary>
internal static class Mods
{
    /// <summary>Optional gameplay mods loaded unless <c>--optional</c> names others. The alternative research mod
    /// (CardResearch) replaces part of Research, so it is opt-in.</summary>
    private const string DefaultOptional = "Votes,ProjectsVictory,Corporations,Espionage,Events,ScriptedWonders,Revolution";

    public static ContentDb Load(string root, string[] args)
    {
        int i = Array.IndexOf(args, "--optional");
        string optional = i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultOptional;
        return ModLoader.Build(Ariel.Composition.GameContent.Staged(root,
            optional.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    }
}
