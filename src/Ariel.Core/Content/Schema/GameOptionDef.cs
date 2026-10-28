using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>GameOption content. All authoritative numbers are integers.</summary>
public sealed class GameOptionDef : GameContentDef
{
    public bool Default { get; init; } = false;
    public bool Visible { get; init; } = false;
}
