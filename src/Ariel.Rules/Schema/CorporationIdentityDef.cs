using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>CorporationIdentity content. All authoritative numbers are integers.</summary>
public sealed class CorporationIdentityDef : GameContentDef
{
    /// <summary>Identity only. The future owning rules mod must implement this mechanic.</summary>
    public bool Deferred { get; init; } = true;
}
