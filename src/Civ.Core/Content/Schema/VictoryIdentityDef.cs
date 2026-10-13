using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>VictoryIdentity content. All authoritative numbers are integers.</summary>
public sealed class VictoryIdentityDef : GameContentDef
{
    /// <summary>Identity only. The future owning rules mod must implement this mechanic.</summary>
    public bool Deferred { get; init; } = true;
}
