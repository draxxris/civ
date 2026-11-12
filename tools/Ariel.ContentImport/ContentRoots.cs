using Ariel.Rules;

namespace Ariel.ContentImport;

/// <summary>Where emitted sidecars go. Base modules (<see cref="BaseRules"/>) live under the rules root
/// (src/Ariel.Rules), every other mod under the mods root (src/Mods); each module keeps its own folder.</summary>
public sealed record ContentRoots(string Rules, string Mods)
{
    /// <summary>The folder of the module or mod with this id, under the root that owns it.</summary>
    public string Module(string mod) => Path.Combine(BaseRules.IsBase(mod) ? Rules : Mods, ContentEmission.DisplayMod(mod));
}
