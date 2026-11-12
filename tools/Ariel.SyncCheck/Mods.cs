using Ariel.Content;
using Ariel.Modding;

namespace Ariel.SyncCheck;

/// <summary>The content a multiplayer game runs on: the base modules and every main mod, plus the chosen optional ones.</summary>
internal static class Mods
{
    /// <summary>Optional gameplay mods loaded unless <c>--optional</c> names others. The alternative research mods
    /// (CardResearch, TechTree) replace part of Research, so they are opt-in.</summary>
    private const string DefaultOptional = "Diplomacy,ProjectsVictory,Corporations,Espionage,Events,ScriptedWonders,Revolution";

    public static ContentDb Load(string root, string[] args)
    {
        int i = Array.IndexOf(args, "--optional");
        string optional = i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultOptional;
        return ModLoader.Build(Ariel.Rules.GameContent.Staged(root,
            optional.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    }
}
