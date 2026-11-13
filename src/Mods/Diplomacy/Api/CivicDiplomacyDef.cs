using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Diplomacy;

/// <summary>A change in attitude toward a player running one civic, with the text that explains it.</summary>
public sealed record CivicAttitudeEffect(int Change, string Text);

/// <summary>What a civic means to leaders' attitudes. Any mod may attach one to a civic, its own or another's. Read
/// with <c>ContentDb.Attachment&lt;CivicDiplomacyDef&gt;(civic)</c>. The share, power and tribute fields are kept for
/// rules this mod does not implement yet; nothing reads them.</summary>
public sealed class CivicDiplomacyDef : ContentAttachment<CivicDef>, IDescribedContent
{
    /// <summary>Attitude of a leader running this civic toward a player running the keyed civic.</summary>
    [ContentReference(typeof(CivicDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CivicAttitudeEffect> AttitudeChanges { get; init; } = ImmutableDictionary<string, CivicAttitudeEffect>.Empty;
    /// <summary>Attitude of a leader running this civic toward every other player.</summary>
    public int GlobalRelationsChange { get; init; }
    public int AttitudeShareMod { get; init; }
    public int PowerfulRelationsChange { get; init; }
    public int LessPowerfulRelationsChange { get; init; }
    public int WeakestRelationsChange { get; init; }
    public bool CanNotCeaseRelations { get; init; }
    public int TributePercent { get; init; }

    public IEnumerable<string> Describe(ContentDb content)
    {
        if (GlobalRelationsChange != 0) yield return $"{GlobalRelationsChange:+#;-#} attitude toward every other player";
        foreach (var pair in AttitudeChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (pair.Value.Change != 0)
                yield return $"{pair.Value.Change:+#;-#} attitude toward players running {(content.TryGet<CivicDef>(pair.Key, out var civic) ? civic.Name : pair.Key)}";
    }
}
