using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>A team or world project. Negative instance limits mean unlimited; all numbers are integers.</summary>
public sealed class ProjectDef : GameContentDef
{
    public int Cost { get; init; } = -1;
    [ContentReference(typeof(TechDef))]
    public string? TechPrereq { get; init; }
    public int MaxGlobalInstances { get; init; } = -1;
    public int MaxTeamInstances { get; init; } = -1;
    /// <summary>Projects the team must already have completed, with the required count of each.</summary>
    [ContentReference(typeof(ProjectDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> PrereqProjects { get; init; } = ImmutableDictionary<string, int>.Empty;
    /// <summary>Some team (any team) must already have completed this project.</summary>
    [ContentReference(typeof(ProjectDef))]
    public string? AnyonePrereqProject { get; init; }
    /// <summary>The victory this project belongs to; it is only buildable while that victory is enabled.</summary>
    [ContentReference(typeof(VictoryDef))]
    public string? Victory { get; init; }
    public bool Spaceship { get; init; }
    public bool AllowsNukes { get; init; }
    /// <summary>Per victory: the most copies of this project one team may build.</summary>
    [ContentReference(typeof(VictoryDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> VictoryThresholds { get; init; } = ImmutableDictionary<string, int>.Empty;
    /// <summary>Per victory: the fewest copies a team needs for that victory. Falls back to the threshold.</summary>
    [ContentReference(typeof(VictoryDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> VictoryMinThresholds { get; init; } = ImmutableDictionary<string, int>.Empty;
}
