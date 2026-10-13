using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>ReligionIdentity content. All authoritative numbers are integers.</summary>
public sealed class ReligionIdentityDef : GameContentDef
{
    /// <summary>Identity only. The future owning rules mod must implement this mechanic.</summary>
    public bool Deferred { get; init; } = true;
}
