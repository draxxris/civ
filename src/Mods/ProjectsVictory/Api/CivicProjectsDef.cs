using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.ProjectsVictory;

/// <summary>What a civic means to projects: whether it enables the mutually assured destruction nuclear rules, the
/// civic side of <see cref="ProjectDef.AllowsNukes"/>. Any mod may attach one to a civic. Kept for the nuclear rules;
/// nothing reads it yet.</summary>
public sealed class CivicProjectsDef : ContentAttachment<CivicDef>
{
    public bool EnablesMadNukes { get; init; }
}
